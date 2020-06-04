using System;
using System.Collections.Generic;

namespace Diagrams
{
    /// <summary>Maintains settings and results for node proximity searches.</summary>
    /// <exclude />
    public class Node2Proximity
    {
        protected int m_max_count;
        protected int m_cur_count;
        protected double m_min_distance;
        protected double m_max_distance;
        protected double m_min_2;
        protected double m_max_2;
        protected Node2 m_base;
        protected int m_base_index;
        protected List<double> m_D;
        protected List<int> m_I;

        /// <summary>Create a new instance for a single result.</summary>
        /// <param name="search_start">Node to search from.</param>
        /// <param name="search_start_index">Index of base node (this index will be ignored during the search).</param>
        public Node2Proximity(Node2 search_start, int search_start_index)
          : this(search_start, search_start_index, 1)
        {
        }

        /// <summary>Create a new instance with a specific search result count</summary>
        /// <param name="search_start">Node to search from.</param>
        /// <param name="search_start_index">Index of base node (this index will be ignored during the search).</param>
        /// <param name="max_results">Maximum number of results to return.</param>
        public Node2Proximity(Node2 search_start, int search_start_index, int max_results)
        {
            this.m_max_count = 1;
            this.m_cur_count = 0;
            this.m_min_distance = double.MinValue;
            this.m_max_distance = double.MaxValue;
            this.m_min_2 = double.MinValue;
            this.m_max_2 = double.MaxValue;
            this.m_base_index = -1;
            this.m_D = new List<double>();
            this.m_I = new List<int>();
            this.m_base = search_start;
            this.m_base_index = search_start_index;
            this.m_max_count = Math.Max(max_results, 1);
            this.ResetLists();
        }

        /// <summary>Create a new instance with a specific search result count</summary>
        /// <param name="search_start">Node to search from.</param>
        /// <param name="search_start_index">Index of base node (this index will be ignored during the search).</param>
        /// <param name="max_results">Maximum number of results to return.</param>
        /// <param name="min_distance">Minimum allowed distance for search results.</param>
        /// <param name="max_distance">Maximum allowed distance for search results.</param>
        public Node2Proximity(
          Node2 search_start,
          int search_start_index,
          int max_results,
          double min_distance,
          double max_distance)
          : this(search_start, search_start_index, max_results)
        {
            this.m_min_distance = min_distance;
            this.m_max_distance = max_distance;
            this.m_max_2 = this.m_max_distance < Math.Sqrt(1E+300) ? Math.Pow(this.m_max_distance, 2.0) : double.MaxValue;
            if (this.m_min_distance < 0.0)
                this.m_min_2 = double.MinValue;
            else
                this.m_min_2 = Math.Pow(this.m_min_distance, 2.0);
        }

        /// <summary>
        /// This function resets all search results and clears all caches.
        /// It is called once prior to a search operation.
        /// </summary>
        public void ResetLists()
        {
            this.m_cur_count = 0;
            this.m_D.Clear();
            this.m_I.Clear();
            this.m_D.Capacity = this.m_max_count;
            this.m_I.Capacity = this.m_max_count;
            int maxCount = this.m_max_count;
            for (int index = 1; index <= maxCount; ++index)
            {
                this.m_D.Add(double.MaxValue);
                this.m_I.Add(-1);
            }
        }

        /// <summary>Gets the node from which the search is performed</summary>
        public Node2 Start
        {
            get
            {
                return this.m_base;
            }
        }

        /// <summary>
        /// Gets the index of the node from which the search is performed.
        /// If the search start node is not part of the search set, this index will be negative.
        /// </summary>
        public int StartIndex
        {
            get
            {
                return this.m_base_index;
            }
        }

        /// <summary>Gets the maximum result count.</summary>
        public int MaximumCount
        {
            get
            {
                return this.m_max_count;
            }
        }

        /// <summary>Gets the number of results currently stored.</summary>
        public int CurrentCount
        {
            get
            {
                return this.m_cur_count;
            }
        }

        /// <summary>Gets the minimum search radius</summary>
        public double MinSearchRadius
        {
            get
            {
                return this.m_min_distance;
            }
        }

        /// <summary>Gets the maximum search radius</summary>
        public double MaxSearchRadius
        {
            get
            {
                return this.m_max_distance;
            }
        }

        /// <summary>Gets the minimum search radius, squared</summary>
        public double MinSearchRadiusSquared
        {
            get
            {
                return this.m_min_2;
            }
        }

