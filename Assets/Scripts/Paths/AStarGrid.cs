// Author: Peter Richards.
using Unity.Mathematics;
using Unity.Collections;
using UnityEngine;
using Unity.Physics;
using System.Collections.Generic;
using System.Linq;
using Unity.Entities;
using Unity.Physics.Authoring;
using System;

public class AStarGrid : MonoBehaviour
{
    public float3 gridSize;
    public float3 nodeSize;
    public int3 gridDimensions;

    public NativeBitArray nodeTraversableBits;
    HashSet<int> walkerNodes = new HashSet<int>();

    private void OnValidate()
    {
        gridDimensions = (int3)math.round(gridSize / nodeSize);
    }

    private void OnDestroy()
    {
        if (nodeTraversableBits.IsCreated)
            nodeTraversableBits.Dispose();
    }

    float3 Origin { get { return (float3)transform.position - gridSize * 0.5f + nodeSize * 0.5f; } }

    public bool IsTraversable(float3 xyz)
    {
        return nodeTraversableBits.IsSet(GetNodeI(GetNodeXYZ(xyz)));
    }

    public bool InBounds(int3 xyz)
    {
        return xyz.x >= 0 && xyz.x < gridDimensions.x &&
               xyz.y >= 0 && xyz.y < gridDimensions.y &&
               xyz.z >= 0 && xyz.z < gridDimensions.z;
    }

    public bool InBounds(float3 xyz) { return InBounds(GetNodeXYZ(xyz)); }

    public void SetGrid(EntityManager entityManager, CollisionWorld collisionWorld)
    {
        gridSize = math.max(gridSize, 1.0f);
        nodeSize = math.max(nodeSize, 1.0f);

        gridDimensions = (int3)math.round(gridSize / nodeSize);

        if (nodeTraversableBits.IsCreated)
            nodeTraversableBits.Dispose();
        nodeTraversableBits = new NativeBitArray(gridDimensions.x * gridDimensions.y * gridDimensions.z, Allocator.Persistent);
        walkerNodes.Clear();

        for (int z = 0; z < gridDimensions.z; z++)
        {
            int k = gridDimensions.y * gridDimensions.x * z;
            for (int y = 0; y < gridDimensions.y; y++)
            {
                int j = k + gridDimensions.x * y;

                for (int x = 0; x < gridDimensions.x; x++)
                {
                    int i = j + x;
                    SetGridNode(entityManager, collisionWorld, i, x, y, z);
                }
            }
        }
    }

    public float3 GetWorldPos(int x, int y, int z)
    {
        return Origin + new float3(x, y, z) * nodeSize;
    }

    public float3 GetWorldPos(int3 xyz)
    {
        return GetWorldPos(xyz.x, xyz.y, xyz.z);
    }

    CollisionFilter collisionFilter = new CollisionFilter { BelongsTo = 1, CollidesWith = ~0u, GroupIndex = 0 };

    void SetGridNode(EntityManager entityManager, CollisionWorld collisionWorld, int i, int x, int y, int z)
    {
        float3 worldPos = GetWorldPos(x, y, z);
        float3 halfNodeSize = nodeSize * 0.5f;

        OverlapAabbInput aabbQuery = new OverlapAabbInput
        {
            Aabb = new Aabb { Min = worldPos - halfNodeSize, Max = worldPos + halfNodeSize },
            Filter = collisionFilter
        };
        
        var hitObjs = new NativeList<int>(Allocator.Temp);
        collisionWorld.OverlapAabb(aabbQuery, ref hitObjs);
        bool hitObstacle = false;

        // Editor does not send collider filter data to collision system so manual filter.
        for (int rI = 0; rI < hitObjs.Length; ++rI)
        {
            if (entityManager.HasComponent<ObstacleComponent>(collisionWorld.Bodies[hitObjs[rI]].Entity))
            {
                hitObstacle = true;
                break;
            }

            if (entityManager.HasComponent<BoidStationComponent>(collisionWorld.Bodies[hitObjs[rI]].Entity))
            {
                walkerNodes.Add(i);

                hitObstacle = true;
                break;
            }
        }

        nodeTraversableBits.Set(i, !hitObstacle);
    }

    public bool drawGizmos = true;

