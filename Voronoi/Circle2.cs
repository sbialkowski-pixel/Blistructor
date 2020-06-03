using System;

namespace Diagrams
{
    /// <summary>Represents a circle, defined by origin and radius</summary>
    /// <exclude />
    public class Circle2
    {
        public Node2 O;
        public double R;

        /// <summary>Blank constructor</summary>
        public Circle2()
        {
        }

        /// <summary>Create a circle from origin and radius</summary>
        /// <param name="origin">Origin point of circle</param>
        /// <param name="radius">Radius of circle (&gt;0.0 for valid circle)</param>
        public Circle2(Node2 origin, double radius)
        {
            this.O = origin;
            this.R = radius;
        }

        /// <summary>Duplicate another circle.</summary>
        /// <param name="other">Circle to mimic</param>
        public Circle2(Circle2 other)
        {
            if (other.O != null)
                this.O = new Node2(other.O);
            this.R = other.R;
        }

        /// <summary>Create a circle through 3 points.</summary>
        /// <param name="A">First point</param>
        /// <param name="B">Second point</param>
        /// <param name="C">Third point</param>
        public Circle2(Node2 A, Node2 B, Node2 C)
        {
            double ox = 0.0;
            double oy = 0.0;
            double r2 = 0.0;
            Circle2.Circle3Pt(A.x, A.y, B.x, B.y, C.x, C.y, ref ox, ref oy, ref r2);
            this.O = new Node2(ox, oy);
            this.R = Math.Sqrt(r2);
        }

        /// <summary>Fit a circle through three 2d points.</summary>
        /// <param name="ax">X of first point</param>
        /// <param name="ay">Y of first point</param>
        /// <param name="bx">X of second point</param>
        /// <param name="by">Y of second point</param>
        /// <param name="cx">X of third point</param>
        /// <param name="cy">Y of third point</param>
        /// <param name="ox">X of origin</param>
        /// <param name="oy">Y of origin</param>
        /// <param name="r2">Radius squared</param>
        /// <returns>True if circle is valid, False if points are coincident or colinear.</returns>
        public static bool Circle3Pt(
          double ax,
          double ay,
          double bx,
          double by,
          double cx,
          double cy,
          ref double ox,
          ref double oy,
          ref double r2)
        {
            double num1 = (bx - ax) * (ax + bx) + (by - ay) * (ay + by);
            double num2 = (cx - ax) * (ax + cx) + (cy - ay) * (ay + cy);
            double num3 = 2.0 * ((bx - ax) * (cy - by) - (by - ay) * (cx - bx));
            bool flag;
            if (Math.Abs(num3) < 1E-32)
            {
                ox = (ax + bx + cx) * (1.0 / 3.0);
                oy = (ay + by + cy) * (1.0 / 3.0);
                r2 = Math.Max((ox - ax) * (ox - ax) + (oy - ay) * (oy - ay), (ox - bx) * (ox - bx) + (oy - by) * (oy - by));
                r2 = Math.Max((ox - cx) * (ox - cx) + (oy - cy) * (oy - cy), r2);
                flag = false;
            }
            else
            {
                double num4 = 1.0 / num3;
                double num5 = ((cy - ay) * num1 - (by - ay) * num2) * num4;
                double num6 = ((bx - ax) * num2 - (cx - ax) * num1) * num4;
                ox = num5;
                oy = num6;
                r2 = (ax - num5) * (ax - num5) + (ay - num6) * (ay - num6);
                flag = true;
            }
            return flag;
        }

        /// <summary>Create an exact duplicate of this circle.</summary>
        public Circle2 Duplicate()
        {
            return new Circle2(this);
        }

        public double Circumference
        {
            get
            {
                return 2.0 * Math.PI * this.R;
            }
        }

        public double Area
        {
            get
            {
                return Math.PI * this.R * this.R;
            }
        }

        /// <summary>Get the point ON the circle at parameter t</summary>
        /// <param name="t">Parameter to evaluate at: (0.0, t, 2pi)</param>
        public Node2 PointAt(double t)
        {
            return new Node2(this.O.x + this.R * Math.Cos(t), this.O.y + this.R * Math.Sin(t));
        }

        public Vec2 TangentAt(double t)
        {
            return new Vec2(-Math.Sin(t), Math.Cos(t));
        }

        public double ClosestPointTo(Node2 pt)
        {
            return this.ClosestPointTo(pt.x, pt.y);
        }

        public double ClosestPointTo(double x, double y)
        {
            double x1 = this.O.x - x;
            double y1 = this.O.y - y;
            return x1 != 0.0 || y1 != 0.0 ? Math.PI + Math.Atan2(y1, x1) : 0.0;
        }

        public Node2 ClosestPointTo(Node2 pt, ref double t)
        {
            return this.ClosestPointTo(pt.x, pt.y, ref t);
        }

        public Node2 ClosestPointTo(double x, double y, ref double t)
        {
            t = this.ClosestPointTo(x, y);
            return this.PointAt(t);
        }

        public Containment Contains(Node2 pt)
        {
            return this.Contains(pt.x, pt.y);
        }

        public Containment Contains(double x, double y)
        {
            double num1 = this.O.DistanceSquared(x, y);
            double num2 = this.R * this.R;
            return num1 != num2 ? (num1 >= num2 ? Containment.outside : Containment.inside) : Containment.coincident;
        }

        public LineCircleX Intersect(Line2 line, ref double l0, ref double l1)
        {
            if (this.R <= 0.0)
                throw new Exception("Invalid circle, zero or negative radius");
            l0 = double.NaN;
            l1 = double.NaN;
            int num1 = (int)this.Contains(line.Ax, line.Ay);
            int num2 = (int)this.Contains(line.Bx, line.By);
            double t = line.ClosestPoint(this.O);
            Node2 pt = line.PointAt(t);
            LineCircleX lineCircleX;
            switch (this.Contains(pt))
            {
                case Containment.coincident:
                    l0 = t;
                    l1 = l0;
                    lineCircleX = LineCircleX.Tangent;
                    break;
                case Containment.outside:
                    lineCircleX = LineCircleX.None;
                    break;
                default:
                    double num3 = Math.Sqrt(this.R * this.R - pt.DistanceSquared(this.O)) / line.Length();
                    l0 = t - num3;
                    l1 = t + num3;
                    lineCircleX = LineCircleX.Secant;
                    break;
            }
            return lineCircleX;
        }

        public LineCircleX Intersect(
          Line2 line,
          ref double l0,
          ref double l1,
          ref double a0,
          ref double a1)
        {
            a0 = double.NaN;
            a1 = double.NaN;
            LineCircleX lineCircleX;
            switch (this.Intersect(line, ref l0, ref l1))
            {
                case LineCircleX.None:
                    lineCircleX = LineCircleX.None;
                    break;
                case LineCircleX.Tangent:
                    a0 = this.ClosestPointTo(line.PointAt(l0));
                    lineCircleX = LineCircleX.Tangent;
                    break;
                case LineCircleX.Secant:
                    a0 = this.ClosestPointTo(line.PointAt(l0));
                    a1 = this.ClosestPointTo(line.PointAt(l1));
                    lineCircleX = LineCircleX.Secant;
                    break;
                default:
                    lineCircleX = LineCircleX.None;
                    break;
            }
            return lineCircleX;
        }
    }
}
