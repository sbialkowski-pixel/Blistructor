using System;
using System.Collections.Generic;
using System.Linq;

#if PIXEL
using Pixel.Rhino;
using Pixel.Rhino.Geometry;
using Pixel.Rhino.Geometry.Intersect;
#else
using Rhino;
using Rhino.Geometry;
#endif

#if DEBUG
using Pixel.Rhino.FileIO;
using Pixel.Rhino.DocObjects;
#endif
using log4net;
using Newtonsoft.Json.Linq;

namespace Blistructor
{
    public class Grasper
    {
        private static readonly ILog log = LogManager.GetLogger("Cutter.Grasper");

        // TODO: Adding Setups.CartesianJawYLimit as a limit for JAW_1 (Or 2)

        #region CONSTRUCTORS
        /// <summary>
        /// Base constructor for Grasper.
        /// </summary>
        /// <param name="blisterQueue">List of Blister to cut. Must have only one element!</param>
        public Grasper(List<Blister> blisterQueue)
        {
            if (blisterQueue.Count != 1) throw new Exception($"On the begining, BlisterQueue should have only ONE blister, not {blisterQueue.Count}!");
            BlisterQueue = blisterQueue;

            //_workspace = workspace;
            JawsPossibleLocation = new List<LineCurve>();

            PolylineCurve mainOutline = BlisterQueue[0].Outline;

            // Generate BBoxes
            BoundingBox blisterBB = mainOutline.GetBoundingBox(false);
            Rectangle3d rect = new Rectangle3d(Plane.WorldXY, blisterBB.Min, blisterBB.Max);
            MABBox = Geometry.MinimumAreaRectangleBF(mainOutline).ToPolyline().ToPolylineCurve();
            Geometry.UnifyCurve(MABBox);
            AABBox = rect.ToPolyline().ToPolylineCurve();
            Geometry.UnifyCurve(AABBox);

            // Find lowest mid point on Blister AA Bounding Box
            List<Line> aaSegments = new List<Line>(AABBox.ToPolyline().GetSegments());
            LineCurve guideLine = new LineCurve(aaSegments.OrderBy(line => line.PointAt(0.5).Y).ToList()[0]);
            // Create line at Y = 1 mm
            double constructionDistance = 1;
            guideLine.SetStartPoint(new Point3d(guideLine.PointAtStart.X, constructionDistance, 0));
            guideLine.SetEndPoint(new Point3d(guideLine.PointAtEnd.X, constructionDistance, 0));

            // Find where GUideLine intersect with BlisterOutline on max Grasper Level
            Polyline guideLineAsPline = new Polyline(2) { guideLine.PointAtStart, guideLine.PointAtEnd };

            List<IntersectionEvent> LimitedGuideLine = Intersection.PolyLinePolyLine(mainOutline.ToPolyline(), guideLineAsPline, Setups.IntersectionTolerance);

            List<Point3d> predLinePoints = new List<Point3d>(2);
            //LineCurve fullPredLine = new LineCurve(;
            if (LimitedGuideLine.Count == 2)
            {
                foreach (IntersectionEvent iEvent in LimitedGuideLine)
                {
                    if (iEvent.IsPoint) predLinePoints.Add(iEvent.PointB);
                    else throw new AnchorException("Cannot find guiding line for Anchors");
                }
            }
            else
            {
                throw new AnchorException("Cannot find guiding line for Anchors");
            }
            predLinePoints = predLinePoints.OrderBy(pt => pt.X).ToList();
            // Place predLine in final possition.
            predLinePoints = predLinePoints.Select(p => { p.Y = Setups.JawDepth; return p; }).ToList();

            LineCurve fullPredLine = new LineCurve(predLinePoints[0], predLinePoints[1]);

            //Add limit fullPredLine to CartesianMaxWidth+CartesianJawYLimit as a max possible location for any Grasper
            double maxCartesianDistanceX = Math.Min(fullPredLine.PointAtEnd.X, (Setups.CartesianMaxWidth + Setups.CartesianJawYLimit));
            fullPredLine.SetEndPoint(new Point3d(maxCartesianDistanceX, Setups.JawDepth, 0));

            // NOTE: Check intersection with Pills (Or maybe with pillsOffset. Rethink problem)
            (List<Curve> inside, List<Curve> outside) = Geometry.TrimWithRegions(fullPredLine, BlisterQueue[0].GetPillsOutline(Setups.JawPillSafeDistance));
            // Gather all parts outside (not in Pills) shrink curve on both sides by half of Grasper width 
            foreach (Curve crv in outside)
            {
                // Shrink pieces on both sides by half of Grasper width.
                Line ln = ((LineCurve)crv).Line;
                if (ln.Length < Setups.JawWidth) continue;
                ln.Extend(-Setups.JawWidth / 2, -Setups.JawWidth / 2);
                LineCurve cln = new LineCurve(ln);
                JawsPossibleLocation.Add(cln);
            }
            //GuessJawPossiblityOnPill();
            Jaws = FindJawPoints();
            log.Info(String.Format("Anchors found: {0}", Jaws.Count));
        }

