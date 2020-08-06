//using Pixel.Geometry;
using Rhino.Geometry;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Diagrams
{
    /// <summary>
    /// Represents a list of (un)sorted nodes.
    /// </summary>
    public class Node2List : IEnumerable<Node2>
    {
        /// <summary>
        /// Represents the different sort types that a VorNodeList can maintain.
        /// </summary>
        public enum NodeListSort
        {
            /// <summary>
            /// No specific sorting. When nodes are added or inserted the sort is always set back to none.
            /// </summary>
            none,
            /// <summary>
            /// Nodes are sorted by ascending x-coordinate
            /// </summary>
            X,
            /// <summary>
            /// Nodes are sorted by ascending y-coordinate
            /// </summary>
            Y,
            /// <summary>
            /// Nodes are sorted by ascending index
            /// </summary>
            Index
        }

        private class FuzzyNode2Comparer : IComparer<Node2>
        {
            private double m_fuzz;

            public FuzzyNode2Comparer(double fuzz)
            {
                m_fuzz = fuzz;
            }

            public int Compare(Node2 A, Node2 B)
            {
                if (A == null)
                {
                    if (B == null)
                    {
                        return 0;
                    }
                    return -1;
                }
                if (B == null)
                {
                    return 1;
                }
                double num = Math.Abs(A.x - B.x);
                double num2 = Math.Abs(A.y - B.y);
                if (num < m_fuzz && num2 < m_fuzz)
                {
                    return 0;
                }
                return A.CompareTo(B);
            }

            int IComparer<Node2>.Compare(Node2 A, Node2 B)
            {
                //ILSpy generated this explicit interface implementation from .override directive in Compare
                return this.Compare(A, B);
            }
        }

        private class Comparer_X : IComparer<Node2>
        {
            public int Compare(Node2 x, Node2 y)
            {
                if (x == null)
                {
                    if (y == null)
                    {
                        return 0;
                    }
                    return -1;
                }
                if (y == null)
                {
                    return 1;
                }
                return x.x.CompareTo(y.x);
            }

            int IComparer<Node2>.Compare(Node2 x, Node2 y)
            {
                //ILSpy generated this explicit interface implementation from .override directive in Compare
                return this.Compare(x, y);
            }
        }

        private class Comparer_Y : IComparer<Node2>
        {
            public int Compare(Node2 x, Node2 y)
            {
                if (x == null)
                {
                    if (y == null)
                    {
                        return 0;
                    }
                    return -1;
                }
                if (y == null)
                {
                    return 1;
                }
                return x.y.CompareTo(y.y);
            }

            int IComparer<Node2>.Compare(Node2 x, Node2 y)
            {
                //ILSpy generated this explicit interface implementation from .override directive in Compare
                return this.Compare(x, y);
            }
        }

        private class Comparer_I : IComparer<Node2>
        {
            public int Compare(Node2 x, Node2 y)
            {
                if (x == null)
                {
                    if (y == null)
                    {
                        return 0;
                    }
                    return -1;
                }
                if (y == null)
                {
                    return 1;
                }
                return x.tag.CompareTo(y.tag);
            }

            int IComparer<Node2>.Compare(Node2 x, Node2 y)
            {
                //ILSpy generated this explicit interface implementation from .override directive in Compare
                return this.Compare(x, y);
            }
        }

        private List<Node2> m_nodes;

        private NodeListSort m_sort;

        /// <summary>
        /// Get or set a node. Be sure to call ExpireSequence() if your change affects sorting caches.
        /// </summary>
        /// <param name="i">Index of node</param>
        [IndexerName("Node")]
        public Node2 this[int i]
        {
            get
            {
                return m_nodes[i];
            }
            set
            {
                m_nodes[i] = value;
            }
        }

        /// <summary>
        /// Get the number of nodes contained in this list.
        /// </summary>
        public int Count => m_nodes.Count;

        /// <summary>
        /// Gets or sets the capacity of this list (i.e. the number of items this list can contain without resizing)
        /// </summary>
        public int Capacity
        {
            get
            {
                return m_nodes.Capacity;
            }
            set
            {
                m_nodes.Capacity = value;
            }
        }

        public List<Node2> InternalList
        {
            get
            {
                return m_nodes;
            }
            set
            {
                m_nodes = value;
                ExpireSequence();
            }
        }

        public Node2List()
        {
            m_nodes = new List<Node2>();
            m_sort = NodeListSort.none;
        }

        public Node2List(IEnumerable<Node2> L)
        {
            m_nodes = new List<Node2>();
            m_sort = NodeListSort.none;
            m_nodes.AddRange(L);
        }

        public Node2List(Node2List L)
        {
            m_nodes = new List<Node2>();
            m_sort = NodeListSort.none;
            m_nodes.Capacity = L.m_nodes.Count;
            m_sort = L.m_sort;
            int num = L.m_nodes.Count - 1;
            for (int i = 0; i <= num; i++)
            {
                if (L.m_nodes[i] == null)
                {
                    m_nodes.Add(null);
                }
                else
                {
                    m_nodes.Add(new Node2(L.m_nodes[i]));
                }
            }
        }

        public Node2List(IEnumerable<Point3d> pts)
        {
            m_nodes = new List<Node2>();
            m_sort = NodeListSort.none;
            if (pts != null)
            {
                foreach (Point3d pt in pts)
                {
                    m_nodes.Add(new Node2(pt.X, pt.Y));
                }
            }
        }

        /// <summary>
        /// Add a single node to this list. This will reset all sorting flags and caches.
        /// </summary>
        /// <param name="node">Node to add</param>
        public void Append(Node2 node)
        {
            m_nodes.Add(node);
            ExpireSequence();
        }

        /// <summary>
        /// Add a range of nodes to this list. This will reset all sorting flags and caches.
        /// </summary>
        /// <param name="nodes">Nodes to add</param>
        public void AppendRange(IEnumerable<Node2> nodes)
        {
            m_nodes.AddRange(nodes);
            ExpireSequence();
        }

        /// <summary>
        /// Insert a single node into this list. This will reset all sorting flags and caches.
        /// </summary>
        /// <param name="node">Node to add</param>
        /// <param name="index">Index at which to insert the node</param>
        public void Insert(int index, Node2 node)
        {
            m_nodes.Insert(index, node);
            ExpireSequence();
        }

        /// <summary>
        /// Insert a range of nodes into this list. This will reset all sorting flags and caches.
        /// </summary>
        /// <param name="nodes">Nodes to add</param>
        /// <param name="index">Index at which insertion begins</param>
        public void InsertRange(int index, IEnumerable<Node2> nodes)
        {
            m_nodes.InsertRange(index, nodes);
            ExpireSequence();
        }

        /// <summary>
        /// Remove a single node from this list. Sorting flags are maintained but caches are destroyed.
        /// </summary>
        /// <param name="node">Node to remove.</param>
        /// <returns>True on success, false if the provided Node does not occur in this list.</returns>
        public bool Remove(Node2 node)
        {
            return m_nodes.Remove(node);
        }

        /// <summary>
        /// Remove the node at the specified index. Sorting flags are maintained but caches are destroyed.
        /// </summary>
        /// <param name="index">Index of node to remove</param>
        public void RemoveAt(int index)
        {
            m_nodes.RemoveAt(index);
        }

        /// <summary>
        /// Removes all duplicates from this list. It also removes ALL null references.
        /// </summary>
        public int CullDuplicates()
        {
            FuzzyNode2Comparer comparer = new FuzzyNode2Comparer(1E-12);
            SortedDictionary<Node2, int> sortedDictionary = new SortedDictionary<Node2, int>(comparer);
            int num = 0;
            int num2 = 0;
            int num3 = m_nodes.Count - 1;
            for (int i = 0; i <= num3; i++)
            {
                if (m_nodes[i] == null || sortedDictionary.ContainsKey(m_nodes[i]))
                {
                    num2++;
                    continue;
                }
                sortedDictionary.Add(m_nodes[i], 1);
                m_nodes[num] = m_nodes[i];
                num++;
            }
            if (num > 0)
            {
                m_nodes.RemoveRange(num, m_nodes.Count - num);
            }
            return num2;
        }

        /// <summary>
        /// Set all duplicate nodes to NULL
        /// </summary>
        public int NullifyDuplicates()
        {
            FuzzyNode2Comparer comparer = new FuzzyNode2Comparer(1E-12);
            SortedDictionary<Node2, int> sortedDictionary = new SortedDictionary<Node2, int>(comparer);
            int num = 0;
            int num2 = m_nodes.Count - 1;
            for (int i = 0; i <= num2; i++)
            {
                if (m_nodes[i] != null)
                {
                    if (sortedDictionary.ContainsKey(m_nodes[i]))
                    {
                        num++;
                        m_nodes[i] = null;
                    }
                    else
                    {
                        sortedDictionary.Add(m_nodes[i], 1);
                    }
                }
            }
            return num;
        }

        /// <summary>
        /// Remove all null references from this list.
        /// </summary>
        public int CullNullRefs()
        {
            int num = 0;
            int num2 = 0;
            int num3 = m_nodes.Count - 1;
            for (int i = 0; i <= num3; i++)
            {
                if (m_nodes[i] == null)
                {
                    num2++;
                    continue;
                }
                m_nodes[num] = m_nodes[i];
                num++;
            }
            if (num > 0)
            {
                m_nodes.RemoveRange(num, m_nodes.Count - num);
            }
            return num2;
        }

        /// <summary>
        /// Randomly mutate all node locations
        /// </summary>
        /// <param name="amount">Maximum distance to move in x and y directions.</param>
        public void JitterNodes(double amount)
        {
            Random random = new Random(623);
            int num = m_nodes.Count - 1;
            for (int i = 0; i <= num; i++)
            {
                if (m_nodes[i] != null)
                {
                    m_nodes[i].x += amount * random.NextDouble() - 0.5 * amount;
                    m_nodes[i].y += amount * random.NextDouble() - 0.5 * amount;
                }
            }
            ExpireSequence();
        }

        /// <summary>
        /// Call this method when you made a change that potentially invalidates the sorting flags and caches.
        /// </summary>
        public void ExpireSequence()
        {
            m_sort = NodeListSort.none;
        }

        /// <summary>
        /// Sort the list using a sorting type.
        /// </summary>
        /// <param name="type">Type of sorting algorithm.</param>
        public void Sort(NodeListSort type)
        {
            if (m_sort != type)
            {
                m_sort = type;
                switch (type)
                {
                    case NodeListSort.none:
                        break;
                    case NodeListSort.X:
                        m_nodes.Sort(Comparison_XAscending);
                        break;
                    case NodeListSort.Y:
                        m_nodes.Sort(Comparison_YAscending);
                        break;
                    case NodeListSort.Index:
                        m_nodes.Sort(Comparison_IAscending);
                        break;
                }
            }
        }

        /// <summary>
        /// Reset all indices of all nodes by renumbering them in their current order. 
        /// Nulls are ignored but they do affect the numbering.
        /// </summary>
        public void RenumberNodes()
        {
            if (m_nodes.Count == 0)
            {
                return;
            }
            int num = m_nodes.Count - 1;
            for (int i = 0; i <= num; i++)
            {
                if (m_nodes[i] != null)
                {
                    m_nodes[i].tag = i;
                }
            }
        }

        private static int Comparison_XAscending(Node2 A, Node2 B)
        {
            if (A == null)
            {
                if (B == null)
                {
                    return 0;
                }
                return -1;
            }
            if (B == null)
            {
                return 1;
            }
            int num = A.x.CompareTo(B.x);
            if (num == 0)
            {
                return A.y.CompareTo(B.y);
            }
            return num;
        }

        private static int Comparison_YAscending(Node2 A, Node2 B)
        {
            if (A == null)
            {
                if (B == null)
                {
                    return 0;
                }
                return -1;
            }
            if (B == null)
            {
                return 1;
            }
            int num = A.y.CompareTo(B.y);
            if (num == 0)
            {
                return A.x.CompareTo(B.x);
            }
            return num;
        }

        private static int Comparison_IAscending(Node2 A, Node2 B)
        {
            if (A == null)
            {
                if (B == null)
                {
                    return 0;
                }
                return -1;
            }
            if (B == null)
            {
                return 1;
            }
            return A.tag.CompareTo(B.tag);
        }

        public int BinarySearch_X(double x)
        {
            if (m_sort != NodeListSort.X)
            {
                throw new Exception("Invalid BinarySearch operation for sort cache");
            }
            return m_nodes.BinarySearch(new Node2(x, 0.0, 0), new Comparer_X());
        }

        public int BinarySearch_Y(double y)
        {
            if (m_sort != NodeListSort.Y)
            {
                throw new Exception("Invalid BinarySearch operation for sort cache");
            }
            return m_nodes.BinarySearch(new Node2(0.0, y, 0), new Comparer_Y());
        }

        public int BinarySearch_I(int i)
        {
            if (m_sort != NodeListSort.Index)
            {
                throw new Exception("Invalid BinarySearch operation for sort cache");
            }
            return m_nodes.BinarySearch(new Node2(0.0, 0.0, i), new Comparer_I());
        }

        /// <summary>
        /// Perform a brute force node search for the N nearest nodes in the set.
        /// </summary>
        /// <param name="x">X coordinate of search start.</param>
        /// <param name="y">Y coordinate of search start.</param>
        /// <param name="N"></param>
        /// <param name="min_dist_squared">Minimum distance threshold, use any negative value to ignore this setting.</param>
        /// <param name="max_dist_squared">Maximum distance threshold.</param>
        /// <returns>The N (or fewer) results, sorted by ascending distance.</returns>
        public List<int> NearestNodes(double x, double y, int N, double min_dist_squared = double.MinValue, double max_dist_squared = double.MaxValue)
        {
            List<double> list = new List<double>();
            List<int> list2 = new List<int>();
            int num = m_nodes.Count - 1;
            for (int i = 0; i <= num; i++)
            {
                if (m_nodes[i] != null)
                {
                    double num2 = m_nodes[i].DistanceSquared(x, y);
                    if (!(num2 > max_dist_squared) && !(num2 < min_dist_squared))
                    {
                        list.Add(num2);
                        list2.Add(i);
                    }
                }
            }
            double[] keys = list.ToArray();
            int[] array = list2.ToArray();
            Array.Sort(keys, array);
            list2.Clear();
            list2.Capacity = array.Length;
            list2.AddRange(array);
            if (list2.Count > N)
            {
                list2.RemoveRange(N, list2.Count - N);
            }
            return list2;
        }

        /// <summary>
        /// Compute the bounding box of all Nodes.
        /// </summary>
        /// <param name="GrowthFactor">Factor by which to grow the boundingbox</param>
        /// <param name="ForceSquareLeaves">If True, the boundingbox will be renormalized</param>
        public bool BoundingBox(double GrowthFactor, bool ForceSquareLeaves, ref double x0, ref double x1, ref double y0, ref double y1)
        {
            if (m_nodes.Count == 0)
            {
                return false;
            }
            x0 = double.MaxValue;
            x1 = double.MinValue;
            y0 = double.MaxValue;
            y1 = double.MinValue;
            int num = m_nodes.Count - 1;
            for (int i = 0; i <= num; i++)
            {
                if (m_nodes[i] != null)
                {
                    x0 = Math.Min(x0, m_nodes[i].x);
                    x1 = Math.Max(x1, m_nodes[i].x);
                    y0 = Math.Min(y0, m_nodes[i].y);
                    y1 = Math.Max(y1, m_nodes[i].y);
                }
            }
            double num2 = x1 - x0;
            double num3 = y1 - y0;
            x0 -= GrowthFactor * num2;
            x1 += GrowthFactor * num2;
            y0 -= GrowthFactor * num3;
            y1 += GrowthFactor * num3;
            if (ForceSquareLeaves)
            {
                if (num3 > num2)
                {
                    x0 -= 0.5 * (num3 - num2);
                    x1 += 0.5 * (num3 - num2);
                }
                if (num2 > num3)
                {
                    y0 -= 0.5 * (num2 - num3);
                    y1 += 0.5 * (num2 - num3);
                }
            }
            return true;
        }

        /// <summary>
        /// Create a VorNodeTree that contains all the Nodes in this list.
        /// </summary>
        /// <param name="GrowthFactor">Factor by which to grow the boundingbox of the topmost Leaf</param>
        /// <param name="SquareLeaves">If True, the boundingbox will be normalized</param>
        /// <param name="GroupLimit">Maximum number of Nodes that are allowed to share a single Leaf</param>
        public Node2Tree CreateTree(double GrowthFactor, bool SquareLeaves, int GroupLimit)
        {
            Node2Tree node2Tree = new Node2Tree(this);
            node2Tree.RecreateTree(GrowthFactor, SquareLeaves, GroupLimit);
            return node2Tree;
        }

        public IEnumerator<Node2> GetEnumerator()
        {
            return m_nodes.GetEnumerator();
        }

        IEnumerator<Node2> IEnumerable<Node2>.GetEnumerator()
        {
            //ILSpy generated this explicit interface implementation from .override directive in GetEnumerator
            return this.GetEnumerator();
        }

        public IEnumerator GetEnumerator1()
        {
            return m_nodes.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            //ILSpy generated this explicit interface implementation from .override directive in GetEnumerator1
            return this.GetEnumerator1();
        }
    }

}
