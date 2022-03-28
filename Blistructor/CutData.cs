using System;
using System.Collections.Generic;
using System.Linq;

#if PIXEL
using Pixel.Rhino.Geometry;
using ExtraMath = Pixel.Rhino.RhinoMath;
#else
using Rhino.Geometry;
using ExtraMath = Rhino.RhinoMath;
#endif

using Newtonsoft.Json.Linq;
using log4net;

namespace Blistructor
{
    public class CutData
    {
        private static readonly ILog log = LogManager.GetLogger("Cutter.CutData");
        private List<Line> isoSegments { set; get; }
        private List<Line> segments { set; get; }

        public List<PolylineCurve> BlisterLeftovers { private set; get; }
        public List<LineCurve> BladeFootPrint { private set; get; }
        public List<PolylineCurve> Path { private set; get; }
        public PolylineCurve Polygon { private set;  get; }
        public List<Curve> Obstacles { set; private get; }
        internal string UUID { private set;  get; }

        public CutData()
        {
            UUID = Guid.NewGuid().ToString();
            segments = new List<Line>();
            isoSegments = new List<Line>();
            BladeFootPrint = new List<LineCurve>();
        }

        private CutData(PolylineCurve polygon, List<PolylineCurve> path) : this()
        {
            this.Path = path;
            this.Polygon = polygon;
        }

        public CutData(PolylineCurve polygon, List<PolylineCurve> path, PolylineCurve blisterLeftover) : this(polygon, path)
        {
            BlisterLeftovers = new List<PolylineCurve>() { blisterLeftover };
            //GenerateBladeFootPrint();
        }

        public CutData(PolylineCurve polygon, List<PolylineCurve> path, List<PolylineCurve> blisterLeftovers) : this(polygon, path)
        {
            BlisterLeftovers = blisterLeftovers;
            // GenerateBladeFootPrint();
        }

        public int EstimatedCuttingCount
        {
            get
            {
                int count = 0;
                foreach (PolylineCurve pline in Path)
                {
                    foreach (Line line in pline.ToPolyline().GetSegments())
                    {
                        count += GetCuttingPartsCount(line);
                    }
                }
                return count;
            }
        }

        public int RealCuttingCount
        {
            get
            {
                if (BladeFootPrint == null) return -1;
                return BladeFootPrint.Count;
            }
        }

        public int Count
        {
            get
            {
                return Polygon.PointCount - 1;
            }
        }

        public List<LineCurve> IsoSegments
        {
            get { return isoSegments.Select(line => new LineCurve(line)).ToList(); }
            set { isoSegments = value.Select(x => x.Line).ToList(); }
        }

        public List<LineCurve> Segments { get { return segments.Select(line => new LineCurve(line)).ToList(); } }


        public bool GenerateBladeFootPrint()
        {
            if (isoSegments == null || segments == null) return false;
            if (!GenerateSegments()) return false;
            //  log.Info("Data are ok.");
            // Loop by all paths and generate Segments and IsoSegments
            for (int i = 0; i < segments.Count; i++)
            {
                List<LineCurve> footPrint = GetKnifeprintPerSegment(segments[i], isoSegments[i]);
                if (footPrint.Count == 0) return false;
                BladeFootPrint.AddRange(footPrint);
            }
            log.Info(String.Format("Generated {0} Blade Footprints.", BladeFootPrint.Count));
            return true;
        }

        public bool GenerateSegments()
        {
            if (Path == null) return false;
            // Loop by all paths and generate Segments
            segments = new List<Line>();
            foreach (PolylineCurve pline in Path)
            {
                foreach (Line ln in pline.ToPolyline().GetSegments())
                {
                    segments.Add(ln);
                }
            }
            return true;
        }