        /// <summary>
        /// Copy constructor
        /// </summary>
        /// <param name="baseGrasper"></param>
        protected Grasper(Grasper baseGrasper)
        {
            BlisterQueue = baseGrasper.BlisterQueue;
            AABBox = new PolylineCurve(baseGrasper.AABBox);
            MABBox = new PolylineCurve(baseGrasper.MABBox);
            JawsPossibleLocation = baseGrasper.JawsPossibleLocation.Select(line => new LineCurve(line)).ToList();
            Jaws = baseGrasper.Jaws.Select(jaw => jaw).ToList();
        }
        #endregion

        #region PROPERTIES
        private List<Blister> BlisterQueue { get; set; }

        /// <summary>
        /// Initial BLister Axis Aligned Bounding Box
        /// </summary>
        public PolylineCurve AABBox { get; private set; }
        /// <summary>
        /// Initial BLister Minimum Area Bounding Box
        /// </summary>
        public PolylineCurve MABBox { get; private set; }

        /// <summary>
        /// Lines describing possible Jaws location
        /// </summary>
        public List<LineCurve> JawsPossibleLocation { get; private set; }

        //public JawPoint Jaw1 { get; private set; }

        //public JawPoint Jaw2 { get; private set; }

        public List<JawPoint> Jaws { get; private set; }
        #endregion

        public List<Curve> GetCartesianAsObstacle()
        {
            return new List<Curve> { CreateCartesianLimitLine() };
        }

        public static LineCurve CreateCartesianOperationLine()
        {
            return new LineCurve(new Line(new Point3d(-Setups.IsoRadius, Setups.JawDepth, 0), Vector3d.XAxis, 2 * Setups.IsoRadius));
        }

        public static LineCurve CreateCartesianLimitLine()
        {
            return new LineCurve(new Line(new Point3d(-Setups.IsoRadius, -Setups.BlisterCartesianDistance, 0), Vector3d.XAxis, 2 * Setups.IsoRadius));
        }

        public List<Interval> GetJawPossibleIntervals()
        {
            return ConvertLinesToIntervals(JawsPossibleLocation);
        }

        #region CONVERTERS INTERVAL<->LINE

        public static List<Interval> ConvertLinesToIntervals(List<LineCurve> lines)
        {
            List<Interval> intervals = lines.Select(line => new Interval(line.PointAtStart.X, line.PointAtEnd.X)).ToList();
            intervals = intervals.Select(spacing => { spacing.MakeIncreasing(); return spacing; }).ToList();
            intervals = intervals.OrderBy(spacing => spacing.T0).ToList();
            return intervals;
        }

        /// <summary>
        /// Convert Intervals to LineCurve. Y is set to Setups.JawDepth
        /// </summary>
        /// <param name="intervals"></param>
        /// <returns></returns>
        public static List<LineCurve> ConvertIntervalsToLines(List<Interval> intervals)
        {
            List<LineCurve> lines = new List<LineCurve>(intervals.Count);
            foreach (Interval interval in intervals)
            {
                Line line = new Line(new Point3d(interval.T0, Setups.JawDepth, 0), new Point3d(interval.T1, Setups.JawDepth, 0));
                lines.Add(new LineCurve(line));
            }
            return lines;
        }

        #endregion

        #region RESTRICTION METHODS

        /// <summary>
        /// Get restricted area, based on actual jaws location as Polyline (rectangle)
        /// To update Jaws location use UpdateJawPoints before using this method.
        /// </summary>
        /// <param name="additionalSafeDistance">Additional safe distance</param>
        /// <returns>List of Rectangles where jaws can occur</returns>
        public List<Polyline> GetRestrictedAreas(double additionalSafeDistance = 1.0)
        {
            return GetRestrictedAreas(Jaws, additionalSafeDistance);
        }

        /// <summary>
        /// Get restricted area, based on jaws location as Polyline (rectangle)
        /// </summary>
        /// <param name="jaws">List of JawPoint</param>
        /// <param name="additionalSafeDistance">Additional safe distance</param>
        /// <returns>List of Polylines (rectangle) where jaws can occur</returns>
        public static List<Polyline> GetRestrictedAreas(List<JawPoint> jaws, double additionalSafeDistance = 1.0)
        {
            //List<JawPoint> a_points = FindJawPoints();
            List<Polyline> restrictedAreas = new List<Polyline>(2);
            foreach (JawPoint jaw in jaws)
            {
                Point3d origin = new Point3d(jaw.Location.X, 0, 0);
                Plane grasperPlane = new Plane(origin, Vector3d.XAxis, Vector3d.YAxis);
                Interval jawWidth = new Interval(t0: -(additionalSafeDistance + (Setups.JawWidth / 2)), t1: additionalSafeDistance + Setups.JawWidth / 2);
                Interval jawHeight = new Interval(t0: -(additionalSafeDistance + Setups.BlisterCartesianDistance), t1: additionalSafeDistance + Setups.JawDepth);
                Rectangle3d area = new Rectangle3d(grasperPlane, jawWidth, jawHeight);
                restrictedAreas.Add(area.ToPolyline());
            }
            return restrictedAreas;
        }

