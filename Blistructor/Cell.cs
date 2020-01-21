using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;
using Combinators;
using Conturer;

namespace Blistructor
{
    public class Cell
    {

        public int id;

        // States
        public bool removed = false;
        //public bool removable = false;
        //public bool final_anchor = false;
        //public bool possible_anchor = false;
        public double CornerDistance = 0;
        public double GuideDistance = 0;

        // Pill Stuff
        public NurbsCurve pill;
        public NurbsCurve pillOffset;
        public Point3d pillCenter;
        private AreaMassProperties pillProp;
        public NurbsCurve orientationCircle;

        // Connection and Adjacent Stuff
        public Curve voronoi;
        //!!connectionLines, proxLines, adjacentCells, samplePoints <- all same sizes, and order!!
        public List<Curve> connectionLines;
        public List<Curve> proxLines;
        public List<Cell> adjacentCells;
        public List<Point3d> samplePoints;
        public List<Curve> obstacles;

        public List<Curve> temp = new List<Curve>();
        // public List<Curve> temp2 = new List<Curve>();
        public List<CutData> cuttingData;
        public CutData bestCuttingData;
        // Int with best cutting index and new Blister for this cutting.

        public Cell(int _id, Curve _pill)
        {
            id = _id;
            // Prepare all needed Pill properties
            pill = _pill.ToNurbsCurve();
            pill = pill.Rebuild(pill.Points.Count, 3, true);
            CurveOrientation orient = pill.ClosedCurveOrientation(Vector3d.ZAxis);
            if (orient == CurveOrientation.Clockwise)
            {
                pill.Reverse();
            }
            pillProp = AreaMassProperties.Compute(pill);
            pillCenter = pillProp.Centroid;

            // Create pill offset
            Curve[] ofCur = pill.Offset(Plane.WorldXY, Setups.BladeWidth / 2, 0.001, CurveOffsetCornerStyle.Sharp);
            if (ofCur.Length == 1)
            {
                pillOffset = ofCur[0].ToNurbsCurve();
            }
            else
            {
                throw new InvalidOperationException();
            }
        }

        public double CoordinateIndicator
        {
            get
            {
                return pillCenter.X + pillCenter.Y * 10000;
            }
        }

        public List<LineCurve> GetTrimmedIsoRays()
        {
            List<LineCurve> output = new List<LineCurve>();
            foreach (CutData cData in cuttingData)
            {
                output.AddRange(cData.TrimmedIsoRays);
            }
            return output;
        }

        public List<PolylineCurve> GetPaths()
        {
            List<PolylineCurve> output = new List<PolylineCurve>();
            foreach (CutData cData in cuttingData)
            {
                output.Add(cData.Path);
            }
            return output;
        }

        public void SetDistance(LineCurve guideLine)
        {
            double t;
            guideLine.ClosestPoint(pillCenter, out t);
            GuideDistance = pillCenter.DistanceTo(guideLine.PointAt(t));
            //double distance_A = pillCenter.DistanceTo(guideLine.PointAtStart);
            //double distance_B = pillCenter.DistanceTo(guideLine.PointAtEnd);
            //CornerDistance = Math.Min(distance_A, distance_B);
            CornerDistance = pillCenter.DistanceTo(guideLine.PointAtStart);
        }

        public void AddConnectionData(List<Cell> cells, List<Curve> lines, List<Point3d> midPoints, Curve blister)
        {
            adjacentCells = new List<Cell>();
            samplePoints = new List<Point3d>();
            connectionLines = new List<Curve>();

            double circle_radius = pill.GetBoundingBox(false).Diagonal.Length / 2;
            orientationCircle = (new Circle(pillCenter, circle_radius)).ToNurbsCurve();
            Geometry.EditSeamBasedOnCurve(orientationCircle, blister);
            int[] ind = Geometry.SortPtsAlongCurve(midPoints, orientationCircle);

            foreach (int id in ind)
            {
                //samplePoints.Add(samples[id]);
                adjacentCells.Add(cells[id]);
                connectionLines.Add(lines[id]);
            }
            proxLines = new List<Curve>();
            foreach (Cell cell in adjacentCells)
            {
                Point3d ptA, ptB;
                if (pillOffset.ClosestPoints(cell.pillOffset, out ptA, out ptB))
                {
                    LineCurve proxLine = new LineCurve(ptA, ptB);
                    proxLines.Add(proxLine);
                    Point3d samplePoint = proxLine.PointAtNormalizedLength(0.5);
                    samplePoints.Add(samplePoint);
                }
            }
        }