    void OnDrawGizmos()
    {
        if (!drawGizmos || !nodeTraversableBits.IsCreated)
            return;
        
        for (int z = 0; z < gridDimensions.z; z++)
        {
            for (int y = 0; y < gridDimensions.y; y++)
            {
                for (int x = 0; x < gridDimensions.x; x++)
                {
                    int3 pos = new int3(x, y, z);
                    bool traversable = nodeTraversableBits.IsSet(GetNodeI(pos));

                    if (traversable)
                        Gizmos.color = Color.green;
                    else
                        Gizmos.color = Color.red;

                    if (!traversable && !walkerNodes.Contains(GetNodeI(pos)))
                        Gizmos.DrawCube(GetWorldPos(x, y, z), nodeSize * 0.9f);
                }
            }
        }
    }

    int GetNodeI(int3 xyz)
    {
        int k = gridDimensions.y * gridDimensions.x * xyz.z;
        int j = k + gridDimensions.x * xyz.y;
        int i = j + xyz.x;
        return i;
    }

    int3 GetNodeXYZ(float3 worldPos)
    {
        int3 xyz = (int3)math.round((worldPos - Origin) / nodeSize);
        return xyz;
    }

    public bool FindPath(int3 start, int3 end, ref DynamicBuffer<int3> path)
    {
        return AStar(start, end, ref path);
    }

    public bool FindPath(float3 start, float3 end, ref DynamicBuffer<int3> path)
    {
        return AStar(GetNodeXYZ(start), GetNodeXYZ(end), ref path);
    }

    struct FNode : IComparable<FNode>
    {
        public int3 XYZ;
        public int FCost;
        public int HCost;

        public int CompareTo(FNode rhs)
        {
            var fCompare = FCost.CompareTo(rhs.FCost);
            if (fCompare != 0)
                return fCompare;

            var hCompare = HCost.CompareTo(rhs.HCost);
            if (hCompare != 0)
                return hCompare;

            int xCompare = XYZ.x.CompareTo(rhs.XYZ.x);
            if (xCompare != 0)
                return xCompare;

            int yCompare = XYZ.y.CompareTo(rhs.XYZ.y);
            if (yCompare != 0)
                return yCompare;

            int zCompare = XYZ.z.CompareTo(rhs.XYZ.z);
            return zCompare;
        }
    }

    int3 GetValidStartNode(int3 startXYZ)
    {
        startXYZ = math.clamp(startXYZ, 0, gridDimensions - 1);
        int startNodeI = GetNodeI(startXYZ);

        if (InBounds(startXYZ) && nodeTraversableBits.IsSet(startNodeI))
            return startXYZ;
        
        Dictionary<int, FNode> openSet = new Dictionary<int, FNode>();
        HashSet<int> closedSet = new HashSet<int>();

        openSet.Add(startNodeI, new FNode { XYZ = startXYZ });

        while (openSet.Count > 0)
        {
            startXYZ = openSet.First().Value.XYZ;
            startNodeI = GetNodeI(startXYZ);
            openSet.Remove(startNodeI);
            closedSet.Add(startNodeI);

            foreach (int3 neighbourOffset in neighbourOffsets)
            {
                int3 neighbourXYZ = startXYZ + neighbourOffset;
                if (!InBounds(neighbourXYZ))
                    continue;

                int neighbourI = GetNodeI(neighbourXYZ);
                if (nodeTraversableBits.IsSet(neighbourI))
                    return neighbourXYZ;

                if (!closedSet.Contains(neighbourI) && !openSet.ContainsKey(neighbourI))
                    openSet.Add(neighbourI, new FNode { XYZ = neighbourXYZ });
            }
        }
        
        return new int3(-1, -1, -1);
    }

    bool AStar(int3 startXYZ, int3 endXYZ, ref DynamicBuffer<int3> path)
    {
        startXYZ = GetValidStartNode(startXYZ);
        endXYZ = GetValidStartNode(endXYZ);
        
        if (!InBounds(startXYZ) || !InBounds(endXYZ))
            return false;
        
        int startNodeI = GetNodeI(startXYZ);
        int endNodeI = GetNodeI(endXYZ);
        
        if (!nodeTraversableBits.IsSet(endNodeI))
            return false;
        
        // G costs repersents the value of the total cost to reach a node from the start node.
        NativeArray<int> gCosts = new NativeArray<int>(nodeTraversableBits.Length, Allocator.Temp);
        NativeArray<int3> parentNodes = new NativeArray<int3>(nodeTraversableBits.Length, Allocator.Temp);

        // F cost as the key and the XYZ as the value.
        // F cost repersents the estimate cost to reach end from a node using the current best path cost and the heuristic value.
        SortedSet<FNode> fCosts = new SortedSet<FNode>();

        Dictionary<int, FNode> openSet = new Dictionary<int, FNode>();
        HashSet<int> closedSet = new HashSet<int>();

        fCosts.Add(new FNode { XYZ = startXYZ, FCost = GetDstTo(startXYZ, endXYZ) });
        openSet.Add(startNodeI, fCosts.Min());
        
        while (openSet.Count > 0)
        {
            int3 currentNodeXYZ = PopNextLowestCostNode(ref fCosts);
            int currentNodeI = GetNodeI(currentNodeXYZ);

            if (currentNodeI == endNodeI)
            {
                FinalizePath(ref path, startXYZ, endXYZ, parentNodes);
                return true;
            }

            openSet.Remove(currentNodeI);
            closedSet.Add(currentNodeI);

            CalculateNeighbourTraversalCosts(currentNodeXYZ, currentNodeI, endXYZ, 
                ref openSet, ref closedSet, 
                ref fCosts, ref gCosts, ref parentNodes
            );
        }
        
        return false;
    }