        /// <summary>
        /// Get restricted area, based on actual jaws location as Interval
        /// To update Jaws location use UpdateJawPoints before using this method.
        /// </summary>
        /// <param name="additionalSafeDistance">Additional safe distance</param>
        /// <returns>>List of Intervals where jaws cannot occurs</returns>
        public List<Interval> GetRestrictedIntervals(double additionalSafeDistance = 1.0)
        {
            return GetRestrictedIntervals(Jaws, additionalSafeDistance);
        }

        /// <summary>
        /// Get restricted area, based on jaws location as Polyline (rectangle)
        /// </summary>
        /// <param name="jaws">List of JawPoint</param>
        /// <param name="additionalSafeDistance">Additional safe distance</param>
        /// <returns>List of Interval where jaws can occur</returns>
        public static List<Interval> GetRestrictedIntervals(List<JawPoint> jaws, double additionalSafeDistance = 1.0)
        {
            List<Polyline> restrictedAreas = GetRestrictedAreas(jaws, additionalSafeDistance);
            List<Interval> restrictedIntervals = new List<Interval>(restrictedAreas.Count);
            foreach (Polyline restrictedArea in restrictedAreas)
            {
                Interval restrictedInterval = new Interval(restrictedArea.BoundingBox.Min.X,
                    restrictedArea.BoundingBox.Max.X);
                restrictedInterval.MakeIncreasing();
                restrictedIntervals.Add(restrictedInterval);
            }
            return restrictedIntervals;
        }

        /// <summary>
        /// Based on CutData compute cut impact area intervals.
        /// </summary>
        /// <param name="cutData">Base data to compute impact area</param>
        /// <param name="applyJawBoundaries">If True, area will be expanded by Setups.JawDepth/2 in both directions</param>
        /// <param name="additionalSafeDistance">Additional safe distance</param>
        /// <returns>List of restricted intervals</returns>
        public static List<Interval> ComputCutImpactInterval(CutData cutData, bool applyJawBoundaries = true, double additionalSafeDistance = 1.0)
        {
            List<BoundingBox> restrictedAreas = ComputeCutImpactAreas(cutData, applyJawBoundaries, additionalSafeDistance);
            List<Interval> restrictedIntervals = new List<Interval>(restrictedAreas.Count);
            foreach (BoundingBox restrictedArea in restrictedAreas)
            {
                Interval restrictedInterval = new Interval(restrictedArea.Min.X,
                    restrictedArea.Max.X);
                restrictedInterval.MakeIncreasing();
                restrictedIntervals.Add(restrictedInterval);
            }
            return restrictedIntervals;
        }

        /// <summary>
        /// Based on CutData compute cut impact area as BBox.
        /// </summary>
        /// <param name="cutData">Base data to compute impact area</param>
        /// <param name="applyJawBoundaries">>If True, area will be expanded by Setups.JawDepth/2 in both directions</param>
        /// <param name="additionalSafeDistance">Additional safe distance</param>
        /// <returns>List of restricted areas as BBox</returns>
        public static List<BoundingBox> ComputeCutImpactAreas(CutData cutData, bool applyJawBoundaries = true, double additionalSafeDistance = 1.0)
        {
            // TODO: Add Boundary on each side of BBox to not allow jaw anter this restrictedArea.
            // Thicken paths from cutting data and check how this influence 
            List<BoundingBox> allRestrictedArea = new List<BoundingBox>(cutData.Segments.Count);

            LineCurve cartesianLimitLine = CreateCartesianLimitLine();
            //Create upperLine - max distance where Jaw can operate
            LineCurve upperCartesianLimitLine = CreateCartesianOperationLine();

            foreach (PolylineCurve ply in cutData.Segments)
            {
                //Check if knife segment intersect with Upper line = knife-jaw collision can occur
                List<IntersectionEvent> checkIntersect = Intersection.CurveCurve(upperCartesianLimitLine, ply, Setups.IntersectionTolerance);

                // If intersection occurs, any
                if (checkIntersect.Count > 0)
                {

                    PolylineCurve extPly = (PolylineCurve)ply.Extend(CurveEnd.Both, 100);

                    // create knife "impact area"
                    PolylineCurve knifeFootprint = Geometry.PolylineThicken(extPly, Setups.BladeWidth / 2);

                    // TODO: Check if this condition is ok.
                    if (knifeFootprint == null) continue;

                    PolylineCurve region = new PolylineCurve(new List<Point3d> { upperCartesianLimitLine.PointAtStart, upperCartesianLimitLine.PointAtEnd, cartesianLimitLine.PointAtEnd, cartesianLimitLine.PointAtStart, upperCartesianLimitLine.PointAtStart });

                    List<Curve> intersect = Curve.CreateBooleanIntersection(region, knifeFootprint);
                    BoundingBox grasperRestrictedAreaBBox = intersect[0].GetBoundingBox(false);
                    if (applyJawBoundaries)
                    {
                        Point3d newMin = new Point3d(grasperRestrictedAreaBBox.Min);
                        newMin.X -= (Setups.JawWidth / 2 + additionalSafeDistance);
                        grasperRestrictedAreaBBox.Min = newMin;
                        Point3d newMax = new Point3d(grasperRestrictedAreaBBox.Max);
                        newMax.X += (Setups.JawWidth / 2 + additionalSafeDistance);
                        grasperRestrictedAreaBBox.Max = newMax;
                    }
                    allRestrictedArea.Add(grasperRestrictedAreaBBox);
                }
            }
            return allRestrictedArea;
        }

