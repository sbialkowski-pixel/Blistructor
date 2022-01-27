﻿using System;
using System.Collections.Generic;
using System.Linq;
#if PIXEL
using Pixel.Rhino;
using Pixel.Rhino.Geometry;
using Pixel.Rhino.Geometry.Intersect;
using ExtraMath = Pixel.Rhino.RhinoMath;
#else
using Rhino;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;
using ExtraMath = Rhino.RhinoMath;
#endif

using Diagrams;
//using Voronoi = Diagrams.Voronoi;
//using Delaunay = Diagrams.Delaunay;


namespace Blistructor
{
    public static class Geometry
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
            Curve temp = crv.Extend(CurveEnd.Both, 10000);

            //Curve temp = crv.Extend(CurveEnd.Both, 10000, CurveExtensionStyle.Line); <- proper Rhino Method, Above my edit.
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
                    List<IntersectionEvent> inter = Intersection.CurveCurve(obstacles[obId], ray, Setups.IntersectionTolerance);
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
                NurbsCurve baseCurve = crvs[i].ToNurbsCurve();
                for (int j = i + 1; j < crvs.Count; j++)
                {
                    NurbsCurve testCurve = crvs[j].ToNurbsCurve();
                    if (baseCurve.EpsilonEquals(testCurve, 0.01))
                    {
                        unique = false;
                        break;
                    }
                    testCurve.Reverse();
                    if (baseCurve.EpsilonEquals(testCurve, 1))
                    {
                        unique = false;
                        break;
                    }
                }
                // if (GeometryBase.GeometryEquals(crvs[i], crvs[j]))
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
        
       
        public static Circle FitCircle(List<Point3d> points)
        {
            Polyline pline = new Polyline(points);
            Point3d center = pline.CenterPoint();
            double radius = (double)points.Select(pt => pt.DistanceTo(center)).Sum() / (double)points.Count;
            return new Circle(center, radius);
        }
        
        /*
        public static Polyline ConvexHull(List<Point3d> pts)
        {
            List<GH_Point> po = new List<GH_Point>();
            foreach (Point3d pt in pts)
            {
                po.Add(new GH_Point(pt));
            }
            return Grasshopper.Kernel.Geometry.ConvexHull.Solver.ComputeHull(po);
        }
        */

        public static List<List<IntersectionEvent>> CurveCurveIntersection(Curve baseCrv, List<Curve> otherCrv)
        {
            List<List<IntersectionEvent>> allIntersections = new List<List<IntersectionEvent>>(otherCrv.Count);
            for (int i = 0; i < otherCrv.Count; i++)
            {
                List<IntersectionEvent> inter = Intersection.CurveCurve(baseCrv, otherCrv[i], Setups.IntersectionTolerance);
                if (inter.Count > 0)
                {
                    allIntersections.Add(inter);
                }
            }
            return allIntersections;
        }

        public static List<List<List<IntersectionEvent>>> CurveCurveIntersection(List<Curve> baseCrv, List<Curve> otherCrv)
        {
            List<List< List<IntersectionEvent>>> allIntersections = new List<List<List<IntersectionEvent>>>(baseCrv.Count);
            for (int i = 0; i < baseCrv.Count; i++)
            {
                List<List<IntersectionEvent>> currentInter = new List<List<IntersectionEvent>>(otherCrv.Count);
                for (int j = 0; j < otherCrv.Count; j++)
                {
                    currentInter.Add(Intersection.CurveCurve(baseCrv[i], otherCrv[j], Setups.IntersectionTolerance));
                }
                allIntersections.Add(currentInter);
            }
            return allIntersections;
        }

