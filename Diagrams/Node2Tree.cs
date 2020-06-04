using System.Collections.Generic;

namespace Diagrams
{
    /// <summary>Frontend for a recursive Quad-tree Node structure.</summary>
    /// <exclude />
    public class Node2Tree
    {
        protected Node2List m_list;
        protected Node2Leaf m_root;

        public Node2Tree()
        {
        }

        public Node2Tree(Node2List owner)
        {
            this.m_list = owner;
        }

        public bool RecreateTree(double GrowthFactor, bool ForceSquareLeaves, int GroupLimit)
        {
            bool flag;
            if (this.m_list == null)
                flag = false;
            else if (this.m_list.Count == 0)
            {
                flag = false;
            }
            else
            {
                if (GroupLimit < 1)
                    GroupLimit = 1;
                double maxValue1 = double.MaxValue;
                double minValue1 = double.MinValue;
                double maxValue2 = double.MaxValue;
                double minValue2 = double.MinValue;
                if (!this.m_list.BoundingBox(GrowthFactor, ForceSquareLeaves, ref maxValue1, ref minValue1, ref maxValue2, ref minValue2))
                {
                    flag = false;
                }
                else
                {
                    this.m_root = new Node2Leaf(maxValue1, minValue1, maxValue2, minValue2);
                    this.m_root.SubDivide(this.m_list, (List<int>)null, GroupLimit);
                    flag = true;
                }
            }
            return flag;
        }

        public void PerformAction(Node2Leaf.LeafAction func, bool call_on_empty_leaves)
        {
            if (this.m_root == null)
                return;
            this.m_root.PerformLeafAction(func, call_on_empty_leaves);
        }

        public void PerformAction(Node2Leaf.ILeafAction func, bool call_on_empty_leaves)
        {
            if (this.m_root == null)
                return;
            this.m_root.PerformLeafAction(func, call_on_empty_leaves);
        }

        public void SolveProximity(Node2Proximity prox)
        {
            if (this.m_root == null)
                return;
            this.m_root.SolveProximity(this.m_list, prox);
        }
    }
}