        /// <summary>
        ///  Based on CutData compute total cut impact area intervals.
        /// </summary>
        /// <param name="cutData">Base data to compute impact area</param>
        /// <returns></returns>
        public static Interval ComputeTotalCutImpactInterval(CutData cutData, List<Interval> cutImpactIntervals)
        {

            // Cut paths does not interact with Grasper
            if (cutImpactIntervals.Count == 0) return Interval.Unset;
            // Only one cut Path interact, that means this is side pill. I need to find other side, to remove jawPossibleLocation inside this cutData.  
            if (cutImpactIntervals.Count == 1)
            {
                LineCurve limitLine = CreateCartesianOperationLine();
                Curve offset = cutData.Polygon.Offset(Plane.WorldXY, Setups.BladeWidth / 2);
                if (offset == null)
                {
                    offset = cutData.Polygon;
                    log.Warn("Offset failed during ComputeTotalCutImpactInterval. Using pure Polygon.");
                }
                (List<Curve> inside, List<Curve> _) = Geometry.TrimWithRegion(limitLine, offset);
                List<LineCurve> common = inside.Select(crv => (LineCurve)crv).ToList();

                List<Interval> impactIntervals = ConvertLinesToIntervals(common);
                impactIntervals.AddRange(cutImpactIntervals);
                Interval restrictedArea = IntervalsInterval(impactIntervals);
                return restrictedArea;
            }
            else
            {
                return IntervalsInterval(cutImpactIntervals);
            }
        }

        /// <summary>
        /// Based on CutData compute total cut impact area intervals.
        /// </summary>
        /// <param name="cutData">Base data to compute impact area</param>
        /// <param name="applyJawBoundaries">If True, area will be expanded by Setups.JawDepth/2 in both directions</param>
        /// <param name="additionalSafeDistance">Additional safe distance</param>
        /// <returns>Interval where cut has impact.</returns>
        public static Interval ComputeTotalCutImpactInterval(CutData cutData, bool applyJawBoundaries = true, double additionalSafeDistance = 1.0)
        {
            List<Interval> cutImpactIntervals = ComputCutImpactInterval(cutData, applyJawBoundaries, additionalSafeDistance);
            return ComputeTotalCutImpactInterval(cutData, cutImpactIntervals);
        }

        #endregion

        #region COLLISION

        /// <summary>
        /// Check collision between given cutData and current Jaws position.
        /// </summary>
        /// <param name="cutData">CutData to evaluate</param>
        /// <param name="updateJaws">Force Jaws position update (like calling UpdateJawsPoints()) </param>
        /// <returns>True, if there is collision between JAw and Knife</returns>
        public bool IsColliding(CutData cutData, bool updateJaws = true)
        {
            if (updateJaws) UpdateJawsPoints();
            // Get Jaw restricted are with additional safe dictance.
            List<Interval> currentJawsInterval = GetRestrictedIntervals(Setups.JawKnifeAdditionalSafeDistance - 1e-4);
            // Get just pure knife impact area.
            List<Interval> cutImpactIntervals = ComputCutImpactInterval(cutData, applyJawBoundaries: false, additionalSafeDistance: 0.0);
            return CollisionCheck(currentJawsInterval, cutImpactIntervals);
        }

        /// <summary>
        /// Check collision between Intervals
        /// </summary>
        /// <param name="currentJawsInterval">Intervals where Jaws are placed. This is restriction areas</param>
        /// <param name="cutImpactIntervals">Intervals where knife hase impact </param>
        /// <remarks> Remember to correctly generate both interval. There is possibility, that each was geenrated with safeDistance or with JawBorders, si they will oferlap, even if cut is correct.</remarks>
        /// <returns>True if any interval overlaps</returns>
        public static bool CollisionCheck(List<Interval> currentJawsInterval, List<Interval> cutImpactIntervals)
        {
            foreach (Interval currentInterval in currentJawsInterval)
            {
                List<Interval> commonInterval = CommonIntervals(cutImpactIntervals, currentInterval);
                if (commonInterval.Count > 0) return true;
            }
            return false;
        }

        #endregion

        #region HasPlaceForJaw/JawsContainingTest
        /// <summary>
        /// Check if blister has place for Jaw additionaly in context of cutting data.
        /// Aim of this funtion is to check, if remaining part of JawPossibleLoction has any chance to be on blister if cut will be applied. 
        /// </summary>
        /// <param name="blister">Blister to check</param>
        /// <param name="cutData">cutData to validate aginst to</param>
        /// <returns></returns>
        public bool HasPlaceForJawInCutContext(Blister blister, CutData cutData)
        {
            return false;
        }

