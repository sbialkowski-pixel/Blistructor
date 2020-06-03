using System;
using System.Collections.Generic;
using System.Linq;

//using Rhino.Geometry;
using Pixel.Geometry;

using Newtonsoft.Json.Linq;
using log4net;

namespace Blistructor
{
    public class CutData
    {
        private static readonly ILog log = LogManager.GetLogger("Blistructor.CutData");
        private List<PolylineCurve> path;
        private PolylineCurve polygon;
        public List<PolylineCurve> BlisterLeftovers;
        public List<LineCurve> bladeFootPrint;
        // public List<LineCurve> bladeFootPrint2;
        public List<Curve> obstacles;
        public List<Line> isoSegments;
        public List<Line> segments;

        public CutData()
        {
            segments = new List<Line>();
            isoSegments = new List<Line>();
            bladeFootPrint = new List<LineCurve>();
        }

        private CutData(PolylineCurve polygon, List<PolylineCurve> path) : this()
        {
            this.path = path;
            this.polygon = polygon;
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

        public List<PolylineCurve> Path { get { return path; } }

        public PolylineCurve Polygon { get { return polygon; } }

        public List<Curve> Obstacles { set { obstacles = value; } }

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
                if (bladeFootPrint == null) return -1;
                return bladeFootPrint.Count;
            }
        }

        public int Count
        {
            get
            {
                return polygon.PointCount - 1;
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
                bladeFootPrint.AddRange(footPrint);
            }
            log.Info(String.Format("Generated {0} Blade Footpronts.", bladeFootPrint.Count));
            return true;
        }

        public bool GenerateSegments()
        {
            if (path == null) return false;
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
            if (polygon == null || path == null) return false;
            //  log.Info("Data are ok.");
            // Loop by all paths and generate Segments and IsoSegments
            segments = new List<Line>();
            isoSegments = new List<Line>();
            foreach (PolylineCurve pline in Path)
            {
                foreach (Line ln in pline.ToPolyline().GetSegments())
                {
                    segments.Add(ln);
                    LineCurve cIsoLn = Geometry.GetIsoLine(ln.PointAt(0.5), ln.UnitTangent, Setups.IsoRadius, obstacles);
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
                    log.Info(String.Format("EndPointDist{0}", endDist));
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
            AreaMassProperties prop = AreaMassProperties.Compute(polygon);
            return prop.Area;
        }

        public double GetPerimeter()
        {
            return polygon.GetLength();
        }

        /// <summary>
        /// Get last possition of blade.
        /// </summary>
        /// <returns>Point3d. Point3d(NaN,NaN,NaN) if there is no cutting data. </returns>
        public Point3d GetLastKnifePossition()
        {
            if (bladeFootPrint == null) return new Point3d(double.NaN, double.NaN, double.NaN);
            if (bladeFootPrint.Count == 0) return new Point3d(double.NaN, double.NaN, double.NaN);
            return bladeFootPrint.Last().PointAtNormalizedLength(0.5);
        }

        public Point3d GlobalCutCoordinates(Point3d localCoordinates, Point3d Jaw1_Local)
        {
            // A = (Point3d)KnifeCenterG - CutL + JawL;
            Point3d knifeCenter = new Point3d(Setups.BladeGlobalX, Setups.BladeGlobalY, 0);
            //NOTE: Zamiana X, Y, należy sprawdzić czy to jest napewno dobrze. Wg. moich danych i opracowanej logiki tak...
            Point3d flipedLocalCoordinates = new Point3d(localCoordinates.Y, localCoordinates.X, 0);
            Point3d fliped_Jaw1 = new Point3d(0 , Jaw1_Local.X, 0);
            return (Point3d)knifeCenter - flipedLocalCoordinates + fliped_Jaw1;                                                                    
        }

        public JArray GetJSON(Point3d Jaw1_Local)
        {
            JArray instructionsArray = new JArray();
            if (bladeFootPrint.Count == 0) return instructionsArray;
            foreach (LineCurve line in bladeFootPrint)
            {
                //Angle
                JObject cutData = new JObject();
                // TODO: Tu moze byc potrzeba zmiany vectora z X na Y w zalzenosci gdzie jest 0 stopni noża
                // YAxiz z wizaku z tym ze nastepuje zmiana koordynatów z X na y przy przejsciu z trybu PICK na WORK...
                Vector3d lineVector = line.Line.UnitTangent;
                lineVector.Y = -lineVector.Y;
                double angle = Vector3d.VectorAngle(Vector3d.XAxis, lineVector, Plane.WorldXY)+ Setups.BladeRotationCalibration;
                cutData.Add("angle", Rhino.RhinoMath.ToDegrees(angle));
                //Point 
                JArray pointArray = new JArray();
                // Apply transformation to global
                Point3d globalMidPt = GlobalCutCoordinates(line.Line.PointAt(0.5), Jaw1_Local);
                // X i Y zamienione już GlobalCutCoordinates...
                pointArray.Add(globalMidPt.X);
                pointArray.Add(globalMidPt.Y);
                cutData.Add("point", pointArray);
                instructionsArray.Add(cutData);
            }
            return instructionsArray;
        }
    }

}