        public bool RecalculateIsoSegments(Curve orientationGuideCurve)
        {
            if (Polygon == null || Path == null) return false;
            //  log.Info("Data are ok.");
            // Loop by all paths and generate Segments and IsoSegments
            segments = new List<Line>();
            isoSegments = new List<Line>();
            foreach (PolylineCurve pline in Path)
            {
                foreach (Line ln in pline.ToPolyline().GetSegments())
                {
                    segments.Add(ln);
                    LineCurve cIsoLn = Geometry.GetIsoLine(ln.PointAt(0.5), ln.UnitTangent, Setups.IsoRadius, Obstacles);
                    if (cIsoLn == null) return false;
                    Geometry.FlipIsoRays(orientationGuideCurve, cIsoLn);
                    Line isoLn = cIsoLn.Line;
                    if (isoLn == null) throw new InvalidOperationException("Computing IsoSegment failed during BladeFootPrint Generation.");
                    isoSegments.Add(isoLn);
                }
            }
            return true;
        }

        public List<LineCurve> GetKnifeprintPerSegment(Line segment, Line isoSegment)
        {
            List<Point3d> knifePts = new List<Point3d>();
            List<LineCurve> knifeLines = new List<LineCurve>();
            int cutCount = GetCuttingPartsCount(segment);
            // Add Knife tolerance
            segment.Extend(Setups.BladeTol, Setups.BladeTol);

            List<double> lineT = new List<double>() { 0.0, 1.0 };
            int segmentSide = -1; // id 0 -> From side is out, 1 -> To side is out, -1 -> none is out.

            foreach (double t in lineT)
            {
                Point3d exSegmentPt = segment.PointAt(t);
                // Check if extended point is still on isoSegment line.
                Point3d testPt = isoSegment.ClosestPoint(exSegmentPt, true);
                // if (testPt.DistanceTo(exSegmentPt) > Setups.GeneralTolerance) return knifeLines;
                // Check if any side of the IsoSegment is out of blister...
                double dist = exSegmentPt.DistanceTo(isoSegment.PointAt(t));
                if (dist > Setups.IsoRadius / 2) segmentSide = (int)t;
            }

            if (segmentSide == 0)
            {
                segment.Flip();
                isoSegment.Flip();
            }

            // If Middle
            if (segmentSide == -1)
            {
                // If only one segment
                if (cutCount == 1)
                {
                    Point3d cutStartPt = segment.From;
                    Point3d cutEndPt = cutStartPt + (segment.UnitTangent * Setups.BladeLength);
                    LineCurve cutPrint = new LineCurve(cutStartPt, cutEndPt);
                    Point3d testPt = isoSegment.ClosestPoint(cutEndPt, true);
                    double endDist = testPt.DistanceTo(cutEndPt);
                    log.Debug(String.Format("EndPointDist{0}", endDist));
                    // Check if CutPrint is not out of isoSegment, if not thak it as blase posotion
                    if (endDist < Setups.GeneralTolerance) knifeLines.Add(new LineCurve(cutStartPt, cutEndPt));
                    // Blade posidion os out of possible location, apply fix, by moving it to the center.   
                    else
                    {
                        // knifeLines.Add(new LineCurve(cutStartPt, cutEndPt));
                        double startdDist = segment.From.DistanceTo(isoSegment.From);
                        double diffDist = startdDist - endDist; // This should be positive...
                        Vector3d translateVector = -segment.UnitTangent * (endDist + (diffDist / 2));
                        cutPrint.Translate(translateVector);
                        knifeLines.Add(new LineCurve(cutPrint));
                    }

                }
                //if more segments, try to distribute them evenly alogn isoSegment
                else if (cutCount > 1)
                {
                    // SHot segment by half of the blade on both sides.
                    segment.Extend(-Setups.BladeLength / 2, -Setups.BladeLength / 2);
                    LineCurve exSegment = new LineCurve(segment);
                    // Divide segment by parts
                    double[] divT = exSegment.DivideByCount(cutCount - 1, true);
                    knifePts.AddRange(divT.Select(t => exSegment.PointAt(t)).ToList());
                    // Add bladePrints
                    knifeLines.AddRange(knifePts.Select(pt => new LineCurve(pt - (segment.UnitTangent * Setups.BladeLength / 2), pt + (segment.UnitTangent * Setups.BladeLength / 2))).ToList());
                }
            }
            // If not Middle (assumprion, IsoSegments are very long on one side. No checking for coverage.
            else
            {
                Point3d cutStartPt = segment.From;
                for (int j = 0; j < cutCount; j++)
                {
                    Point3d cutEndPt = cutStartPt + (segment.UnitTangent * Setups.BladeLength);
                    Line cutPrint = new Line(cutStartPt, cutEndPt);
                    knifeLines.Add(new LineCurve(cutPrint));
                    cutStartPt = cutEndPt - (segment.UnitTangent * Setups.BladeTol);
                }
            }
            return knifeLines;

        }

