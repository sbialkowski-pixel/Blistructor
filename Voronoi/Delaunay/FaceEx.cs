using System;
using System.Diagnostics;

namespace Diagrams.Delaunay
{
	/// <summary>
	/// Represents a triangle bounded by 3 corners. FaceEx adds circumcircle caching and tests to Face
	/// </summary>
	/// <exclude />
	[DebuggerDisplay("{DebuggerDisplay()}")]
	public class FaceEx : Face
	{
		/// <summary>
		/// Center of the circumcircle
		/// </summary>
		public Node2 center;

		/// <summary>
		/// Radius of the circumcircle
		/// </summary>
		public double radius;

		/// <summary>
		/// Squared radius of the circumcircle
		/// </summary>
		public double radius_squared;

		public double Front => center.x + radius;

		public FaceEx()
		{
			radius = double.NaN;
			radius_squared = double.NaN;
			A = -1;
			B = -1;
			C = -1;
		}

		public FaceEx(int nA, int nB, int nC)
		{
			radius = double.NaN;
			radius_squared = double.NaN;
			A = nA;
			B = nB;
			C = nC;
		}

		public FaceEx(Face other)
		{
			radius = double.NaN;
			radius_squared = double.NaN;
			A = other.A;
			B = other.B;
			C = other.C;
		}

		public void ComputeBC(Node2List Nodes)
		{
			ComputeBC(Nodes[A], Nodes[B], Nodes[C]);
		}

		public void ComputeBC(Node2 D, Node2 E, Node2 F)
		{
			double ox = default(double);
			double oy = default(double);
			Circle2.Circle3Pt(D.x, D.y, E.x, E.y, F.x, F.y, ref ox, ref oy, ref radius_squared);
			center = new Node2(ox, oy);
			radius = Math.Sqrt(radius_squared);
		}

		public bool ContainsInBoundingCircle(Node2 N)
		{
			return ContainsInBoundingCircle(N.x, N.y);
		}

		public bool ContainsInBoundingCircle(double x, double y)
		{
			return center.DistanceSquared(x, y) <= radius_squared;
		}
	}

}