        public void RemoveConnectionData()
        {
            for (int i = 0; i < adjacentCells.Count; i++)
            {
                adjacentCells[i].RemoveConnectionData(id);
            }
        }

        protected void RemoveConnectionData(int cellId)
        {
            for (int i = 0; i < adjacentCells.Count; i++)
            {
                if (adjacentCells[i].id == cellId)
                {
                    adjacentCells.RemoveAt(i);
                    connectionLines.RemoveAt(i);
                    proxLines.RemoveAt(i);
                    samplePoints.RemoveAt(i);
                    --i;
                }
            }
        }

        // Get ProxyLines without lines pointed as Id
        public List<Curve> GetUniqueProxy(int id)
        {
            List<Curve> proxyLines = new List<Curve>();
            for (int i = 0; i < adjacentCells.Count; i++)
            {
                if (adjacentCells[i].id != id)
                {
                    proxyLines.Add(proxLines[i]);
                }
            }
            return proxyLines;
        }

        public List<Curve> BuildObstacles()
        {
            List<Curve> limiters = new List<Curve>
            {
                pillOffset
            };
            for (int i = 0; i < adjacentCells.Count; i++)
            {
                limiters.Add(adjacentCells[i].pillOffset);

                List<Curve> prox = adjacentCells[i].GetUniqueProxy(id);
                foreach (Curve crv in prox)
                {
                    if (Geometry.CurveCurveIntersection(crv, proxLines).Count == 0)
                    {
                        limiters.Add(crv);
                    }
                }
            }
            return Geometry.RemoveDuplicateCurves(limiters);
        }

        public bool GenerateSimpleCuttingData(Curve blister)
        {
            // Initialise new Arrays
            obstacles = BuildObstacles();
            cuttingData = new List<CutData>();
            // Stage I - naive Cutting
            // Get cutting Directions

            List<LineCurve> isoLinesStage1 = GenerateIsoCurvesStage1(blister);
            if (isoLinesStage1.Count > 0)
            {
                // Check each Ray seperatly
                foreach (LineCurve ray in isoLinesStage1)
                {
                    //LineCurve t_ray = trimIsoCurve(ray, blister);
                    // temp.Add(t_ray);
                    //  if (t_ray != null){
                    CutData cData = VerifyPath(new PolylineCurve(new Point3d[] { ray.PointAtStart, ray.PointAtEnd }), blister);
                    if (cData != null)
                    {
                        cData.TrimmedIsoRays = new List<LineCurve> { ray };
                        cuttingData.Add(cData);
                    }
                    //  }
                }
                PolygonBuilder(isoLinesStage1, blister);
            }
            //if (cuttingData.Count > 0) return true;
            List<LineCurve> isoLinesStage2 = GenerateIsoCurvesStage2(blister);
            if (isoLinesStage2.Count > 0)
            {
                foreach (LineCurve ray in isoLinesStage2)
                {
                    //  LineCurve t_ray = trimIsoCurve(ray, blister);
                    //  temp.Add(t_ray);
                    //  if (t_ray != null){
                    CutData cData = VerifyPath(new PolylineCurve(new Point3d[] { ray.PointAtStart, ray.PointAtEnd }), blister);
                    if (cData != null)
                    {
                        cData.TrimmedIsoRays = new List<LineCurve>() { ray };
                        cuttingData.Add(cData);
                    }
                    //  }
                }
                PolygonBuilder(isoLinesStage2, blister);
            }
            //if (cuttingData.Count > 0) return true;
            List<LineCurve> isoLinesStage3 = GenerateIsoCurvesStage3(blister);
            if (isoLinesStage3.Count > 0)
            {
                foreach (LineCurve ray in isoLinesStage3)
                {
                    // LineCurve t_ray = trimIsoCurve(ray, blister);
                    // temp.Add(t_ray);
                    // if (t_ray != null){
                    CutData cData = VerifyPath(new PolylineCurve(new Point3d[] { ray.PointAtStart, ray.PointAtEnd }), blister);
                    if (cData != null)
                    {
                        cData.TrimmedIsoRays = new List<LineCurve> { ray };
                        cuttingData.Add(cData);
                    }
                    // }
                }
                PolygonBuilder(isoLinesStage3, blister);
            }
            if (cuttingData.Count > 0) return true;
            else return false;
        }

