using System.Collections.Generic;
using Diagrams.ConvexHull;

namespace Diagrams.Delaunay
{

	/// <summary>
	/// Represents a connectivity diagram for a triangulated mesh with 
	/// fast node-node neighbour lookup.
	/// </summary>
	/// <exclude />
	public class Connectivity
	{
		private List<List<int>> m_map;

		public int Count => m_map.Count;

		public void SolveConnectivity(Node2List nodes, List<Face> faces, bool include_convex_hull_edges)
		{
			m_map = new List<List<int>>(nodes.Count);
			int num = nodes.Count - 1;
			for (int i = 0; i <= num; i++)
			{
				m_map.Add(new List<int>(6));
			}
			int num2 = faces.Count - 1;
			for (int j = 0; j <= num2; j++)
			{
				Face face = faces[j];
				m_map[face.A].Add(face.B);
				m_map[face.A].Add(face.C);
				m_map[face.B].Add(face.A);
				m_map[face.B].Add(face.C);
				m_map[face.C].Add(face.A);
				m_map[face.C].Add(face.B);
			}
			if (include_convex_hull_edges)
			{
				List<int> list = new List<int>();
				if (Diagrams.ConvexHull.Solver.Compute(nodes, list))
				{
					int num3 = list.Count - 1;
					for (int k = 0; k <= num3; k++)
					{
						int num4 = k + 1;
						if (num4 == list.Count)
						{
							num4 = 0;
						}
						m_map[list[k]].Add(list[num4]);
						m_map[list[num4]].Add(list[k]);
					}
				}
			}
			RemoveDuplicates();
		}

		private void RemoveDuplicates()
		{
			if (m_map != null)
			{
				int num = m_map.Count - 1;
				for (int i = 0; i <= num; i++)
				{
					RemoveDuplicateIntegers(m_map[i]);
				}
			}
		}

		private static void RemoveDuplicateIntegers(List<int> N)
		{
			if (N == null || N.Count < 2)
			{
				return;
			}
			N.Sort();
			int num = 0;
			if (N[0] == 0)
			{
				num = -1;
			}
			int num2 = 0;
			int num3 = N.Count - 1;
			for (int i = 0; i <= num3; i++)
			{
				if (N[i] != num)
				{
					N[num2] = N[i];
					num = N[i];
					num2++;
				}
			}
			if (num2 < N.Count)
			{
				N.RemoveRange(num2, N.Count - num2);
			}
		}

		public List<int> GetConnections(int node_index)
		{
			if (node_index >= m_map.Count)
			{
				return new List<int>();
			}
			return m_map[node_index];
		}
	}

}
