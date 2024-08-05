using Microsoft.Xna.Framework;

namespace StardewValley3D.Rendering;

public struct OctreeNode
{
    public BoundingBox BoundingBox;
    public int ChildIndex; // Index of the first child node (-1 if none)
    public int Start;      // Start index for objects (used in leaf nodes)
    public int Count;      // Number of objects (used in leaf nodes)

    public bool IsLeaf => ChildIndex == -1;
}

public struct FlatOctree
{
    public OctreeNode[] Nodes;
    public Object3D[] Objects;

    private int _maxDepth;
    private int _maxObjectsPerNode;

    public FlatOctree(BoundingBox boundingBox, List<Object3D> objects, int maxDepth, int maxObjectsPerNode)
    {
        _maxDepth = maxDepth;
        _maxObjectsPerNode = maxObjectsPerNode;
        Objects = objects.ToArray();
        Nodes = new OctreeNode[0];
        BuildOctree(boundingBox, 0, Objects.Length, 0);
    }

    private int BuildOctree(BoundingBox boundingBox, int start, int count, int depth)
    {
        int nodeIndex = Nodes.Length;
        Array.Resize(ref Nodes, nodeIndex + 1);

        if (count <= _maxObjectsPerNode || depth >= _maxDepth)
        {
            // Create a leaf node
            Nodes[nodeIndex] = new OctreeNode
            {
                BoundingBox = boundingBox,
                ChildIndex = -1,
                Start = start,
                Count = count
            };
            return nodeIndex;
        }

        // Create an internal node and subdivide
        Nodes[nodeIndex] = new OctreeNode
        {
            BoundingBox = boundingBox,
            ChildIndex = Nodes.Length,
            Start = -1,
            Count = -1
        };

        // Subdivide the bounding box
        Vector3 size = (boundingBox.Max - boundingBox.Min) / 2.0f;
        Vector3 mid = boundingBox.Min + size;

        var children = new BoundingBox[8];
        children[0] = new BoundingBox(boundingBox.Min, mid);
        children[1] = new BoundingBox(new Vector3(mid.X, boundingBox.Min.Y, boundingBox.Min.Z), new Vector3(boundingBox.Max.X, mid.Y, mid.Z));
        children[2] = new BoundingBox(new Vector3(boundingBox.Min.X, mid.Y, boundingBox.Min.Z), new Vector3(mid.X, boundingBox.Max.Y, mid.Z));
        children[3] = new BoundingBox(new Vector3(mid.X, mid.Y, boundingBox.Min.Z), new Vector3(boundingBox.Max.X, boundingBox.Max.Y, mid.Z));
        children[4] = new BoundingBox(new Vector3(boundingBox.Min.X, boundingBox.Min.Y, mid.Z), new Vector3(mid.X, mid.Y, boundingBox.Max.Z));
        children[5] = new BoundingBox(new Vector3(mid.X, boundingBox.Min.Y, mid.Z), new Vector3(boundingBox.Max.X, mid.Y, boundingBox.Max.Z));
        children[6] = new BoundingBox(new Vector3(boundingBox.Min.X, mid.Y, mid.Z), new Vector3(mid.X, boundingBox.Max.Y, boundingBox.Max.Z));
        children[7] = new BoundingBox(mid, boundingBox.Max);

        // Partition objects into children
        int[] childCounts = new int[8];
        int[] childStarts = new int[8];
        List<Object3D>[] childObjects = new List<Object3D>[8];

        for (int i = 0; i < 8; i++)
        {
            childObjects[i] = new List<Object3D>();
        }

        for (int i = start; i < start + count; i++)
        {
            BoundingBox objBox = Objects[i].GetBoundingBox();
            for (int j = 0; j < 8; j++)
            {
                if (children[j].Contains(objBox) != ContainmentType.Disjoint)
                {
                    childObjects[j].Add(Objects[i]);
                    break;
                }
            }
        }

        // Recursively build child nodes
        for (int i = 0; i < 8; i++)
        {
            childCounts[i] = childObjects[i].Count;
            childStarts[i] = Objects.Length;
            Objects = Objects.Concat(childObjects[i]).ToArray();
            Nodes[nodeIndex].ChildIndex = BuildOctree(children[i], childStarts[i], childCounts[i], depth + 1);
        }

        return nodeIndex;
    }
}