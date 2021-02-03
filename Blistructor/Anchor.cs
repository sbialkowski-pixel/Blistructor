using System;
using System.Collections.Generic;
using System.Linq;

#if PIXEL
using Pixel.Rhino;
using Pixel.Rhino.Geometry;
#else
using Rhino;
using Rhino.Geometry;
#endif
using log4net;
using Newtonsoft.Json.Linq;

namespace Blistructor
{
    public class Anchor
    {
        private static readonly ILog log = LogManager.GetLogger("Cutter.Anchor");

        public LineCurve cartesianLimitLine;

        public MultiBlister mBlister;
        public PolylineCurve mainOutline;
        public PolylineCurve aaBBox;
        public PolylineCurve maBBox;
        public LineCurve GuideLine;

        public List<LineCurve> GrasperPossibleLocation;
        public List<AnchorPoint> anchors;
        public List<Point3d> GlobalAnchors;


        public Anchor(MultiBlister mBlister)
        {
            GlobalAnchors = new List<Point3d>(2);
            // Create Cartesian Limit Line
            Line tempLine = new Line(new Point3d(0, -Setups.BlisterCartesianDistance, 0), Vector3d.XAxis, 1.0);
            tempLine.Extend(100, Setups.IsoRadius);
            cartesianLimitLine = new LineCurve(tempLine);

            // Get needed data
            this.mBlister = mBlister;
            GrasperPossibleLocation = new List<LineCurve>();
            //Build initial blister shape data
            mainOutline = mBlister.Queue[0].Outline;

           // Generate BBoxes
            BoundingBox blisterBB = mainOutline.GetBoundingBox(false);
            Rectangle3d rect = new Rectangle3d(Plane.WorldXY, blisterBB.Min, blisterBB.Max);
            maBBox = Geometry.MinimumAreaRectangleBF(mainOutline);
            Geometry.UnifyCurve(maBBox);
            aaBBox = rect.ToPolyline().ToPolylineCurve();
            Geometry.UnifyCurve(aaBBox);

            // Find lowest mid point on Blister AA Bounding Box
            List<Line> aaSegments = new List<Line>(aaBBox.ToPolyline().GetSegments());
            GuideLine = new LineCurve(aaSegments.OrderBy(line => line.PointAt(0.5).Y).ToList()[0]);
            // Move line to Y => 0
            GuideLine.SetStartPoint(new Point3d(GuideLine.PointAtStart.X, 0, 0));
            GuideLine.SetEndPoint(new Point3d(GuideLine.PointAtEnd.X, 0, 0));

            // Create initial predition Lines
            LineCurve fullPredLine = new LineCurve(GuideLine);
            fullPredLine.Translate(Vector3d.YAxis * Setups.JawDepth / 2);

            // Find limits based on Blister Shape
            double[] paramT = GuideLine.DivideByCount(50, true);
            List<double> limitedParamT = new List<double>(paramT.Length);
            foreach (double t in paramT)
            {
                //double parT;
                if (mainOutline.ClosestPoint(GuideLine.PointAt(t), out double parT, Setups.JawDepth / 2)) limitedParamT.Add(parT);
            }

            if (limitedParamT.Count == 0) throw new AnchorException("Blister to far from Barier. Cannot grab blister.");


            // Find Extreme points on Blister
            List<Point3d> extremePointsOnBlister = new List<Point3d>(){
                mainOutline.PointAt(limitedParamT.First()),
                mainOutline.PointAt(limitedParamT.Last())
            };
            
            // Project this point to Predition Line
            List<double> fullPredLineParamT = new List<double>(paramT.Length);
            foreach (Point3d pt in extremePointsOnBlister)
            {
                double parT;
                if (fullPredLine.ClosestPoint(pt, out parT)) fullPredLineParamT.Add(parT);
            }


            // keep lines between extreme points
            fullPredLine = (LineCurve)fullPredLine.Trim(fullPredLineParamT[0], fullPredLineParamT[1]);
            // Shrink curve on both sides by half of Grasper width.

            // Move temporaly predLine to the upper position, too chceck intersection with pills.
            fullPredLine.Translate(Vector3d.YAxis * Setups.JawDepth / 2);
            // NOTE: Check intersection with pills (Or maybe with pillsOffset. Rethink problem)
            Tuple<List<Curve>, List<Curve>> trimResult = Geometry.TrimWithRegions(fullPredLine, mBlister.Queue[0].GetPills(false));
            // Gather all parts outsite (not in pills) shrink curve on both sides by half of Grasper width and move it back to mid position 
            foreach (Curve crv in trimResult.Item2)
            {
                // Shrink pieces on both sides by half of Grasper width.
                Line ln = ((LineCurve)crv).Line;
                if (ln.Length < Setups.JawWidth) continue;
                ln.Extend(-Setups.JawWidth / 2, -Setups.JawWidth / 2);
                LineCurve cln = new LineCurve(ln);
                //move it to 0 position
                cln.Translate(Vector3d.YAxis * -Setups.JawDepth);
                // Gather 
                GrasperPossibleLocation.Add(cln);
            }

            anchors = GetGraspersPoints();
            log.Info(String.Format("Anchors found: {0}", anchors.Count));

        }

