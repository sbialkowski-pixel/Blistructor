using System;
using System.Collections.Generic;
using System.Linq;

using Rhino.Geometry;
using log4net;
using Newtonsoft.Json.Linq;

namespace Blistructor
{
    public class Anchor
    {
        private static readonly ILog log = LogManager.GetLogger("Blistructor.Anchor");

        public LineCurve cartesianLimitLine;

        public MultiBlister mBlister;
        public PolylineCurve mainOutline;
        public PolylineCurve aaBBox;
        public PolylineCurve maBBox;
        public LineCurve GuideLine;
        //public LineCurve aaUpperLimitLine;
        //public LineCurve maGuideLine;
        //public LineCurve maUpperLimitLine;

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

            // Create initial predition Line
            LineCurve fullPredLine = new LineCurve(GuideLine);
            fullPredLine.Translate(Vector3d.YAxis * Setups.CartesianDepth / 2);

            // Find limits based on Blister Shape
            double[] paramT = GuideLine.DivideByCount(50, true);
            List<double> limitedParamT = new List<double>(paramT.Length);
            foreach (double t in paramT)
            {
                double parT;
                if (mainOutline.ClosestPoint(GuideLine.PointAt(t), out parT, Setups.CartesianDepth / 2)) limitedParamT.Add(parT);
            }
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
            fullPredLine.Translate(Vector3d.YAxis * Setups.CartesianDepth / 2);
            // NOTE: Check intersection with pills (Or maybe with pillsOffset. Rethink problem)
            Tuple<List<Curve>, List<Curve>> trimResult = Geometry.TrimWithRegions(fullPredLine, mBlister.Queue[0].GetPills(false));
            // Gather all parts outsite (not in pills) shrink curve on both sides by half of Grasper width and move it back to mid position 
            foreach (Curve crv in trimResult.Item2)
            {
                // Shrink pieces on both sides by half of Grasper width.
                Line ln = ((LineCurve)crv).Line;
                if (ln.Length < Setups.CartesianThickness) continue;
                ln.Extend(-Setups.CartesianThickness / 2, -Setups.CartesianThickness / 2);
                LineCurve cln = new LineCurve(ln);
                //move it back to mid position
                cln.Translate(Vector3d.YAxis * -Setups.CartesianDepth / 2);
                // Gather 
                GrasperPossibleLocation.Add(cln);
            }

            anchors = GetJawsPoints();
            log.Info(String.Format("Anchors found: {0}", anchors.Count));

        }

        //  public void Update

