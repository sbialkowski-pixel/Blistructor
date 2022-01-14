using System;
using System.Collections;
using System.Collections.Generic;

using Rhino;
using Rhino.Geometry;

using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

// <Custom using>
using System.IO;
using System.Linq;
using System.Reflection;
using System.ComponentModel;
using System.Globalization;

using Px = BlistructorGH;

using PxGeo = Pixel.Rhino.Geometry;
using PxGeoI = Pixel.Rhino.Geometry.Intersect;
using ExtraMath = Pixel.Rhino.RhinoMath;
// </Custom using>


namespace Blistructor.Prototype
{
    public class Script_Instance
    {
        /* Wypełniacz zeny linie sie zgadzały
         * 
         * 
         * 
         * 
         * 
         * 
         * 
         * 
         * 
         * 
         * 
         * 
         * 
         * 
         * 
         * 
         * 
         * 
         * 
         * 
         * 
         * 
         * 
         * 
         * 
         * 
         * 
         * 
         * 
         * 
         * 
         * 
         * 
         */
        private void RunScript(Polyline blister, Polyline pill, List<Curve> pillNeigh, List<Line> connLine, List<Line> proxLine, ref object A)
        {
            // <Custom code>
            // BASIC STUFF
            PxGeo.PolylineCurve Pill = Px.Convert.ToPix(pill).ToPolylineCurve();
            PxGeo.PolylineCurve Blister = Px.Convert.ToPix(blister).ToPolylineCurve();
            List<PxGeo.Line> ConnLine = connLine.Select(line => Px.Convert.ToPix(line)).ToList();
            List<PxGeo.Line> ProxLine = proxLine.Select(line => Px.Convert.ToPix(line)).ToList();

            ConnLine.RemoveAt(0);

            Geometry.UnifyCurve(Pill);
            PxGeo.Point3d center = Pill.CenterPoint();
            PxGeo.PolylineCurve offset = (PxGeo.PolylineCurve)Pill.Offset(PxGeo.Plane.WorldXY, Setups.BladeWidth / 2);

            PxGeo.PolylineCurve limitingCircle = PxGeo.Polyline.CreateInscribedPolygon(new PxGeo.Circle(PxGeo.Plane.WorldXY, center, Setups.MinimumCutOutSize/2), 32).ToPolylineCurve();

            Tuple<PxGeo.PolylineCurve,double> minBBoxData = Geometry.MinimumAreaRectangleBF(offset);
            PxGeo.PolylineCurve minBBox = minBBoxData.Item1;
            double radius = minBBoxData.Item2;
            PxGeo.Polyline minBBoxPline = minBBox.ToPolyline();
            PxGeo.Polyline pline = new PxGeo.Polyline(new List<PxGeo.Point3d> {minBBoxPline[0], minBBoxPline[1], minBBoxPline[2]});
            double minBoxProportion = (minBBoxPline[0] - minBBoxPline[1]).Length / (minBBoxPline[1] - minBBoxPline[2]).Length;
            if (minBoxProportion > 0.95 && minBoxProportion < 1.05) {

                PxGeo.BoundingBox bbox = offset.GetBoundingBox(false);
                minBBox = new PxGeo.Rectangle3d(PxGeo.Plane.WorldXY, bbox.Min, bbox.Max).ToPolyline().ToPolylineCurve();
            }
            List<PolylineCurve> connAABB = new List<PolylineCurve>();
            foreach (PxGeo.Line ln in ConnLine)
            {
                connAABB.Add(Px.Convert.ToRh(Geometry.AngleAlignedBoundingBox(offset,ln.Direction)));
            }

            foreach (PxGeo.Line ln in ProxLine)
            {
                connAABB.Add(Px.Convert.ToRh(Geometry.AngleAlignedBoundingBox(offset, ln.Direction)));
            }

            //A = Px.Convert.ToRh(minBBox);
            // A = connAABB;
            Tuple<List<PxGeo.Curve>, List<PxGeo.Curve>> data =  Geometry.TrimWithRegion(Blister, limitingCircle);
            //A = data.Item1.Select(d => Px.Convert.ToRh((PxGeo.PolylineCurve)d));
            //A = Geometry.SplitRegion(Blister, limitingCircle).Select(pline1 => Px.Convert.ToRh((PxGeo.PolylineCurve)pline1));




            // </Custom code>
        }
        // <Custom additional code>
        public static class Geometry
        {
            //TODO: SnapToPoints could not check only poilt-point realtion byt also point-cyrve... to investigate
            //public static PxGeo.PolylineCurve SnapToPoints_v2(PxGeo.PolylineCurve moving, PxGeo.PolylineCurve stationary, double tolerance)
            //{
            //    stationary.ClosestPoint()

            //}