        public bool GenerateAdvancedCuttingData(Curve blister)
        {
            // If all Stages 1-3 failed, start looking more!
            if (cuttingData.Count == 0)
            {
                // if (cuttingData.Count == 0){
                List<List<LineCurve>> isoLinesStage4 = GenerateIsoCurvesStage4(60, Setups.IsoRadius, blister);
                if (isoLinesStage4.Count != 0)
                {
                    List<List<LineCurve>> RaysCombinations = (List<List<LineCurve>>)isoLinesStage4.CartesianProduct();
                    for (int i = 0; i < RaysCombinations.Count; i++)
                    {
                        if (RaysCombinations[i].Count > 0)
                        {
                            PolygonBuilder(RaysCombinations[i], blister);
                        }
                    }
                }
            }
            if (cuttingData.Count > 0) return true;
            else return false;
        }

        public void PolygonSelector()
        {
            cuttingData = cuttingData.OrderBy(x => x.GetCuttingLength).ToList();
            bestCuttingData = cuttingData[0];
        }

        public void PolygonSelector2()
        {
            //List<PolylineCurve> output = new List<PolylineCurve>();

            cuttingData.OrderBy(x => x.Count);
            // List<CutData> selected = cuttingData.Where(x => x.Count == cuttingData[0].Count).ToList();
            //foreach(CutData cData in selected){
            //  output.Add(cData.Path);
            // }
            // Here some more filtering, if more polygons hase same number of cutting segments...
            // Dummy get first...
            // if (selected.Count > 0){
            //   bestCuttingData = selected[0];
            // }
            // return output;
        }

        #region Polygon Builder Stuff

        private List<LineCurve> GenerateIsoCurvesStage1(Curve blister)
        {
            List<LineCurve> isoLines = new List<LineCurve>(samplePoints.Count);
            for (int i = 0; i < samplePoints.Count; i++)
            {
                Vector3d direction = Vector3d.CrossProduct((connectionLines[i].PointAtEnd - connectionLines[i].PointAtStart), Vector3d.ZAxis);
                LineCurve isoLine = Geometry.GetIsoLine(samplePoints[i], direction, Setups.IsoRadius, obstacles);
                if (isoLine != null)
                {
                    LineCurve t_ray = TrimIsoCurve(isoLine, samplePoints[i], blister);
                    if (t_ray != null)
                    {
                        isoLines.Add(t_ray);
                    }
                }
            }
            return isoLines;
        }

        private List<LineCurve> GenerateIsoCurvesStage2(Curve blister)
        {
            List<LineCurve> isoLines = new List<LineCurve>(samplePoints.Count);
            for (int i = 0; i < samplePoints.Count; i++)
            {
                Vector3d direction = Vector3d.CrossProduct((proxLines[i].PointAtEnd - proxLines[i].PointAtStart), Vector3d.ZAxis);
                LineCurve isoLine = Geometry.GetIsoLine(samplePoints[i], direction, Setups.IsoRadius, obstacles);
                if (isoLine != null)
                {
                    LineCurve t_ray = TrimIsoCurve(isoLine, samplePoints[i], blister);
                    if (t_ray != null)
                    {
                        isoLines.Add(t_ray);
                    }
                }
            }
            return isoLines;
        }

