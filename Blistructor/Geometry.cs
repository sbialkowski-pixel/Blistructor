using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Rhino.Geometry;
using Rhino.Geometry.Intersect;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

using Grasshopper.Kernel.Geometry.Voronoi;
using Grasshopper.Kernel.Geometry.Delaunay;


namespace Blistructor
{
    static class Geometry
    {
        public static void FlipIsoRays(NurbsCurve guideCrv, LineCurve crv)
        {
            Curve temp = crv.Extend(CurveEnd.Both, 10000, CurveExtensionStyle.Line);
            LineCurve extended = new LineCurve(temp.PointAtStart, temp.PointAtEnd);
            Point3d guidePt, crvPt;
            if (guideCrv.ClosestPoints(extended, out guidePt, out crvPt))
            {
                double guide_t;
                if (guideCrv.ClosestPoint(guidePt, out guide_t, 1.0))
                {
                    Vector3d guide_v = guideCrv.TangentAt(guide_t);
                    Vector3d crv_v = crv.Line.UnitTangent;
                    if (guide_v * crv_v < 0.01)
                    {
                        crv.Reverse();
                    }
                }
            }
        }

        public static LineCurve GetIsoLine(Point3d source, Vector3d direction, double radius, List<Curve> obstacles)
        {

            LineCurve rayA = new LineCurve(new Line(source, direction, radius));
            LineCurve rayB = new LineCurve(new Line(source, -direction, radius));

            List<LineCurve> rays = new List<LineCurve> { rayA, rayB };
            List<Point3d> pts = new List<Point3d>(2);

            foreach (LineCurve ray in rays)
            {
                SortedList<double, Point3d> interData = new SortedList<double, Point3d>();
                for (int obId = 0; obId < obstacles.Count; obId++)
                {
                    CurveIntersections inter = Intersection.CurveCurve(obstacles[obId], ray, Setups.IntersectionTolerance, Setups.OverlapTolerance);
                    if (inter.Count > 0)
                    {
                        foreach (IntersectionEvent cross in inter)
                        {
                            interData.Add(cross.ParameterB, cross.PointB);
                        }
                    }
                }
                LineCurve rayent = new LineCurve(ray);
                if (interData.Count > 0)
                {
                    pts.Add(interData[interData.Keys[0]]);
                }
                else
                {
                    pts.Add(rayent.PointAtEnd);
                }
            }
            LineCurve isoLine = new LineCurve(pts[0], pts[1]);
            if (isoLine.GetLength() >= Setups.BladeLength) return isoLine;
            else return null;
        }

        public static void EditSeamBasedOnCurve(Curve editCrv, Curve baseCrv)
        {
            Point3d thisPt, otherPt;
            if (editCrv.ClosestPoints(baseCrv, out thisPt, out otherPt))
            {
                double this_t;
                editCrv.ClosestPoint(thisPt, out this_t);
                editCrv.ChangeClosedCurveSeam(this_t);
            }
        }

        public static List<Curve> RemoveDuplicateCurves(List<Curve> crvs)
        {
            List<Curve> uniqueCurves = new List<Curve>();
            for (int i = 0; i < crvs.Count; i++)
            {
                bool unique = true;
                for (int j = i + 1; j < crvs.Count; j++)
                {
                    if (GeometryBase.GeometryEquals(crvs[i], crvs[j]))
                    {
                        unique = false;
                    }
                }
                if (unique)
                {
                    uniqueCurves.Add(crvs[i]);
                }
            }
            return uniqueCurves;
        }

        public static int[] SortPtsAlongCurve(List<Point3d> pts, Curve crv)
        {//out Point3d[] points
            int L = pts.Count;//points = pts.ToArray();
            int[] iA = new int[L]; double[] tA = new double[L];
            for (int i = 0; i < L; i++)
            {
                double t;
                crv.ClosestPoint(pts[i], out t);
                iA[i] = i; tA[i] = t;
            }
            Array.Sort(tA, iA);// Array.Sort(tA, iA);//Array.Sort(tA, points);
            return iA;
        }

