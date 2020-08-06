//using Pixel.Geometry;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
//using System.Drawing.Drawing2D;

namespace Diagrams.Voronoi
{
    /// <summary>Represents a two-dimensional voronoi diagram cell.</summary>
    /// <exclude />
    public class Cell2
    {
        public Node2 M;
        public List<Node2> C;

        public Cell2()
        {
            this.C = new List<Node2>();
        }

        public Cell2(Node2 pt, double Radius)
        {
            this.C = new List<Node2>();
            this.M = pt;
            this.C.Add(new Node2(pt, -Radius, -Radius));
            this.C.Add(new Node2(pt, Radius, -Radius));
            this.C.Add(new Node2(pt, Radius, Radius));
            this.C.Add(new Node2(pt, -Radius, Radius));
        }

        public Cell2(Node2 pt, IEnumerable<Node2> Contour)
        {
            this.C = new List<Node2>();
            this.M = pt;
            this.C.AddRange(Contour);
        }

        public bool Slice(Node2 other)
        {
            Line2 line = Line2.MidLine(this.M, other);
            return line != null && this.Slice(line);
        }

        public bool Slice(Line2 line)
        {
            Side2 side = Line2.Side(line, this.M);
            bool flag;
            if (side == Side2.Coincident)
            {
                flag = false;
            }
            else
            {
                bool changed = true;
                this.C = Cell2.SliceConvexNGon(this.C, line, side, ref changed);
                flag = changed;
            }
            return flag;
        }

        /// <summary>
        /// Generic n-gon slicer. If the nodes in V do not represent a valid, convex NGon
        /// then the result will not be reliable.
        /// </summary>
        /// <param name="V">Corners of NGon</param>
        /// <param name="line">The line to slice with</param>
        /// <param name="side">Side of NGon to keep (with respect to line)</param>
        /// <returns>Result. List may share Node instances with V</returns>
        public static List<Node2> SliceConvexNGon(
          List<Node2> V,
          Line2 line,
          Side2 side,
          ref bool changed)
        {
            changed = false;
            List<Node2> node2List1;
            if (V.Count < 2)
            {
                node2List1 = V;
            }
            else
            {
                List<Node2> node2List2 = new List<Node2>(V.Count + 2);
                int num1 = 0;
                Side2 side2 = Side2.Coincident;
                int num2 = V.Count - 1;
                for (int index1 = 0; index1 <= num2; ++index1)
                {
                    if (side2 == Side2.Coincident)
                    {
                        side2 = Line2.Side(line, V[index1]);
                        if (side2 != side)
                            changed = true;
                    }
                    int index2 = index1 + 1;
                    if (index2 == V.Count)
                        index2 = 0;
                    Line2 A = new Line2(V[index1], V[index2]);
                    double t = 0.0;
                    switch (Line2.Intersect(A, line, ref t))
                    {
                        case LineX.None:
                            if (side2 == side)
                            {
                                node2List2.Add(V[index1]);
                                break;
                            }
                            break;
                        case LineX.Parallel:
                            if (side2 == side)
                            {
                                node2List2.Add(V[index1]);
                                break;
                            }
                            break;
                        case LineX.Coincident:
                            changed = false;
                            node2List1 = V;
                            goto label_28;
                        case LineX.Point:
                            Node2 node2 = A.PointAt(t);
                            if (node2.IsCoincident(V[index1]))
                            {
                                node2List2.Add(V[index1]);
                                side2 = Side2.Coincident;
                                ++num1;
                                changed = true;
                                break;
                            }
                            if (node2.IsCoincident(V[index2]))
                            {
                                if (side2 == side)
                                {
                                    node2List2.Add(V[index1]);
                                    break;
                                }
                                break;
                            }
                            if (t < 0.0 || t > 1.0)
                            {
                                if (side2 == side)
                                {
                                    node2List2.Add(V[index1]);
                                    break;
                                }
                                break;
                            }
                            if (side2 == side)
                                node2List2.Add(V[index1]);
                            node2List2.Add(node2);
                            side2 = Side2.Coincident;
                            ++num1;
                            changed = true;
                            break;
                    }
                }
                node2List1 = node2List2;
            }
        label_28:
            return node2List1;
        }

        public double Radius()
        {
            double num1;
            if (this.C.Count == 0)
            {
                num1 = 0.0;
            }
            else
            {
                double num2 = 0.0;
                int num3 = this.C.Count - 1;
                for (int index = 0; index <= num3; ++index)
                    num2 = Math.Max(num2, this.M.DistanceSquared(this.C[index]));
                num1 = 2.1 * Math.Sqrt(num2);
            }
            return num1;
        }

        /// <summary>Get a list of all the edges that decribe the boundary of this cell.</summary>
        public List<Line2> Edges()
        {
            List<Line2> line2List1 = new List<Line2>();
            List<Line2> line2List2;
            if (this.C.Count < 2)
            {
                line2List2 = line2List1;
            }
            else
            {
                int num = this.C.Count - 1;
                for (int index1 = 0; index1 <= num; ++index1)
                {
                    int index2 = index1 + 1;
                    if (index2 == this.C.Count)
                        index2 = 0;
                    line2List1.Add(new Line2(this.C[index1], this.C[index2]));
                }
                line2List2 = line2List1;
            }
            return line2List2;
        }