        private List<LineCurve> GenerateIsoCurvesStage3(Curve blister)
        {
            List<LineCurve> isoLines = new List<LineCurve>(samplePoints.Count);
            for (int i = 0; i < samplePoints.Count; i++)
            {
                Vector3d direction = Vector3d.CrossProduct((proxLines[i].PointAtEnd - proxLines[i].PointAtStart), Vector3d.ZAxis);
                Vector3d direction2 = Vector3d.CrossProduct((connectionLines[i].PointAtEnd - connectionLines[i].PointAtStart), Vector3d.ZAxis);

                LineCurve isoLine = Geometry.GetIsoLine(samplePoints[i], (direction + direction2), Setups.IsoRadius, obstacles);
                if (isoLine != null)
                {
                    LineCurve t_ray = TrimIsoCurve(isoLine, samplePoints[i], blister);
                    if (t_ray != null)
                    {
                        isoLines.Add(t_ray);
                    }
                }
            }
            return isoLines;
        }

        private List<List<LineCurve>> GenerateIsoCurvesStage4(int count, double radius, Curve blister)
        {
            // Obstacles need to be calculated or updated earlier
            List<List<LineCurve>> isoLines = new List<List<LineCurve>>();
            for (int i = 0; i < samplePoints.Count; i++)
            {
                Circle cir = new Circle(samplePoints[i], radius);
                List<LineCurve> iLines = new List<LineCurve>();
                ArcCurve arc = new ArcCurve(new Arc(cir, new Interval(0, Math.PI)));
                double[] t = arc.DivideByCount(count, false);
                for (int j = 0; j < t.Length; j++)
                {
                    Point3d Pt = arc.PointAt(t[j]);
                    LineCurve ray = Geometry.GetIsoLine(samplePoints[i], Pt - samplePoints[i], Setups.IsoRadius, obstacles);
                    if (ray != null)
                    {
                        LineCurve t_ray = TrimIsoCurve(ray, samplePoints[i], blister);
                        if (t_ray != null)
                        {
                            iLines.Add(t_ray);
                        }
                    }
                }
                isoLines.Add(iLines);
            }
            return isoLines;
        }

        private LineCurve TrimIsoCurve(LineCurve ray, Point3d samplePoint, Curve blister)
        {
            LineCurve outLine = null;
            if (ray != null)
            {
                Geometry.FlipIsoRays(orientationCircle, ray);
                Tuple<List<Curve>, List<Curve>> result = Geometry.TrimWithRegion(ray, blister);
                if (result.Item1.Count >= 1)
                {
                    foreach (Curve crv in result.Item1)
                    {
                        double t;
                        if (crv.ClosestPoint(samplePoint, out t, 0.1))
                        {
                            LineCurve line = (LineCurve)crv;
                            if (line != null)
                            {
                                //  flipIsoRays(orientationCircle, line);
                                outLine = line;
                            }
                        }
                    }
                }
            }
            return outLine;
        }
        /*
            private List<LineCurve> trimIsoCurves(List<LineCurve> rays, Curve blister){
              List<LineCurve> outLine = new List<LineCurve>(rays.Count);
              foreach (LineCurve ray in rays){
                LineCurve line = trimIsoCurve(ray, blister);
                if (line != null){
                  outLine.Add(line);
                }
              }
              return outLine;
            }
          */

        private void PolygonBuilder(List<LineCurve> rays, Curve blister)
        {
            //List<LineCurve> cutters = trimIsoCurves(rays, blister);
            //Generate Combinations array
            List<List<LineCurve>> combinations = Combinators.Combinators.UniqueCombinations(rays, 2);
            // Loop over combinations
            for (int combId = 0; combId < combinations.Count; combId++)
            {
                PolylineCurve curveToCheck = PathBuilder(combinations[combId]);
                if (curveToCheck != null)
                {
                    CutData cutData = VerifyPath(curveToCheck, blister);
                    if (cutData != null)
                    {
                        cutData.TrimmedIsoRays = combinations[combId];
                        cuttingData.Add(cutData);
                    }
                }
            }
        }

