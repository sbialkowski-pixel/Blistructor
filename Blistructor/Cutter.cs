using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.IO;
using System.Threading.Tasks;

#if PIXEL
using Pixel.Rhino;
using Pixel.Rhino.FileIO;
using Pixel.Rhino.DocObjects;
using Pixel.Rhino.Geometry;
using Pixel.Rhino.Geometry.Intersect;
using ExtraMath = Pixel.Rhino.RhinoMath;
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
    public class Cutter
    {
        private static readonly ILog log = LogManager.GetLogger("Cutter.PillCutter");
        //private List<CutData> CuttingData { get; set; }
        private Blister Blister { get; set; }
        private Pill Pill { get; set; }

        //private Grasper Grasper { get; set; }
        private List<Curve> FixedObstacles { get; set; }
        private List<Curve> WorkingObstacles { get; set; }

        private List<List<CutProposal>> AlreadyCutLevels { get; set; }
        private List<int> LevelSetup { get; set; }

        public Cutter(List<Curve> fixedObstacles)
        {
            LevelSetup = new List<int>() { 0, 4, 8, 16, 32 };
            AlreadyCutLevels = new List<List<CutProposal>>(LevelSetup.Count);
            //Init internal lists
            LevelSetup.ForEach(s => AlreadyCutLevels.Add(new List<CutProposal>()));
            FixedObstacles = fixedObstacles;
            WorkingObstacles = new List<Curve>(FixedObstacles);
        }


        /// <summary>
        /// Cutting with idea to keep each cutting state.
        /// </summary>
        /// <param name="blisterTotCut"></param>
        /// <returns></returns>
        public CutProposal CutNext(Blister blisterTotCut)
        {
            Blister = blisterTotCut;

            foreach ((List<CutProposal> alreadyCut, int setup) in AlreadyCutLevels.Zip(LevelSetup, (p, s) => (Proposals: p, Setup: s)))
            {
                List<Pill> pillsToProcess = Blister.Pills.Where(x => alreadyCut.All(y => y.Pill.Id != x.Id)).ToList();
                foreach (Pill pill in pillsToProcess)
                {
                    CutProposal proposal = TryCut(pill, setup);
                    alreadyCut.Add(proposal);
                    if (proposal.State == CutState.Failed) continue;
                    else return proposal;
                }
            }
            return null;
            //log.Warn("No cutting data generated for whole Blister.");
            // throw new Exception("No cutting data generated for whole Blister.");
        }

        /// <summary>
        /// After passing all cutProposition (CutNext is returning null), get remaining proposition from internal cutter state.
        /// </summary>
        /// <param name="state">Desiered State</param>
        /// <returns></returns>
        public CutProposal GetNextCut(CutState state)
        {
            foreach (List<CutProposal> alreadyCut in AlreadyCutLevels)
            {
                CutProposal nextProposal = alreadyCut.FirstOrDefault(proposal => proposal.State == state);
                if (nextProposal != null) return nextProposal;
            }
            return null;

            //return AlreadyCut.FirstOrDefault(proposal => proposal.State == state);
        }

        public CutProposal TryCut(Pill pillToCut, int advancedMethodSamples = 0)
        {
            // Create obstacles (limiters) for cutting process.
            WorkingObstacles = new List<Curve>(FixedObstacles);
            pillToCut.UpdateObstacles();
            WorkingObstacles.AddRange(pillToCut.obstacles);

            List<CutData> cuttingData = new List<CutData>();
            Pill = pillToCut;
            log.Info(String.Format("Trying to cut Outline id: {0} with status: {1}", pillToCut.Id, pillToCut.State));
            // If Outline is cut, don't try to cut it again. It suppose to be in chunk blisters list.
            if (pillToCut.State == PillState.Cut)
            {
                return new CutProposal(pillToCut, cuttingData, CutState.Succeed);
            }

            // If Outline is not surrounded by other Outline, update data
            log.Debug(String.Format("Check if Outline is alone on blister: No. adjacent pills: {0}", pillToCut.adjacentPills.Count));
            if (pillToCut.adjacentPills.Count == 0)
            {
                log.Debug("This is last Outline on blister.");
                return new CutProposal(pillToCut, cuttingData, CutState.Last);
            }
            // If still here, try to cut 
            log.Debug("Perform cutting data generation");
            if (advancedMethodSamples > 0) cuttingData = GenerateAdvancedCuttingData(samples: advancedMethodSamples);
            else cuttingData = GenerateSimpleCuttingData_v3();

            if (cuttingData.Count > 0)
            {
                return new CutProposal(pillToCut, cuttingData, CutState.Succeed);
            }
            return new CutProposal(pillToCut, cuttingData, CutState.Failed);
        }

        #region CUT STUFF
        private List<CutData> GenerateSimpleCuttingData_v3()
        {
            List<List<LineCurve>> isoLinesStage0 = GenerateIsoCurvesStage0_v2(2, 5.0);
            List<CutData> cuttingData = GenerateCuttingData(isoLinesStage0, debugFileName: "SimpleV3");
            return cuttingData;
        }

        /// <summary>
        /// Cosine similarity.
        /// </summary>
        /// <param name="vec1"></param>
        /// <param name="vec2"></param>
        /// <returns></returns>
        private double VecSim(Vector3d vec1, Vector3d vec2)
        {
            return (vec1 * vec2) / (vec1.Length * vec2.Length);
        }


        private List<List<LineCurve>> GenerateIsoCurvesStage0_v2(int raysCount, double stepAngle)
        {
            List<List<LineCurve>> isoLines = new List<List<LineCurve>>(Pill.samplePoints.Count);
            for (int i = 0; i < Pill.samplePoints.Count; i++)
            {
                Vector3d direction = Vector3d.CrossProduct((Pill.proxLines[i].PointAtEnd - Pill.proxLines[i].PointAtStart), Vector3d.ZAxis);
                Vector3d direction2 = Vector3d.CrossProduct((Pill.connectionLines[i].PointAtEnd - Pill.connectionLines[i].PointAtStart), Vector3d.ZAxis);

                List<LineCurve> currentIsoLines = new List<LineCurve>((2 * raysCount) + 1);
                // If both vectors are much different, add IsoLines based on both, else only average will be taken.
                if (Math.Abs(VecSim(direction, direction2)) > 0.9)
                {
                    foreach (Vector3d vec in new List<Vector3d>() { direction, direction2 }) { }
                    LineCurve isoLine = Geometry.GetIsoLine(Pill.samplePoints[i], direction, Setups.IsoRadius, WorkingObstacles);
                    if (isoLine == null) continue;
                    currentIsoLines.Add(isoLine);
                }
                // Compute avr vector and generate bunch of rays.
                Vector3d sum_direction = direction + direction2;
                double stepAngleInRadians = ExtraMath.ToRadians(stepAngle);
                if (!sum_direction.Rotate(-raysCount * stepAngleInRadians, Vector3d.ZAxis)) continue;
                foreach (double angle in Enumerable.Range(0, (2 * raysCount) + 1))
                {
                    if (!sum_direction.Rotate(stepAngleInRadians, Vector3d.ZAxis)) continue;
                    LineCurve isoLine = Geometry.GetIsoLine(Pill.samplePoints[i], sum_direction, Setups.IsoRadius, WorkingObstacles);
                    if (isoLine == null) continue;
                    currentIsoLines.Add(isoLine);
                }
                if (currentIsoLines.Count == 0) continue;
                isoLines.Add(currentIsoLines);
            }
            return isoLines;
        }

        private List<CutData> GenerateCuttingData(List<List<LineCurve>> rays, string debugFileName = "")
        {
            ConcurrentBag<CutData> cuttingData = new ConcurrentBag<CutData>();
            // List<CutData> cuttingData = new List<CutData>();
            // If all Stages 1-3 failed, start looking more!
            if (rays.Count != 0)
            {

                List<List<LineCurve>> RaysCombinations = Combinators.Combinators.UniqueCombinations(rays);
                Parallel.ForEach(RaysCombinations, RaysCombination =>
                {
                    if (RaysCombination.Count > 0)
                    {
                        List<CutData> currentCuttingData = PolygonBuilder_v2(RaysCombination, use_all_combinations: false);
                        foreach (CutData data in currentCuttingData) cuttingData.Add(data);
                    }
                });

            }
            List<CutData> cuttingDataList = cuttingData.ToList();
            //DEBUG - SAVE FILE:
            if (debugFileName != "")
            {
                Random rnd = new Random();
                int id = rnd.Next(0, 1000);
                string path = string.Format("D:\\PIXEL\\Blistructor\\DebugModels\\{0}_{1}.3dm", debugFileName, id);
                File3dm file = new File3dm();

                Layer l_polygon = new Layer();
                l_polygon.Name = "polygon";
                l_polygon.Index = 0;
                file.AllLayers.Add(l_polygon);
                Layer l_lines = new Layer();
                l_lines.Name = "lines";
                l_lines.Index = 1;
                file.AllLayers.Add(l_lines);
                Layer l_obst = new Layer();
                l_obst.Name = "obstacles";
                l_obst.Index = 2;
                file.AllLayers.Add(l_obst);

                ObjectAttributes a_polygon = new ObjectAttributes();
                a_polygon.LayerIndex = l_polygon.Index;
                cuttingDataList.ForEach(cData => file.Objects.AddCurve(cData.Polygon, a_polygon));

                ObjectAttributes a_lines = new ObjectAttributes();
                a_lines.LayerIndex = l_lines.Index;
                rays.ForEach(list => list.ForEach(l => file.Objects.AddCurve(l, a_lines)));

                ObjectAttributes a_obs = new ObjectAttributes();
                a_obs.LayerIndex = l_obst.Index;
                WorkingObstacles.ForEach(crv => file.Objects.AddCurve(crv, a_obs));

                file.Write(path, 6);
            }
            // END DEBUG
            return cuttingDataList;
        }

   
        private List<CutData> GenerateAdvancedCuttingData(int samples = 30)
        {
            ConcurrentBag<CutData> cuttingData = new ConcurrentBag<CutData>();
            // List<CutData> cuttingData = new List<CutData>();
            // If all Stages 1-3 failed, start looking more!
            List<List<LineCurve>> isoLinesStage4 = GenerateIsoCurvesStage4(samples);
            if (isoLinesStage4.Count != 0)
            {

                List<List<LineCurve>> RaysCombinations = Combinators.Combinators.UniqueCombinations(isoLinesStage4);
                Parallel.ForEach(RaysCombinations, RaysCombination =>
                 {
                     if (RaysCombination.Count > 0)
                     {
                         List<CutData> currentCuttingData = PolygonBuilder_v2(RaysCombination, use_all_combinations: false);
                         foreach (CutData data in currentCuttingData) cuttingData.Add(data);
                     }
                 });

            }
            List<CutData> cuttingDataList = cuttingData.ToList();
            //DEBUG - SAVE FILE:
            if (true)
            {
                Random rnd = new Random();
                int id = rnd.Next(0, 1000);
                String path = String.Format("D:\\PIXEL\\Blistructor\\DebugModels\\AdvancedCut_{0}.3dm", id);
                File3dm file = new File3dm();

                Layer l_polygon = new Layer();
                l_polygon.Name = "polygon";
                l_polygon.Index = 0;
                file.AllLayers.Add(l_polygon);
                Layer l_lines = new Layer();
                l_lines.Name = "lines";
                l_lines.Index = 1;
                file.AllLayers.Add(l_lines);
                Layer l_obst = new Layer();
                l_obst.Name = "obstacles";
                l_obst.Index = 2;
                file.AllLayers.Add(l_obst);

                ObjectAttributes a_polygon = new ObjectAttributes();
                a_polygon.LayerIndex = l_polygon.Index;
                cuttingDataList.ForEach(cData => file.Objects.AddCurve(cData.Polygon, a_polygon));

                ObjectAttributes a_lines = new ObjectAttributes();
                a_lines.LayerIndex = l_lines.Index;
                isoLinesStage4.ForEach(list => list.ForEach(l => file.Objects.AddCurve(l, a_lines)));

                ObjectAttributes a_obs = new ObjectAttributes();
                a_obs.LayerIndex = l_obst.Index;
                WorkingObstacles.ForEach(crv => file.Objects.AddCurve(crv, a_obs));

                file.Write(path, 6);
            }
            // END DEBUG
            return cuttingDataList;
        }

        #endregion

        #region Polygon Builder Stuff

        /// <summary>
        /// Generate bunch of rays arround sample points in range 0 - Math.Pi (half circle).
        /// Starting ray (on position 0) is missing.
        /// </summary>
        /// <param name="count">Number of rays to generate</param>
        /// <returns></returns>
        private List<List<LineCurve>> GenerateIsoCurvesStage4(int count)
        {
            // Obstacles need to be calculated or updated earlier
            List<List<LineCurve>> isoLines = new List<List<LineCurve>>();
            for (int i = 0; i < Pill.samplePoints.Count; i++)
            {
                Circle cir = new Circle(Pill.samplePoints[i], Setups.IsoRadius);
                List<LineCurve> iLines = new List<LineCurve>();
                IEnumerable<double> parts = Enumerable.Range(0, count).Select(x => x * (Math.PI / count));
                List<Point3d> pts = parts.Select(part => cir.PointAt(part)).ToList();

                for (int j = 0; j < pts.Count; j++)
                {
                    Point3d Pt = pts[j];
                    LineCurve ray = Geometry.GetIsoLine(Pill.samplePoints[i], Pt - Pill.samplePoints[i], Setups.IsoRadius, WorkingObstacles);
                    if (ray != null)
                    {
                        iLines.Add(ray);
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
            Geometry.FlipIsoRays(Pill.OrientationCircle, ray);
            Tuple<List<Curve>, List<Curve>> result = Geometry.TrimWithRegion(ray, Pill.blister.Outline);
            if (result == null) return null;
            if (result.Item1.Count < 1) return outLine;
            // log.Debug("After trimming.");
            foreach (Curve crv in result.Item1)
            {
                PointContainment test = Pill.blister.Outline.Contains(crv.PointAtNormalizedLength(0.5), Plane.WorldXY, 0.1);
                if (test == PointContainment.Inside) return (LineCurve)crv;

            }
            return outLine;
        }

        /// <summary>
        /// Generates closed polygon around Outline based on rays (cutters) combination
        /// </summary>
        /// <param name="rays"></param>
        private List<CutData> PolygonBuilder_v2(List<LineCurve> rays, bool use_all_combinations = true)
        {

            // Trim incoming rays and build current working full ray aray.
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
            int minCombinations;
            if (use_all_combinations) minCombinations = 1;
            else minCombinations = rays.Count;
            List<List<int>> raysIndiciesCombinations = Combinators.Combinators.UniqueCombinations(raysIndicies, minCombinations);
            log.Debug(String.Format("Building cut data from {0} rays organized in {1} combinations", trimedRays.Count, raysIndiciesCombinations.Count));

            List<CutData> cuttingData = new List<CutData>();

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
                CutData afterVerificationSeperate = VerifyPath(pLinecurrentTimmedIsoRays);
                localCutData.Add(afterVerificationSeperate);
                log.Debug(String.Format("STAGE 1: Pass. {0}", afterVerificationSeperate != null ? "Cut FOUND" : "Cut NOT found"));
                // STAGE 2: Looking for 1 (ONE) continouse cutpath...
                // Generate Continouse Path, If there is one curve in combination, PathBuilder will return that curve, so it can be checked.
                PolylineCurve curveToCheck = PathBuilder(currentTimmedIsoRays);

                // If PathBuilder return any curve... (ONE)
                if (curveToCheck != null)
                {
                    // Remove very short segments
                    Polyline pLineToCheck = curveToCheck.ToPolyline();
                    //pLineToCheck.DeleteShortSegments(Setups.CollapseTolerance);
                    // Look if end of cutting line is close to existing point on blister. If tolerance is smaller snap to this point
                    curveToCheck = pLineToCheck.ToPolylineCurve();
                    //curveToCheck = Geometry.SnapToPoints(curveToCheck, subBlister.Outline, Setups.SnapDistance);
                    // NOTE: straighten parts of curve????
                    CutData afterVerificationJoined = VerifyPath(curveToCheck);

                    localCutData.Add(afterVerificationJoined);
                    log.Debug(String.Format("STAGE 2: Pass. {0}", afterVerificationSeperate != null ? "Cut FOUND" : "Cut NOT found"));
                }

                foreach (CutData cutData in localCutData)
                {
                    if (cutData is null) continue;
                    //cutData.TrimmedIsoRays = currentTimmedIsoRays;
                    cutData.IsoSegments = currentFullIsoRays;
                    cutData.Obstacles = WorkingObstacles;
                    cuttingData.Add(cutData);
                }

            }
            return cuttingData;
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
                List<List<IntersectionEvent>> intersectionsData = new List<List<IntersectionEvent>>();
                for (int interId = 1; interId < cutters.Count; interId++)
                {
                    List<IntersectionEvent> inter = Intersection.CurveCurve(cutters[interId - 1], cutters[interId], Setups.IntersectionTolerance);
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
            List<Curve> splited_blister = Geometry.SplitRegion(Pill.blister.Outline, splitters);
            // If after split there is less then 2 region it means nothing was cutted and bliseter stays unchanged
            if (splited_blister == null) return null;
            //splitters.ForEach(crv => file.Objects.AddCurve(crv));
            //splited_blister.ForEach(crv => file.Objects.AddCurve(crv));
            if (splited_blister.Count < 2) return null;

            log.Debug(string.Format("Blister splitited onto {0} parts", splited_blister.Count));
            Polyline pill_region = null;
            List<PolylineCurve> cutted_blister_regions = new List<PolylineCurve>();

            // Get region with Outline
            foreach (Curve s_region in splited_blister)
            {
                if (!s_region.IsValid || !s_region.IsClosed) continue;
                RegionContainment test = Curve.PlanarClosedCurveRelationship(s_region, Pill.Outline);
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

            // Chceck if only this Outline is inside pill_region, After checking if Outline region exists of course....
            log.Debug("Chceck if only this Outline is inside pill_region.");
            foreach (Pill pill in Pill.blister.Pills)
            {
                if (pill.Id == Pill.Id) continue;
                RegionContainment test = Curve.PlanarClosedCurveRelationship(pill.Offset, pill_region_curve);
                if (test == RegionContainment.AInsideB)
                {
                    log.Debug("More then one Outline in cutout region. CutData creation failed.");
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


    }

}