            public static PxGeo.PolylineCurve SnapToPoints(PxGeo.PolylineCurve moving, PxGeo.PolylineCurve stationary, double tolerance)
            {
                PxGeo.Polyline pMoving = moving.ToPolyline();
                PxGeo.PointCloud fixedPoints = new PxGeo.PointCloud(stationary.ToPolyline());
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

            public static PxGeo.PolylineCurve SnapToPoints(PxGeo.LineCurve moving, PxGeo.PolylineCurve stationary, double tolerance)
            {
                return Geometry.SnapToPoints(new PxGeo.PolylineCurve(new PxGeo.Point3d[] { moving.PointAtStart, moving.PointAtEnd }), stationary, tolerance);
            }

            public static void FlipIsoRays(PxGeo.Curve guideCrv, PxGeo.LineCurve crv)
            {
                PxGeo.Curve temp = crv.Extend(PxGeo.CurveEnd.Both, 10000);

                //PxGeo.Curve temp = crv.Extend(PxGeo.CurveEnd.Both, 10000, CurveExtensionStyle.PxGeo.Line); <- proper Rhino Method, Above my edit.
                PxGeo.LineCurve extended = new PxGeo.LineCurve(temp.PointAtStart, temp.PointAtEnd);
                PxGeo.Point3d guidePt, crvPt;
                if (guideCrv.ClosestPoints(extended, out guidePt, out crvPt))
                {
                    double guide_t;
                    if (guideCrv.ClosestPoint(guidePt, out guide_t, 1.0))
                    {
                        PxGeo.Vector3d guide_v = guideCrv.TangentAt(guide_t);
                        PxGeo.Vector3d crv_v = crv.Line.UnitTangent;
                        if (guide_v * crv_v < 0.01)
                        {
                            crv.Reverse();
                        }
                    }
                }
            }

            public static PxGeo.LineCurve GetIsoLine(PxGeo.Point3d source, PxGeo.Vector3d direction, double radius, List<PxGeo.Curve> obstacles)
            {

                PxGeo.LineCurve rayA = new PxGeo.LineCurve(new PxGeo.Line(source, direction, radius));
                PxGeo.LineCurve rayB = new PxGeo.LineCurve(new PxGeo.Line(source, -direction, radius));

                List<PxGeo.LineCurve> rays = new List<PxGeo.LineCurve> { rayA, rayB };
                List<PxGeo.Point3d> pts = new List<PxGeo.Point3d>(2);

                foreach (PxGeo.LineCurve ray in rays)
                {
                    SortedList<double, PxGeo.Point3d> interData = new SortedList<double, PxGeo.Point3d>();
                    for (int obId = 0; obId < obstacles.Count; obId++)
                    {
                        List<PxGeoI.IntersectionEvent> inter = PxGeoI.Intersection.CurveCurve(obstacles[obId], ray, Setups.IntersectionTolerance);
                        if (inter.Count > 0)
                        {
                            foreach (PxGeoI.IntersectionEvent cross in inter)
                            {
                                interData.Add(cross.ParameterB, cross.PointB);
                            }
                        }
                    }
                    PxGeo.LineCurve rayent = new PxGeo.LineCurve(ray);
                    if (interData.Count > 0)
                    {
                        pts.Add(interData[interData.Keys[0]]);
                    }
                    else
                    {
                        pts.Add(rayent.PointAtEnd);
                    }
                }
                PxGeo.LineCurve isoLine = new PxGeo.LineCurve(pts[0], pts[1]);
                if (isoLine.GetLength() >= Setups.BladeLength) return isoLine;
                else return null;
            }

            public static void EditSeamBasedOnCurve(PxGeo.Curve editCrv, PxGeo.Curve baseCrv)
            {
                PxGeo.Point3d thisPt, otherPt;
                if (editCrv.ClosestPoints(baseCrv, out thisPt, out otherPt))
                {
                    double this_t;
                    editCrv.ClosestPoint(thisPt, out this_t);
                    editCrv.ChangeClosedCurveSeam(this_t);
                }
            }

            public static List<PxGeo.Curve> RemoveDuplicateCurves(List<PxGeo.Curve> crvs)
            {
                if (crvs.Count <= 1) return crvs;
                List<PxGeo.Curve> uniqueCurves = new List<PxGeo.Curve>();
                for (int i = 0; i < crvs.Count; i++)
                {
                    bool unique = true;
                    PxGeo.NurbsCurve baseCurve = crvs[i].ToNurbsCurve();
                    for (int j = i + 1; j < crvs.Count; j++)
                    {
                        PxGeo.NurbsCurve testCurve = crvs[j].ToNurbsCurve();
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

            public static int[] SortPtsAlongCurve(List<PxGeo.Point3d> pts, PxGeo.Curve crv)
            {//out PxGeo.Point3d[] points
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

            public static PxGeo.Point3d[] SortPtsAlongCurve(PxGeo.Point3d[] pts, PxGeo.Curve crv)
            {
                int L = pts.Length;
                PxGeo.Point3d[] points = pts;
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

            public static PxGeo.Circle FitCircle(List<PxGeo.Point3d> points)
            {
                PxGeo.Polyline pline = new PxGeo.Polyline(points);
                PxGeo.Point3d center = pline.CenterPoint();
                double radius = (double)points.Select(pt => pt.DistanceTo(center)).Sum() / (double)points.Count;
                return new PxGeo.Circle(center, radius);
            }

            public static List<List<PxGeoI.IntersectionEvent>> CurveCurveIntersection(PxGeo.Curve baseCrv, List<PxGeo.Curve> otherCrv)
            {
                List<List<PxGeoI.IntersectionEvent>> allIntersections = new List<List<PxGeoI.IntersectionEvent>>(otherCrv.Count);
                for (int i = 0; i < otherCrv.Count; i++)
                {
                    List<PxGeoI.IntersectionEvent> inter = PxGeoI.Intersection.CurveCurve(baseCrv, otherCrv[i], Setups.IntersectionTolerance);
                    if (inter.Count > 0)
                    {
                        allIntersections.Add(inter);
                    }
                }
                return allIntersections;
            }

            public static List<List<List<PxGeoI.IntersectionEvent>>> CurveCurveIntersection(List<PxGeo.Curve> baseCrv, List<PxGeo.Curve> otherCrv)
            {
                List<List<List<PxGeoI.IntersectionEvent>>> allIntersections = new List<List<List<PxGeoI.IntersectionEvent>>>(baseCrv.Count);
                for (int i = 0; i < baseCrv.Count; i++)
                {
                    List<List<PxGeoI.IntersectionEvent>> currentInter = new List<List<PxGeoI.IntersectionEvent>>(otherCrv.Count);
                    for (int j = 0; j < otherCrv.Count; j++)
                    {
                        currentInter.Add(PxGeoI.Intersection.CurveCurve(baseCrv[i], otherCrv[j], Setups.IntersectionTolerance));
                    }
                    allIntersections.Add(currentInter);
                }
                return allIntersections;
            }

            public static List<List<PxGeoI.IntersectionEvent>> MultipleCurveIntersection(List<PxGeo.Curve> curves)
            {
                List<List<PxGeoI.IntersectionEvent>> allIntersections = new List<List<PxGeoI.IntersectionEvent>>();
                for (int i = 0; i < curves.Count; i++)
                {
                    for (int j = i + 1; j < curves.Count; j++)
                    {
                        List<PxGeoI.IntersectionEvent> inter = PxGeoI.Intersection.CurveCurve(curves[i], curves[j], Setups.IntersectionTolerance);
                        if (inter.Count > 0)
                        {
                            allIntersections.Add(inter);
                        }
                    }
                }
                return allIntersections;
            }

            public static Tuple<List<PxGeo.Curve>, List<PxGeo.Curve>> TrimWithRegion(PxGeo.Curve crv, PxGeo.Curve region)
            {
                List<PxGeo.Curve> inside = new List<PxGeo.Curve>();
                List<PxGeo.Curve> outside = new List<PxGeo.Curve>();
                List<PxGeoI.IntersectionEvent> inter = PxGeoI.Intersection.CurveCurve(crv, region, Setups.IntersectionTolerance);
                if (inter.Count > 0)
                {
                    List<double> t_param = new List<double>();
                    foreach (PxGeoI.IntersectionEvent i in inter)
                    {
                        t_param.Add(i.ParameterA);
                    }
                    t_param.Sort();
                    PxGeo.Curve[] splitedCrv = crv.Split(t_param);
                    if (splitedCrv.Length > 0)
                    {
                        foreach (PxGeo.Curve part_crv in splitedCrv)
                        {
                            PxGeo.Point3d testPt = part_crv.PointAtNormalizedLength(0.5);
                            PxGeo.PointContainment result = region.Contains(testPt, PxGeo.Plane.WorldXY, 0.0001);
                            if (result == PxGeo.PointContainment.Inside) inside.Add(part_crv);
                            else if (result == PxGeo.PointContainment.Outside) outside.Add(part_crv);
                            else if (result == PxGeo.PointContainment.Unset) throw new InvalidOperationException("Unset");
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
                    PxGeo.Point3d testPt = crv.PointAtNormalizedLength(0.5);
                    PxGeo.PointContainment result = region.Contains(testPt, PxGeo.Plane.WorldXY, 0.0001);
                    if (result == PxGeo.PointContainment.Inside) inside.Add(crv);
                    else if (result == PxGeo.PointContainment.Outside) outside.Add(crv);
                    else if (result == PxGeo.PointContainment.Unset) throw new InvalidOperationException("Unset");
                    else throw new InvalidOperationException("Trim Failed");
                }
                return Tuple.Create(inside, outside);
            }

            public static Tuple<List<PxGeo.Curve>, List<PxGeo.Curve>> TrimWithRegions(PxGeo.Curve crv, List<PxGeo.Curve> regions)
            {
                List<PxGeo.Curve> inside = new List<PxGeo.Curve>();
                List<PxGeo.Curve> outside = new List<PxGeo.Curve>();
                List<List<PxGeoI.IntersectionEvent>> inter = CurveCurveIntersection(crv, regions);
                SortedList<double, PxGeo.Point3d> data = new SortedList<double, PxGeo.Point3d>();
                foreach (List<PxGeoI.IntersectionEvent> crvInter in inter)
                {
                    foreach (PxGeoI.IntersectionEvent inEv in crvInter)
                    {
                        data.Add(inEv.ParameterA, inEv.PointA);
                    }
                }
                List<PxGeo.Curve> splitedCrv = crv.Split(data.Keys).ToList<PxGeo.Curve>();
                // If ther is intersection...
                if (splitedCrv.Count > 0)
                {
                    // Look for all inside parts of cure and move them to inside list.
                    for (int i = 0; i < splitedCrv.Count; i++)
                    {
                        PxGeo.Curve part_crv = splitedCrv[i];
                        PxGeo.Point3d testPt = part_crv.PointAtNormalizedLength(0.5);
                        foreach (PxGeo.Curve region in regions)
                        {
                            PxGeo.PointContainment result = region.Contains(testPt, PxGeo.Plane.WorldXY, 0.0001);
                            if (result == PxGeo.PointContainment.Inside)
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
                    foreach (PxGeo.Curve region in regions)
                    {
                        PxGeo.Point3d testPt = crv.PointAtNormalizedLength(0.5);
                        PxGeo.PointContainment result = region.Contains(testPt, PxGeo.Plane.WorldXY, 0.0001);
                        if (result == PxGeo.PointContainment.Inside)
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
            public static Tuple<List<PxGeo.Curve>, List<PxGeo.Curve>> TrimWithRegion(List<PxGeo.Curve> crv, PxGeo.Curve region)
            {
                List<PxGeo.Curve> inside = new List<PxGeo.Curve>();
                List<PxGeo.Curve> outside = new List<PxGeo.Curve>();
                foreach (PxGeo.Curve c in crv)
                {
                    Tuple<List<PxGeo.Curve>, List<PxGeo.Curve>> result = Geometry.TrimWithRegion(c, region);
                    inside.AddRange(result.Item1);
                    outside.AddRange(result.Item2);
                }
                return Tuple.Create(inside, outside);
            }

            // BUGERSONS!!!!
            public static Tuple<List<List<PxGeo.Curve>>, List<List<PxGeo.Curve>>> TrimWithRegions(List<PxGeo.Curve> crv, List<PxGeo.Curve> regions)
            {
                List<List<PxGeo.Curve>> inside = new List<List<PxGeo.Curve>>();
                List<List<PxGeo.Curve>> outside = new List<List<PxGeo.Curve>>();
                foreach (PxGeo.Curve region in regions)
                {
                    Tuple<List<PxGeo.Curve>, List<PxGeo.Curve>> result = Geometry.TrimWithRegion(crv, region);
                    inside.Add(result.Item1);
                    outside.Add(result.Item2);
                }
                return Tuple.Create(inside, outside);
            }

            /// <summary>
            /// Split Closed PxGeo.Curve by any other curve
            /// </summary>
            /// <param name="region">Closed curve (region) to split</param>
            /// <param name="splittingCurve">PxGeo.Curve for spliting</param>
            /// <returns>Splitted regions or null if no splitting occured or region is not closed curve.</returns>

            public static List<PxGeo.Curve> SplitRegion(PxGeo.Curve region, PxGeo.Curve splittingCurve)
            {
                List<double> region_t_params = new List<double>();
                List<double> splitter_t_params = new List<double>();
                List<PxGeoI.IntersectionEvent> intersection = PxGeoI.Intersection.CurveCurve(splittingCurve, region, Setups.IntersectionTolerance);
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
                foreach (PxGeoI.IntersectionEvent inter in intersection)
                {
                    splitter_t_params.Add(inter.ParameterA);
                    region_t_params.Add(inter.ParameterB);
                }
                splitter_t_params.Sort();
                region_t_params.Sort();
                PxGeo.Curve[] splited_splitter = splittingCurve.Split(splitter_t_params);
                List<PxGeo.Curve> sCurve = new List<PxGeo.Curve>();
                foreach (PxGeo.Curve crv in splited_splitter)
                {
                    PxGeo.Point3d testPt = crv.PointAtNormalizedLength(0.5);
                    PxGeo.PointContainment result = region.Contains(testPt, PxGeo.Plane.WorldXY, 0.0001);
                    if (result == PxGeo.PointContainment.Inside)
                    {
                        sCurve.Add(crv);
                    }
                }

                // If ther is only one splitter
                List<PxGeo.Curve> pCurve = new List<PxGeo.Curve>();
                if (sCurve.Count == 1 && region_t_params.Count == 2)
                {
                    List<PxGeo.Curve> splited_region = region.Split(region_t_params).ToList();
                    if (splited_region.Count == 2)
                    {
                        foreach (PxGeo.Curve out_segment in splited_region)
                        {
                            PxGeo.Curve[] temp = PxGeo.Curve.JoinCurves(new List<PxGeo.Curve>() { out_segment, sCurve[0] });
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

            public static List<PxGeo.Curve> SplitRegion(PxGeo.Curve region, List<PxGeo.Curve> splitters)
            {
                //List<PxGeo.PolylineCurve> out_regions = new List<PxGeo.PolylineCurve>();
                List<PxGeo.Curve> temp_regions = new List<PxGeo.Curve>();
                temp_regions.Add(region);

                foreach (PxGeo.Curve splitter in splitters)
                {
                    List<PxGeo.Curve> current_temp_regions = new List<PxGeo.Curve>();
                    foreach (PxGeo.Curve current_region in temp_regions)
                    {
                        List<PxGeo.Curve> choped_region = SplitRegion(current_region, splitter);
                        if (choped_region != null)
                        {
                            foreach (PxGeo.Curve _region in choped_region)
                            {
                                List<PxGeo.Curve> c_inter = PxGeo.Curve.CreateBooleanIntersection(_region, region);
                                foreach (PxGeo.Curve inter_curve in c_inter)
                                {
                                    current_temp_regions.Add(inter_curve);
                                }
                            }
                        }
                        else
                        {
                            if (region.Contains(current_region.CenterPoint(), PxGeo.Plane.WorldXY, Setups.GeneralTolerance) == PxGeo.PointContainment.Inside)
                            {
                                current_temp_regions.Add(current_region);
                            }
                        }
                    }
                    temp_regions = new List<PxGeo.Curve>(current_temp_regions);
                }
                return temp_regions;
            }

            //TODO: Dokonczyć reguralny voronoi i dodac jako stage 0 ciecia.
            /*
            public static List<PxGeo.PolylineCurve> RegularVoronoi(List<Cell> cells, PxGeo.Polyline blister,  double tolerance = 0.05)
            {
            Diagrams.Node2List n2l = new Diagrams.Node2List();
            List < Diagrams.Node2 > outline = new List<Diagrams.Node2>();
            foreach (Cell cell in cells)
            {
            n2l.Append(new Diagrams.Node2(cell.PillCenter.X, cell.PillCenter.Y));
            }

            foreach (PxGeo.Point3d pt in blister)
            {
            outline.Add(new Diagrams.Node2(pt.X, pt.Y));
            }
            List<Diagrams.Voronoi.Cell2> voronoi = Diagrams.Voronoi.Solver.Solve_BruteForce(n2l, outline);
            //Diagrams.Delaunay.Connectivity del_con = Diagrams.Delaunay.Solver.Solve_Connectivity(n2l, 0.0001, true);
            // List<Diagrams.Voronoi.Cell2> voronoi = Diagrams.Voronoi.Solver.Solve_Connectivity(n2l, del_con, outline);

            List<PxGeo.PolylineCurve> output = new List<PxGeo.PolylineCurve>(voronoi.Count);
            foreach (Diagrams.Voronoi.Cell2 cell in voronoi)
            {
            output.Add(cell.ToPolyline().ToPolylineCurve());
            }
            return output;
            }

            public static List<PxGeo.PolylineCurve> IrregularVoronoi(List<Cell> cells, PxGeo.Polyline blister, int resolution = 50, double tolerance = 0.05)
            {
            Diagrams.Node2List n2l = new Diagrams.Node2List();
            List<Diagrams.Node2> outline = new List<Diagrams.Node2>();
            foreach (Cell cell in cells)
            {
            PxGeo.Point3d[] pts;
            cell.pill.DivideByCount(resolution, false, out pts);
            foreach (PxGeo.Point3d pt in pts)
            {
            n2l.Append(new Diagrams.Node2(pt.X, pt.Y));
            }
            }

            foreach (PxGeo.Point3d pt in blister)
            {
            outline.Add(new Diagrams.Node2(pt.X, pt.Y));
            }

            Diagrams.Delaunay.Connectivity del_con = Diagrams.Delaunay.Solver.Solve_Connectivity(n2l, 0.0001, true);
            List < Diagrams.Voronoi.Cell2 > voronoi = Diagrams.Voronoi.Solver.Solve_Connectivity(n2l, del_con, outline);

            List<PxGeo.PolylineCurve> vCells = new List<PxGeo.PolylineCurve>();
            for (int i = 0; i < cells.Count; i++)
            {
            List<PxGeo.Point3d> pts = new List<PxGeo.Point3d>();
            for (int j = 0; j < resolution - 1; j++)
            {
            int glob_index = (i * (resolution - 1)) + j;
            // vor.Add(voronoi[glob_index].ToPolyline());
            if (voronoi[glob_index].C.Count == 0) continue;
            PxGeo.Point3d[] vert = voronoi[glob_index].ToPolyline().ToArray();
            foreach (PxGeo.Point3d pt in vert)
            {
            PointContainment result = cells[i].pill.Contains(pt, PxGeo.Plane.WorldXY, 0.0001);
            if (result == PointContainment.Outside)
            {
            pts.Add(pt);
            }
            }
            }

            // Circle fitCirc;
            // Circle.TryFitCircleToPoints(pts, out fitCirc);
            Circle fitCirc = Geometry.FitCircle(pts);
            PxGeo.Polyline poly = new PxGeo.Polyline(SortPtsAlongCurve(PxGeo.Point3d.CullDuplicates(pts, 0.0001), fitCirc.ToNurbsCurve()));
            poly.Add(poly[0]);
            poly.ReduceSegments(tolerance);
            vCells.Add(new PxGeo.PolylineCurve(poly));
            cells[i].voronoi = new PxGeo.PolylineCurve(poly);
            }
            return vCells;
            }
            */
            public static PxGeo.PolylineCurve AngleAlignedBoundingBox(PxGeo.Curve crv, PxGeo.Plane plane)
            {
                return AngleAlignedBoundingBox(crv, plane.XAxis);
            }

            public static PxGeo.PolylineCurve AngleAlignedBoundingBox(PxGeo.Curve crv, PxGeo.Vector3d guideVec)
            {
                double angle = PxGeo.Vector3d.VectorAngle(PxGeo.Vector3d.XAxis, guideVec);
                return AngleAlignedBoundingBox(crv, angle);
            }

            public static PxGeo.PolylineCurve AngleAlignedBoundingBox(PxGeo.Curve crv, double angle)
            {
                PxGeo.Point3d center = ((PxGeo.PolylineCurve)crv).ToPolyline().CenterPoint();
                PxGeo.Curve currentCurve = crv.DuplicateCurve();
                currentCurve.Rotate(angle, PxGeo.Vector3d.ZAxis, center);
                PxGeo.BoundingBox box = currentCurve.GetBoundingBox(false);
                PxGeo.Rectangle3d rect = new PxGeo.Rectangle3d(PxGeo.Plane.WorldXY, box.Min, box.Max);
                PxGeo.PolylineCurve r = rect.ToPolyline().ToPolylineCurve();
                r.Rotate(-angle, PxGeo.Vector3d.ZAxis, center);
                return r;
            }

            public static Tuple<PxGeo.PolylineCurve,double> MinimumAreaRectangleBF(PxGeo.Curve crv)
            {

                PxGeo.Point3d centre = ((PxGeo.PolylineCurve)crv).ToPolyline().CenterPoint();
                double minArea = double.MaxValue;
                PxGeo.PolylineCurve outCurve = null;
                double radius = 0;

                for (double i = 0; i < 180; i += 0.5)
                {
                    double radians = ExtraMath.ToRadians(i);
                    PxGeo.Curve currentCurve = crv.DuplicateCurve();
                    currentCurve.Rotate(radians, PxGeo.Vector3d.ZAxis, centre);
                    PxGeo.BoundingBox box = currentCurve.GetBoundingBox(false);
                    if (box.Area < minArea)
                    {
                        minArea = box.Area;
                        PxGeo.Rectangle3d rect = new PxGeo.Rectangle3d(PxGeo.Plane.WorldXY, box.Min, box.Max);
                        PxGeo.PolylineCurve r = rect.ToPolyline().ToPolylineCurve();
                        r.Rotate(-radians, PxGeo.Vector3d.ZAxis, centre);
                        outCurve = r;
                        radius = -radians;
                    }
                }
                // outCurve.ToPolyline()
                return Tuple.Create(outCurve, radius);
            }

            public static PxGeo.PolylineCurve PolylineThicken(PxGeo.PolylineCurve crv, double thickness)
            {

                List<PxGeo.Curve> Outline = new List<PxGeo.Curve>();
                PxGeo.Curve offser_1 = crv.Offset(PxGeo.Plane.WorldXY, thickness);
                if (offser_1 != null) Outline.Add(offser_1);
                else return null;
                PxGeo.Curve offser_2 = crv.Offset(PxGeo.Plane.WorldXY, -thickness);
                if (offser_2 != null) Outline.Add(offser_2);
                else return null;

                if (Outline.Count != 2) return null;
                Outline.Add(new PxGeo.LineCurve(Outline[0].PointAtStart, Outline[1].PointAtStart));
                Outline.Add(new PxGeo.LineCurve(Outline[0].PointAtEnd, Outline[1].PointAtEnd));
                PxGeo.Curve[] result = PxGeo.Curve.JoinCurves(Outline);
                if (result.Length != 1) return null;
                return (PxGeo.PolylineCurve)result[0];
            }

            /// <summary>
            /// Set Counter-Clockwise direction of curve and set it domain to 0.0 - 1.0
            /// </summary>
            /// <param name="crv"></param>
            public static void UnifyCurve(PxGeo.Curve crv)
            {
                PxGeo.CurveOrientation orient = crv.ClosedCurveOrientation(PxGeo.Vector3d.ZAxis);
                if (orient == PxGeo.CurveOrientation.Clockwise)
                {
                    crv.Reverse();
                }
                crv.Domain = new PxGeo.Interval(0.0, 1.0);
            }


            public const double EPSILON = 1.2e-12;
            /// <summary>
            /// "Reduces" a set of line segments by removing points that are too far away. Does not modify the input list; returns
            /// a new list with the points removed.
            /// The image says it better than I could ever describe: http://upload.wikimedia.org/wikipedia/commons/3/30/Douglas-Peucker_animated.gif
            /// The wiki article: http://en.wikipedia.org/wiki/Ramer%E2%80%93Douglas%E2%80%93Peucker_algorithm
            /// Based on:  http://www.codeproject.com/Articles/18936/A-Csharp-Implementation-of-Douglas-Peucker-PxGeo.Line-Ap
            /// </summary>
            /// <param name="pts">Points to reduce</param>
            /// <param name="error">Maximum distance of a point to a line. Low values (~2-4) work well for mouse/touchscreen data.</param>
            /// <returns>A new list containing only the points needed to approximate the curve.</returns>
            public static PxGeo.Polyline DouglasPeuckerReduce(PxGeo.Polyline pLine, double error)
            {

                List<PxGeo.Point3d> pts = pLine.ToList();
                if (pts == null) throw new ArgumentNullException("pts");
                pts = RemoveDuplicates(pts);
                if (pts.Count < 3)
                    return new PxGeo.Polyline(pts);
                List<int> keepIndex = new List<int>(Math.Max(pts.Count / 2, 16));
                keepIndex.Add(0);
                keepIndex.Add(pts.Count - 1);
                DouglasPeuckerRecursive(pts, error, 0, pts.Count - 1, keepIndex);
                keepIndex.Sort();
                //List<PxGeo.Vector3d> res = new List<PxGeo.Vector3d>(keepIndex.Count);
                // ReSharper disable once LoopCanBeConvertedToQuery
                PxGeo.Polyline outLine = new PxGeo.Polyline(keepIndex.Count);

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
            public static List<PxGeo.Point3d> RemoveDuplicates(List<PxGeo.Point3d> pts)
            {
                if (pts.Count < 2)
                    return pts;

                // Common case -- no duplicates, so just return the source list
                PxGeo.Point3d prev = pts[0];
                int len = pts.Count;
                int nDup = 0;
                for (int i = 1; i < len; i++)
                {
                    PxGeo.Point3d cur = pts[i];
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
                    List<PxGeo.Point3d> dst = new List<PxGeo.Point3d>(len - nDup);
                    prev = pts[0];
                    dst.Add(prev);
                    for (int i = 1; i < len; i++)
                    {
                        PxGeo.Point3d cur = pts[i];
                        if (!EqualsOrClose(prev, cur))
                        {
                            dst.Add(cur);
                            prev = cur;
                        }
                    }
                    return dst;
                }
            }

            public static bool EqualsOrClose(PxGeo.Point3d v1, PxGeo.Point3d v2)
            {
                return v1.DistanceToSquared(v2) < EPSILON;
            }

            private static void DouglasPeuckerRecursive(List<PxGeo.Point3d> pts, double error, int first, int last, List<int> keepIndex)
            {
                int nPts = last - first + 1;
                if (nPts < 3)
                    return;

                PxGeo.Point3d a = pts[first];
                PxGeo.Point3d b = pts[last];
                double abDist = a.DistanceTo(b);
                double aCrossB = a.X * b.Y - b.X * a.Y;
                double maxDist = error;
                int split = 0;
                for (int i = first + 1; i < last - 1; i++)
                {
                    PxGeo.Point3d p = pts[i];
                    PxGeo.Line ab = new PxGeo.Line(a, b);
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
        }

        static class Setups
        {

            public static double PixelSpacing = GetEnvironmentVariableWithDefault("PIXEL_SPACING", 1.0);
            public static Vector3d ZeroPosition = new Vector3d(GetEnvironmentVariableWithDefault("ZERO_POSITION_X", 0), GetEnvironmentVariableWithDefault("ZERO_POSITION_Y", 0), 0);

            #region GENERAL TOLERANCES
            public static double MaxBlisterPossitionDeviation = GetEnvironmentVariableWithDefault("BLISTER_POSITION_MAX_DEVIATION", 1.1);
            public static double GeneralTolerance = GetEnvironmentVariableWithDefault("TOLERANCE_GENERAL", 1e-5);

            //public static readonly double CurveDistanceTolerance = 0.05;  // Curve tO polyline distance tolerance.
            public static double IntersectionTolerance = GetEnvironmentVariableWithDefault("TOLERANCE_INTERSECTION", 1e-5);
            public static double ColinearTolerance = GetEnvironmentVariableWithDefault("TOLERANCE_COLINEAR", 1e-3);
            #endregion

            #region BLADE STUFF

            public static double BladeLength = GetEnvironmentVariableWithDefault("BLADE_CUT_LENGTH", 44.0);
            public static double BladeTol = GetEnvironmentVariableWithDefault("BLADE_CUT_TOLERANCE", 2.0);
            public static double BladeWidth = GetEnvironmentVariableWithDefault("BLADE_CUT_WIDTH", 3.0);
            public static Vector3d BladeGlobal = new Vector3d(GetEnvironmentVariableWithDefault("BLADE_GLOBAL_X", 200), GetEnvironmentVariableWithDefault("BLADE_GLOBAL_Y", 199.0), 0);


            //Axis (Cartesian Global) to calculate angles.
            public static string BladeRotationAxis = GetEnvironmentVariableWithDefault("BLADE_ROTATION_AXIS", "X");
            // Knife cutting angles is calculated base od Global Cartesian X axis. Extra Rotation (in radians) if other angles are need.

            public static double BladeRotationCalibration = ExtraMath.ToRadians(GetEnvironmentVariableWithDefault("BLADE_EXTRA_ROTATION", 0));
            #endregion

            #region CARTESIAN/

            public static double CartesianPickModeAngle = ExtraMath.ToRadians(GetEnvironmentVariableWithDefault("CARTESIAN_PICK_MODE_ANGLE", 30));
            public static double JawWidth = GetEnvironmentVariableWithDefault("CARTESIAN_JAW_WIDTH", 5.5);
            public static double JawDepth = GetEnvironmentVariableWithDefault("CARTESIAN_JAW_DEPTH", 3.0);

            public static double BlisterCartesianDistance = GetEnvironmentVariableWithDefault("CARTESIAN_SAFE_DISTANCE_TO_BLISTER", 3.5);
            public static double CartesianMaxWidth = GetEnvironmentVariableWithDefault("CARTESIAN_JAWS_MAX_RANGE", 85.0);
            public static double CartesianMinWidth = GetEnvironmentVariableWithDefault("CARTESIAN_JAWS_MIN_RANGE", 10.0);
            public static double CartesianJawYLimit = GetEnvironmentVariableWithDefault("CARTESIAN_JAW_Y_LIMIT", 15.0);


            public static Vector3d CartesianPivotJawVector = new Vector3d(GetEnvironmentVariableWithDefault("CARTESIAN_PIVOT_JAW_X", 112.4), GetEnvironmentVariableWithDefault("CARTESIAN_PIVOT_JAW_Y", 19.5), 0);
            #endregion

            //OTHER
            public static string BlisterGlobalSystem = GetEnvironmentVariableWithDefault("BLISTER_GLOBAL_System", "PICK");
            public static Vector3d BlisterGlobal = new Vector3d(GetEnvironmentVariableWithDefault("BLISTER_GLOBAL_X", 108.1), GetEnvironmentVariableWithDefault("BLISTER_GLOBAL_Y", 411.5), 0);
            public static Vector3d BlisterGlobalPick = new Vector3d(GetEnvironmentVariableWithDefault("BLISTER_GLOBAL_PICK_X", 113), GetEnvironmentVariableWithDefault("BLISTER_GLOBAL_PICK_Y", 358), 0);

            public static double IsoRadius = GetEnvironmentVariableWithDefault("RAY_LENGTH", 2000.0);
            public static double MinimumCutOutSize = GetEnvironmentVariableWithDefault("CUTOUT_MIN_SIZE", 25.0);

            // SIMPLIFY PATH TOLERANCES
            public static double CurveReduceTolerance = GetEnvironmentVariableWithDefault("TOLERANCE_CURVE_REDUCTION", 2.0);
            public static double CurveSmoothTolerance = GetEnvironmentVariableWithDefault("TOLERANCE_CURVE_SMOOTH", 1.0);
            public static double AngleTolerance = GetEnvironmentVariableWithDefault("SIMPLIFY_ANGLE", 0.1 * Math.PI);
            // if path segment is shorter then this, it will be collapsed
            public static double CollapseTolerance = GetEnvironmentVariableWithDefault("SIMPLIFY_COLLAPSE_DISTANCE", 1.0);

            public static double SnapDistance = GetEnvironmentVariableWithDefault("SIMPLIFY_TOLERANCE_SNAP_DISTANCE", 1.0);
            public static bool TrimBlisterToXAxis = GetEnvironmentVariableWithDefault("TRIM_BLISTER_X_AXIS", true);


            private static T GetEnvironmentVariableWithDefault<T>(string variable, T defaultValue)
            {
                string value = Environment.GetEnvironmentVariable(variable);
                if (value != null) return TryParse<T>(value);
                else return defaultValue;
            }

            public static T TryParse<T>(string inValue)
            {
                TypeConverter converter =
                  TypeDescriptor.GetConverter(typeof(T));

                return (T)converter.ConvertFromString(null,
                CultureInfo.InvariantCulture, inValue);
            }
        }

        // </Custom additional code>
    
    }
}