        public static Point3d[] SortPtsAlongCurve(Point3d[] pts, Curve crv)
        {
            int L = pts.Length;
            Point3d[] points = pts;
            int[] iA = new int[L]; double[] tA = new double[L];
            for (int i = 0; i < L; i++)
            {
                double t;
                crv.ClosestPoint(pts[i], out t);
                iA[i] = i; tA[i] = t;
            }
            Array.Sort(tA, points);
            return points;
        }

        public static Polyline ConvexHull(List<Point3d> pts)
        {
            List<GH_Point> po = new List<GH_Point>();
            foreach (Point3d pt in pts)
            {
                po.Add(new GH_Point(pt));
            }
            return Grasshopper.Kernel.Geometry.ConvexHull.Solver.ComputeHull(po);
        }

        public static List<CurveIntersections> CurveCurveIntersection(Curve baseCrv, List<Curve> otherCrv)
        {
            List<CurveIntersections> allIntersections = new List<CurveIntersections>(otherCrv.Count);
            for (int i = 0; i < otherCrv.Count; i++)
            {
                CurveIntersections inter = Intersection.CurveCurve(baseCrv, otherCrv[i], Setups.IntersectionTolerance, Setups.OverlapTolerance);
                if (inter.Count > 0)
                {
                    allIntersections.Add(inter);
                }
            }
            return allIntersections;
        }

        public static List<List<CurveIntersections>> CurveCurveIntersection(List<Curve> baseCrv, List<Curve> otherCrv)
        {
            List<List<CurveIntersections>> allIntersections = new List<List<CurveIntersections>>(baseCrv.Count);
            for (int i = 0; i < baseCrv.Count; i++)
            {
                List<CurveIntersections> currentInter = new List<CurveIntersections>(otherCrv.Count);
                for (int j = 0; j < otherCrv.Count; j++)
                {
                    currentInter.Add(Intersection.CurveCurve(baseCrv[i], otherCrv[j], Setups.IntersectionTolerance, Setups.OverlapTolerance));
                }
                allIntersections.Add(currentInter);
            }
            return allIntersections;
        }

        public static List<CurveIntersections> MultipleCurveIntersection(List<Curve> curves)
        {
            List<CurveIntersections> allIntersections = new List<CurveIntersections>();
            for (int i = 0; i < curves.Count; i++)
            {
                for (int j = i + 1; j < curves.Count; j++)
                {
                    CurveIntersections inter = Intersection.CurveCurve(curves[i], curves[j], Setups.IntersectionTolerance, Setups.OverlapTolerance);
                    if (inter.Count > 0)
                    {
                        allIntersections.Add(inter);
                    }
                }
            }
            return allIntersections;
        }

        public static Tuple<List<Curve>, List<Curve>> TrimWithRegion(Curve crv, Curve region)
        {
            List<Curve> inside = new List<Curve>();
            List<Curve> outside = new List<Curve>();
            CurveIntersections inter = Intersection.CurveCurve(crv, region, 0.001, 0.001);
            if (inter.Count > 0)
            {
                List<double> t_param = new List<double>();
                foreach (IntersectionEvent i in inter)
                {
                    t_param.Add(i.ParameterA);
                }
                t_param.Sort();
                Curve[] splitedCrv = crv.Split(t_param);
                if (splitedCrv.Length > 0)
                {
                    foreach (Curve part_crv in splitedCrv)
                    {
                        Point3d testPt = part_crv.PointAtNormalizedLength(0.5);
                        PointContainment result = region.Contains(testPt, Plane.WorldXY, 0.0001);
                        if (result == PointContainment.Inside) inside.Add(part_crv);
                        else if (result == PointContainment.Outside) outside.Add(part_crv);
                        else if (result == PointContainment.Unset) throw new InvalidOperationException("Unset");
                        else throw new InvalidOperationException("Trim Failed");
                    }
                }
                else throw new InvalidOperationException("Trim Failed on Split");
            }
            // IF no intersection...
            else
            {
                Point3d testPt = crv.PointAtNormalizedLength(0.5);
                PointContainment result = region.Contains(testPt, Plane.WorldXY, 0.0001);
                if (result == PointContainment.Inside) inside.Add(crv);
                else if (result == PointContainment.Outside) outside.Add(crv);
                else if (result == PointContainment.Unset) throw new InvalidOperationException("Unset");
                else throw new InvalidOperationException("Trim Failed");
            }
            return Tuple.Create(inside, outside);
        }