        private int GetCuttingPartsCount(Line line)
        {
            return (int)Math.Ceiling(line.Length / (Setups.BladeLength - (2 * Setups.BladeTol)));
        }

        public double GetArea()
        {
            return Polygon.Area();
        }

        public double GetPerimeter()
        {
            return Polygon.GetLength();
        }

        /// <summary>
        /// Get last position of blade.
        /// </summary>
        /// <returns>Point3d. Point3d(NaN,NaN,NaN) if there is no cutting data. </returns>
        public Point3d GetLastKnifePossition()
        {
            if (BladeFootPrint == null) return new Point3d(double.NaN, double.NaN, double.NaN);
            if (BladeFootPrint.Count == 0) return new Point3d(double.NaN, double.NaN, double.NaN);
            return BladeFootPrint.Last().PointAtNormalizedLength(0.5);
        }

        private double CalculateAngle(LineCurve line)
        {
            // Tu moze byc potrzeba zmiany vectora z X na Y w zalzenosci gdzie jest 0 stopni noża
            // YAxiz z wizaku z tym ze nastepuje zmiana koordynatów z X na y przy przejsciu z trybu PICK na WORK...
            Vector3d lineVector = line.Line.UnitTangent;
            lineVector.Y = -lineVector.Y;

            Vector3d baseVector = Vector3d.YAxis;
            if (Setups.BladeRotationAxis == "Y") baseVector = Vector3d.XAxis;

            double angle = Vector3d.VectorAngle(baseVector, lineVector, Plane.WorldXY) + Setups.BladeRotationCalibration;
            double angleDegree = ExtraMath.ToDegrees(angle);
            angleDegree = angleDegree > 360 ? angleDegree - 360 : angleDegree;
            angleDegree = angleDegree > 180 ? angleDegree - 180 : angleDegree;
            return angleDegree;
        }

