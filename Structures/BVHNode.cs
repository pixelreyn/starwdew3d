using System.Collections.Concurrent;
using Microsoft.Xna.Framework;
using StardewValley3D.Rendering;

namespace StardewValley3D.Structures;

public class BVHNode
{
    public BoundingBox Bounds;  // Bounding volume of this node
    public BVHNode? Left;        // Left child node
    public BVHNode? Right;       // Right child node
    public WorldObject? Object;  // The object stored in this node (only for leaf nodes)
    
    public bool IsLeaf => Object != null;
    
    public BVHNode(BoundingBox bounds)
    {
        this.Bounds = bounds;
        this.Left = null;
        this.Right = null;
        this.Object = null;
    }

}

public struct FlattenedBVHNode
{
    public BoundingBox Bounds;  // Bounding volume of this node
    public int LeftChildIndex;   // Index of the left child node, or -1 if this is a leaf
    public int RightChildIndex;  // Index of the right child node, or -1 if this is a leaf
    public WorldObject Object;  // The object stored in this node (only for leaf nodes)
    public int IsLeaf;
    
    public int Start; // Start index for tiles (used in leaf nodes)
    public int Count; // Number of tiles (used in leaf nodes)
    public FlattenedBVHNode(BoundingBox bounds, int leftChildIndex, int rightChildIndex, WorldObject obj, bool isLeaf)
    {
        Bounds = bounds;
        LeftChildIndex = leftChildIndex;
        RightChildIndex = rightChildIndex;
        Object = obj;
        Start = -1;
        Count = -1;
        IsLeaf = isLeaf ? 1 : 0;
    }

    private static int count = 0;
    public static void ResetCount() => count = 0;
    
    public static int ConstructBvh(List<WorldObject> _worldObjects, FlattenedBVHNode[] _flatBvhNodes,  int start, int end)
    {
        FlattenedBVHNode node = new FlattenedBVHNode();
        node.Start = start;
        node.Count = end - start;

        // Compute the bounding box for the current set of tiles
        node.Bounds = ComputeBoundingBox(_worldObjects, start, end);
        var nodeIndex = Interlocked.Add(ref count, 1) - 1;
        _flatBvhNodes[nodeIndex] = node;

        // If this is a leaf node, return
        if (node.Count == 1)
        {
            node.LeftChildIndex = -1;
            node.RightChildIndex = -1;
            node.IsLeaf = 1;
            node.Object = _worldObjects[node.Start];
            _flatBvhNodes[nodeIndex] = node; // Update node with children
            return nodeIndex;
        }

        // Determine axis to split on (using longest axis for example)
        Vector3 size = node.Bounds.Max - node.Bounds.Min;
        int axis = size.X > size.Y && size.X > size.Z ? 0 : (size.Y > size.Z ? 1 : 2);

        // Sort tiles based on the chosen axis
        _worldObjects.Sort(start, end - start, Comparer<WorldObject>.Create((a, b) =>
        {
            return axis switch
            {
                0 => a.Position.X.CompareTo(b.Position.X),
                1 => a.Position.Y.CompareTo(b.Position.Y),
                _ => a.Position.Z.CompareTo(b.Position.Z),
            };
        }));

        // Split the tiles into two groups and recurse
        int mid = start + node.Count / 2;
        Parallel.Invoke(
            () => node.LeftChildIndex = ConstructBvh(_worldObjects, _flatBvhNodes, start, mid),
            () => node.RightChildIndex = ConstructBvh(_worldObjects, _flatBvhNodes, mid, end)
        );

        // Update the current node with child information
        _flatBvhNodes[nodeIndex] = node; // Update node with children
        return nodeIndex;
    }

    private static BoundingBox ComputeBoundingBox(List<WorldObject> tiles, int start, int end)
    {
        Vector3 min = new Vector3(float.MaxValue);
        Vector3 max = new Vector3(float.MinValue);

        for (int i = start; i < end; i++)
        {
            WorldObject tile = tiles[i];
            BoundingBox tileBox = tile.Bounds;

            min = Vector3.Min(min, tileBox.Min);
            max = Vector3.Max(max, tileBox.Max);
        }

        return new BoundingBox(min, max);
    }
}