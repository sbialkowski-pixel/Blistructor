﻿using System;
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
        private static readonly ILog log = LogManager.GetLogger("Cutter.Anchor");

        // TODO: Adding Setups.CartesianJawYLimit as a limit for JAW_1 (Or 2)
        // TODO: Consistent names: Grasper, JAw, ANchor...

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
            MABBox = Geometry.MinimumAreaRectangleBF(mainOutline);
            Geometry.UnifyCurve(MABBox);
            AABBox = rect.ToPolyline().ToPolylineCurve();
            Geometry.UnifyCurve(AABBox);

            // Find lowest mid point on Blister AA Bounding Box
            List<Line> aaSegments = new List<Line>(AABBox.ToPolyline().GetSegments());
            LineCurve guideLine = new LineCurve(aaSegments.OrderBy(line => line.PointAt(0.5).Y).ToList()[0]);
            // Move line to Y => 0
            guideLine.SetStartPoint(new Point3d(guideLine.PointAtStart.X, Setups.JawDepth, 0));
            guideLine.SetEndPoint(new Point3d(guideLine.PointAtEnd.X, Setups.JawDepth, 0));

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
            LineCurve fullPredLine = new LineCurve(predLinePoints[0], predLinePoints[1]);

            //Add limit fullPredLine to CartesianMaxWidth+CartesianJawYLimit as a max possible location for any Grasper
            double maxCartesianDistanceX = Math.Min(fullPredLine.PointAtEnd.X, (Setups.CartesianMaxWidth + Setups.CartesianJawYLimit));
            fullPredLine.SetEndPoint(new Point3d(maxCartesianDistanceX, Setups.JawDepth, 0));

            // NOTE: Check intersection with pills (Or maybe with pillsOffset. Rethink problem)
            Tuple<List<Curve>, List<Curve>> trimResult = Geometry.TrimWithRegions(fullPredLine, BlisterQueue[0].GetPills(false));
            // Gather all parts outside (not in pills) shrink curve on both sides by half of Grasper width and move it back to mid position 
            foreach (Curve crv in trimResult.Item2)
            {
                // Shrink pieces on both sides by half of Grasper width.
                Line ln = ((LineCurve)crv).Line;
                if (ln.Length < Setups.JawWidth) continue;
                ln.Extend(-Setups.JawWidth / 2, -Setups.JawWidth / 2);
                LineCurve cln = new LineCurve(ln);
                JawsPossibleLocation.Add(cln);
            }
            GuessJawPossiblityOnPill();
            Jaws = FindJawPoints();
            //GuideLine.Translate(new Vector3d(0, -Setups.JawDepth, 0));
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

        public List<JawPoint> Jaws { get; private set; }
        #endregion

        public List<Curve> GetCartesianAsObstacle()
        {
            return new List<Curve> { CreateCartesianLimitLine() };
        }

        public static LineCurve CreateCartesianLimitLine()
        {
            return new LineCurve(new Line(new Point3d(-Setups.IsoRadius, -Setups.BlisterCartesianDistance, 0), Vector3d.XAxis, 2 * Setups.IsoRadius));
        }

        private List<Interval> GetJawPossibleIntervals()
        {
            List<Interval> jawPossibleIntervals = JawsPossibleLocation.Select(line => new Interval(line.PointAtStart.X, line.PointAtEnd.X)).ToList();
            jawPossibleIntervals = jawPossibleIntervals.Select(spacing => { spacing.MakeIncreasing(); return spacing; }).ToList();
            jawPossibleIntervals = jawPossibleIntervals.OrderBy(spacing => spacing.T0).ToList();
            return jawPossibleIntervals;
        }

        #region RESTRICTION METHODS

        /// <summary>
        /// Get restricted area, based on actual jaws location as Polyline (rectangle)
        /// To update Jaws location use UpdateJawPoints before using this method.
        /// </summary>
        /// <returns>List of Rectangles where jaws can occur</returns>
        public List<Polyline> GetRestrictedAreas(double additionalSafeDistance = 1.0)
        {
            //List<JawPoint> a_points = FindJawPoints();
            List<Polyline> restrictedAreas = new List<Polyline>(2);
            foreach (JawPoint anchor in Jaws)
            {
                Plane grasperPlane = new Plane(anchor.Location, Vector3d.XAxis, Vector3d.YAxis);
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
        /// <param name="additionalSafeDistance"></param>
        /// <returns>>List of Intervals where jaws cannot occurs</returns>
        public List<Interval> GetRestrictedIntervals(double additionalSafeDistance = 1.0)
        {
            List<Polyline> restrictedAreas = GetRestrictedAreas(additionalSafeDistance);
            List<Interval> restrictedIntervals = new List<Interval>(restrictedAreas.Count);
            foreach (Polyline restrictedArea in restrictedAreas)
            {
                Interval restrictedInterval = new Interval(restrictedArea.BoundingBox.Min.Y,
                    restrictedArea.BoundingBox.Max.Y);
                restrictedInterval.MakeIncreasing();
                restrictedIntervals.Add(restrictedInterval);
            }
            return restrictedIntervals;
        }

        /// <summary>
        /// Based on CutData compute cut impact area intervals.
        /// </summary>
        /// <param name="cutData"></param>
        /// <returns>List of restricted intervals</returns>
        public List<Interval> ComputCutImpactInterval(CutData cutData)
        {
            List<BoundingBox> restrictedAreas = ComputeCutImpactAreas(cutData);
            List<Interval> restrictedIntervals = new List<Interval>(restrictedAreas.Count);
            foreach (BoundingBox restrictedArea in restrictedAreas)
            {
                Interval restrictedInterval = new Interval(restrictedArea.Min.Y,
                    restrictedArea.Max.Y);
                restrictedInterval.MakeIncreasing();
                restrictedIntervals.Add(restrictedInterval);
            }
            return restrictedIntervals;
        }

        /// <summary>
        /// Based on CutData compute cut impact area as BBox.
        /// </summary>
        /// <param name="cutData"></param>
        /// <returns>List of restricted areas as BBox</returns>
        public List<BoundingBox> ComputeCutImpactAreas(CutData cutData)
        {
            // Thicken paths from cutting data and check how this influence 
            List<BoundingBox> allRestrictedArea = new List<BoundingBox>(cutData.Segments.Count);
            foreach (PolylineCurve ply in cutData.Segments)
            {
                //Create upperLine - max distance where Jaw can operate
                LineCurve uppeLimitLine = new LineCurve(new Line(new Point3d(-Setups.IsoRadius, Setups.JawDepth, 0), Vector3d.XAxis, 2 * Setups.IsoRadius));

                //Create lowerLimitLine - lower line where for this segment knife can operate
                double knifeY = ply.ToPolyline().OrderBy(pt => pt.Y).First().Y;
                LineCurve lowerLimitLine = new LineCurve(new Line(new Point3d(-Setups.IsoRadius, knifeY, 0), Vector3d.XAxis, 2 * Setups.IsoRadius));

                //Check if knife segment intersect with Upper line = knife-jaw collision can occur
                List<IntersectionEvent> checkIntersect = Intersection.CurveCurve(uppeLimitLine, ply, Setups.IntersectionTolerance);

                // If intersection occurs, any
                if (checkIntersect.Count > 0)
                {

                    PolylineCurve extPly = (PolylineCurve)ply.Extend(CurveEnd.Both, 100);

                    // create knife "impact area"
                    PolylineCurve knifeFootprint = Geometry.PolylineThicken(extPly, Setups.BladeWidth / 2);

                    if (knifeFootprint == null) continue;

                    LineCurve cartesianLimitLine = Grasper.CreateCartesianLimitLine();
                    // Split knifeFootprint by upper and lower line
                    List<PolylineCurve> splited = (List<PolylineCurve>)Geometry.SplitRegion(knifeFootprint, cartesianLimitLine).Select(crv => (PolylineCurve)crv);

                    if (splited.Count != 2) continue;

                    PolylineCurve forFurtherSplit = splited.OrderBy(pline => pline.CenterPoint().Y).Last();

                    LineCurve upperCartesianLimitLine = new LineCurve(cartesianLimitLine);

                    splited = (List<PolylineCurve>)Geometry.SplitRegion(forFurtherSplit, upperCartesianLimitLine).Select(crv => (PolylineCurve)crv);

                    if (splited.Count != 2) continue;

                    PolylineCurve grasperRestrictedArea = splited.OrderBy(pline => pline.CenterPoint().Y).First();

                    // After split, there is area where knife can operate.
                    // Transform into Interval as min, max values where jaw should not appear

                    BoundingBox grasperRestrictedAreaBBox = grasperRestrictedArea.GetBoundingBox(false);
                    allRestrictedArea.Add(grasperRestrictedAreaBBox);
                }
            }
            return allRestrictedArea;
        }

        #endregion

        #region COLLISION/VALIDATION

        /// <summary>
        /// Validate cut against collision, and impossible to hold cuts
        /// </summary>
        /// <param name="cutData"></param>
        /// <returns>True if valid, falsie if not</returns>
        public bool IsValidateCut(CutData cutData)
        {
            Interval blisterImpactInterval = CutBlisterImpactInterval(cutData);
            // Cut not influancing grasper
            if (!blisterImpactInterval.IsValid) return true;
            // If this cut will remove whole jawPossibleLocation line, its is not good, at least it is last blister...
            // tHis need CutBLister, not onlu CUtData... TO redesign.
            if (blisterImpactInterval.IncludesInterval(IntervalsInterval(GetJawPossibleIntervals()),true)) return false ;
            return true; //FAKE
        }

        /// <summary>
        /// Check collision with curent cutData
        /// </summary>
        /// <param name="cutData"></param>
        /// <returns></returns>
        public bool IsColliding(CutData cutData)
        {
            UpdateJawsPoints();
            List<Interval> currentJawsInterval = GetRestrictedIntervals();
            List<Interval> cutBasedIntervals = ComputCutImpactInterval(cutData);
            foreach (Interval currentInterval in currentJawsInterval)
            {
                List<Interval> commonInterval = CommonIntervals(cutBasedIntervals, currentInterval);
                if (commonInterval.Count > 0) return true;
            }
            return false;
        }




        #endregion


        /// <summary>
        /// Check if Pill intersect with PredLine and update pill status - possibleAnchor
        /// </summary>
        public void GuessJawPossiblityOnPill()
        {
            foreach (Blister blister in BlisterQueue)
            {
                foreach (Pill pill in blister.Pills)
                {
                    pill.possibleAnchor = false;
                    foreach (LineCurve line in JawsPossibleLocation)
                    {
                        List<IntersectionEvent> intersection = Intersection.CurveCurve(pill.voronoi, line, Setups.IntersectionTolerance);
                        if (intersection.Count > 0)
                        {
                            pill.possibleAnchor = true;
                        }
                        else
                        {
                            foreach (Point3d pt in new List<Point3d> { line.PointAtStart, line.PointAtEnd })
                            {
                                PointContainment contains = pill.voronoi.Contains(pt, Plane.WorldXY, Setups.IntersectionTolerance);
                                if (contains == PointContainment.Inside)
                                {
                                    pill.possibleAnchor = true;
                                    break;
                                }
                            }
                        }
                        if (pill.possibleAnchor == true) break;
                    }
                }
            }
        }


        #region GRASPER LOCATION - FINAL
        /// <summary>
        /// Based on JawsPossibleLocation find JawPoints
        /// </summary>
        /// <returns></returns>
        public List<JawPoint> FindJawPoints()
        {
            // List<AnchorPoint> outputGrasperPoints = new List<AnchorPoint>();
            List<Interval> grasperPossibleLocation = GetJawPossibleIntervals();

            //Get Extreme Points and create Spectrum line
            List<double> allStart = grasperPossibleLocation.OrderBy(spacing => spacing.T0).Select(spacing => spacing.T0).ToList();
            List<double> allEnd = grasperPossibleLocation.OrderBy(line => line.T0).Select(spacing => spacing.T1).ToList();

            double extremeLeft = allStart.First();
            double extremeRight = allEnd.Last();

            Interval spectrumLine = new Interval(extremeLeft, extremeRight);

            //If spectrum smaller then CartesianMaxWidth, and bigger then CartesianMinWidth, just give me spectrumLine as Locations.
            if (spectrumLine.Length < Setups.CartesianMaxWidth && spectrumLine.Length > Setups.CartesianMinWidth) return ConvertIntervalToJawPoints(spectrumLine);
            //If spectrum smaller then CartesianMinWidth, return null
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
        private List<JawPoint> ConvertIntervalToJawPoints(Interval jawInterval)
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
        private Interval FindGraspersSpacing(Interval possibleGrasperLocation, double spectrumCenter)
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
        private Interval FindGraspersSpacing(List<Interval> possibleGrasperPairLocations, double spectrumCenter)
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
        private double EvaluateGraspers(Interval grasperLocation, Interval spectrum)
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
        private Interval TryGetGraspersPoints(List<Interval> graspersPossibleLocation, double centrePoint)
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
        private List<Interval> CommonIntervals(List<Interval> toEvaluate, Interval bounds)
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
        private Interval IntervalsInterval(List<Interval> intervals)
        {
            double minValue = intervals.OrderBy(interval => interval.T0).First().T0;
            double maxValue = intervals.OrderBy(interval => interval.T1).First().T1;
            return new Interval(minValue, maxValue);
        }

        private void moveGrasperPossibleLocation(double factor)
        {
            JawsPossibleLocation.ForEach(line => line.Translate(Vector3d.YAxis * factor));
        }
        #endregion

        #region ANCHOR UPDATE
        /// <summary>
        /// Check which Jaw belongs to which pill and reset other cells anchors. 
        /// </summary>
        /// <returns></returns>
        public bool ApplyAnchorOnBlister()
        {
            if (Jaws.Count == 0) return false;
            // NOTE: For loop by all queue blisters.
            foreach (Blister blister in BlisterQueue)
            {
                if (blister.Pills == null) return false;
                if (blister.Pills.Count == 0) return false;
                foreach (Pill pill in blister.Pills)
                {
                    // Reset anchors in each pill.
                    pill.Anchors = new List<JawPoint>(2);
                    //pill.Anchor = new AnchorPoint();
                    foreach (JawPoint pt in Jaws)
                    {
                        PointContainment result = pill.voronoi.Contains(pt.Location, Plane.WorldXY, Setups.IntersectionTolerance);
                        if (result == PointContainment.Inside)
                        {
                            log.Info(String.Format("Anchor appied on pill - {0} with status {1}", pill.Id, pt.State));
                            //pill.Anchor = pt;
                            pill.Anchors.Add(pt);
                            break;
                        }
                    }

                }
            }
            return true;
        }

        public void UpdateJawsPoints()
        {
            Jaws = FindJawPoints();
        }

        private Interval CutBlisterImpactInterval(CutData cutData)
        {
            List<Interval> cutImpactAreas = ComputCutImpactInterval(cutData);
            // Cut paths does not interact with Grasper
            if (cutImpactAreas.Count == 0) return Interval.Unset;
            // Only one cut Path interact, that means this is side pill. I need to find other side, to remove jawPossibleLocation inside this cutData.  
            if (cutImpactAreas.Count == 1)
            {
                BoundingBox bbox = cutData.Polygon.GetBoundingBox(false);
                Interval bboxInterval = new Interval(bbox.Min.Y, bbox.Max.Y);
                Interval restrictedArea = Interval.FromUnion(bboxInterval, cutImpactAreas[0]);
                restrictedArea.MakeIncreasing();
                return restrictedArea;
            }
            else
            {
                return IntervalsInterval(cutImpactAreas);
            }
        }
        // TODO: UpdateJawsPoints should be called inside Update??? So the Jaws property is always up to date after Update.
        /// <summary>
        /// Update grasper with confirmed to cut piece of blister.
        /// </summary>
        /// <param name="chunk">Confirmed cut piece of blister.</param>
        public void Update(CutBlister chunk)
        {
            Interval restrictedArea = CutBlisterImpactInterval(chunk.CutData);
            if (!restrictedArea.IsValid) return;
            List<Interval> grasperIntervals = GetJawPossibleIntervals();
            List<Interval> remainingGraspersLocation = new List<Interval>(grasperIntervals.Count);
            foreach (Interval currentGraspersLocation in grasperIntervals)
            {
                remainingGraspersLocation.AddRange(Interval.FromSubstraction(currentGraspersLocation, restrictedArea).Where(interval => interval.Length > 0).ToList());
            }
            remainingGraspersLocation = remainingGraspersLocation.Select(spacing => { spacing.MakeIncreasing(); return spacing; }).ToList();
            remainingGraspersLocation = remainingGraspersLocation.OrderBy(spacing => spacing.T0).ToList();
            JawsPossibleLocation = remainingGraspersLocation.Select(interval => new LineCurve(new Point2d(interval.T0, Setups.JawDepth), new Point2d(interval.T1, Setups.JawDepth))).ToList();
        }

        /*
        public void Update_Legacy(Blister cuttedBlister)
        {
#if DEBUG
            var file = new File3dm();
#endif
            PolylineCurve polygon = cuttedBlister.Pills[0].bestCuttingData.Polygon;

            // List<RegionContainment> cont1 = GrasperPossibleLocation.Select(crv => Curve.PlanarClosedCurveRelationship(polygon, crv)).ToList();
            // Simplify polygon
            Curve simplePolygon = polygon;
            simplePolygon.RemoveShortSegments(Setups.CollapseTolerance);
            // Offset by half BladeWidth
            Curve offset = simplePolygon.Offset(Plane.WorldXY, Setups.BladeWidth / 2);
#if DEBUG
            var att = new ObjectAttributes();
            att.Name = "simplePolygon";
            file.Objects.AddCurve(simplePolygon, att);
            att = new ObjectAttributes();
            att.Name = "offset";
            file.Objects.AddCurve(offset, att);
            att = new ObjectAttributes();
            att.Name = "path";
            cuttedBlister.Pills[0].bestCuttingData.Path.ForEach(crv => file.Objects.AddCurve(crv, att));
            att = new ObjectAttributes();
            att.Name = "path_segemnts";
            cuttedBlister.Pills[0].bestCuttingData.bladeFootPrint.ForEach(crv => file.Objects.AddCurve(crv, att));
#endif
            if (offset != null)
            {
                //log.Info(String.Format("offset Length: {0}", offset.Length));
                Curve rightMove = (Curve)offset.Duplicate();
                rightMove.Translate(Vector3d.XAxis * Setups.JawWidth / 2);
                Curve leftMove = (Curve)offset.Duplicate();
                leftMove.Translate(-Vector3d.XAxis * Setups.JawWidth / 2);
                List<Curve> unitedCurve = Curve.CreateBooleanUnion(new List<Curve>() { rightMove, offset, leftMove });
                if (unitedCurve.Count == 1)
                {
                    //log.Info(String.Format("unitedCurve Length: {0}", unitedCurve.Length));
                    // Assuming GrasperPossibleLocation is in the Setups.JawDepth possition... Trim
                    Tuple<List<Curve>, List<Curve>> result = Geometry.TrimWithRegion(GrasperPossibleLocation.Select(crv => (Curve)crv).ToList(), unitedCurve[0]);
                    //GrasperPossibleLocation = result.Item2.Select(crv => (LineCurve)crv).ToList();
#if DEBUG
                    att = new ObjectAttributes();
                    att.Name = "unitedCurve";
                    file.Objects.AddCurve(unitedCurve[0], att);
                    att = new ObjectAttributes();
                    att.Name = "trim_result";
                    result.Item2.ForEach(crv => file.Objects.AddCurve(crv, att));
                    att = new ObjectAttributes();
                    att.Name = "GrasperPossibleLocation_1";
                    GrasperPossibleLocation.ForEach(crv => file.Objects.AddCurve(crv, att));
#endif

                    // Move fullway down, trim
                    moveGrasperPossibleLocation(-Setups.JawDepth);
                    result = Geometry.TrimWithRegion(GrasperPossibleLocation.Select(crv => (Curve)crv).ToList(), unitedCurve[0]);
                    GrasperPossibleLocation = result.Item2.Select(crv => (LineCurve)crv).ToList();
                    //Put It back on place.
                    moveGrasperPossibleLocation(Setups.JawDepth);

#if DEBUG
                    att = new ObjectAttributes();
                    att.Name = "GrasperPossibleLocation_Final";
                    GrasperPossibleLocation.ForEach(crv => file.Objects.AddCurve(crv, att));
#endif
                }
            }
#if DEBUG

            file.Write(String.Format(@"D:\PIXEL\DEBUG_FILES\ANCHORS\Update_{0}.3dm", _workspace.Cutted.Count()), 6);
#endif
        }
        */

        /// <summary>
        /// 
        /// </summary>
        /// <param name="cuttedBlister"></param>
        public void FindNewAnchorAndApplyOnBlister(CutBlister cuttedBlister)
        {
            Update(cuttedBlister);
            Jaws = FindJawPoints();
            ApplyAnchorOnBlister();
        }

        #endregion
        //TODO: Lepsza analiza czy Blister jest prosto i można go złapać łapkami. Moze trzeba liczyc powierzchnie stylku miedzy łakpa a blistrem i jak jest mniej niż 50% to uchwyt niepewny.
        public bool IsBlisterStraight(double maxDeviation)
        {
            if ((AABBox.Area() / MABBox.Area()) > maxDeviation) return false;
            else return true;
        }

        public JObject GetJSON()
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
