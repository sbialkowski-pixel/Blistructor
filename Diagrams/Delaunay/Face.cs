using System.Diagnostics;

namespace Diagrams.Delaunay
{

	/// <summary>
	/// Represents a triangle bounded by 3 corners
	/// </summary>
	/// <exclude />
	[DebuggerDisplay("{DebuggerDisplay()}")]
	public class Face
	{
		public int A;

		public int B;

		public int C;

		public bool IsValid
		{
			get
			{
				if (A < 0)
				{
					return false;
				}
				if (B < 0)
				{
					return false;
				}
				if (C < 0)
				{
					return false;
				}
				if (A == B)
				{
					return false;
				}
				if (A == C)
				{
					return false;
				}
				if (B == C)
				{
					return false;
				}
				return true;
			}
		}

		public string DebuggerDisplay => $"{A}, {B}, {C}";

		public Face()
		{
			A = -1;
			B = -1;
			C = -1;
		}

		public Face(int nA, int nB, int nC)
		{
			A = nA;
			B = nB;
			C = nC;
		}

		public Face(Face other)
		{
			A = other.A;
			B = other.B;
			C = other.C;
		}

		public Face Duplicate()
		{
			return new Face(this);
		}

		public void Set(int nA, int nB, int nC)
		{
			A = nA;
			B = nB;
			C = nC;
		}

		public void Set(Face other)
		{
			A = other.A;
			B = other.B;
			C = other.C;
		}

		public bool ContainsVertex(int index)
		{
			if (index == A)
			{
				return true;
			}
			if (index == B)
			{
				return true;
			}
			if (index == C)
			{
				return true;
			}
			return false;
		}

		public bool ContainsEdge(int E0, int E1)
		{
			if (E0 == E1)
			{
				return false;
			}
			if (A != E0 && B != E0 && C != E0)
			{
				return false;
			}
			if (A != E1 && B != E1 && C != E1)
			{
				return false;
			}
			return true;
		}
	}
}