    int3 PopNextLowestCostNode(ref SortedSet<FNode> fCosts)
    {
        FNode fNode = fCosts.Min();
        fCosts.Remove(fNode);
        return fNode.XYZ;
    }

    void FinalizePath(ref DynamicBuffer<int3> path, int3 startXYZ, int3 endXYZ, in NativeArray<int3> parentNodes)
    {
        int3 currentNodeXYZ = endXYZ;
        float3 currentTraversalDir = float3.zero;

        path.Add(endXYZ);
        
        // Trace path from end to start.
        while (math.any(currentNodeXYZ != startXYZ))
        {
            int currentNodeI = GetNodeI(currentNodeXYZ);
            int3 parentNodeXYZ = parentNodes[currentNodeI];
            float3 traversalDir = math.normalize(currentNodeXYZ - parentNodeXYZ);
            
            // If the path has changed direction.
            if (math.all(traversalDir != currentTraversalDir))  
                path.Add(currentNodeXYZ);

            currentNodeXYZ = parentNodeXYZ;
            currentTraversalDir = traversalDir;
        }
    }

    static readonly int3[] neighbourOffsets;

    static AStarGrid()
    {
        neighbourOffsets = new int3[3*3*3];
        int i = 0;

        for (int z = -1; z <= 1; ++z)
        {
            for (int y = -1; y <= 1; ++y)
            {
                for (int x = -1; x <= 1; ++x)
                {
                    neighbourOffsets[i++] = new int3(x, y, z);
                }
            }
        }
    }

    // H costs repersents the value of the estimate cost from a node to the end node.
    public int GetDstTo(int3 a, int3 b)
    {
        // The Manhattan distance is the sum of the absolute values of the horizontal and the vertical distances.
        int3 abs = math.abs(a - b);
        return abs.x + abs.y + abs.z;
    }

    void CalculateNeighbourTraversalCosts(int3 currentNodeXYZ, int currentNodeI, int3 endXYZ,
        ref Dictionary<int, FNode> openSet, ref HashSet<int> closedSet,
        ref SortedSet<FNode> fCosts, ref NativeArray<int> gCosts, ref NativeArray<int3> parentNodes)
    {
        foreach (int3 neighbourOffset in neighbourOffsets)
        {
            int3 neighbourXYZ = currentNodeXYZ + neighbourOffset;
            if (!InBounds(neighbourXYZ))
                continue;

            int neighbourI = GetNodeI(neighbourXYZ);
            if (!nodeTraversableBits.IsSet(neighbourI) || closedSet.Contains(neighbourI))
                continue;
            
            // If traversing to this neighbour from the current node is cheaper then add/update it.
            int newGCost = gCosts[currentNodeI] + GetDstTo(currentNodeXYZ, neighbourXYZ);
            bool neighbourInOpenSet = openSet.ContainsKey(neighbourI);

            if (neighbourInOpenSet && newGCost >= gCosts[neighbourI])
                continue;

            // Update new neighbour costs.
            gCosts[neighbourI] = newGCost;
            parentNodes[neighbourI] = currentNodeXYZ;

            int hCost = GetDstTo(neighbourXYZ, endXYZ);
            int fCost = newGCost + hCost;
            FNode newFNode = new FNode { XYZ = neighbourXYZ, FCost = fCost, HCost = hCost };
            
            if (neighbourInOpenSet)
                fCosts.Remove(openSet[neighbourI]);

            fCosts.Add(newFNode);
            openSet[neighbourI] = newFNode;
        }
    }
}
