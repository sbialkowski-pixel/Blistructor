//using Pixel.Geometry;
using Rhino.Geometry;
using System;
using System.Diagnostics;

namespace Diagrams
{
	/// <summary>
	/// Represents a single, two-dimensional vector.
	/// </summary>
	/// <exclude />
	[DebuggerDisplay("{DebuggerDisplay()}")]
	public class Vec2 : IComparable<Vec2>
	{
		public double x;

		public double y;

		private static double m_angle_tolerance = 0.0017453292519943296;

		private static double m_unit_tolerance = 1E-32;

		public static Vec2 Unit_X => new Vec2(1.0, 0.0);

		public static Vec2 Unit_Y => new Vec2(0.0, 1.0);

		public bool IsValid
		{
			get
			{
				if (double.IsNaN(x))
				{
					return false;
				}
				if (double.IsNaN(y))
				{
					return false;
				}
				return true;
			}
		}

		public string DebuggerDisplay => ToString();

		public Vec2()
		{
		}

		public Vec2(double nX, double nY)
		{
			x = nX;
			y = nY;
		}

		public Vec2(Vec2 other)
		{
			x = other.x;
			y = other.y;
		}

		public Vec2(Vector2d other)
		{
			x = other.X;
			y = other.Y;
		}

		public Vec2(Vector2f other)
		{
			x = other.X;
			y = other.Y;
		}

		public Vec2 Duplicate()
		{
			return new Vec2(this);
		}

		public void Set(Vec2 other)
		{
			x = other.x;
			y = other.y;
		}

		public void Set(double nX, double nY)
		{
			x = nX;
			y = nY;
		}

		public static Vec2 operator +(Vec2 A, Vec2 B)
		{
			return new Vec2(A.x + B.x, A.y + B.y);
		}

		public static Vec2 operator -(Vec2 A, Vec2 B)
		{
			return new Vec2(A.x - B.x, A.y - B.y);
		}

		public static Vec2 operator *(Vec2 V, double F)
		{
			return new Vec2(V.x * F, V.y * F);
		}

		public double Length()
		{
			return Math.Sqrt(LengthSquared());
		}

		public double LengthSquared()
		{
			return x * x + y * y;
		}

		public override string ToString()
		{
			return $"{x:0.00}, {y:0.00}";
		}

		public int CompareTo(Vec2 other)
		{
			if (other == null)
			{
				return 1;
			}
			if (x == other.x)
			{
				if (y == other.y)
				{
					return 0;
				}
				return y.CompareTo(other.y);
			}
			return x.CompareTo(other.x);
		}

		int IComparable<Vec2>.CompareTo(Vec2 other)
		{
			//ILSpy generated this explicit interface implementation from .override directive in CompareTo
			return this.CompareTo(other);
		}

		public Vec2 CreatePerpendicular()
		{
			if (LengthSquared() < m_unit_tolerance)
			{
				return new Vec2(0.0, 0.0);
			}
			return new Vec2(y, x);
		}

		public bool PerpendicularTo(Vec2 v)
		{
			return PerpendicularTo(v, m_angle_tolerance);
		}

		public bool PerpendicularTo(Vec2 v, double angle_tol)
		{
			double num = Length() * v.Length();
			if (num <= 0.0)
			{
				return false;
			}
			return Math.Abs((x * v.x + y * v.y) / num) <= Math.Sin(angle_tol);
		}

		public Parallax ParallelTo(Vec2 v)
		{
			return ParallelTo(v, Math.PI / m_angle_tolerance);
		}

		public Parallax ParallelTo(Vec2 v, double angle_tol)
		{
			Parallax result = Parallax.Divergent;
			double num = Length() * v.Length();
			if (num <= 0.0)
			{
				return result;
			}
			double num2 = (x * v.x + y * v.y) / num;
			double num3 = Math.Cos(angle_tol);
			if (num2 >= num3)
			{
				result = Parallax.Parallel;
			}
			else if (num2 <= 0.0 - num3)
			{
				result = Parallax.AntiParallel;
			}
			return result;
		}

		public void Unitize()
		{
			double num = Length();
			if (num < m_unit_tolerance)
			{
				x = 1.0;
				y = 0.0;
			}
			else
			{
				double num2 = 1.0 / num;
				x *= num2;
				y *= num2;
			}
		}
	}

}
