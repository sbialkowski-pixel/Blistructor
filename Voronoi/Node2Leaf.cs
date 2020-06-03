using System;
using System.Collections.Generic;

namespace Diagrams
{
    /// <summary>Represents a single element in a recursive quad-tree Node structure.</summary>
    /// <exclude />
    public class Node2Leaf
    {
        protected List<int> m_nodes;
        protected double m_x0;
        protected double m_x1;
        protected double m_y0;
        protected double m_y1;
        protected Node2Leaf m_A;
        protected Node2Leaf m_B;
        protected Node2Leaf m_C;
        protected Node2Leaf m_D;

        /// <summary>Blank constructor</summary>
        public Node2Leaf()
        {
            this.m_A = (Node2Leaf)null;
            this.m_B = (Node2Leaf)null;
            this.m_C = (Node2Leaf)null;
            this.m_D = (Node2Leaf)null;
        }

        /// <summary>Box constructor. Create an empty leaf with a specific box.</summary>
        public Node2Leaf(double x0, double x1, double y0, double y1)
        {
            this.m_A = (Node2Leaf)null;
            this.m_B = (Node2Leaf)null;
            this.m_C = (Node2Leaf)null;
            this.m_D = (Node2Leaf)null;
            this.m_x0 = x0;
            this.m_x1 = x1;
            this.m_y0 = y0;
            this.m_y1 = y1;
        }

        /// <summary>Copy constructor</summary>
        /// <param name="other">Leaf to mimic</param>
        public Node2Leaf(Node2Leaf other)
        {
            this.m_A = (Node2Leaf)null;
            this.m_B = (Node2Leaf)null;
            this.m_C = (Node2Leaf)null;
            this.m_D = (Node2Leaf)null;
            if (other == null)
                throw new ArgumentNullException(nameof(other));
            this.m_x0 = other.m_x0;
            this.m_x1 = other.m_x1;
            this.m_y0 = other.m_y0;
            this.m_y1 = other.m_y1;
            if (other.m_nodes != null)
            {
                this.m_nodes = new List<int>();
                this.m_nodes.AddRange((IEnumerable<int>)other.m_nodes);
            }
            if (other.m_A != null)
                this.m_A = new Node2Leaf(other.m_A);
            if (other.m_B != null)
                this.m_B = new Node2Leaf(other.m_B);
            if (other.m_C != null)
                this.m_C = new Node2Leaf(other.m_C);
            if (other.m_D == null)
                return;
            this.m_D = new Node2Leaf(other.m_D);
        }

        /// <summary>Gets the indices of all nodes which are contained in this leaf.</summary>
        public List<int> Nodes
        {
            get
            {
                return this.m_nodes;
            }
        }

        /// <summary>Builds a subtree from a Node list.</summary>
        /// <param name="nodes">All the nodes that are supposed to be included in this tree.</param>
        /// <param name="index_subset">A subset of nodes on which to perform the subdivision. Pass a null-pointer to use ALL nodes.
        /// If the list is not null, used indices will be extracted from the list, in order to reduce the number of pointless inclusion tests.</param>
        /// <param name="group_limit">The number of allowed Nodes per Leaf. If a Leaf contains more than N nodes, it will subdivide.</param>
        public void SubDivide(Node2List nodes, List<int> index_subset, int group_limit)
        {
            this.m_nodes = new List<int>(nodes.Count);
            this.m_A = (Node2Leaf)null;
            this.m_B = (Node2Leaf)null;
            this.m_C = (Node2Leaf)null;
            this.m_D = (Node2Leaf)null;
            if (index_subset == null)
            {
                int num = nodes.Count - 1;
                for (int index = 0; index <= num; ++index)
                    this.m_nodes.Add(index);
            }
            else
            {
                int num1 = index_subset.Count - 1;
                int index1 = 0;
                int num2 = num1;
                for (int index2 = 0; index2 <= num2; ++index2)
                {
                    int index3 = index_subset[index2];
                    Node2 node = nodes[index3];
                    if (node != null)
                    {
                        if (this.Contains(node.x, node.y))
                        {
                            this.m_nodes.Add(index3);
                        }
                        else
                        {
                            index_subset[index1] = index3;
                            ++index1;
                        }
                    }
                }
                if (index1 < index_subset.Count)
                {
                    if (index1 == 0)
                        index_subset.Clear();
                    else
                        index_subset.RemoveRange(index1, index_subset.Count - index1);
                }
            }
            double num3 = (this.m_x1 - this.m_x0) * (this.m_x1 - this.m_x0) + (this.m_y1 - this.m_y0) * (this.m_y1 - this.m_y0);
            if (this.m_nodes.Count > group_limit && num3 > 1E-08)
            {
                this.CreateSubLeafs();
                this.m_A.SubDivide(nodes, this.m_nodes, group_limit);
                this.m_B.SubDivide(nodes, this.m_nodes, group_limit);
                this.m_C.SubDivide(nodes, this.m_nodes, group_limit);
                this.m_D.SubDivide(nodes, this.m_nodes, group_limit);
                this.TrimSubLeafs();
                this.m_nodes = (List<int>)null;
            }
            else if (this.m_nodes.Count == 0)
                this.m_nodes = (List<int>)null;
            else
                this.m_nodes.TrimExcess();
        }