        private List<Interval> ConvertGrasperstoInterval()
        {
            List<Interval> GrasperPossibleSpacing = GrasperPossibleLocation.Select(line => new Interval(line.PointAtStart.X, line.PointAtEnd.X)).ToList();
            GrasperPossibleSpacing = GrasperPossibleSpacing.Select(spacing => { spacing.MakeIncreasing(); return spacing; }).ToList();
            GrasperPossibleSpacing = GrasperPossibleSpacing.OrderBy(spacing => spacing.T0).ToList();
            return GrasperPossibleSpacing;
        }

        private List<AnchorPoint> ConvertToAnchors (Interval grasperLocation)
        {
            return new List<AnchorPoint>() {
                        new AnchorPoint(new Point3d(grasperLocation.T0,0,0), AnchorSite.JAW_1),
                        new AnchorPoint(new Point3d(grasperLocation.T1,0,0), AnchorSite.JAW_2)
                    };
        }

        public List<AnchorPoint> GetGraspersPoints()
        {
           // List<AnchorPoint> outputGrasperPoints = new List<AnchorPoint>();
            List<Interval> grasperPossibleLocation = ConvertGrasperstoInterval();


            //Get Extreme Points and create Spectrum line
            List<double> allStart = grasperPossibleLocation.OrderBy(spacing => spacing.T0).Select(spacing => spacing.T0).ToList();
            List<double> allEnd = grasperPossibleLocation.OrderBy(line => line.T0).Select(spacing => spacing.T1).ToList();

            double extremeLeft = allStart.First();
            double extremeRight = allEnd.Last();

            Interval spectrumLine = new Interval(extremeLeft, extremeRight);

            //If spectrum samller then CartesianMaxWidth, and bigger then CartesianMinWidth, just give me spectrumLine as Locations.
            if (spectrumLine.Length < Setups.CartesianMaxWidth && spectrumLine.Length > Setups.CartesianMinWidth) return ConvertToAnchors(spectrumLine);
            //If spectrum samller then CartesianMinWidth, return null
            else if (spectrumLine.Length < Setups.CartesianMinWidth) return new List<AnchorPoint>();
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
                        return ConvertToAnchors(estimatedGraspers);
                    }
                }
                // If not working, just look further for best anchor location
                // Try to get all single and uniq pairs of Grapssers Possible Location combination and get best one (highest score)
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
                if  (result.Count>0)
                {
                    // Best First..
                    result = result.OrderBy(data => data.Item1).Reverse().ToList();
                    return ConvertToAnchors(result[0].Item2);
                }
                return new List<AnchorPoint>();
            }
        }

        [Obsolete("This method is deprecated, please use GetGraspersPoints")]
        public List<AnchorPoint> GetJawsPoints()
        {
            List<AnchorPoint> jawPoints = new List<AnchorPoint>();
            List<AnchorPoint> extremePoints = GetExtremePoints();
            //log.Info(String.Format("extremePoints found: {0}", extremePoints.Count));
            if (extremePoints == null) return jawPoints;
            // Create line connectng extreme points
            Line spectrumLine = new Line(extremePoints[0].location, extremePoints[1].location);
            // If line length is within MinMax Cartesian Width, just get this points
            if (spectrumLine.Length < Setups.CartesianMaxWidth && spectrumLine.Length > Setups.CartesianMinWidth)
            {
                return extremePoints;
            }
            // If not..
            else if (spectrumLine.Length > Setups.CartesianMaxWidth)
            {
                //log.Info("spectrumLine.Length > Setups.CartesianMaxWidth");

                // Create circle with MaxWidth diameter in teh center of spectrum line
                Point3d midPoint = spectrumLine.PointAt(0.5);
                NurbsCurve maxJawsCircle = (new Circle(midPoint, Setups.CartesianMaxWidth / 2)).ToNurbsCurve();

                // Trim GrasperPossibleLocation byt this circle.
                Tuple<List<Curve>, List<Curve>> trimResult = Geometry.TrimWithRegion(GrasperPossibleLocation.Select(crv => (Curve)crv).ToList(), maxJawsCircle);
                // If nothing inside return empty list
                if (trimResult.Item1.Count == 0)
                {
                    return jawPoints;
                }
                // Check which point in GrasperPossibleLocation lines insied circle is closest to circle
                List<Point3d> toEvaluate = new List<Point3d>(trimResult.Item1.Count);
                foreach (Curve ln in trimResult.Item1)
                {
                    Point3d pt, pt2;
                    ln.ClosestPoints(maxJawsCircle, out pt, out pt2);
                    toEvaluate.Add(pt);
                }
                // Get this points....
                toEvaluate = toEvaluate.OrderBy(pt => pt.X).ToList();
                Point3d leftJaw = toEvaluate.First();
                Point3d rightJaw = toEvaluate.Last();
                //TODO: leftJaw i rightJaw maja takie same cords....
                //log.Info(leftJaw.ToString());
                //log.Info(rightJaw.ToString());
                // If new points are closer than MinWidth, return empty list
                if (leftJaw.DistanceTo(rightJaw) < Setups.CartesianMinWidth)
                {
                    return jawPoints;
                }
                // Get JawPoints...
                return new List<AnchorPoint>() {
                        new AnchorPoint(leftJaw, AnchorSite.JAW_1),
                        new AnchorPoint(rightJaw, AnchorSite.JAW_2)
                    };
            }
            //Return empty list...
            else
            {
                return jawPoints;
            }
        }

        private List<AnchorPoint> GetExtremePoints()
        {
            if (GrasperPossibleLocation == null) return null;
            if (GrasperPossibleLocation.Count == 0) return null;
            Point3d leftJaw = GrasperPossibleLocation.OrderBy(line => line.PointAtStart.X).Select(line => line.PointAtStart).First();
            Point3d rightJaw = GrasperPossibleLocation.OrderBy(line => line.PointAtStart.X).Select(line => line.PointAtEnd).Last();
            return new List<AnchorPoint>() {
                        new AnchorPoint(leftJaw, AnchorSite.JAW_1),
                        new AnchorPoint(rightJaw, AnchorSite.JAW_2)
                    };
        }

        #region grasperLocation

        /// <summary>
        ///  Look for best Graspers Location for one possibleGrasperLocation depend on spectrum Centre
        /// </summary>
        /// <param name="possibleGrasperLocation"></param>
        /// <param name="spectrumCenter">spectrum Centre</param>
        /// <returns></returns>
        private Interval FindGraspersSpacing(Interval possibleGrasperLocation, double spectrumCenter)
        {
            possibleGrasperLocation.MakeIncreasing();
             Interval graspersSpacing = new  Interval(spectrumCenter - (Setups.CartesianMaxWidth / 2), spectrumCenter + (Setups.CartesianMaxWidth / 2));

            //If possibleGraspersLocation is maller then desierd grasperSpacing, nothing to do here. just return possibleGrasperLocation 
            if (possibleGrasperLocation.Length < graspersSpacing.Length) return possibleGrasperLocation;
            
            //Check if physicaly graspers are inside this spacing. If yes, just return graspersSpacing, else more calulation 
            if (possibleGrasperLocation.IncludesInterval(graspersSpacing))return graspersSpacing;
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
        private Interval FindGraspersSpacing(List< Interval> possibleGrasperPairLocations, double spectrumCenter)
        {
            if (possibleGrasperPairLocations.Count != 2)throw new InvalidOperationException("Only two intervals allowd in this method.");

            // Compute gap between posibleLocations
            Interval gap = new  Interval(possibleGrasperPairLocations[0].T1, possibleGrasperPairLocations[1].T0);
            if (gap.Length > Setups.CartesianMaxWidth) return  Interval.Unset;

            // Compute localSpectrum between posibleLocations. IS smaller then CartesianMaxWidth, just return Extreme points...
            Interval localSpectrum =  Interval.FromUnion(possibleGrasperPairLocations[0], possibleGrasperPairLocations[1]);
            if (localSpectrum.Length < Setups.CartesianMaxWidth) return localSpectrum;

            // Create Ideal Grapsers Loacation.
            Interval graspersSpacing = new Interval(spectrumCenter - (Setups.CartesianMaxWidth / 2), spectrumCenter + (Setups.CartesianMaxWidth / 2));

            //Calculate difference between gap, and graspersSpacing, this will bes usefull to estimate possible movments of graspers.
            double missing = Math.Abs(gap.Length - graspersSpacing.Length);
            // Get common space, to moce graspers
            Interval common = new  Interval(Math.Max(gap.T0 - missing, possibleGrasperPairLocations[0].T0), Math.Min(gap.T1 + missing, possibleGrasperPairLocations[1].T1));
            //Check if physicaly graspers are inside this spacing. If yes, just return graspersSpacing, else more calulation 
            if (common.IncludesInterval(graspersSpacing)) return graspersSpacing;
            else
            {
                // calcualte distances between graspersSpacing and common edges
                double distLeft = Math.Abs(common.T0 - graspersSpacing.T0);
                double distRight = Math.Abs(common.T1 - graspersSpacing.T1);
                double multi = 1;
                //Depend which distance is bigger estimate sides.
                if (distRight < distLeft) multi = -1;
                // Get samller one
                double d = Math.Min(distLeft, Math.Abs(distRight));
                // estimate location based on side, and smaller distane
                graspersSpacing += multi * d;
                return graspersSpacing;
            }
        }

        /// <summary>
        ///  Calcualte score to evaluate Graspers location. More in spectrum center (blisterCentre), more wide (CartesianMaxWidth), the higher score will be returend.
        /// </summary>
        /// <param name="grasperLocation">Graspers location to evaluate</param>
        /// <param name="spectrum">Spectrum</param>
        /// <returns>Score</returns>
        private double EvaluateGraspers( Interval grasperLocation,  Interval spectrum)
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
        /// <returns>Interval.Unset of canot find positions, else, most wide grasper spacing found in this location</returns>
        private Interval TryGetGraspersPoints(List< Interval> graspersPossibleLocation, double centrePoint)
        {
            Interval limits = new  Interval(centrePoint - (Setups.CartesianMaxWidth / 2), centrePoint + (Setups.CartesianMaxWidth / 2));
            List< Interval> commonIntervals = GetCommonIntervals(graspersPossibleLocation, limits);
            if (commonIntervals.Count == 0) return  Interval.Unset;
            // Check which point in GrasperPossibleLocation lines insied circle is closest to circle
             Interval result = new  Interval(commonIntervals[0]);
            for (int i = 1; i < commonIntervals.Count; i++)
            {
                result =  Interval.FromUnion(result, commonIntervals[i]);
            }
            return result;
        }

        /// <summary>
        /// Get common intervals between set of intervals and main one.
        /// </summary>
        /// <param name="toEvaluate">Intervals to check for inclusion</param>
        /// <param name="bounds">Main interval</param>
        /// <returns>List of interval win bounds of Main </returns>
        public List< Interval> GetCommonIntervals(List< Interval> toEvaluate,  Interval bounds)
        {

            List< Interval> output = new List< Interval>(toEvaluate.Count);
            foreach ( Interval test in toEvaluate)
            {
                 Interval result =  Interval.FromIntersection(bounds, test);
                if (result.IsValid) output.Add(result);
            }
            return output;
        }
        #endregion

        /// <summary>
        /// Check which Anchor belongs to which cell and reset other cells anchors. 
        /// </summary>
        /// <returns></returns>
        public bool ApplyAnchorOnBlister()
        {
            if (anchors.Count == 0) return false;
            // NOTE: For loop by all queue blisters.
            foreach (Blister blister in mBlister.Queue)
            {
                if (blister.Cells == null) return false;
                if (blister.Cells.Count == 0) return false;
                foreach (Cell cell in blister.Cells)
                {
                    // Reset anchors in each cell.
                    cell.Anchor = new AnchorPoint();
                    foreach (AnchorPoint pt in anchors)
                    {
                        PointContainment result = cell.voronoi.Contains(pt.location, Plane.WorldXY, Setups.IntersectionTolerance);
                        if (result == PointContainment.Inside)
                        {
                            log.Info(String.Format("Anchor appied on cell - {0} with status {1}", cell.id, pt.state));
                            cell.Anchor = pt;
                            break;
                        }
                    }

                }
            }
            return true;
        }

        private void moveGrasperPossibleLocation(double factor)
        {
            GrasperPossibleLocation.ForEach(line => line.Translate(Vector3d.YAxis * factor));
        }

        public void Update(Blister cuttedBlister)
        {
            //Update(null, cuttedBlister.Cells[0].bestCuttingData.Polygon);
            Update(cuttedBlister.Cells[0].bestCuttingData.Polygon);
        }

        public void Update(PolylineCurve polygon)
        {        
            // Simplify polygon
            Curve simplePolygon = polygon;
            simplePolygon.RemoveShortSegments(Setups.CollapseTolerance);
            // Offset by half BladeWidth
            Curve offset = simplePolygon.Offset(Plane.WorldXY, Setups.BladeWidth / 2);
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
                    // Assuming GrasperPossibleLocation is in the 0 possition...
                    // First make upper. Move halfway up, trim
                    moveGrasperPossibleLocation(Setups.JawDepth);
                    Tuple<List<Curve>, List<Curve>> result = Geometry.TrimWithRegion(GrasperPossibleLocation.Select(crv => (Curve)crv).ToList(), unitedCurve[0]);
                    GrasperPossibleLocation = result.Item2.Select(crv => (LineCurve)crv).ToList();
                    // Move fullway down, trim
                    moveGrasperPossibleLocation(-Setups.JawDepth);
                    result = Geometry.TrimWithRegion(GrasperPossibleLocation.Select(crv => (Curve)crv).ToList(), unitedCurve[0]);
                    GrasperPossibleLocation = result.Item2.Select(crv => (LineCurve)crv).ToList();
                }
            }
        }

        [Obsolete("This Update is deprecated, please use method with PolylineCurve as argument")]
        public void Update(PolylineCurve path, PolylineCurve polygon)
        {
            if (polygon != null)
            {
                // Simplify polygon
                Curve simplePolygon = polygon;
                simplePolygon.RemoveShortSegments(Setups.CollapseTolerance);

                Curve offset = simplePolygon.Offset(Plane.WorldXY, Setups.JawWidth / 2);

                //Curve[] offset = simplePolygon.Offset(Plane.WorldXY, Setups.JawWidth / 2, Setups.GeneralTolerance, CurveOffsetCornerStyle.Sharp);
                if (offset != null)
                {
                    //    log.Warn(String.Format("Anchor Pred Line Update - Polygon Oreint {0}", polygon.ClosedCurveOrientation()));
                    //log.Warn(String.Format("Anchor - Update Pred Line  - SimpOffset Len {0} | Polygon Len {1}", simpleOffset.GetLength(), polygon.GetLength()));

                    Tuple<List<Curve>, List<Curve>> result = Geometry.TrimWithRegion(GrasperPossibleLocation.Select(crv => (Curve)crv).ToList(), offset);
                    GrasperPossibleLocation = result.Item2.Select(crv => (LineCurve)crv).ToList();
                }
                else
                {
                    log.Warn("Anchor Pred Line Update not possible. Offset return no result for polygon.");
                }
            }

            if (path != null)
            {
                PolylineCurve pathOutline = Geometry.PolylineThicken(path, Setups.BladeWidth / 2 + Setups.JawWidth / 2);
                Tuple<List<Curve>, List<Curve>> result = Geometry.TrimWithRegion(GrasperPossibleLocation.Select(crv => (Curve)crv).ToList(), pathOutline);
                GrasperPossibleLocation = result.Item2.Select(crv => (LineCurve)crv).ToList();
            }

        }

        public void FindNewAnchorAndApplyOnBlister(Blister cuttedBlister)
        {
            Update(cuttedBlister);
            anchors = GetGraspersPoints();
            ApplyAnchorOnBlister();
        }

        //TODO: To nie jest najszczęśliwsze....
        public bool IsBlisterStraight(double maxDeviation)
        {
            if ((aaBBox.Area() / maBBox.Area()) > maxDeviation) return false;
            else return true;
        }

        #region CoordiantesTransform
        public Point3d GlobalLocalCSTransform(Point3d point, Point3d csLoc, double csAngle)
        {
            Vector3d pt = point - csLoc;
            double newX = pt.X * Math.Cos(csAngle) + pt.Y * Math.Sin(csAngle);
            double newY = -pt.X * Math.Sin(csAngle) + pt.Y * Math.Cos(csAngle);
            return new Point3d(newX, newY, 0);
        }

        public Point3d LocalGlobalCSTransform(Point3d point, Point3d csLoc, double csAngle)
        {
            //Vector3d pt = point - csLoc;
            double newX = point.X * Math.Cos(csAngle) - point.Y * Math.Sin(csAngle);
            double newY = point.X * Math.Sin(csAngle) + point.Y * Math.Cos(csAngle);
            Point3d pt = new Point3d(newX, newY, 0);
            return pt + csLoc;
        }

        public Vector3d CartesianWorkPickVector(Vector3d PivotJawVector, double pickAngle)
        {
            Point3d pt = GlobalLocalCSTransform((Point3d)PivotJawVector, new Point3d(0, 0, 0), pickAngle);
            return (Vector3d)pt - PivotJawVector;
        }

        public Vector3d CartesianPickWorkVector(Vector3d PivotJawVector, double pickAngle)
        {
            Vector3d vec = CartesianWorkPickVector(PivotJawVector, pickAngle);
            vec.Reverse();
            return vec;
        }

        public Point3d CartesianGlobalJaw1L(Point3d localJaw1, Point3d blisterCSLocation, double blisterCSangle, Vector3d PivotJawVector)
        {
            Point3d globalJaw1 = LocalGlobalCSTransform(localJaw1, blisterCSLocation, blisterCSangle);
            Vector3d correctionVector = CartesianWorkPickVector(PivotJawVector, -blisterCSangle);
            return globalJaw1 - correctionVector;
        }

        public void computeGlobalAnchors()
        {
            // Compute Global location for JAW_1
            Point3d blisterCS = new Point3d(Setups.BlisterGlobal);
          //  Vector3d pivotJaw = Setups.CartesianPivotJawVector;
            foreach (AnchorPoint pt in anchors)
            {
                //NOTE: Zamiana X, Y, należy sprawdzić czy to jest napewno dobrze. Wg. moich danych i opracowanej logiki tak...
                Point3d flipedPoint = new Point3d(0, anchors[0].location.X, 0);
                Point3d globalJawLocation = CartesianGlobalJaw1L(flipedPoint, blisterCS, Setups.CartesianPickModeAngle, Setups.CartesianPivotJawVector);
                GlobalAnchors.Add(globalJawLocation);
            }
        }
        #endregion

        public JObject GetJSON()
            {
            anchors = GetGraspersPoints();
            JObject jawPoints = new JObject();
            if (anchors.Count == 0) return jawPoints;
            computeGlobalAnchors();
            // JAW1 Stuff
            JArray jaw1_PointArray = new JArray();
            jaw1_PointArray.Add(GlobalAnchors[0].X);
            jaw1_PointArray.Add(GlobalAnchors[0].Y);
            jawPoints.Add("jaw_1", jaw1_PointArray);
            // JAW2 Stuff
            // Calculate distance between JAW1 and JAW2
            // NOTE: Czy moze byc sytuacja ze mamy tylko 1 Anchor?
            double distance = Math.Abs((anchors[0].location - anchors[1].location).Length);
            jawPoints.Add("jaw_2", distance);

            //foreach (AnchorPoint pt in anchors)
            //{
            //    JArray pointArray = new JArray();
            //    pointArray.Add(pt.location.X);
            //    pointArray.Add(pt.location.Y);
            //    anchorPoints.Add(pt.orientation.ToString(), pointArray);
            //}
            //return anchorPoints;
            return jawPoints;
        }
    }
}