        #region JSON
        public JObject GetDisplayJSON(Point3d Jaw1_Local)
        {
            JObject displayData = new JObject();
            if (BladeFootPrint.Count == 0) return displayData;
            JArray cutLineDisplayData = new JArray();
            // CutLine stuff
            foreach (LineCurve line in BladeFootPrint)
            {
                LineCurve imageline = (LineCurve)Geometry.ReverseCalibration(line, Setups.ZeroPosition, Setups.PixelSpacing, Setups.CartesianPickModeAngle);
                //Display Part
                JObject localCutLineDisplayData = new JObject();
                JArray startPointArray = new JArray() { imageline.PointAtStart.X, imageline.PointAtStart.Y };
                JArray endPointArray = new JArray() { imageline.PointAtEnd.X, imageline.PointAtEnd.Y };
                Point3d globalMidPt = CoordinateSystem.GlobalCutCoordinates(line.Line.PointAt(0.5), Jaw1_Local);
                JArray midPointArray = new JArray() { globalMidPt.X, globalMidPt.Y };
                localCutLineDisplayData.Add("cutLine", new JArray() { startPointArray, endPointArray });
                localCutLineDisplayData.Add("midPoint", midPointArray);
                localCutLineDisplayData.Add("angle", CalculateAngle(line));
                cutLineDisplayData.Add(localCutLineDisplayData);
            }
            displayData.Add("cutLines", cutLineDisplayData);
            // Polygon stuff
            BoundingBox bbox = this.Polygon.GetBoundingBox(false);
            Point3d minBBoxPoint = ((Point)Geometry.ReverseCalibration(new Point(bbox.Min), Setups.ZeroPosition, Setups.PixelSpacing, Setups.CartesianPickModeAngle)).Location;
            Point3d maxBBoxPoint = ((Point)Geometry.ReverseCalibration(new Point(bbox.Max), Setups.ZeroPosition, Setups.PixelSpacing, Setups.CartesianPickModeAngle)).Location;

            JObject bboxData = new JObject();
            bboxData.Add("min", new JArray() { minBBoxPoint.X, minBBoxPoint.Y });
            bboxData.Add("max", new JArray() { maxBBoxPoint.X, maxBBoxPoint.Y });
            bboxData.Add("diag", new JArray() { bbox.Diagonal.X, bbox.Diagonal.Y });
            displayData.Add("bbox", bboxData);
            PolylineCurve polygon = (PolylineCurve)Geometry.ReverseCalibration(this.Polygon, Setups.ZeroPosition, Setups.PixelSpacing, Setups.CartesianPickModeAngle);
            JArray polygonDisplayData = new JArray();
            foreach (Point3d pt in polygon.ToPolyline())
            {
                polygonDisplayData.Add(new JArray() { pt.X, pt.Y });
            }
            displayData.Add("polygon", polygonDisplayData);
            //PATH
            JArray pathDisplayData = new JArray();
            foreach (LineCurve line in this.Segments)
            {
                double length = line.GetLength();
                LineCurve imageLine = (LineCurve)Geometry.ReverseCalibration(line, Setups.ZeroPosition, Setups.PixelSpacing, Setups.CartesianPickModeAngle);
                JObject segmentData = new JObject();
                segmentData.Add("length", length);
                segmentData.Add("line", new JArray() {
                    new JArray() { imageLine.PointAtStart.X, imageLine.PointAtStart.Y },
                    new JArray() { imageLine.PointAtEnd.X, imageLine.PointAtEnd.Y }
                });
                pathDisplayData.Add(segmentData);
            }
            displayData.Add("segments", pathDisplayData);
            // BlisterLeftOvers
            JArray leftOverDisplayData = new JArray();
            foreach (PolylineCurve pline in this.BlisterLeftovers) {
                PolylineCurve imagePline = (PolylineCurve)Geometry.ReverseCalibration(pline, Setups.ZeroPosition, Setups.PixelSpacing, Setups.CartesianPickModeAngle);
                JArray leftOverPolygon = new JArray();
                foreach (Point3d pt in imagePline.ToPolyline())
                {
                    leftOverPolygon.Add(new JArray() { pt.X, pt.Y });
                }
                leftOverDisplayData.Add(leftOverPolygon);
            }
            displayData.Add("leftOver", leftOverDisplayData);
            return displayData;
        }

        public JArray GetJSON(Point3d Jaw1_Local)
        {
            //JObject totalData = new JObject();
            JArray instructionsArray = new JArray();
            JArray displayArray = new JArray();
            if (BladeFootPrint.Count == 0) return instructionsArray;
            foreach (LineCurve line in BladeFootPrint)
            {
                //Angle
                JObject cutData = new JObject();
                cutData.Add("angle", CalculateAngle(line));
                //Point 
                JArray pointArray = new JArray();
                // Apply transformation to global
                Point3d globalMidPt = CoordinateSystem.GlobalCutCoordinates(line.Line.PointAt(0.5), Jaw1_Local);
                // X i Y zamienione już GlobalCutCoordinates...
                pointArray.Add(globalMidPt.X);
                pointArray.Add(globalMidPt.Y);
                cutData.Add("point", pointArray);
                instructionsArray.Add(cutData);
            }
            return instructionsArray;
        }
        #endregion
    }

}