        /// <summary>Gets the maximum search radius, squared</summary>
        public double MaxSearchRadiusSquared
        {
            get
            {
                return this.m_max_2;
            }
        }

        /// <summary>Gets the index of the nearest point in the current solution.</summary>
        public int NearestPoint
        {
            get
            {
                return this.m_cur_count != 0 ? this.m_I[0] : -1;
            }
        }

        /// <summary>Gets the index of the furthest point in the current solution.</summary>
        public int FurthestPoint
        {
            get
            {
                return this.m_cur_count != 0 ? this.m_I[this.m_cur_count - 1] : -1;
            }
        }

        /// <summary>Gets the distance to the nearest point in the current solution.</summary>
        public double NearestDistance
        {
            get
            {
                return this.m_cur_count != 0 ? Math.Sqrt(this.m_D[0]) : double.MinValue;
            }
        }

        /// <summary>Gets the squared distance to the nearest point in the current solution.</summary>
        public double NearestDistanceSquared
        {
            get
            {
                return this.m_cur_count != 0 ? this.m_D[0] : double.MinValue;
            }
        }

        /// <summary>Gets the distance to the furthest point in the current solution.</summary>
        public double FurthestDistance
        {
            get
            {
                return this.m_cur_count != 0 ? Math.Sqrt(this.m_D[this.m_cur_count - 1]) : double.MaxValue;
            }
        }

        /// <summary>Gets the squared distance to the furthest point in the current solution.</summary>
        public double FurthestDistanceSquared
        {
            get
            {
                return this.m_cur_count != 0 ? this.m_D[this.m_cur_count - 1] : double.MaxValue;
            }
        }

        /// <summary>Gets a list representing the sorted indices in the current solution.</summary>
        public List<int> IndexList
        {
            get
            {
                List<int> intList = new List<int>(this.m_max_count);
                int num = this.m_I.Count - 1;
                for (int index = 0; index <= num && this.m_I[index] >= 0; ++index)
                    intList.Add(this.m_I[index]);
                return intList;
            }
        }

        /// <summary>Gets a list representing the sorted distances in the current solution.</summary>
        public List<double> DistanceList
        {
            get
            {
                List<double> doubleList = new List<double>(this.m_max_count);
                int num = this.m_I.Count - 1;
                for (int index = 0; index <= num && (this.m_I[index] >= 0 && this.m_D[index] >= 0.0); ++index)
                    doubleList.Add(Math.Sqrt(this.m_D[index]));
                return doubleList;
            }
        }

        /// <summary>Gets the squared minimum and maximum distance</summary>
        /// <param name="d0">Squared minimum distance in current solution.</param>
        /// <param name="d1">Squared maximum distance in current solution.</param>
        public void DistanceRange(ref double d0, ref double d1)
        {
            if (this.m_cur_count == 0)
            {
                d0 = double.MaxValue;
                d1 = double.MaxValue;
            }
            else
            {
                d0 = this.m_D[0];
                d1 = this.m_D[this.m_D.Count - 1];
            }
        }

        /// <summary>Rgister a new node with this collection.</summary>
        /// <param name="node">Node to register</param>
        /// <param name="index">Index of node</param>
        /// <returns>True if node was accepted, false if the node is too far or too close.</returns>
        public bool RegisterNode(Node2 node, int index)
        {
            if (index == m_base_index)
            {
                return false;
            }
            double num = m_base.DistanceSquared(node);
            if (num < m_min_2)
            {
                return false;
            }
            if (num > m_max_2)
            {
                return false;
            }
            int num2 = m_D.BinarySearch(num);
            if (num2 < 0)
            {
                num2 ^= -1;
            }
            if (num2 == m_max_count)
            {
                return false;
            }
            if (m_I[m_max_count - 1] < 0)
            {
                m_cur_count++;
            }
            if (num2 == m_max_count - 1)
            {
                m_I[num2] = index;
                m_D[num2] = num;
                return true;
            }
            if (m_I[num2] < 0)
            {
                m_I[num2] = index;
                m_D[num2] = num;
            }
            else
            {
                int num3 = m_max_count - 1;
                int num4 = num2 + 1;
                for (int i = num3; i >= num4; i += -1)
                {
                    m_I[i] = m_I[i - 1];
                    m_D[i] = m_D[i - 1];
                }
                m_I[num2] = index;
                m_D[num2] = num;
            }
            return true;
        }
    }
}