        public List<AnchorPoint> GetJawsPoints()
        {
            List<AnchorPoint> jawPoints = new List<AnchorPoint>();
            List<AnchorPoint> extremePoints = GetExtremePoints();
            log.Info(String.Format("extremePoints found: {0}", extremePoints.Count));
            if (extremePoints == null) return jawPoints;
            Line spectrumLine = new Line(extremePoints[0].location, extremePoints[1].location);
            if (spectrumLine.Length < Setups.CartesianMaxWidth && spectrumLine.Length > Setups.CartesianMinWidth)
            {
                return extremePoints;
            }
            else if (spectrumLine.Length > Setups.CartesianMaxWidth)
            {
                log.Info("spectrumLine.Length > Setups.CartesianMaxWidth");

                Point3d midPoint = spectrumLine.PointAt(0.5);
                NurbsCurve maxJawsCircle = (new Circle(midPoint, Setups.CartesianMaxWidth / 2)).ToNurbsCurve();
                //  List<Curve> trimResult = new List<Curve>();

                Tuple<List<Curve>, List<Curve>> trimResult = Geometry.TrimWithRegion(GrasperPossibleLocation.Select(crv => (Curve)crv).ToList(), maxJawsCircle);
                /*
               foreach (LineCurve ln in GrasperPossibleLocation)
               {
                   Tuple<List<Curve>, List<Curve>> result = Geometry.TrimWithRegion(ln, maxJawsCircle);
                   if (result.Item1.Count > 0) trimResult.AddRange(result.Item1);
               }
               */

                if (trimResult.Item1.Count == 0)
                {
                    return jawPoints;
                }
                List<Point3d> toEvaluate = new List<Point3d>(trimResult.Item1.Count);
                foreach (Curve ln in trimResult.Item1)
                {
                    Point3d pt, pt2;
                    ln.ClosestPoints(maxJawsCircle, out pt, out pt2);
                    toEvaluate.Add(pt);
                }
                toEvaluate = toEvaluate.OrderBy(pt => pt.X).ToList();
                Point3d leftJaw = toEvaluate.First();
                Point3d rightJaw = toEvaluate.Last();
                //TODO: leftJaw i rightJaw maja takie same cords....
                log.Info(leftJaw.ToString());
                log.Info(rightJaw.ToString());
                if (leftJaw.DistanceTo(rightJaw) < Setups.CartesianMinWidth)
                {
                    return jawPoints;
                }
                return new List<AnchorPoint>() {
                        new AnchorPoint(leftJaw, AnchorSite.Left),
                        new AnchorPoint(rightJaw, AnchorSite.Right)
                    };
            }
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
                        new AnchorPoint(leftJaw, AnchorSite.Left),
                        new AnchorPoint(rightJaw, AnchorSite.Right)
                    };
        }

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


        public void Update(Blister cuttedBlister)
        {
            Update(null, cuttedBlister.Cells[0].bestCuttingData.Polygon);
        }

        public void Update(PolylineCurve path, PolylineCurve polygon)
        {
            if (polygon != null)
            {
                // Simplify polygon
                Curve simplePolygon = polygon;
                simplePolygon.RemoveShortSegments(Setups.CollapseTolerance);

                Curve[] offset = simplePolygon.Offset(Plane.WorldXY, Setups.CartesianThickness / 2, Setups.GeneralTolerance, CurveOffsetCornerStyle.Sharp);
                if (offset.Length > 0)
                {
                    //    log.Warn(String.Format("Anchor Pred Line Update - Polygon Oreint {0}", polygon.ClosedCurveOrientation()));
                    //log.Warn(String.Format("Anchor - Update Pred Line  - SimpOffset Len {0} | Polygon Len {1}", simpleOffset.GetLength(), polygon.GetLength()));

                    Tuple<List<Curve>, List<Curve>> result = Geometry.TrimWithRegion(GrasperPossibleLocation.Select(crv => (Curve)crv).ToList(), offset[0]);
                    GrasperPossibleLocation = result.Item2.Select(crv => (LineCurve)crv).ToList();
                }
                else
                {
                    log.Warn("Anchor Pred Line Update not possible. Offset return no result for polygon.");
                }
            }

            if (path != null)
            {
                PolylineCurve pathOutline = Geometry.PolylineThicken(path, Setups.BladeWidth / 2 + Setups.CartesianThickness / 2);
                Tuple<List<Curve>, List<Curve>> result = Geometry.TrimWithRegion(GrasperPossibleLocation.Select(crv => (Curve)crv).ToList(), pathOutline);
                GrasperPossibleLocation = result.Item2.Select(crv => (LineCurve)crv).ToList();
            }

        }

        public void FindNewAnchorAndApplyOnBlister(Blister cuttedBlister)
        {
            Update(cuttedBlister);
            anchors = GetJawsPoints();
            ApplyAnchorOnBlister();
        }

        public bool IsBlisterStraight(double maxDeviation)
        {
            AreaMassProperties aaProp = AreaMassProperties.Compute(aaBBox);
            AreaMassProperties maProp = AreaMassProperties.Compute(maBBox);
            if ((aaProp.Area / maProp.Area) > maxDeviation) return false;
            else return true;
        }

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
            Point3d blisterCS = new Point3d(Setups.BlisterGlobalX, Setups.BlisterGlobalY, 0);
            Vector3d pivotJaw = new Vector3d(Setups.CartesianPivotJawVectorX, Setups.CartesianPivotJawVectorY, 0);
            foreach (AnchorPoint pt in anchors)
            {
                //NOTE: Zamiana X, Y, należy sprawdzić czy to jest napewno dobrze. Wg. moich danych i opracowanej logiki tak...
                Point3d flipedPoint = new Point3d(anchors[0].location.Y, anchors[0].location.X, 0);
                Point3d globalJawLocation = CartesianGlobalJaw1L(flipedPoint, blisterCS, Setups.CartesianPickModeAngle, pivotJaw);
                GlobalAnchors.Add(globalJawLocation);
            }
        }

    public JObject GetJSON()
        {
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
            double distance = (anchors[0].location - anchors[1].location).Length;
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
