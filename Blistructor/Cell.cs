using System;
using System.Collections.Generic;
using System.Linq;

#if PIXEL
using Pixel;
using Pixel.Geometry;
using Pixel.Geometry.Intersect;
using ExtraMath = Pixel.PixelMath;
#else
using Rhino;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;
using ExtraMath = Rhino.RhinoMath;
#endif

using log4net;
using Newtonsoft.Json.Linq;

using Combinators;

namespace Blistructor
{
    public class Cell
    {
        private static readonly ILog log = LogManager.GetLogger("Blistructor.Cell");

        public int id;

        // Parent Blister
        private Blister blister;

        // States
        private CellState state = CellState.Queue;
        public AnchorPoint Anchor;
        //public double CornerDistance = 0;
        //public double GuideDistance = 0;

        // Pill Stuff
        public PolylineCurve pill;
        public PolylineCurve pillOffset;

        private AreaMassProperties pillProp;

        // Connection and Adjacent Stuff
        public Curve voronoi;
        //!!connectionLines, proxLines, adjacentCells, samplePoints <- all same sizes, and order!!
        public List<Curve> connectionLines;
        public List<Curve> proxLines;
        public List<Cell> adjacentCells;
        public List<Point3d> samplePoints;

        public List<Curve> obstacles;

        //public List<Curve> temp = new List<Curve>();
        // public List<Curve> temp2 = new List<Curve>();
        public List<CutData> cuttingData;
        public CutData bestCuttingData;
        // Int with best cutting index and new Blister for this cutting.

        public Cell(int _id, PolylineCurve _pill, Blister _blister)
        {
            id = _id;
            blister = _blister;
            // Prepare all needed Pill properties
            pill = _pill;
            // Make Pill curve oriented in proper direction.
            Geometry.UnifyCurve(pill);

            Anchor = new AnchorPoint();

            pillProp = AreaMassProperties.Compute(pill);

            // Create pill offset
            Curve ofCur = pill.Offset(Plane.WorldXY, Setups.BladeWidth / 2);
            if (ofCur == null)
            {
                log.Error("Incorrect pill offseting");
                throw new InvalidOperationException("Incorrect pill offseting");
            }
            else 
            {
                pillOffset = (PolylineCurve)ofCur;
            }
        }

        #region PROPERTIES
        public Point3d PillCenter
        {
            get { return pillProp.Centroid; }
        }

        public Blister Blister
        {
            set
            {
                blister = value;
                EstimateOrientationCircle();
                SortData();
            }
        }

        public double CoordinateIndicator
        {
            get
            {
                return PillCenter.X + PillCenter.Y * 100;
            }
        }

        public NurbsCurve OrientationCircle { get; private set; }

        public CellState State { get; set; }

        #endregion
        /*
        public List<LineCurve> GetTrimmedIsoRays()
        {
            List<LineCurve> output = new List<LineCurve>();
            foreach (CutData cData in cuttingData)
            {
                output.AddRange(cData.TrimmedIsoRays);
            }
            return output;
        }
        */

        public List<PolylineCurve> GetPaths()
        {
            List<PolylineCurve> output = new List<PolylineCurve>();
            foreach (CutData cData in cuttingData)
            {
                output.AddRange(cData.Path);
            }
            return output;
        }

        #region GENERAL MANAGE

        #region DISTANCES
        public double GetDirectionIndicator(Point3d pt)
        {
            Vector3d vec = pt - this.PillCenter;
            return Math.Abs(vec.X) + Math.Abs(vec.Y) * 100;
        }
        public double GetDistance(Point3d pt)
        {
            return pt.DistanceTo(this.PillCenter);
        }
        public double GetClosestDistance(List<Point3d> pts)
        {
            PointCloud ptC = new PointCloud(pts);
            int closestIndex = ptC.ClosestPoint(this.PillCenter);
            return this.PillCenter.DistanceTo(pts[closestIndex]);
        }
        public double GetDistance(Curve crv)
        {
            double t;
            crv.ClosestPoint(this.PillCenter, out t);
            return crv.PointAt(t).DistanceTo(this.PillCenter);
        }
        public double GetClosestDistance(List<Curve> crvs)
        {
            List<Point3d> ptc = new List<Point3d>();
            foreach (Curve crv in crvs)
            {
                double t;
                crv.ClosestPoint(this.PillCenter, out t);
                ptc.Add(crv.PointAt(t));
            }
            return GetClosestDistance(ptc);
        }

