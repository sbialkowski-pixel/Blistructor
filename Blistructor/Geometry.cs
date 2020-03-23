using System;
using System.Collections.Generic;
using System.Linq;

using Rhino;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;
using Grasshopper.Kernel.Types;

using GH_Delanuey = Grasshopper.Kernel.Geometry.Delaunay;
using GH_Voronoi = Grasshopper.Kernel.Geometry.Voronoi;


namespace Blistructor
{
    static class Geometry
    {
        //TODO: SnapToPoints could not check only poilt-point realtion byt also point-cyrve... to investigate
        //public static PolylineCurve SnapToPoints_v2(PolylineCurve moving, PolylineCurve stationary, double tolerance)
        //{
        //    stationary.ClosestPoint()

        //}

        public static PolylineCurve SnapToPoints(PolylineCurve moving, PolylineCurve stationary, double tolerance)
        {
            Polyline pMoving = moving.ToPolyline();
            PointCloud fixedPoints = new PointCloud(stationary.ToPolyline());
            // Check start point
            int s_index = fixedPoints.ClosestPoint(pMoving.First);
            if (s_index != -1)
            {
                if (fixedPoints[s_index].Location.DistanceTo(pMoving.First) < tolerance)
                {
                    pMoving[0] = fixedPoints[s_index].Location;
                }
            }
            // Check end point
            int e_index = fixedPoints.ClosestPoint(pMoving.Last);
            if (e_index != -1)
            {
                if (fixedPoints[e_index].Location.DistanceTo(pMoving.Last) < tolerance)
                {
                    pMoving[pMoving.Count - 1] = fixedPoints[e_index].Location;
                }
            }
            return pMoving.ToPolylineCurve();

        }

        public static PolylineCurve SnapToPoints(LineCurve moving, PolylineCurve stationary, double tolerance)
        {
            return Geometry.SnapToPoints(new PolylineCurve(new Point3d[] { moving.PointAtStart, moving.PointAtEnd }), stationary, tolerance);
        }

        public static void FlipIsoRays(Curve guideCrv, LineCurve crv)
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
            if (crvs.Count <= 1) return crvs;
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
                        PointContainment result = region.Contains(testPt, Plane.WorldXY, 0.000001);
                        if (result == PointContainment.Inside) inside.Add(part_crv);
                        else if (result == PointContainment.Outside) outside.Add(part_crv);
                        else if (result == PointContainment.Unset) throw new InvalidOperationException("Unset");
                        else
                        {

                            throw new InvalidOperationException(String.Format("Trim Failed- {0}", result.ToString()));
                        }
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
            List<Curve> splitedCrv = crv.Split(data.Keys).ToList<Curve>();
            // If ther is intersection...
            if (splitedCrv.Count > 0)
            {
                // Look for all inside parts of cure and move them to inside list.
                for (int i = 0; i < splitedCrv.Count; i++)
                {
                    Curve part_crv = splitedCrv[i];
                    Point3d testPt = part_crv.PointAtNormalizedLength(0.5);
                    foreach (Curve region in regions)
                    {
                        PointContainment result = region.Contains(testPt, Plane.WorldXY, 0.0001);
                        if (result == PointContainment.Inside)
                        {
                            inside.Add(part_crv);
                            splitedCrv.RemoveAt(i);
                            i--;
                            break;
                        }

                    }
                }
                // add leftovers to outside list
                outside.AddRange(splitedCrv);
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

        public static Tuple<List<Curve>, List<Curve>> TrimWithRegion(List<Curve> crv, Curve region)
        {
            List<Curve> inside = new List<Curve>();
            List<Curve> outside = new List<Curve>();
            foreach (Curve c in crv)
            {
                Tuple<List<Curve>, List<Curve>> result = Geometry.TrimWithRegion(c, region);
                inside.AddRange(result.Item1);
                outside.AddRange(result.Item2);
            }
            return Tuple.Create(inside, outside);
        }

        // BUGERSONS!!!!
        public static Tuple<List<List<Curve>>, List<List<Curve>>> TrimWithRegions(List<Curve> crv, List<Curve> regions)
        {
            List<List<Curve>> inside = new List<List<Curve>>();
            List<List<Curve>> outside = new List<List<Curve>>();
            foreach (Curve region in regions)
            {
                Tuple<List<Curve>, List<Curve>> result = Geometry.TrimWithRegion(crv, region);
                inside.Add(result.Item1);
                outside.Add(result.Item2);
            }
            return Tuple.Create(inside, outside);
        }

        public static List<Curve> SplitRegion(Curve region, Curve splittingCurve)
        {
            List<double> region_t_params = new List<double>();
            List<double> splitter_t_params = new List<double>();
            CurveIntersections intersection = Intersection.CurveCurve(splittingCurve, region, Setups.IntersectionTolerance, Setups.OverlapTolerance);
            if (!region.IsClosed)
            {
                return null;
            }

            if (intersection == null)
            {
                return null;
            }
            if (intersection.Count % 2 != 0 || intersection.Count == 0)
            {
                return null;
            }
            foreach (IntersectionEvent inter in intersection)
            {
                splitter_t_params.Add(inter.ParameterA);
                region_t_params.Add(inter.ParameterB);
            }
            splitter_t_params.Sort();
            region_t_params.Sort();
            Curve[] splited_splitter = splittingCurve.Split(splitter_t_params);
            List<Curve> sCurve = new List<Curve>();
            foreach (Curve crv in splited_splitter)
            {
                Point3d testPt = crv.PointAtNormalizedLength(0.5);
                PointContainment result = region.Contains(testPt, Plane.WorldXY, 0.0001);
                if (result == PointContainment.Inside)
                {
                    sCurve.Add(crv);
                }
            }

            // If ther is only one splitter
            List<Curve> pCurve = new List<Curve>();
            if (sCurve.Count == 1 && region_t_params.Count == 2)
            {
                List<Curve> splited_region = region.Split(region_t_params).ToList();
                if (splited_region.Count == 2)
                {
                    foreach (Curve out_segment in splited_region)
                    {
                        Curve[] temp = Curve.JoinCurves(new List<Curve>() { out_segment, sCurve[0] });
                        if (temp.Length != 1)
                        {
                            break;
                        }
                        if (temp[0].IsClosed)
                        {
                            pCurve.Add(temp[0]);
                        }
                    }
                }
            }
            else
            {
                // Use recursieve option
                pCurve.AddRange(SplitRegion(region, sCurve));
            }
            return pCurve;
        }

        public static List<Curve> SplitRegion(Curve region, List<Curve> splitters)
        {
            //List<PolylineCurve> out_regions = new List<PolylineCurve>();
            List<Curve> temp_regions = new List<Curve>();
            temp_regions.Add(region);

            foreach (PolylineCurve splitter in splitters)
            {
                List<Curve> current_temp_regions = new List<Curve>();
                foreach (Curve current_region in temp_regions)
                {
                    List<Curve> choped_region = SplitRegion(current_region, splitter);
                    if (choped_region != null)
                    {
                        foreach (Curve _region in choped_region)
                        {
                            Curve[] c_inter = Curve.CreateBooleanIntersection(_region, region, Setups.GeneralTolerance);
                            foreach (Curve inter_curve in c_inter)
                            {
                                current_temp_regions.Add(inter_curve);
                            }
                        }
                    }
                    else
                    {
                        if (region.Contains(AreaMassProperties.Compute(current_region).Centroid, Plane.WorldXY, Setups.GeneralTolerance) == PointContainment.Inside)
                        {
                            current_temp_regions.Add(current_region);
                        }
                    }
                }
                temp_regions = new List<Curve>(current_temp_regions);
            }
            return temp_regions;
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

            GH_Delanuey.Connectivity del_con = GH_Delanuey.Solver.Solve_Connectivity(n2l, 0.0001, true);
            List<GH_Voronoi.Cell2> voronoi = GH_Voronoi.Solver.Solve_Connectivity(n2l, del_con, outline);

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

                Circle fitCirc;
                Circle.TryFitCircleToPoints(pts, out fitCirc);
                Polyline poly = new Polyline(SortPtsAlongCurve(Point3d.CullDuplicates(pts, 0.0001), fitCirc.ToNurbsCurve()));
                poly.Add(poly[0]);
                poly.ReduceSegments(tolerance);
                vCells.Add(new PolylineCurve(poly));
                cells[i].voronoi = new PolylineCurve(poly);
            }
            return vCells;
        }