        /// <summary>
        /// Check if region (closed PolylineCurve) contains any part of jawsPossibleLocation.
        /// </summary>
        /// <param name="jawsPossibleLocation">List of jawsPossibleLocation as LineCurves</param>
        /// <param name="regionToEvaluate">Region to evaluate</param>
        /// <returns>True if any part of jawsPossibleLocation will be inside regionToEvaluate</returns>
        public static bool HasPlaceForJaw(List<LineCurve> jawsPossibleLocation, PolylineCurve regionToEvaluate)
        {
            foreach (LineCurve ln in jawsPossibleLocation)
            {
                (List<Curve> inside, List<Curve> _) = Geometry.TrimWithRegion(ln, regionToEvaluate);
                if (inside.Count == 0) continue;
                else
                {
                    //Very simple statment. Not sure if will be working.
                    //if (trim.Item1.Any(crv => crv.GetLength() >= Setups.JawWidth / 2)) 
                    return true;
                }
            }
            return false;
        }

        public bool HasPlaceForJaw(PolylineCurve regionToEvaluate)
        {
            return HasPlaceForJaw(JawsPossibleLocation, regionToEvaluate);
        }

        public bool HasBlisterPlaceForJaw(Blister blister)
        {
            return HasPlaceForJaw(blister.Outline);
        }

        //public bool HasPillPlaceForJaw(Pill pill)
        //{
        //    return HasPlaceForJaw(pill.IrVoronoi);
        //}

        public bool HasPlaceForJawInCutContext(CutData cutData)
        {
            //List<Interval> futureJawPosibleIntervals = Grasper.ApplyCutOnGrasperLocation(GetJawPossibleIntervals(), cutData);
            // This cut is not influancing grasper.

            List<Interval> cutImpactIntervals = ComputCutImpactInterval(cutData);
            List<Interval> grasperIntervals = GetJawPossibleIntervals();
            //List<Interval> remainingGraspersLocation = new List<Interval>(grasperIntervals.Count);

            foreach (Interval cutImpact in cutImpactIntervals)
            {
                List<Interval> remainingGraspersLocation = new List<Interval>(grasperIntervals.Count);

                foreach (Interval currentGraspersLocation in grasperIntervals)
                {
                    remainingGraspersLocation.AddRange(Interval.FromSubstraction(currentGraspersLocation, cutImpact).Where(interval => interval.Length > 0).ToList());
                }
                grasperIntervals = remainingGraspersLocation;
            }
            grasperIntervals = grasperIntervals.Select(spacing => { spacing.MakeIncreasing(); return spacing; }).ToList();
            grasperIntervals.OrderBy(spacing => spacing.T0).ToList();


            if (grasperIntervals == null) return false;

            List<LineCurve> futureJawPosibleLocation = Grasper.ConvertIntervalsToLines(grasperIntervals);
            if (!Grasper.HasPlaceForJaw(futureJawPosibleLocation, cutData.Polygon))
            {
                log.Warn("This cut failed: Current cut has no place for Jaw.");
                return false;
            }
            return true;
        }


        /// <summary>
        /// Check if any Jaw is associated with this cutData.
        /// As check area, Polygon is used.
        /// </summary>
        /// <param name="cutData">CutData to check</param>
        /// <returns>True if any Jaw is associeted with this cutData.Polygon</returns>
        public bool ContainsJaw(CutData cutData)
        {
            if (ContainsJaws(cutData).Count > 0) return true;
            else return false;

        }

        /// <summary>
        /// Get Jaw associated with this cutData.
        /// </summary>
        /// <param name="cutData">CutData to check</param>
        /// <returns>List of JawPoints inside cutData</returns>
        public List<JawPoint> ContainsJaws(CutData cutData)
        {
            return ContainsJaws(cutData.Polygon);
        }

        /// <summary>
        /// Get Jaw associated with this cutData.
        /// </summary>
        /// <param name="cutData">CutData to check</param>
        /// <returns>List of JawPoints inside cutData</returns>
        public List<JawPoint> ContainsJaws(CutBlister blister)
        {
            return ContainsJaws(blister.Outline);
        }

        public List<JawPoint> ContainsJaws(PolylineCurve polyline)
        {
            List<JawPoint> output = new List<JawPoint>(2);
            foreach (JawPoint pt in Jaws)
            {
                PointContainment pointContainment = polyline.Contains(pt.Location, Plane.WorldXY, Setups.IntersectionTolerance);
                if (PointContainment.Inside == pointContainment) output.Add(pt);
            }
            return output;
        }

        #endregion

        #region GRASPER LOCATION - FINAL
        /// <summary>
        /// Based on JawsPossibleLocation find JawPoints
        /// </summary>
        /// <returns>List of JawPoint</returns>
        public List<JawPoint> FindJawPoints()
        {
            List<Interval> grasperPossibleLocation = GetJawPossibleIntervals();
            return FindJawPoints(grasperPossibleLocation);
        }