        #endregion

        /*
        public void SetDistance(LineCurve guideLine)
        {
            double t;
            guideLine.ClosestPoint(PillCenter, out t);
            GuideDistance = PillCenter.DistanceTo(guideLine.PointAt(t));
            double distance_A = PillCenter.DistanceTo(guideLine.PointAtStart);
            double distance_B = PillCenter.DistanceTo(guideLine.PointAtEnd);
            //Rhino.RhinoApp.WriteLine(String.Format("Dist_A: {0}, Dist_B: {1}", distance_A, distance_B));
            CornerDistance = Math.Min(distance_A, distance_B);

            //CornerDistance = distance_A + distance_B;
            //CornerDistance = pillCenter.DistanceTo(guideLine.PointAtStart);
        }
        */

        public void AddConnectionData(List<Cell> cells, List<Curve> lines, List<Point3d> midPoints)
        {
            adjacentCells = new List<Cell>();
            samplePoints = new List<Point3d>();
            connectionLines = new List<Curve>();

            EstimateOrientationCircle();
            int[] ind = Geometry.SortPtsAlongCurve(midPoints, OrientationCircle);

            foreach (int id in ind)
            {
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

        /// <summary>
        /// Call from adjacent cell
        /// </summary>
        /// <param name="cellId">ID of Cell which is executing this method</param>
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
                    i--;
                }
            }
            SortData();
        }

        public List<int> GetAdjacentCellsIds()
        {
            return adjacentCells.Select(cell => cell.id).ToList();
        }
        private void EstimateOrientationCircle()
        {
            double circle_radius = pill.GetBoundingBox(false).Diagonal.Length / 2;
            OrientationCircle = (new Circle(PillCenter, circle_radius)).ToNurbsCurve();
            Geometry.EditSeamBasedOnCurve(OrientationCircle, blister.Outline);
        }

        private void SortData()
        {
            EstimateOrientationCircle();
            int[] sortingIndexes = Geometry.SortPtsAlongCurve(samplePoints, OrientationCircle);

            samplePoints = sortingIndexes.Select(index => samplePoints[index]).ToList();
            connectionLines = sortingIndexes.Select(index => connectionLines[index]).ToList();
            proxLines = sortingIndexes.Select(index => proxLines[index]).ToList();
            adjacentCells = sortingIndexes.Select(index => adjacentCells[index]).ToList();

            //samplePoints = samplePoints.Zip(sortingIndexes, Tuple.Create).OrderBy(i => i.Item2).Select(i => i.Item1).ToList();
            //connectionLines = connectionLines.Zip(sortingIndexes, Tuple.Create).OrderBy(i => i.Item2).Select(i => i.Item1).ToList();
            // proxLines = proxLines.Zip(sortingIndexes, Tuple.Create).OrderBy(i => i.Item2).Select(i => i.Item1).ToList();
            // adjacentCells = adjacentCells.Zip(sortingIndexes, Tuple.Create).OrderBy(i => i.Item2).Select(i => i.Item1).ToList();
        }

        // Get ProxyLines without lines pointed as Id
        private List<Curve> GetUniqueProxy(int id)
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