        public static PolylineCurve MinimumAreaRectangleBF(Curve crv)
        {
            Point3d centre = AreaMassProperties.Compute(crv).Centroid;
            double minArea = double.MaxValue;
            PolylineCurve outCurve = null;

            for (double i = 0; i < 180; i++)
            {
                double radians = RhinoMath.ToRadians(i);
                Curve currentCurve = crv.DuplicateCurve();
                currentCurve.Rotate(radians, Vector3d.ZAxis, centre);
                BoundingBox box = currentCurve.GetBoundingBox(false);
                if (box.Area < minArea)
                {
                    minArea = box.Area;
                    Rectangle3d rect = new Rectangle3d(Plane.WorldXY, box.Min, box.Max);
                    PolylineCurve r = rect.ToPolyline().ToPolylineCurve();
                    r.Rotate(-radians, Vector3d.ZAxis, centre);
                    outCurve = r;
                }
            }
            return outCurve;
        }

        public static PolylineCurve PolylineThicken(PolylineCurve crv, double thickness)
        {

            List<Curve> Outline = new List<Curve>();
            Curve[] offser_1 = crv.Offset(Plane.WorldXY, thickness, Setups.GeneralTolerance, CurveOffsetCornerStyle.Sharp);
            if (offser_1.Length == 1) Outline.Add(offser_1[0]);
            else return null;
            Curve[] offser_2 = crv.Offset(Plane.WorldXY, -thickness, Setups.GeneralTolerance, CurveOffsetCornerStyle.Sharp);
            if (offser_2.Length == 1) Outline.Add(offser_2[0]);
            else return null;

            if (Outline.Count != 2) return null;
            Outline.Add(new LineCurve(Outline[0].PointAtStart, Outline[1].PointAtStart));
            Outline.Add(new LineCurve(Outline[0].PointAtEnd, Outline[1].PointAtEnd));
            Curve[] result = Curve.JoinCurves(Outline);
            if (result.Length != 1) return null;
            return (PolylineCurve)result[0];
        }

        /// <summary>
        /// Set Counter-Clockwise direction of curve and set it domain to 0.0 - 1.0
        /// </summary>
        /// <param name="crv"></param>
        public static void UnifyCurve(Curve crv)
        {
            CurveOrientation orient = crv.ClosedCurveOrientation(Vector3d.ZAxis);
            if (orient == CurveOrientation.Clockwise)
            {
                crv.Reverse();
            }
            crv.Domain = new Interval(0.0, 1.0);
        }
    }

}