        // Wg mnie nie dziala dobrze...
        public static Tuple<List<Curve>, List<Curve>> TrimWithRegions(Curve crv, List<Curve> regions)
        {
            List<Curve> inside = new List<Curve>();
            List<Curve> outside = new List<Curve>();
            List<CurveIntersections> inter = CurveCurveIntersection(crv, regions);
            SortedList<double, Point3d> data = new SortedList<double, Point3d>();
            foreach (CurveIntersections crvInter in inter)
            {
                foreach (IntersectionEvent inEv in crvInter)
                {
                    data.Add(inEv.ParameterA, inEv.PointA);
                }
            }
            Curve[] splitedCrv = crv.Split(data.Keys);
            if (splitedCrv.Length > 0)
            {
                foreach (Curve part_crv in splitedCrv)
                {
                    Point3d testPt = part_crv.PointAtNormalizedLength(0.5);
                    foreach (Curve region in regions)
                    {
                        PointContainment result = region.Contains(testPt, Plane.WorldXY, 0.0001);
                        if (result == PointContainment.Inside) inside.Add(part_crv);
                        else if (result == PointContainment.Outside) outside.Add(part_crv);
                        else if (result == PointContainment.Unset) throw new InvalidOperationException("Unset");
                        else throw new InvalidOperationException("Trim Failed");
                    }
                }
                // IF no intersection...
            }
            else
            {
                foreach (Curve region in regions)
                {
                    Point3d testPt = crv.PointAtNormalizedLength(0.5);
                    PointContainment result = region.Contains(testPt, Plane.WorldXY, 0.0001);
                    if (result == PointContainment.Inside)
                    {
                        inside.Add(crv);
                    }
                }
                if (inside.Count == 0)
                {
                    outside.Add(crv);
                }
            }
            return Tuple.Create(inside, outside);
        }

        public static List<PolylineCurve> IrregularVoronoi(List<Cell> cells, Polyline blister, int resolution = 50, double tolerance = 0.05)
        {
            Grasshopper.Kernel.Geometry.Node2List n2l = new Grasshopper.Kernel.Geometry.Node2List();
            List<Grasshopper.Kernel.Geometry.Node2> outline = new List<Grasshopper.Kernel.Geometry.Node2>();
            foreach (Cell cell in cells)
            {
                Point3d[] pts;
                cell.pill.DivideByCount(resolution, false, out pts);
                foreach (Point3d pt in pts)
                {
                    n2l.Append(new Grasshopper.Kernel.Geometry.Node2(pt.X, pt.Y));
                }
            }

            foreach (Point3d pt in blister)
            {
                outline.Add(new Grasshopper.Kernel.Geometry.Node2(pt.X, pt.Y));
            }

            Connectivity del_con = Grasshopper.Kernel.Geometry.Delaunay.Solver.Solve_Connectivity(n2l, 0.001, true);
            List<Cell2> voronoi = Grasshopper.Kernel.Geometry.Voronoi.Solver.Solve_Connectivity(n2l, del_con, outline);

            List<PolylineCurve> vCells = new List<PolylineCurve>();
            for (int i = 0; i < cells.Count; i++)
            {
                List<Point3d> pts = new List<Point3d>();
                for (int j = 0; j < resolution - 1; j++)
                {
                    int glob_index = (i * (resolution - 1)) + j;
                    // vor.Add(voronoi[glob_index].ToPolyline());
                    Point3d[] vert = voronoi[glob_index].ToPolyline().ToArray();
                    foreach (Point3d pt in vert)
                    {
                        PointContainment result = cells[i].pill.Contains(pt, Rhino.Geometry.Plane.WorldXY, 0.0001);
                        if (result == PointContainment.Outside)
                        {
                            pts.Add(pt);
                        }
                    }
                }

                Polyline poly = new Polyline(SortPtsAlongCurve(Point3d.CullDuplicates(pts, 0.001), cells[i].pill));
                poly.Add(poly[0]);
                poly.ReduceSegments(tolerance);
                vCells.Add(new PolylineCurve(poly));
                cells[i].voronoi = new PolylineCurve(poly);
            }
            return vCells;
        }
    }
}