        private Dictionary<int, Curve> GetUniqueProxy_v2(int id)
        {
            Dictionary<int, Curve> proxData = new Dictionary<int, Curve>();

            //List<Curve> proxyLines = new List<Curve>();
            for (int i = 0; i < adjacentCells.Count; i++)
            {
                if (adjacentCells[i].id != id)
                {
                    proxData.Add(adjacentCells[i].id, proxLines[i]);
                    // proxyLines.Add(proxLines[i]);
                }
            }
            return proxData;
        }

        public List<Curve> BuildObstacles()
        {
            List<Curve> limiters = new List<Curve> { pillOffset };
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

        public List<Curve> BuildObstacles_v2(List<Curve> worldObstacles)
        {

            List<Curve> limiters = new List<Curve> { pillOffset };
            if (worldObstacles != null) limiters.AddRange(worldObstacles);
            Dictionary<int, Curve> uniqueCellsOffset = new Dictionary<int, Curve>();

            for (int i = 0; i < adjacentCells.Count; i++)
            {
                // limiters.Add(adjacentCells[i].pillOffset);
                Dictionary<int, Curve> proxDict = adjacentCells[i].GetUniqueProxy_v2(id);
                uniqueCellsOffset[adjacentCells[i].id] = adjacentCells[i].pillOffset;
                //List<Curve> prox = adjacentCells[i].GetUniqueProxy(id);
                foreach (KeyValuePair<int, Curve> prox_crv in proxDict)
                {
                    uniqueCellsOffset[prox_crv.Key] = blister.CellByID(prox_crv.Key).pillOffset;

                    if (Geometry.CurveCurveIntersection(prox_crv.Value, proxLines).Count == 0)
                    {
                        limiters.Add(prox_crv.Value);
                    }
                }
            }
            limiters.AddRange(uniqueCellsOffset.Values.ToList());
            return Geometry.RemoveDuplicateCurves(limiters);
        }

        #endregion

        #region CUT STUFF
        public bool GenerateSimpleCuttingData_v2(List<Curve> worldObstacles)
        {
            // Initialise new Arrays
            obstacles = BuildObstacles_v2(worldObstacles);
            log.Debug(String.Format("Obstacles count {0}", obstacles.Count));
            cuttingData = new List<CutData>();
            // Stage I - naive Cutting
            // Get cutting Directions
            //PolygonBuilder_v2(GenerateIsoCurvesStage0());
            //log.Info(String.Format(">>>After STAGE_0: {0} cuttng possibilietes<<<", cuttingData.Count));
            PolygonBuilder_v2(GenerateIsoCurvesStage1());
            log.Info(String.Format(">>>After STAGE_1: {0} cuttng possibilietes<<<", cuttingData.Count));
            PolygonBuilder_v2(GenerateIsoCurvesStage2());
            log.Info(String.Format(">>>After STAGE_2: {0} cuttng possibilietes<<<", cuttingData.Count));
            IEnumerable<IEnumerable<LineCurve>> isoLines = GenerateIsoCurvesStage3a(1, 2.0);
            foreach (List<LineCurve> isoLn in isoLines)
            {
                PolygonBuilder_v2(isoLn);
            }

            //PolygonBuilder_v2(GenerateIsoCurvesStage3a(1, 2.0));
            log.Info(String.Format(">>>After STAGE_3: {0} cuttng possibilietes<<<", cuttingData.Count));
            if (cuttingData.Count > 0) return true;
            else return false;
        }


        public bool GenerateAdvancedCuttingData()
        {
            // If all Stages 1-3 failed, start looking more!
            if (cuttingData.Count == 0)
            {
                // if (cuttingData.Count == 0){
                List<List<LineCurve>> isoLinesStage4 = GenerateIsoCurvesStage4(60, Setups.IsoRadius);
                if (isoLinesStage4.Count != 0)
                {
                    List<List<LineCurve>> RaysCombinations = (List<List<LineCurve>>)isoLinesStage4.CartesianProduct();
                    for (int i = 0; i < RaysCombinations.Count; i++)
                    {
                        if (RaysCombinations[i].Count > 0)
                        {
                            PolygonBuilder_v2(RaysCombinations[i]);
                        }
                    }
                }
            }
            if (cuttingData.Count > 0) return true;
            else return false;
        }

        /// <summary>
        /// Get best Cutting Data from all generated and asign it to /bestCuttingData/ field.
        /// </summary>
        public bool PolygonSelector()
        {
            // Order by number of cuts to be performed.

            cuttingData = cuttingData.OrderBy(x => x.EstimatedCuttingCount * x.Polygon.GetBoundingBox(false).Area * x.BlisterLeftovers.Select(y => y.PointCount).Sum()).ToList();
            // cuttingData = cuttingData.OrderBy(x => x.EstimatedCuttingCount* x.GetPerimeter()).ToList();
            List<CutData> selected = cuttingData;
            // Limit only to lower number of cuts
            //List<CutData> selected = cuttingData.Where(x => x.EstimatedCuttingCount == cuttingData[0].EstimatedCuttingCount).ToList();
            //Then sort by perimeter
            //selected = selected.OrderBy(x => x.GetPerimeter()).ToList();
            foreach (CutData cData in selected)
            {
                // if (!cData.RecalculateIsoSegments(OrientationCircle)) continue;
                if (!cData.GenerateBladeFootPrint()) continue;
                bestCuttingData = cData;
                return true;
            }
            //Pick best one.
            //bestCuttingData = selected[0];
            //if (bestCuttingData.RecalculateIsoSegments(OrientationCircle))
            //{
            //    bestCuttingData.GenerateBladeFootPrint();
            //     return true;
            // }
            return false;
            // bestCuttingData = cuttingData[0];
        }

        public void PolygonSelector2()
        {
            /*
            List<PolylineCurve> output = new List<PolylineCurve>();
             List<CutData> selected = cuttingData.Where(x => x.Count == cuttingData[0].Count).ToList();
            foreach(CutData cData in selected){
              output.Add(cData.Path);
             }
             Here some more filtering, if more polygons hase same number of cutting segments...
             Dummy get first...
             if (selected.Count > 0){
               bestCuttingData = selected[0];
             }
             return output;
            */
        }

        public CutState TryCut(bool ommitAnchor, List<Curve> worldObstacles)
        {
            log.Info(String.Format("Trying to cut cell id: {0} with status: {1}", id, state));
            // If cell is cutted, dont try to cut it again... It supose to be in cutted blisters list...
            if (state == CellState.Cutted) return CutState.Cutted;


            // If cell is not surrounded by other cell, update data
            log.Debug(String.Format("Check if cell is alone on blister: No. adjacent cells: {0}", adjacentCells.Count));
            if (adjacentCells.Count == 0)
            {
                //state = CellState.Alone;
                log.Debug("This is last cell on blister.");
                return CutState.Alone;
            }
            // If cell is marekd as possible anchor, also dont try to cut
            if (ommitAnchor == true && Anchor.state == AnchorState.Active)
            {
                log.Info("Marked as anchored. Omitting");
                return CutState.Failed;
            }

            // If still here, try to cut 
            log.Debug("Perform cutting data generation");
            if (GenerateSimpleCuttingData_v2(worldObstacles))
            {
                // RemoveConnectionData();
                PolygonSelector();
                // state = CellState.Cutted;
                return CutState.Cutted;
            }
            else return CutState.Failed;

        }
        #endregion

        #region Polygon Builder Stuff

        // All methods will generat full Rays, without trimming to blister! PoligonBuilder is responsible for trimming.
        private List<LineCurve> GenerateIsoCurvesStage0()
        {

            List<LineCurve> isoLines = new List<LineCurve>(samplePoints.Count);
            for (int i = 0; i < samplePoints.Count; i++)
            {
                Vector3d direction = Vector3d.CrossProduct((connectionLines[i].PointAtEnd - connectionLines[i].PointAtStart), Vector3d.ZAxis);
                //direction = StraigtenVector(direction);
                LineCurve isoLine = Geometry.GetIsoLine(samplePoints[i], direction, Setups.IsoRadius, obstacles);
                if (isoLine == null) continue;
                isoLines.Add(isoLine);
            }
            return isoLines;
        }

        private List<LineCurve> GenerateIsoCurvesStage1()
        {

            List<LineCurve> isoLines = new List<LineCurve>(samplePoints.Count);
            for (int i = 0; i < samplePoints.Count; i++)
            {
                Vector3d direction = Vector3d.CrossProduct((connectionLines[i].PointAtEnd - connectionLines[i].PointAtStart), Vector3d.ZAxis);
                //direction = StraigtenVector(direction);
                LineCurve isoLine = Geometry.GetIsoLine(samplePoints[i], direction, Setups.IsoRadius, obstacles);
                if (isoLine == null) continue;
                isoLines.Add(isoLine);
            }
            return isoLines;
        }

        private List<LineCurve> GenerateIsoCurvesStage2()
        {
            List<LineCurve> isoLines = new List<LineCurve>(samplePoints.Count);
            for (int i = 0; i < samplePoints.Count; i++)
            {
                Vector3d direction = Vector3d.CrossProduct((proxLines[i].PointAtEnd - proxLines[i].PointAtStart), Vector3d.ZAxis);
                //direction = StraigtenVector(direction);
                LineCurve isoLine = Geometry.GetIsoLine(samplePoints[i], direction, Setups.IsoRadius, obstacles);
                if (isoLine == null) continue;
                isoLines.Add(isoLine);
            }
            return isoLines;
        }

        private List<LineCurve> GenerateIsoCurvesStage3()
        {
            List<LineCurve> isoLines = new List<LineCurve>(samplePoints.Count);
            for (int i = 0; i < samplePoints.Count; i++)
            {
                Vector3d direction = Vector3d.CrossProduct((proxLines[i].PointAtEnd - proxLines[i].PointAtStart), Vector3d.ZAxis);
                Vector3d direction2 = Vector3d.CrossProduct((connectionLines[i].PointAtEnd - connectionLines[i].PointAtStart), Vector3d.ZAxis);
                //Vector3d sum_direction = StraigtenVector(direction + direction2);
                Vector3d sum_direction = direction + direction2;
                LineCurve isoLine = Geometry.GetIsoLine(samplePoints[i], sum_direction, Setups.IsoRadius, obstacles);
                if (isoLine == null) continue;
                isoLines.Add(isoLine);
            }
            return isoLines;
        }

        private List<List<LineCurve>> GenerateIsoCurvesStage3a(int raysCount, double stepAngle)
        {
            List<List<LineCurve>> isoLines = new List<List<LineCurve>>(samplePoints.Count);
            for (int i = 0; i < samplePoints.Count; i++)
            {
                Vector3d direction = Vector3d.CrossProduct((proxLines[i].PointAtEnd - proxLines[i].PointAtStart), Vector3d.ZAxis);
                Vector3d direction2 = Vector3d.CrossProduct((connectionLines[i].PointAtEnd - connectionLines[i].PointAtStart), Vector3d.ZAxis);
                //Vector3d sum_direction = StraigtenVector(direction + direction2);
                Vector3d sum_direction = direction + direction2;
                double stepAngleInRadians = ExtraMath.ToRadians(stepAngle);
                if (!sum_direction.Rotate(-raysCount * stepAngleInRadians, Vector3d.ZAxis)) continue;
                //List<double>rotationAngles = Enumerable.Range(-raysCount, (2 * raysCount) + 1).Select(x => x* RhinoMath.ToRadians(stepAngle)).ToList();
                List<LineCurve> currentIsoLines = new List<LineCurve>((2 * raysCount) + 1);
                foreach (double angle in Enumerable.Range(0, (2 * raysCount) + 1))
                {
                    if (!sum_direction.Rotate(stepAngleInRadians, Vector3d.ZAxis)) continue;
                    LineCurve isoLine = Geometry.GetIsoLine(samplePoints[i], sum_direction, Setups.IsoRadius, obstacles);
                    if (isoLine == null) continue;
                    currentIsoLines.Add(isoLine);
                }
                if (currentIsoLines.Count == 0) continue;
                isoLines.Add(currentIsoLines);
            }
            return (List<List<LineCurve>>)Combinators.Combinators.CartesianProduct(isoLines);
        }

        private List<List<LineCurve>> GenerateIsoCurvesStage4(int count, double radius)
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
                        LineCurve t_ray = TrimIsoCurve(ray);
                        //LineCurve t_ray = TrimIsoCurve(ray, samplePoints[i]);
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

        private Vector3d StraigtenVector(Vector3d vec)
        {
            Vector3d direction = vec;
            double angle = Vector3d.VectorAngle(vec, Vector3d.XAxis);
            if (angle <= Setups.AngleTolerance || angle >= Math.PI - Setups.AngleTolerance)
            {
                direction = Vector3d.XAxis;
            }
            else if (angle <= (0.5 * Math.PI) + Setups.AngleTolerance && angle > (0.5 * Math.PI) - Setups.AngleTolerance)
            {
                direction = Vector3d.YAxis;
            }
            return direction;
        }

        /// <summary>
        /// Trim curve 
        /// </summary>
        /// <param name="ray"></param>
        /// <param name="samplePoint"></param>
        /// <returns></returns>
        private LineCurve TrimIsoCurve(LineCurve ray)
        {
            LineCurve outLine = null;
            if (ray == null) return outLine;
            //  log.Debug("Ray not null");
            Geometry.FlipIsoRays(OrientationCircle, ray);
            Tuple<List<Curve>, List<Curve>> result = Geometry.TrimWithRegion(ray, blister.Outline);
            if (result.Item1.Count < 1) return outLine;
            // log.Debug("After trimming.");
            foreach (Curve crv in result.Item1)
            {
                PointContainment test = blister.Outline.Contains(crv.PointAtNormalizedLength(0.5), Plane.WorldXY, 0.1);
                if (test == PointContainment.Inside) return (LineCurve)crv;

            }
            return outLine;
        }

        /// <summary>
        /// Generates closed polygon around cell based on rays (cutters) combination
        /// </summary>
        /// <param name="rays"></param>
        private void PolygonBuilder_v2(List<LineCurve> rays)
        {
            // Trim incomming rays and build current working full ray aray.
            List<LineCurve> trimedRays = new List<LineCurve>(rays.Count);
            List<LineCurve> fullRays = new List<LineCurve>(rays.Count);
            foreach (LineCurve ray in rays)
            {
                LineCurve trimed_ray = TrimIsoCurve(ray);
                if (trimed_ray == null) continue;
                trimedRays.Add(trimed_ray);
                fullRays.Add(ray);
            }
            if (trimedRays.Count != rays.Count) log.Warn("After trimming there is less rays!");


            List<int> raysIndicies = Enumerable.Range(0, trimedRays.Count).ToList();

            //Generate Combinations array
            List<List<int>> raysIndiciesCombinations = Combinators.Combinators.UniqueCombinations(raysIndicies, 1);
            log.Debug(String.Format("Building cut data from {0} rays organized in {1} combinations", trimedRays.Count, raysIndiciesCombinations.Count));
            // Loop over combinations even with 1 ray
            foreach (List<int> combinationIndicies in raysIndiciesCombinations)
            //for (int combId = 0; combId < raysIndiciesCombinations.Count; combId++)
            {
                List<LineCurve> currentTimmedIsoRays = new List<LineCurve>(combinationIndicies.Count);
                List<LineCurve> currentFullIsoRays = new List<LineCurve>(combinationIndicies.Count);
                List<PolylineCurve> pLinecurrentTimmedIsoRays = new List<PolylineCurve>(combinationIndicies.Count);
                foreach (int combinationIndex in combinationIndicies)
                {
                    currentTimmedIsoRays.Add(trimedRays[combinationIndex]);
                    currentFullIsoRays.Add(fullRays[combinationIndex]);
                    pLinecurrentTimmedIsoRays.Add(new PolylineCurve(new List<Point3d>() { trimedRays[combinationIndex].Line.From, trimedRays[combinationIndex].Line.To }));
                }

                log.Debug(String.Format("STAGE 1: Checking {0} rays.", currentTimmedIsoRays.Count));
                List<CutData> localCutData = new List<CutData>(2);
                // STAGE 1: Check if each ray in combination, can cut sucessfully blister.

                // Convert LineCurve to PolylineCurve....

                localCutData.Add(VerifyPath(pLinecurrentTimmedIsoRays));
                log.Debug("STAGE 1: Pass.");
                //log.Debug(String.Format("RAYS KURWA : {0}", combinations[combId].Count));
                // STAGE 2: Looking for 1 (ONE) continouse cutpath...
                // Generate Continouse Path, If there is one curve in combination, PathBuilder will return that curve, so it can be checked.
                PolylineCurve curveToCheck = PathBuilder(currentTimmedIsoRays);
                // If PathBuilder retun any curve... (ONE)
                if (curveToCheck != null)
                {
                    // Remove very short segments
                    Polyline pLineToCheck = curveToCheck.ToPolyline();
                    //pLineToCheck.DeleteShortSegments(Setups.CollapseTolerance);
                    // Look if end of cutting line is close to existing point on blister. If tolerance is smaller snap to this point
                    curveToCheck = pLineToCheck.ToPolylineCurve();
                    //curveToCheck = Geometry.SnapToPoints(curveToCheck, blister.Outline, Setups.SnapDistance);
                    // NOTE: straighten parts of curve????
                    localCutData.Add(VerifyPath(curveToCheck));
                    log.Debug("STAGE 2: Pass.");
                }


                foreach (CutData cutData in localCutData)
                {
                    if (cutData == null) continue;
                    //cutData.TrimmedIsoRays = currentTimmedIsoRays;
                    cutData.IsoSegments = currentFullIsoRays;
                    cutData.Obstacles = obstacles;
                    cuttingData.Add(cutData);
                }
            }
        }

        /// <summary>
        /// Takes group of separate cutting lines, and tries to find continous cuttiing path.
        /// </summary>
        /// <param name="cutters">cutting iso line.</param>
        /// <returns> Joined PolylineCurve if path was found. null if not.</returns>
        private PolylineCurve PathBuilder(List<LineCurve> cutters)
        {
            PolylineCurve pLine = null;
            // If more curves, generate one!
            if (cutters.Count > 1)
            {
                // Perform intersectiona based on combination array.
                List<CurveIntersections> intersectionsData = new List<CurveIntersections>();
                for (int interId = 1; interId < cutters.Count; interId++)
                {
                    CurveIntersections inter = Intersection.CurveCurve(cutters[interId - 1], cutters[interId], Setups.IntersectionTolerance, Setups.OverlapTolerance);
                    // If no intersection, at any curve, break all testing process
                    if (inter.Count == 0) break;
                    //If exist, Store it
                    else intersectionsData.Add(inter);
                }
                // If intersection are equal to curveCount-1, this mean, all cuvre where involve.. so..
                if (intersectionsData.Count == cutters.Count - 1)
                {
                    //Create JoinedCurve from all interesection data
                    List<Point3d> polyLinePoints = new List<Point3d> { cutters[0].PointAtStart };
                    for (int i = 0; i < intersectionsData.Count; i++)
                    {
                        polyLinePoints.Add(intersectionsData[i][0].PointA);
                    }
                    polyLinePoints.Add(cutters[cutters.Count - 1].PointAtEnd);
                    pLine = new PolylineCurve(polyLinePoints);
                }
            }
            else
            {
                pLine = new PolylineCurve(new List<Point3d> { cutters[0].Line.From, cutters[0].Line.To });
            }
            //if (pLine != null) log.Info(pLine.ClosedCurveOrientation().ToString());
            return pLine;
        }

        private CutData VerifyPath(PolylineCurve pathCrv)
        {
            return VerifyPath(new List<PolylineCurve>() { pathCrv });
        }
        private CutData VerifyPath(List<PolylineCurve> pathCrv)
        {
            log.Debug(string.Format("Verify path. Segments: {0}", pathCrv.Count));
            if (pathCrv == null) return null;
            // Check if this curves creates closed polygon with blister edge.
            List<Curve> splitters = pathCrv.Cast<Curve>().ToList();
            List<Curve> splited_blister = Geometry.SplitRegion(blister.Outline, splitters);
            // If after split there is less then 2 region it means nothing was cutted and bliseter stays unchanged
            if (splited_blister == null) return null;
            if (splited_blister.Count < 2) return null;

            log.Debug(string.Format("Blister splitited onto {0} parts", splited_blister.Count));
            Polyline pill_region = null;
            List<PolylineCurve> cutted_blister_regions = new List<PolylineCurve>();

            // Get region with pill
            foreach (Curve s_region in splited_blister)
            {
                if (!s_region.IsValid || !s_region.IsClosed) continue;
                RegionContainment test = Curve.PlanarClosedCurveRelationship(s_region, pill);
                if (test == RegionContainment.BInsideA) s_region.TryGetPolyline(out pill_region);
                else if (test == RegionContainment.Disjoint)
                {
                    Polyline cutted_blister_region = null;
                    s_region.TryGetPolyline(out cutted_blister_region);
                    PolylineCurve pl_cutted_blister_region = cutted_blister_region.ToPolylineCurve();
                    Geometry.UnifyCurve(pl_cutted_blister_region);
                    cutted_blister_regions.Add(pl_cutted_blister_region);
                }
                else return null;
            }
            if (pill_region == null) return null;
            PolylineCurve pill_region_curve = pill_region.ToPolylineCurve();

            // Chceck if only this pill is inside pill_region, After checking if pill region exists of course....
            log.Debug("Chceck if only this pill is inside pill_region.");
            foreach (Cell cell in blister.Cells)
            {
                if (cell.id == this.id) continue;
                RegionContainment test = Curve.PlanarClosedCurveRelationship(cell.pillOffset, pill_region_curve);
                if (test == RegionContainment.AInsideB)
                {
                    log.Debug("More then one pill in cutout region. CutData creation failed.");
                    return null;
                }
            }
            log.Debug("Check smallest segment size requerment.");
            // Check if smallest segment from cutout blister is smaller than some size.
            PolylineCurve pill_region_Crv = pill_region.ToPolylineCurve();
            PolylineCurve bbox = Geometry.MinimumAreaRectangleBF(pill_region_Crv);
            Line[] pill_region_segments = pill_region.GetSegments().OrderBy(line => line.Length).ToArray();
            if (pill_region_segments[0].Length > Setups.MinimumCutOutSize) return null;
            log.Debug("CutData created.");
            Geometry.UnifyCurve(pill_region_Crv);
            return new CutData(pill_region_Crv, pathCrv, cutted_blister_regions);

        }

        #endregion

        public JObject GetJSON(Point3d Jaw1_Local)
        {
            JObject data = new JObject();
            data.Add("pillIndex", this.id);
            // Add Anchor Data <- to be implement.
            data.Add("openJaw", Anchor.orientation.ToString().ToLower());
            // Add Cutting Instruction
            if (bestCuttingData != null) data.Add("cutInstruction", bestCuttingData.GetJSON(Jaw1_Local));
            else data.Add("cutInstruction", new JArray());
            return data;
        }
    }
}