        public static List<List<IntersectionEvent>> MultipleCurveIntersection(List<Curve> curves)
        {
            List<List<IntersectionEvent>> allIntersections = new List<List<IntersectionEvent>>();
            for (int i = 0; i < curves.Count; i++)
            {
                for (int j = i + 1; j < curves.Count; j++)
                {
                    List<IntersectionEvent> inter = Intersection.CurveCurve(curves[i], curves[j], Setups.IntersectionTolerance);
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
            List<IntersectionEvent> inter = Intersection.CurveCurve(crv, region, Setups.IntersectionTolerance);
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
            List<List<IntersectionEvent>> inter = CurveCurveIntersection(crv, regions);
            SortedList<double, Point3d> data = new SortedList<double, Point3d>();
            foreach (List<IntersectionEvent> crvInter in inter)
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="crv"></param>
        /// <param name="region"></param>
        /// <returns>Tuple <Inside, Outside></returns>
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
        
        /// <summary>
        /// Split Closed Curve by any other curve
        /// </summary>
        /// <param name="region">Closed curve (region) to split</param>
        /// <param name="splittingCurve">Curve for spliting</param>
        /// <returns>Splitted regions or null if no splitting occured or region is not closed curve.</returns>

        public static List<Curve> SplitRegion(Curve region, Curve splittingCurve)
        {
            List<double> region_t_params = new List<double>();
            List<double> splitter_t_params = new List<double>();
            List<IntersectionEvent> intersection = Intersection.CurveCurve(splittingCurve, region, Setups.IntersectionTolerance);
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

            foreach (Curve splitter in splitters)
            {
                List<Curve> current_temp_regions = new List<Curve>();
                foreach (Curve current_region in temp_regions)
                {
                    List<Curve> choped_region = SplitRegion(current_region, splitter);
                    if (choped_region != null)
                    {
                        foreach (Curve _region in choped_region)
                        {
                            List<Curve> c_inter = Curve.CreateBooleanIntersection(_region, region);
                            foreach (Curve inter_curve in c_inter)
                            {
                                current_temp_regions.Add(inter_curve);
                            }
                        }
                    }
                    else
                    {
                        if (region.Contains(current_region.CenterPoint(), Plane.WorldXY, Setups.GeneralTolerance) == PointContainment.Inside)
                        {
                            current_temp_regions.Add(current_region);
                        }
                    }
                }
                temp_regions = new List<Curve>(current_temp_regions);
            }
            return temp_regions;
        }

        //TODO: Dokonczyć reguralny voronoi i dodac jako stage 0 ciecia.
        public static List<PolylineCurve> RegularVoronoi(List<Pill> pills, Polyline blister,  double tolerance = 0.05)
        {
            Diagrams.Node2List n2l = new Diagrams.Node2List();
            List < Diagrams.Node2 > outline = new List<Diagrams.Node2>();
            foreach (Pill pill in pills)
            {
                n2l.Append(new Diagrams.Node2(pill.Center.X, pill.Center.Y));
            }

            foreach (Point3d pt in blister)
            {
                outline.Add(new Diagrams.Node2(pt.X, pt.Y));
            }
            List<Diagrams.Voronoi.Cell2> voronoi = Diagrams.Voronoi.Solver.Solve_BruteForce(n2l, outline);
            //Diagrams.Delaunay.Connectivity del_con = Diagrams.Delaunay.Solver.Solve_Connectivity(n2l, 0.0001, true);
           // List<Diagrams.Voronoi.Cell2> voronoi = Diagrams.Voronoi.Solver.Solve_Connectivity(n2l, del_con, outline);

            List<PolylineCurve> output = new List<PolylineCurve>(voronoi.Count);
            foreach (Diagrams.Voronoi.Cell2 pill in voronoi)
            {
                output.Add(pill.ToPolyline().ToPolylineCurve());
            }
            return output;
        }

        public static List<PolylineCurve> IrregularVoronoi(List<Pill> pills, Polyline blister, int resolution = 50, double tolerance = 0.05)
        {
            Diagrams.Node2List n2l = new Diagrams.Node2List();
            List<Diagrams.Node2> outline = new List<Diagrams.Node2>();
            foreach (Pill pill in pills)
            {
                Point3d[] pts;
                pill.Outline.DivideByCount(resolution, false, out pts);
                foreach (Point3d pt in pts)
                {
                    n2l.Append(new Diagrams.Node2(pt.X, pt.Y));
                }
            }

            foreach (Point3d pt in blister)
            {
                outline.Add(new Diagrams.Node2(pt.X, pt.Y));
            }

            Diagrams.Delaunay.Connectivity del_con = Diagrams.Delaunay.Solver.Solve_Connectivity(n2l, 0.0001, true);
            List < Diagrams.Voronoi.Cell2 > voronoi = Diagrams.Voronoi.Solver.Solve_Connectivity(n2l, del_con, outline);

            List<PolylineCurve> vCells = new List<PolylineCurve>();
            for (int i = 0; i < pills.Count; i++)
            {
                List<Point3d> pts = new List<Point3d>();
                for (int j = 0; j < resolution - 1; j++)
                {
                    int glob_index = (i * (resolution - 1)) + j;
                    // vor.Add(voronoi[glob_index].ToPolyline());
                    if (voronoi[glob_index].C.Count == 0) continue;
                    Point3d[] vert = voronoi[glob_index].ToPolyline().ToArray();
                    foreach (Point3d pt in vert)
                    {
                        PointContainment result = pills[i].Outline.Contains(pt, Plane.WorldXY, 0.0001);
                        if (result == PointContainment.Outside)
                        {
                            pts.Add(pt);
                        }
                    }
                }

               // Circle fitCirc;
               // Circle.TryFitCircleToPoints(pts, out fitCirc);
                Circle fitCirc = Geometry.FitCircle(pts);
                Polyline poly = new Polyline(SortPtsAlongCurve(Point3d.CullDuplicates(pts, 0.0001), fitCirc.ToNurbsCurve()));
                poly.Add(poly[0]);
                poly.ReduceSegments(tolerance);
                vCells.Add(new PolylineCurve(poly));
                pills[i].voronoi = new PolylineCurve(poly);
            }
            return vCells;
        }

        public static PolylineCurve MinimumAreaRectangleBF(Curve crv)
        {

            Point3d centre = ((PolylineCurve)crv).ToPolyline().CenterPoint();
            double minArea = double.MaxValue;
            PolylineCurve outCurve = null;

            for (double i = 0; i < 180; i+=0.5)
            {
                double radians = ExtraMath.ToRadians(i);
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
           // outCurve.ToPolyline()
            return outCurve;
        }

        public static PolylineCurve PolylineThicken(PolylineCurve crv, double thickness)
        {

            List<Curve> Outline = new List<Curve>();
            Curve offser_1 = crv.Offset(Plane.WorldXY, thickness);
            if (offser_1 != null) Outline.Add(offser_1);
            else return null;
            Curve offser_2 = crv.Offset(Plane.WorldXY, -thickness);
            if (offser_2 != null) Outline.Add(offser_2);
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

        #region Douglas-Peucker Reduce 
        public const double EPSILON = 1.2e-12;
        /// <summary>
        /// "Reduces" a set of line segments by removing points that are too far away. Does not modify the input list; returns
        /// a new list with the points removed.
        /// The image says it better than I could ever describe: http://upload.wikimedia.org/wikipedia/commons/3/30/Douglas-Peucker_animated.gif
        /// The wiki article: http://en.wikipedia.org/wiki/Ramer%E2%80%93Douglas%E2%80%93Peucker_algorithm
        /// Based on:  http://www.codeproject.com/Articles/18936/A-Csharp-Implementation-of-Douglas-Peucker-Line-Ap
        /// </summary>
        /// <param name="pts">Points to reduce</param>
        /// <param name="error">Maximum distance of a point to a line. Low values (~2-4) work well for mouse/touchscreen data.</param>
        /// <returns>A new list containing only the points needed to approximate the curve.</returns>
        public static Polyline DouglasPeuckerReduce(Polyline pLine, double error)
        {

            List<Point3d> pts = pLine.ToList();
            if (pts == null) throw new ArgumentNullException("pts");
            pts = RemoveDuplicates(pts);
            if (pts.Count < 3)
                return new Polyline(pts);
            List<int> keepIndex = new List<int>(Math.Max(pts.Count / 2, 16));
            keepIndex.Add(0);
            keepIndex.Add(pts.Count - 1);
            DouglasPeuckerRecursive(pts, error, 0, pts.Count - 1, keepIndex);
            keepIndex.Sort();
            //List<Vector3d> res = new List<Vector3d>(keepIndex.Count);
            // ReSharper disable once LoopCanBeConvertedToQuery
            Polyline outLine = new Polyline(keepIndex.Count);

            foreach (int idx in keepIndex)
                outLine.Add(pts[idx]);
            return outLine;
        }

        /// <summary>
        /// Removes any repeated points (that is, one point extremely close to the previous one). The same point can
        /// appear multiple times just not right after one another. This does not modify the input list. If no repeats
        /// were found, it returns the input list; otherwise it creates a new list with the repeats removed.
        /// </summary>
        /// <param name="pts">Initial list of points.</param>
        /// <returns>Either pts (if no duplicates were found), or a new list containing pts with duplicates removed.</returns>
        public static List<Point3d> RemoveDuplicates(List<Point3d> pts)
        {
            if (pts.Count < 2)
                return pts;

            // Common case -- no duplicates, so just return the source list
            Point3d prev = pts[0];
            int len = pts.Count;
            int nDup = 0;
            for (int i = 1; i < len; i++)
            {
                Point3d cur = pts[i];
                if (EqualsOrClose(prev, cur))
                    nDup++;
                else
                    prev = cur;
            }

            if (nDup == 0)
                return pts;
            else
            {
                // Create a copy without them
                List<Point3d> dst = new List<Point3d>(len - nDup);
                prev = pts[0];
                dst.Add(prev);
                for (int i = 1; i < len; i++)
                {
                    Point3d cur = pts[i];
                    if (!EqualsOrClose(prev, cur))
                    {
                        dst.Add(cur);
                        prev = cur;
                    }
                }
                return dst;
            }
        }

        public static bool EqualsOrClose(Point3d v1, Point3d v2)
        {
            return v1.DistanceToSquared(v2) < EPSILON;
        }

        private static void DouglasPeuckerRecursive(List<Point3d> pts, double error, int first, int last, List<int> keepIndex)
        {
            int nPts = last - first + 1;
            if (nPts < 3)
                return;

            Point3d a = pts[first];
            Point3d b = pts[last];
            double abDist = a.DistanceTo(b);
            double aCrossB = a.X * b.Y - b.X * a.Y;
            double maxDist = error;
            int split = 0;
            for (int i = first + 1; i < last - 1; i++)
            {
                Point3d p = pts[i];
                Line ab = new Line(a, b);
                double pDist = ab.DistanceTo(p, true);

                if (pDist > maxDist)
                {
                    maxDist = pDist;
                    split = i;
                }
            }

            if (split != 0)
            {
                keepIndex.Add(split);
                DouglasPeuckerRecursive(pts, error, first, split, keepIndex);
                DouglasPeuckerRecursive(pts, error, split, last, keepIndex);
            }
        }
        #endregion

        /*
        public static void ApplyCalibrationData(PolylineCurve curve, Vector3d calibrationVector, double pixelSpacing = 1.0, double rotation = Math.PI / 6)
        {
            curve.Translate(-calibrationVector);
            curve.Rotate(rotation, Vector3d.ZAxis, new Point3d(0, 0, 0));
            curve.Scale(pixelSpacing);
        }
        */

        #region InclusionTests
        public static bool InclusionTest(Pill testCell, Blister blister)
        {
            return InclusionTest(testCell.Offset, blister.Outline);
        }

        public static bool InclusionTest(Pill testCell, Curve Region)
        {
            return InclusionTest(testCell.Offset, Region);
        }

        public static bool InclusionTest(Curve testCurve, Curve Region)
        {
            RegionContainment test = Curve.PlanarClosedCurveRelationship(Region, testCurve);
            if (test == RegionContainment.BInsideA) return true;
            else return false;
        }
        #endregion


        public static void ApplyCalibration(GeometryBase geometry, Vector3d calibrationVector, double pixelSpacing = 1.0, double rotation = Math.PI / 6)
        {
            geometry.Translate(-calibrationVector);
            geometry.Rotate(rotation, Vector3d.ZAxis, new Point3d(0, 0, 0));
            geometry.Scale(pixelSpacing);
        }

        public static GeometryBase ReverseCalibration(GeometryBase geometry, Vector3d calibrationVector, double pixelSpacing = 1.0, double rotation = Math.PI / 6)
        {
            GeometryBase newGeometry = geometry.Duplicate();
            newGeometry.Scale(1 / pixelSpacing);
            newGeometry.Rotate(-rotation, Vector3d.ZAxis, new Point3d(0, 0, 0));
            newGeometry.Translate(calibrationVector);
            return newGeometry;
        }
    }

}