        public Polyline ToPolyline()
        {
            Polyline polyline1 = new Polyline();
            Polyline polyline2;
            if (this.C.Count < 2)
            {
                polyline2 = (Polyline)null;
            }
            else
            {
                int num = this.C.Count - 1;
                for (int index = 0; index <= num; ++index)
                    polyline1.Add(this.C[index].x, this.C[index].y, 0.0);
                polyline1.Add(polyline1[0]);
                polyline2 = polyline1;
            }
            return polyline2;
        }

        public PolyCurve ToPolyCurve(double radius)
        {
            Circle2 circle2 = new Circle2(this.M, radius);
            PolyCurve polyCurve1 = new PolyCurve();
            Circle circle = new Circle(new Point3d(this.M.x, this.M.y, 0.0), radius);
            double num1 = double.NaN;
            double num2 = double.NaN;
            List<Line2> line2List = this.Edges();
            PolyCurve polyCurve2;
            if (line2List.Count == 0)
            {
                polyCurve2 = (PolyCurve)null;
            }
            else
            {
                int num3 = line2List.Count - 1;
                for (int index = 0; index <= num3; ++index)
                {
                    double l0 = 0.0;
                    double l1 = 0.0;
                    double a0 = 0.0;
                    double a1 = 0.0;
                    switch (circle2.Intersect(line2List[index], ref l0, ref l1, ref a0, ref a1))
                    {
                        case LineCircleX.Secant:
                            Node2 node2_1 = line2List[index].PointAt(l0);
                            Node2 node2_2 = line2List[index].PointAt(l1);
                            if (l0 <= 1.0 && l1 >= 0.0)
                            {
                                if (l0 >= 0.0)
                                {
                                    if (l0 < 1.0)
                                    {
                                        if (double.IsNaN(num1))
                                            num1 = a0;
                                        if (!double.IsNaN(num2))
                                        {
                                            Node2 node2_3 = circle2.PointAt(num2);
                                            Node2 node2_4 = circle2.PointAt(a0);
                                            Vec2 vec2 = circle2.TangentAt(num2);
                                            Arc arc = new Arc(new Point3d(node2_3.x, node2_3.y, 0.0), new Vector3d(vec2.x, vec2.y, 0.0), new Point3d(node2_4.x, node2_4.y, 0.0));
                                            polyCurve1.Append(arc);
                                        }
                                        if (l1 > l0 && l1 <= 1.0)
                                        {
                                            polyCurve1.Append(new Line(new Point3d(node2_1.x, node2_1.y, 0.0), new Point3d(node2_2.x, node2_2.y, 0.0)));
                                            num2 = a1;
                                            break;
                                        }
                                        polyCurve1.Append(new Line(new Point3d(node2_1.x, node2_1.y, 0.0), new Point3d(line2List[index].Bx, line2List[index].By, 0.0)));
                                        num2 = double.NaN;
                                        break;
                                    }
                                    break;
                                }
                                if (l1 >= 0.0 && l1 <= 1.0)
                                {
                                    polyCurve1.Append(new Line(new Point3d(line2List[index].Ax, line2List[index].Ay, 0.0), new Point3d(node2_2.x, node2_2.y, 0.0)));
                                    num2 = a1;
                                    break;
                                }
                                polyCurve1.Append(new Line(new Point3d(line2List[index].Ax, line2List[index].Ay, 0.0), new Point3d(line2List[index].Bx, line2List[index].By, 0.0)));
                                num2 = double.NaN;
                                break;
                            }
                            break;
                    }
                }
                if (!double.IsNaN(num2) && !double.IsNaN(num1))
                {
                    Node2 node2_1 = circle2.PointAt(num2);
                    Node2 node2_2 = circle2.PointAt(num1);
                    Vec2 vec2 = circle2.TangentAt(num2);
                    Arc arc = new Arc(new Point3d(node2_1.x, node2_1.y, 0.0), new Vector3d(vec2.x, vec2.y, 0.0), new Point3d(node2_2.x, node2_2.y, 0.0));
                    polyCurve1.Append(arc);
                }
                if (polyCurve1.SegmentCount == 0)
                    polyCurve1.Append((Curve)new ArcCurve(new Circle(new Point3d(this.M.x, this.M.y, 0.0), radius)));
                polyCurve2 = polyCurve1;
            }
            return polyCurve2;
        }
        /*
        public GraphicsPath ToGraphicsPath()
        {
            GraphicsPath graphicsPath1;
            if (this.C.Count < 3)
            {
                graphicsPath1 = (GraphicsPath)null;
            }
            else
            {
                GraphicsPath graphicsPath2 = new GraphicsPath();
                int num = this.C.Count - 2;
                for (int index = 0; index <= num; ++index)
                {
                    float single1 = Convert.ToSingle(this.C[index].x);
                    float single2 = Convert.ToSingle(this.C[index].y);
                    float single3 = Convert.ToSingle(this.C[index + 1].x);
                    float single4 = Convert.ToSingle(this.C[index + 1].y);
                    graphicsPath2.AddLine(single1, single2, single3, single4);
                }
                graphicsPath2.CloseFigure();
                graphicsPath1 = graphicsPath2;
            }
            return graphicsPath1;
        }
        */
    }
}
