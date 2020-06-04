using System;

namespace Diagrams
{
    /// <summary>Represents a single, two-dimensional coordinate with index specifier.</summary>
    /// <exclude />
    [System.Diagnostics.DebuggerDisplay("{DebuggerDisplay()}")]
    public class Node2 : IComparable<Node2>
    {
        private static double m_coincidence_tolerance = 1E-12;
        public double x;
        public double y;
        public int tag;

        public Node2()
        {
        }

        public Node2(double nx, double ny)
        {
            this.x = nx;
            this.y = ny;
            this.tag = -1;
        }

        public Node2(double nx, double ny, int n_tag)
        {
            this.x = nx;
            this.y = ny;
            this.tag = n_tag;
        }

        public Node2(Node2 other)
        {
            this.Set(other);
        }

        public Node2(Node2 other, double dx, double dy)
        {
            this.Set(other);
            this.Offset(dx, dy);
        }

        public Node2(Node2 A, Node2 B, double f, int n_tag)
        {
            this.tag = n_tag;
            this.x = A.x + f * (B.x - A.x);
            this.y = A.y + f * (B.y - A.y);
        }

        public Node2 Duplicate()
        {
            return new Node2(this);
        }

        public void Set(Node2 other)
        {
            this.x = other.x;
            this.y = other.y;
            this.tag = other.tag;
        }

        public void Set(double nX, double nY)
        {
            this.x = nX;
            this.y = nY;
        }

        public static Vec2 operator -(Node2 A, Node2 B)
        {
            return new Vec2(A.x - B.x, A.y - B.y);
        }

        public static Node2 operator +(Node2 P, Vec2 V)
        {
            return new Node2(P.x + V.x, P.y + V.y, P.tag);
        }

        public static Node2 operator +(Node2 A, Node2 B)
        {
            return new Node2(A.x + B.x, A.y + B.y, A.tag);
        }

        public static Node2 operator -(Node2 P, Vec2 V)
        {
            return new Node2(P.x - V.x, P.y - V.y, P.tag);
        }

        public static Node2 operator *(Node2 N, double f)
        {
            return new Node2(N.x * f, N.y * f, N.tag);
        }

        public static Node2 operator *(double f, Node2 N)
        {
            return new Node2(N.x * f, N.y * f, N.tag);
        }

        public void Offset(double dx, double dy)
        {
            x += dx;
            y += dy;
        }


        public double DistanceSquared(Node2 other)
        {
            return this.DistanceSquared(other.x, other.y);
        }

        public double DistanceSquared(double nx, double ny)
        {
            return (this.x - nx) * (this.x - nx) + (this.y - ny) * (this.y - ny);
        }

        public double Distance(Node2 other)
        {
            return Math.Sqrt(this.DistanceSquared(other));
        }

        public double Distance(double nx, double ny)
        {
            return Math.Sqrt(this.DistanceSquared(nx, ny));
        }

        public bool IsCoincident(Node2 other)
        {
            return this.IsCoincident(other.x, other.y);
        }

        public bool IsCoincident(double ox, double oy)
        {
            return Math.Abs(this.x - ox) <= Node2.m_coincidence_tolerance && Math.Abs(this.y - oy) <= Node2.m_coincidence_tolerance;
        }

        public bool IsValid
        {
            get
            {
                return !double.IsNaN(this.x) && !double.IsNaN(this.y);
            }
        }

        public int CompareTo(Node2 other)
        {
            return other != null ? (this.x != other.x ? this.x.CompareTo(other.x) : (this.y != other.y ? this.y.CompareTo(other.y) : 0)) : 1;
        }

        public int CompareTo(Node2 other, double tolerance)
        {
            return other != null ? (Math.Abs(this.x - other.x) >= tolerance ? this.x.CompareTo(other.x) : (Math.Abs(this.y - other.y) >= tolerance ? this.y.CompareTo(other.y) : 0)) : 1;
        }

        public override string ToString()
        {
            return string.Format("{0:0.00},{1:0.00} ({2})", (object)this.x, (object)this.y, (object)this.tag);
        }

        public string DebuggerDisplay
        {
            get
            {
                return this.ToString();
            }
        }
    }
}
