using System;
using System.Diagnostics;

namespace Diagrams.Delaunay
{
	/// <summary>
	/// Represents a topological edge connecting two node indices.
	/// </summary>
	/// <exclude />
	[DebuggerDisplay("{DebuggerDisplay()}")]
	public struct Edge : IComparable<Edge>
	{
		public int A;

		public int B;

		public int N;

		public string DebuggerDisplay => $"{A}, {B} ({N})";

		public Edge(int nA, int nB, int nN = 1)
		{
			this = default(Edge);
			if (nA < nB)
			{
				A = nA;
				B = nB;
			}
			else
			{
				A = nB;
				B = nA;
			}
			N = nN;
		}

		public int CompareTo(Edge other)
		{
			if (A == other.A)
			{
				return B.CompareTo(other.B);
			}
			return A.CompareTo(other.A);
		}

		int IComparable<Edge>.CompareTo(Edge other)
		{
			//ILSpy generated this explicit interface implementation from .override directive in CompareTo
			return this.CompareTo(other);
		}
	}

}
