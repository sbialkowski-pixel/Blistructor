using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Diagrams.Delaunay
{
	/// <summary>
	/// Provides fast access to a collection of edges
	/// </summary>
	/// <exclude />
	public class EdgeList
	{
		protected List<Edge> m_E;

		[IndexerName("Edge")]
		public Edge this[int index] => m_E[index];

		public int Capacity
		{
			get
			{
				return m_E.Capacity;
			}
			set
			{
				m_E.Capacity = value;
			}
		}

		public int Count => m_E.Count;

		public EdgeList()
		{
			m_E = new List<Edge>();
		}

		/// <summary>
		/// Creates a collection of edges from a list of Faces (no nulls allowed)
		/// </summary>
		/// <param name="F">Faces</param>
		public EdgeList(List<FaceEx> F)
		{
			m_E = new List<Edge>();
			int num = F.Count - 1;
			for (int i = 0; i <= num; i++)
			{
				AddEdge(new Edge(F[i].A, F[i].B));
				AddEdge(new Edge(F[i].B, F[i].C));
				AddEdge(new Edge(F[i].C, F[i].A));
			}
		}

		/// <summary>
		/// Creates a collection of edges from a list of Faces (no nulls allowed)
		/// </summary>
		/// <param name="F">Faces</param>
		public EdgeList(List<Face> F)
		{
			m_E = new List<Edge>();
			int num = F.Count - 1;
			for (int i = 0; i <= num; i++)
			{
				AddEdge(new Edge(F[i].A, F[i].B));
				AddEdge(new Edge(F[i].B, F[i].C));
				AddEdge(new Edge(F[i].C, F[i].A));
			}
		}

		public void Clear()
		{
			m_E.Clear();
		}

		public void AddEdge(int A, int B)
		{
			AddEdge(new Edge(A, B));
		}

		public void AddEdge(Edge E)
		{
			int num = m_E.BinarySearch(E);
			if (num < 0)
			{
				E.N = 1;
				m_E.Insert(num ^ -1, E);
			}
			else
			{
				E.N = m_E[num].N + 1;
				m_E[num] = E;
			}
		}

		public bool RemoveEdge(int A, int B)
		{
			return RemoveEdge(new Edge(A, B));
		}

		public bool RemoveEdge(Edge E)
		{
			int num = m_E.BinarySearch(E);
			if (num < 0)
			{
				return false;
			}
			E.N = m_E[num].N - 1;
			if (E.N <= 0)
			{
				m_E.RemoveAt(num);
			}
			else
			{
				m_E[num] = E;
			}
			return true;
		}

		public int ContainsEdge(int A, int B)
		{
			return ContainsEdge(new Edge(A, B));
		}

		public int ContainsEdge(Edge E)
		{
			int num = m_E.BinarySearch(E);
			if (num < 0)
			{
				return -1;
			}
			return num;
		}

		public int TrimHighValenceEdges()
		{
			int num = 0;
			int count = m_E.Count;
			if (count == 0)
			{
				return 0;
			}
			int num2 = count - 1;
			for (int i = 0; i <= num2; i++)
			{
				if (m_E[i].N <= 1)
				{
					m_E[num] = m_E[i];
					num++;
				}
			}
			if (num < count)
			{
				m_E.RemoveRange(num, count - num);
			}
			return count - m_E.Count;
		}
	}

}