        /// <summary>
        /// Find best Jaw points for a given list of Intervals describing grasperPossibleLocation
        /// </summary>
        /// <param name="grasperPossibleLocation">List of Intervals describing grasperPossibleLocation where Jaws can be placed</param>
        /// <returns>List of JawPoint</returns>
        public static List<JawPoint> FindJawPoints(List<Interval> grasperPossibleLocation)
        {
            // List<AnchorPoint> outputGrasperPoints = new List<AnchorPoint>();
            // List<Interval> grasperPossibleLocation = GetJawPossibleIntervals();

            //Get Extreme Points and create Spectrum line
            List<double> allStart = grasperPossibleLocation.OrderBy(spacing => spacing.T0).Select(spacing => spacing.T0).ToList();
            List<double> allEnd = grasperPossibleLocation.OrderBy(line => line.T0).Select(spacing => spacing.T1).ToList();

            double extremeLeft = allStart.First();
            double extremeRight = allEnd.Last();

            Interval spectrumLine = new Interval(extremeLeft, extremeRight);

            //If spectrum smaller then CartesianMaxWidth, and bigger then CartesianMinWidth, just give me spectrumLine as Locations.
            if (spectrumLine.Length < Setups.CartesianMaxWidth && spectrumLine.Length > Setups.CartesianMinWidth) return ConvertIntervalToJawPoints(spectrumLine);
            //If spectrum smaller then CartesianMinWidth, return empty list
            else if (spectrumLine.Length < Setups.CartesianMinWidth) return new List<JawPoint>();
            else
            {
                //First, Check the easiest case, try just in the spectrum middle...
                double globalMid = spectrumLine.ParameterAt(0.5);
                Interval estimatedGraspers = TryGetGraspersPoints(grasperPossibleLocation, globalMid);
                if (estimatedGraspers.IsValid)
                {
                    double score = EvaluateGraspers(estimatedGraspers, spectrumLine);
                    // If result is in the middle or very close to it, return it and we are done here...
                    if (score > 0.95)
                    {
                        return ConvertIntervalToJawPoints(estimatedGraspers);
                    }
                }
                // If not working, just look further for best anchor location
                // Try to get all single and unique pairs of Grapsers Possible Location combination and get best one (highest score)
                List<List<Interval>> GrasperPossibleLocationCombination = Combinators.Combinators.UniqueCombinations<Interval>(grasperPossibleLocation, 1, 2);
                // score,location
                List<Tuple<double, Interval>> result = new List<Tuple<double, Interval>>();
                foreach (List<Interval> GrasperPair in GrasperPossibleLocationCombination)
                {
                    // If one GrasperPossibleLocation only
                    if (GrasperPair.Count == 1 && GrasperPair[0].Length > Setups.CartesianMinWidth)
                    {
                        Interval temp = FindGraspersSpacing(GrasperPair[0], globalMid);
                        if (temp.IsValid)
                        {
                            double score = EvaluateGraspers(temp, spectrumLine);
                            result.Add(Tuple.Create(score, temp));
                        }

                    }
                    // If pair of GrasperPossibleLocations
                    else if (GrasperPair.Count == 2)
                    {
                        List<Interval> reorderedGrasperPair = GrasperPair.OrderBy(line => line.T0).ToList();
                        Interval temp = FindGraspersSpacing(reorderedGrasperPair, globalMid);
                        if (temp.IsValid)
                        {
                            double score = EvaluateGraspers(temp, spectrumLine);
                            result.Add(Tuple.Create(score, temp));
                        }
                    }
                }
                if (result.Count > 0)
                {
                    // Best First..
                    result = result.OrderBy(data => data.Item1).Reverse().ToList();
                    return ConvertIntervalToJawPoints(result[0].Item2);
                }
                return new List<JawPoint>();
            }
        }

        /// <summary>
        /// Convert interval into JawPoints.
        /// </summary>
        /// <param name="jawInterval">Interval representing Jaw1 and Jaw2</param>
        /// <returns>List of JawPoints</returns>
        private static List<JawPoint> ConvertIntervalToJawPoints(Interval jawInterval)
        {
            return new List<JawPoint>() {
                        new JawPoint(new Point3d(jawInterval.T0,Setups.JawDepth,0), JawSite.JAW_2),
                        new JawPoint(new Point3d(jawInterval.T1,Setups.JawDepth,0), JawSite.JAW_1)
                    };
        }

        /// <summary>
        ///  Look for best Graspers Location for one possibleGrasperLocation depend on spectrum Centre
        /// </summary>
        /// <param name="possibleGrasperLocation"></param>
        /// <param name="spectrumCenter">spectrum Centre</param>
        /// <returns></returns>
        private static Interval FindGraspersSpacing(Interval possibleGrasperLocation, double spectrumCenter)
        {
            possibleGrasperLocation.MakeIncreasing();
            Interval graspersSpacing = new Interval(spectrumCenter - (Setups.CartesianMaxWidth / 2), spectrumCenter + (Setups.CartesianMaxWidth / 2));

            //If possibleGraspersLocation is smaller then desired grasperSpacing, nothing to do here. just return possibleGrasperLocation 
            if (possibleGrasperLocation.Length < graspersSpacing.Length) return possibleGrasperLocation;

            //Check if physically grasper is inside this spacing. If yes, just return graspersSpacing, else more calulation 
            if (possibleGrasperLocation.IncludesInterval(graspersSpacing)) return graspersSpacing;
            else
            {
                //Same stuff like for two possibleGraspersLocation
                double distLeft = Math.Abs(possibleGrasperLocation.T0 - graspersSpacing.T0);
                double distRight = Math.Abs(possibleGrasperLocation.T1 - graspersSpacing.T1);
                double multi = 1;
                if (distRight < distLeft) multi = -1;
                double d = Math.Min(distLeft, Math.Abs(distRight));
                graspersSpacing += multi * d;
                return graspersSpacing;
            }
        }