        private void CreateSubLeafs()
        {
            double xMid = this.x_mid;
            double yMid = this.y_mid;
            this.m_A = new Node2Leaf(this.m_x0, xMid, this.m_y0, yMid);
            this.m_B = new Node2Leaf(xMid, this.m_x1, this.m_y0, yMid);
            this.m_C = new Node2Leaf(xMid, this.m_x1, yMid, this.m_y1);
            this.m_D = new Node2Leaf(this.m_x0, xMid, yMid, this.m_y1);
        }

        private void TrimSubLeafs()
        {
            if (this.m_A != null && this.m_A.SubLeafCount == 0 && (this.m_A.Nodes == null || this.m_A.Nodes.Count == 0))
                this.m_A = (Node2Leaf)null;
            if (this.m_B != null && this.m_B.SubLeafCount == 0 && (this.m_B.Nodes == null || this.m_B.Nodes.Count == 0))
                this.m_B = (Node2Leaf)null;
            if (this.m_C != null && this.m_C.SubLeafCount == 0 && (this.m_C.Nodes == null || this.m_C.Nodes.Count == 0))
                this.m_C = (Node2Leaf)null;
            if (this.m_D == null || this.m_D.SubLeafCount != 0 || this.m_D.Nodes != null && this.m_D.Nodes.Count != 0)
                return;
            this.m_D = (Node2Leaf)null;
        }

        /// <summary>Gets the left bounary of the Leaf space.</summary>
        public double x_min
        {
            get
            {
                return this.m_x0;
            }
        }

        /// <summary>Gets the right bounary of the Leaf space.</summary>
        public double x_max
        {
            get
            {
                return this.m_x1;
            }
        }

        /// <summary>Gets the bottom bounary of the Leaf space.</summary>
        public double y_min
        {
            get
            {
                return this.m_y0;
            }
        }

        /// <summary>Gets the top bounary of the Leaf space.</summary>
        public double y_max
        {
            get
            {
                return this.m_y1;
            }
        }

        /// <summary>Gets the vertical center axis of the Leaf space.</summary>
        public double x_mid
        {
            get
            {
                return 0.5 * (this.m_x0 + this.m_x1);
            }
        }

        /// <summary>Gets the horizontal center axis of the Leaf space.</summary>
        public double y_mid
        {
            get
            {
                return 0.5 * (this.m_y0 + this.m_y1);
            }
        }

        public Node2Leaf A
        {
            get
            {
                return this.m_A;
            }
        }

        public Node2Leaf B
        {
            get
            {
                return this.m_B;
            }
        }

        public Node2Leaf C
        {
            get
            {
                return this.m_C;
            }
        }

        public Node2Leaf D
        {
            get
            {
                return this.m_D;
            }
        }

        public int SubLeafCount
        {
            get
            {
                int num = 0;
                if (this.m_A != null)
                    ++num;
                if (this.m_B != null)
                    ++num;
                if (this.m_C != null)
                    ++num;
                if (this.m_D != null)
                    ++num;
                return num;
            }
        }

        public bool Contains(double x, double y)
        {
            return x >= this.m_x0 && (x <= this.m_x1 && (y >= this.m_y0 && y <= this.m_y1));
        }

        public double MinimumDistance(double x, double y)
        {
            return Math.Sqrt(this.MinimumDistanceSquared(x, y));
        }

        public double MinimumDistanceSquared(double x, double y)
        {
            double num1 = 0.0;
            double num2 = 0.0;
            if (x < this.m_x0)
                num1 = this.m_x0 - x;
            else if (x > this.m_x1)
                num1 = x - this.m_x1;
            if (y < this.m_y0)
                num2 = this.m_y0 - y;
            else if (y > this.m_y1)
                num2 = y - this.m_y1;
            return num1 * num1 + num2 * num2;
        }

        public double MaximumDistance(double x, double y)
        {
            return Math.Sqrt(this.MaximumDistanceSquared(x, y));
        }