        private PolylineCurve PathBuilder(List<LineCurve> cutters)
        {
            PolylineCurve pLine = null;
            // If more curves, generate one!
            if (cutters.Count > 1)
            {
                // Performe intersectiona based on comination array.
                List<CurveIntersections> intersectionsData = new List<CurveIntersections>();
                for (int interId = 1; interId < cutters.Count; interId++)
                {
                    CurveIntersections inter = Intersection.CurveCurve(cutters[interId - 1], cutters[interId], Setups.IntersectionTolerance, Setups.OverlapTolerance);
                    // If no intersection, at any curve, break all testing process
                    if (inter.Count == 0)
                    {
                        break;
                    }
                    else
                    {
                        //If exist, Store it
                        intersectionsData.Add(inter);
                    }
                }
                // If intersection are equat to curveCount-1, this mean, all cuvre where involve.. so..
                if (intersectionsData.Count == cutters.Count - 1)
                {
                    //Create JoinedCure from all interesection data
                    List<Point3d> polyLinePoints = new List<Point3d>
                    {
                        cutters[0].PointAtStart
                    };
                    for (int i = 0; i < intersectionsData.Count; i++)
                    {
                        polyLinePoints.Add(intersectionsData[i][0].PointA);
                    }
                    polyLinePoints.Add(cutters[cutters.Count - 1].PointAtEnd);
                    pLine = new PolylineCurve(polyLinePoints);
                }
            }
            return pLine;
        }

        private CutData VerifyPath(PolylineCurve pCrv, Curve blister)
        {
            CutData data = null;
            if (pCrv != null)
            {
                // Check if curve is not self-intersecting
                CurveIntersections selfChecking = Intersection.CurveSelf(pCrv, Setups.IntersectionTolerance);
                if (selfChecking.Count == 0)
                {
                    // Check if this curve creates closed polygon with blister edge.
                    CurveIntersections blisterInter = Intersection.CurveCurve(pCrv, blister, Setups.IntersectionTolerance, Setups.OverlapTolerance);
                    // If both ends of Plyline cuts blister, it will create close polygon.
                    if (blisterInter.Count == 2)
                    {
                        // Get part of blister which is between Plyline ends.
                        double[] ts = new double[blisterInter.Count];
                        for (int i = 0; i < blisterInter.Count; i++)
                        {
                            ts[i] = blisterInter[i].ParameterB;
                        }
                        List<Curve> commonParts = new List<Curve>(2)
                        {
                            blister.Trim(ts[0], ts[1]),
                            blister.Trim(ts[1], ts[0])
                        };
                        // Look for shorter part.
                        List<Curve> blisterParts = commonParts.OrderBy(x => x.GetLength()).ToList();
                        //Curve common_part = commonParts.OrderBy(x => x.GetLength()).ToList()[0];
                        //temp.Add(common_part);
                        //Join Curve into closed polygon
                        Curve[] pCurve = Curve.JoinCurves(new Curve[2] { blisterParts[0], pCrv });
                        Polyline polygon = new Polyline();
                        if (pCurve.Length == 1)
                        {
                            pCurve[0].TryGetPolyline(out polygon);
                        }
                        // Check if polygon is closed and no. vertecies is bigger then 2
                        if (polygon.Count > 3 && polygon.IsClosed)
                        {
                            // Check if polygon is "surounding" Pill
                            PolylineCurve poly = polygon.ToPolylineCurve();
                            RegionContainment test = Curve.PlanarClosedCurveRelationship(poly, pill, Plane.WorldXY, 0.01);
                            if (test == RegionContainment.BInsideA)
                            {
                                // If yes, generate newBlister
                                Curve[] bCurve = Curve.JoinCurves(new Curve[2] { blisterParts[1], pCrv });
                                Polyline newBlister = new Polyline();
                                if (bCurve.Length == 1)
                                {
                                    bCurve[0].TryGetPolyline(out newBlister);
                                }
                                data = new CutData(polygon.ToPolylineCurve(), newBlister.ToPolylineCurve(), pCrv);
                            }
                        }
                    }
                }
            }
            return data;
        }
        #endregion

        public void GetInstructions()
        {
            //TO IMPLEMENT
        }

    }
}