        /// <summary>
        /// Look for best Graspers Location for two possibleGrasperLocation  depend on spectrum Centre
        /// </summary>
        /// <param name="possibleGrasperPairLocations"></param>
        /// <param name="spectrumCenter">spectrum Centre</param>
        /// <returns></returns>
        private static Interval FindGraspersSpacing(List<Interval> possibleGrasperPairLocations, double spectrumCenter)
        {
            if (possibleGrasperPairLocations.Count != 2) throw new InvalidOperationException("Only two intervals are allowed in this method.");

            // Compute gap between posibleLocations
            Interval gap = new Interval(possibleGrasperPairLocations[0].T1, possibleGrasperPairLocations[1].T0);
            if (gap.Length > Setups.CartesianMaxWidth) return Interval.Unset;

            // Compute localSpectrum between posibleLocations. IS smaller then CartesianMaxWidth, just return Extreme points...
            Interval localSpectrum = Interval.FromUnion(possibleGrasperPairLocations[0], possibleGrasperPairLocations[1]);
            if (localSpectrum.Length < Setups.CartesianMaxWidth) return localSpectrum;

            // Create Ideal Grapsers Location.
            Interval graspersSpacing = new Interval(spectrumCenter - (Setups.CartesianMaxWidth / 2), spectrumCenter + (Setups.CartesianMaxWidth / 2));

            //Calculate difference between gap, and graspersSpacing, this will bes useful to estimate possible movements of graspers.
            double missing = Math.Abs(gap.Length - graspersSpacing.Length);
            // Get common space, to moce graspers
            Interval common = new Interval(Math.Max(gap.T0 - missing, possibleGrasperPairLocations[0].T0), Math.Min(gap.T1 + missing, possibleGrasperPairLocations[1].T1));
            //Check if physically graspers are inside this spacing. If yes, just return graspersSpacing, else more calculation 
            if (common.IncludesInterval(graspersSpacing)) return graspersSpacing;
            else
            {
                // calculate distances between graspersSpacing and common edges
                double distLeft = Math.Abs(common.T0 - graspersSpacing.T0);
                double distRight = Math.Abs(common.T1 - graspersSpacing.T1);
                double multi = 1;
                //Depend which distance is bigger estimate sides.
                if (distRight < distLeft) multi = -1;
                // Get smaller one
                double d = Math.Min(distLeft, Math.Abs(distRight));
                // estimate location based on side, and smaller distance
                graspersSpacing += multi * d;
                return graspersSpacing;
            }
        }

        /// <summary>
        ///  Calculate score to evaluate Grasper location. More in spectrum centre (blisterCentre), more wide (CartesianMaxWidth), the higher score will be returned.
        /// </summary>
        /// <param name="grasperLocation">Grasper location to evaluate</param>
        /// <param name="spectrum">Spectrum</param>
        /// <returns>Score</returns>
        private static double EvaluateGraspers(Interval grasperLocation, Interval spectrum)
        {
            double distance = Math.Abs(grasperLocation.T0 - grasperLocation.T1);
            if (distance < Setups.CartesianMinWidth) return 0.0;
            double bestGrasperRange = Math.Min(spectrum.Length, Setups.CartesianMaxWidth);
            double jawSpaceCost = distance / bestGrasperRange;
            double deviation = Math.Abs(spectrum.ParameterAt(0.5) - (grasperLocation.Mid)) / bestGrasperRange;
            double jawGeneralLocationCost = Math.Pow(0.1, deviation);
            return (jawGeneralLocationCost + jawSpaceCost) / 2;
        }

        /// <summary>
        /// Try to Get Grassper position based on given centre point
        /// </summary>
        /// <param name="graspersPossibleLocation">All possible(allowed) location of Graspers (as intervals)</param>
        /// <param name="centrePoint">Centre Point to test</param>
        /// <returns>Interval.Unset if cannot find positions, else, most wide grasper spacing found in this location</returns>
        private static Interval TryGetGraspersPoints(List<Interval> graspersPossibleLocation, double centrePoint)
        {
            Interval limits = new Interval(centrePoint - (Setups.CartesianMaxWidth / 2), centrePoint + (Setups.CartesianMaxWidth / 2));
            List<Interval> commonIntervals = CommonIntervals(graspersPossibleLocation, limits);
            if (commonIntervals.Count == 0) return Interval.Unset;
            // Check which point in GrasperPossibleLocation lines inside circle is closest to circle
            Interval result = new Interval(commonIntervals[0]);
            for (int i = 1; i < commonIntervals.Count; i++)
            {
                result = Interval.FromUnion(result, commonIntervals[i]);
            }
            return result;
        }