        public double MaximumDistanceSquared(double x, double y)
        {
            double xMid = this.x_mid;
            double yMid = this.y_mid;
            double num1 = xMid - this.x_min;
            double num2 = yMid - this.y_min;
            if (x < xMid)
                num1 = this.m_x1 - x;
            else if (x > xMid)
                num1 = x - this.m_x0;
            if (y < yMid)
                num2 = this.m_y1 - y;
            else if (y > yMid)
                num2 = y - this.m_y0;
            return num1 * num1 + num2 * num2;
        }

        public void PerformLeafAction(Node2Leaf.LeafAction func, bool call_on_empty_leaves)
        {
            if (call_on_empty_leaves)
            {
                if (func(this) == Node2Leaf.VorLeafRecursionResult.Abort)
                    return;
            }
            else if (this.m_nodes != null && this.m_nodes.Count != 0 && func(this) == Node2Leaf.VorLeafRecursionResult.Abort)
                return;
            if (this.m_A != null)
                this.m_A.PerformLeafAction(func, call_on_empty_leaves);
            if (this.m_B != null)
                this.m_B.PerformLeafAction(func, call_on_empty_leaves);
            if (this.m_C != null)
                this.m_C.PerformLeafAction(func, call_on_empty_leaves);
            if (this.m_D == null)
                return;
            this.m_D.PerformLeafAction(func, call_on_empty_leaves);
        }

        public void PerformLeafAction(Node2Leaf.ILeafAction func, bool call_on_empty_leaves)
        {
            if (call_on_empty_leaves)
            {
                if (func.LeafAction(this) == Node2Leaf.VorLeafRecursionResult.Abort)
                    return;
            }
            else if (this.m_nodes != null && this.m_nodes.Count != 0 && func.LeafAction(this) == Node2Leaf.VorLeafRecursionResult.Abort)
                return;
            if (this.m_A != null)
                this.m_A.PerformLeafAction(func, call_on_empty_leaves);
            if (this.m_B != null)
                this.m_B.PerformLeafAction(func, call_on_empty_leaves);
            if (this.m_C != null)
                this.m_C.PerformLeafAction(func, call_on_empty_leaves);
            if (this.m_D == null)
                return;
            this.m_D.PerformLeafAction(func, call_on_empty_leaves);
        }

        /// <summary>Recursive method that solves a proximity search.</summary>
        /// <param name="nodes">Node set to search</param>
        /// <param name="prox">Proximity object to search with</param>
        public void SolveProximity(Node2List nodes, Node2Proximity prox)
        {
            double d0 = default(double);
            double d1 = default(double);
            prox.DistanceRange(ref d0, ref d1);
            double num1 = this.MinimumDistanceSquared(prox.Start.x, prox.Start.y);
            if (num1 >= d1 || num1 > prox.MaxSearchRadiusSquared || this.MaximumDistanceSquared(prox.Start.x, prox.Start.y) <= prox.MinSearchRadiusSquared)
                return;
            if (this.m_nodes == null)
            {
                Node2Leaf[] node2LeafArray;
                if (prox.Start.x < this.x_mid)
                {
                    if (prox.Start.y < this.y_mid)
                        node2LeafArray = new Node2Leaf[4]
                        {
              this.m_A,
              this.m_B,
              this.m_D,
              this.m_C
                        };
                    else
                        node2LeafArray = new Node2Leaf[4]
                        {
              this.m_D,
              this.m_A,
              this.m_C,
              this.m_B
                        };
                }
                else if (prox.Start.y < this.y_mid)
                    node2LeafArray = new Node2Leaf[4]
                    {
            this.m_B,
            this.m_A,
            this.m_C,
            this.m_D
                    };
                else
                    node2LeafArray = new Node2Leaf[4]
                    {
            this.m_C,
            this.m_D,
            this.m_B,
            this.m_A
                    };
                int num2 = node2LeafArray.Length - 1;
                for (int index = 0; index <= num2; ++index)
                {
                    if (node2LeafArray[index] != null)
                        node2LeafArray[index].SolveProximity(nodes, prox);
                }
            }
            else
            {
                if (this.m_nodes == null)
                    return;
                int num2 = this.m_nodes.Count - 1;
                for (int index = 0; index <= num2; ++index)
                    prox.RegisterNode(nodes[this.m_nodes[index]], this.m_nodes[index]);
            }
        }

        public enum VorLeafRecursionResult
        {
            Abort = -1, // 0xFFFFFFFF
            Continue = 0,
        }

        public delegate Node2Leaf.VorLeafRecursionResult LeafAction(Node2Leaf Leaf);

        public interface ILeafAction
        {
            Node2Leaf.VorLeafRecursionResult LeafAction(Node2Leaf Leaf);
        }
    }
}
