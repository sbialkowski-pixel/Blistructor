using System;

namespace Diagrams
{ 
    /// <summary>Represents a single, two-dimensional line segment.</summary>
    /// <exclude />
    public class Line2
    {
        private static double tolerance = 1E-14;
        public double Ax;
        public double Ay;
        public double Bx;
        public double By;

        public Line2()
        {
        }

        public Line2(double nAx, double nAy, double nBx, double nBy)
        {
            this.Ax = nAx;
            this.Ay = nAy;
            this.Bx = nBx;
            this.By = nBy;
        }

        public Line2(Node2 nA, Node2 nB)
        {
            this.Ax = nA.x;
            this.Ay = nA.y;
            this.Bx = nB.x;
            this.By = nB.y;
        }

        public Line2(Line2 other)
        {
            this.Set(other);
        }

        public Line2 Duplicate()
        {
            return new Line2(this);
        }

        public void Set(Line2 other)
        {
            this.Ax = other.Ax;
            this.Ay = other.Ay;
            this.Bx = other.Bx;
            this.By = other.By;
        }

        public void Set(Node2 A, Node2 B)
        {
            this.Ax = A.x;
            this.Ay = A.y;
            this.Bx = B.x;
            this.By = B.y;
        }

        public double Length()
        {
            return Math.Sqrt(this.LengthSquared());
        }

        public double LengthSquared()
        {
            return (this.Ax - this.Bx) * (this.Ax - this.Bx) + (this.Ay - this.By) * (this.Ay - this.By);
        }

        public Node2 PointAt(double t)
        {
            return new Node2(this.Ax + t * (this.Bx - this.Ax), this.Ay + t * (this.By - this.Ay));
        }

        public double ClosestPoint(Node2 pt)
        {
            return this.ClosestPoint(pt.x, pt.y);
        }

        public double ClosestPoint(double x, double y)
        {
            double num = this.LengthSquared();
            return num >= 1E-32 ? ((this.Ay - y) * (this.Ay - this.By) - (this.Ax - x) * (this.Bx - this.Ax)) / num : 0.5;
        }

        public double DistanceTo(Node2 pt)
        {
            return this.DistanceTo(pt.x, pt.y);
        }

        public double DistanceTo(double x, double y)
        {
            return Math.Sqrt(this.DistanceToSquared(x, y));
        }

        public double DistanceToSquared(Node2 pt)
        {
            return this.DistanceToSquared(pt.x, pt.y);
        }

        public double DistanceToSquared(double x, double y)
        {
            return this.PointAt(this.ClosestPoint(x, y)).DistanceSquared(x, y);
        }

        public static Side2 Side(Line2 edge, Node2 pt)
        {
            return Line2.Side(edge.Ax, edge.Ay, edge.Bx, edge.By, pt.x, pt.y);
        }

        public static Side2 Side(
          double Ax,
          double Ay,
          double Bx,
          double By,
          double Px,
          double Py)
        {
            double num = (Bx - Ax) * (Py - Ay) - (By - Ay) * (Px - Ax);
            return Math.Abs(num) >= Line2.tolerance ? (num >= 0.0 ? Side2.Left : Side2.Right) : Side2.Coincident;
        }

        public static LineX Intersect(Line2 A, Line2 B, ref double t)
        {
            return Line2.Intersect(A.Ax, A.Ay, A.Bx, A.By, B.Ax, B.Ay, B.Bx, B.By, ref t);
        }

        public static LineX Intersect(
          double Ax,
          double Ay,
          double Bx,
          double By,
          double Cx,
          double Cy,
          double Dx,
          double Dy,
          ref double t)
        {
            double num1 = (Dy - Cy) * (Bx - Ax) - (Dx - Cx) * (By - Ay);
            double num2 = (Dx - Cx) * (Ay - Cy) - (Dy - Cy) * (Ax - Cx);
            LineX lineX;
            if (Math.Abs(num1) < Line2.tolerance)
            {
                double num3 = (Bx - Ax) * (Ay - Cy) - (By - Ay) * (Ax - Cx);
                lineX = Math.Abs(num2) >= Line2.tolerance || Math.Abs(num3) >= Line2.tolerance ? LineX.Parallel : LineX.Coincident;
            }
            else
            {
                t = num2 / num1;
                lineX = LineX.Point;
            }
            return lineX;
        }

        public static LineX Intersect(Line2 A, Line2 B, ref double t0, ref double t1)
        {
            return Line2.Intersect(A.Ax, A.Ay, A.Bx, A.By, B.Ax, B.Ay, B.Bx, B.By, ref t0, ref t1);
        }

        public static LineX Intersect(
          double Ax,
          double Ay,
          double Bx,
          double By,
          double Cx,
          double Cy,
          double Dx,
          double Dy,
          ref double t0,
          ref double t1)
        {
            double num1 = (Dy - Cy) * (Bx - Ax) - (Dx - Cx) * (By - Ay);
            double num2 = (Dx - Cx) * (Ay - Cy) - (Dy - Cy) * (Ax - Cx);
            double num3 = (Bx - Ax) * (Ay - Cy) - (By - Ay) * (Ax - Cx);
            LineX lineX;
            if (Math.Abs(num1) < Line2.tolerance)
            {
                lineX = Math.Abs(num2) >= Line2.tolerance || Math.Abs(num3) >= Line2.tolerance ? LineX.Parallel : LineX.Coincident;
            }
            else
            {
                t0 = num2 / num1;
                t1 = num3 / num1;
                lineX = LineX.Point;
            }
            return lineX;
        }

        public static Line2 MidLine(Node2 A, Node2 B)
        {
            Line2 line2;
            if (Math.Abs(A.DistanceSquared(B)) < Line2.tolerance)
            {
                line2 = (Line2)null;
            }
            else
            {
                double nAx = 0.5 * (A.x + B.x);
                double nAy = 0.5 * (A.y + B.y);
                double num1 = B.x - A.x;
                double num2 = B.y - A.y;
                line2 = new Line2(nAx, nAy, nAx + num2, nAy - num1);
            }
            return line2;
        }

        public static Line2 MidLine(Node2 A, Node2 B, double Wa, double Wb)
        {
            Line2 line2;
            if (Math.Abs(A.DistanceSquared(B)) < Line2.tolerance)
            {
                line2 = (Line2)null;
            }
            else
            {
                double num1 = Wa / (Wa + Wb);
                double nAx = A.x + num1 * (B.x - A.x);
                double nAy = A.y + num1 * (B.y - A.y);
                double num2 = B.x - A.x;
                double num3 = B.y - A.y;
                line2 = new Line2(nAx, nAy, nAx + num3, nAy - num2);
            }
            return line2;
        }
    }
}