        /// <summary>
        /// Get common intervals between set of intervals and main one.
        /// </summary>
        /// <param name="toEvaluate">Intervals to check for inclusion</param>
        /// <param name="bounds">Main interval</param>
        /// <returns>List of interval in bounds of main </returns>
        public static List<Interval> CommonIntervals(List<Interval> toEvaluate, Interval bounds)
        {

            List<Interval> output = new List<Interval>(toEvaluate.Count);
            foreach (Interval test in toEvaluate)
            {
                Interval result = Interval.FromIntersection(bounds, test);
                if (result.IsValid) output.Add(result);
            }
            return output;
        }

        /// <summary>
        /// Get "global" interval from sets of Intervals.
        /// </summary>
        /// <param name="intervals">List of Intervals to find Min and Max from them.
        /// All intervals must be in increasing flavour</param>
        /// <returns>Min Max interval</returns>
        public static Interval IntervalsInterval(List<Interval> intervals)
        {
            double minValue = intervals.OrderBy(interval => interval.T0).First().T0;
            double maxValue = intervals.OrderBy(interval => interval.T1).Last().T1;
            return new Interval(minValue, maxValue);
        }

        private void moveGrasperPossibleLocation(double factor)
        {
            JawsPossibleLocation.ForEach(line => line.Translate(Vector3d.YAxis * factor));
        }
        #endregion

        #region JAWS UPDATE

        public void UpdateJawsPoints()
        {
            Jaws = FindJawPoints();
        }

        public static List<Interval> ApplyCutOnGrasperLocation(List<Interval> grasperIntervals, Interval restrictedArea)
        {
            if (!restrictedArea.IsValid) return null;
            List<Interval> remainingGraspersLocation = new List<Interval>(grasperIntervals.Count);
            foreach (Interval currentGraspersLocation in grasperIntervals)
            {
                remainingGraspersLocation.AddRange(Interval.FromSubstraction(currentGraspersLocation, restrictedArea).Where(interval => interval.Length > 0).ToList());
            }
            remainingGraspersLocation = remainingGraspersLocation.Select(spacing => { spacing.MakeIncreasing(); return spacing; }).ToList();
            return remainingGraspersLocation.OrderBy(spacing => spacing.T0).ToList();
        }


        public static List<Interval> ApplyCutOnGrasperLocation(List<Interval> grasperIntervals, CutData cutData)
        {
            Interval restrictedArea = ComputeTotalCutImpactInterval(cutData, true, Setups.JawKnifeAdditionalSafeDistance);
            return ApplyCutOnGrasperLocation(grasperIntervals, restrictedArea);
        }

        public void ApplyCut(CutBlister chunk, bool updateJaws = true)
        {
            List<Interval> grasperIntervals = GetJawPossibleIntervals();
            List<Interval> remainingGraspersLocation = ApplyCutOnGrasperLocation(grasperIntervals, chunk.CutData);
            if (remainingGraspersLocation == null) return;
            JawsPossibleLocation = ConvertIntervalsToLines(remainingGraspersLocation);
            //JawsPossibleLocation = remainingGraspersLocation.Select(interval => new LineCurve(new Point2d(interval.T0, Setups.JawDepth), new Point2d(interval.T1, Setups.JawDepth))).ToList();
            if (updateJaws) Jaws = FindJawPoints();
        }

        #endregion
        //TODO: Lepsza analiza czy Blister jest prosto i można go złapać łapkami. Moze trzeba liczyc powierzchnie stylku miedzy łakpa a blistrem i jak jest mniej niż 50% to uchwyt niepewny.
        public bool IsBlisterStraight(double maxDeviation)
        {
            if ((AABBox.Area() / MABBox.Area()) > maxDeviation) return false;
            else return true;
        }
        public JObject GetLocalJSON()
        {
            Jaws = FindJawPoints();
            JObject jawPoints = new JObject();
            if (Jaws.Count == 0) return jawPoints;
            jawPoints.Add("jaw_2", Jaws[0].Location.X);
            jawPoints.Add("jaw_1", Jaws[1].Location.X);
            return jawPoints;
        }

       public JObject GetGlobalJSON()
        {
            Jaws = FindJawPoints();
            JObject jawPoints = new JObject();
            if (Jaws.Count == 0) return jawPoints;
            List<Point3d> globalAnchors = CoordinateSystem.ComputeGlobalAnchors(Jaws);
            // JAW1 Stuff
            // 1 i 2 sa zamienione na zyczenie Artura
            JArray jaw1_PointArray = new JArray();
            jaw1_PointArray.Add(globalAnchors[0].X);
            jaw1_PointArray.Add(globalAnchors[0].Y);
            jawPoints.Add("jaw_2", jaw1_PointArray);
            // JAW2 Stuff
            // Calculate distance between JAW1 and JAW2
            // NOTE: Czy moze byc sytuacja ze mamy tylko 1 Anchor?
            double distance = Math.Abs((Jaws[0].Location - Jaws[1].Location).Length);
            jawPoints.Add("jaw_1", distance);

            return jawPoints;
        }
    }
}
