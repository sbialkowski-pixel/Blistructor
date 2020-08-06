using System.Collections.Generic;
using Rhino.Geometry;
//using Pixel.Geometry;

namespace Diagrams.Delaunay
{

	/// <summary>
	/// Represents a list of sorted faces.
	/// </summary>
	/// <exclude />
	public class FaceExList
	{
		protected class CompareFaceFront : IComparer<FaceEx>
		{
			public int Compare(FaceEx x, FaceEx y)
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
				return x.Front.CompareTo(y.Front);
			}

			int IComparer<FaceEx>.Compare(FaceEx x, FaceEx y)
			{
				//ILSpy generated this explicit interface implementation from .override directive in Compare
				return this.Compare(x, y);
			}
		}

		protected CompareFaceFront m_compare_front;

		protected List<FaceEx> m_F;

		public int Capacity
		{
			get
			{
				return m_F.Capacity;
			}
			set
			{
				m_F.Capacity = value;
			}
		}

		public FaceExList()
		{
			m_compare_front = new CompareFaceFront();
			m_F = new List<FaceEx>();
		}

		public FaceExList(int initial_capacity)
		{
			m_compare_front = new CompareFaceFront();
			m_F = new List<FaceEx>();
			m_F.Capacity = initial_capacity;
		}

		public void Clear()
		{
			m_F.Clear();
		}

		public void AddFace(int A, int B, int C, Node2List Nodes)
		{
			FaceEx faceEx = new FaceEx(A, B, C);
			faceEx.ComputeBC(Nodes);
			AddFace(faceEx);
		}

		public void AddFace(FaceEx F)
		{
			int num = m_F.BinarySearch(F, m_compare_front);
			if (num < 0)
			{
				m_F.Insert(num ^ -1, F);
			}
			else
			{
				m_F.Insert(num, F);
			}
		}

		public int CullFaces(double x, double y, List<FaceEx> F)
		{
			F.Capacity = F.Count + 100;
			int num = 0;
			for (int i = m_F.Count - 1; i >= 0; i += -1)
			{
				if (m_F[i] != null)
				{
					if (m_F[i].Front < x)
					{
						break;
					}
					if (m_F[i].ContainsInBoundingCircle(x, y))
					{
						F.Add(m_F[i]);
						m_F[i] = null;
						num++;
					}
				}
			}
			if (num > 0)
			{
				TrimNulls();
			}
			return num;
		}

		public void MigrateRemainingFaces(List<Face> static_list)
		{
			int num = m_F.Count - 1;
			for (int i = 0; i <= num; i++)
			{
				if (m_F[i] != null)
				{
					static_list.Add(m_F[i]);
				}
			}
			Clear();
		}

		public int MigrateStaticFaces(List<Face> static_list, double wave_front)
		{
			int num = 0;
			int num2 = m_F.Count - 1;
			for (int i = 0; i <= num2; i++)
			{
				if (m_F[i] != null)
				{
					if (m_F[i].Front >= wave_front)
					{
						break;
					}
					static_list.Add(m_F[i]);
					m_F[i] = null;
					num++;
				}
			}
			return num;
		}

		public int TrimNulls()
		{
			int num = -1;
			int count = m_F.Count;
			if (count == 0)
			{
				return 0;
			}
			int num2 = m_F.Count - 1;
			for (int i = 0; i <= num2; i++)
			{
				if (m_F[i] != null)
				{
					num++;
					m_F[num] = m_F[i];
				}
			}
			num++;
			if (num < count)
			{
				m_F.RemoveRange(num, count - num);
			}
			return count - m_F.Count;
		}

		public void InsertFaces(Node2List nodes)
		{
			int num = m_F.Count - 1;
			for (int i = 0; i <= num; i++)
			{
				FaceEx faceEx = m_F[i];
				if (faceEx != null)
				{
					Polyline polyline = new Polyline();
					polyline.Add(nodes[faceEx.A].x, nodes[faceEx.A].y, 0.0);
					polyline.Add(nodes[faceEx.B].x, nodes[faceEx.B].y, 0.0);
					polyline.Add(nodes[faceEx.C].x, nodes[faceEx.C].y, 0.0);
					polyline.Add(nodes[faceEx.A].x, nodes[faceEx.A].y, 0.0);
				}
			}
		}
	}
}