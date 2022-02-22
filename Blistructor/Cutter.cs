using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading.Tasks;

#if PIXEL
using Pixel.Rhino;
using Pixel.Rhino.FileIO;
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
using Pixel.Rhino.DocObjects;

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

        private List<CutProposal> AlreadyCut { get; set; }
        private List<CutProposal> AlreadyCutAdvanced { get; set; }

        public Cutter(List<Curve> fixedObstacles)
        {
            AlreadyCut = new List<CutProposal>();
            AlreadyCutAdvanced = new List<CutProposal>();
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
            // Get all Uncut pills, and try to cut.
            List<Pill> pillsToProcess = Blister.Pills.Where(x => AlreadyCut.All(y => y.Pill.Id != x.Id)).ToList();
            foreach (Pill pill in pillsToProcess)
            {
                CutProposal proposal = TryCut(pill);
                AlreadyCut.Add(proposal);
                if (proposal.State == CutState.Failed) continue;
                else return proposal;
            }

            return null;
            //log.Warn("No cutting data generated for whole Blister.");
            // throw new Exception("No cutting data generated for whole Blister.");
        }

        public CutProposal CutNextAdvanced(Blister blisterTotCut)
        {
            Blister = blisterTotCut;
            // Get all Uncut pills, and try to cut.
            List<Pill> pillsToProcess = Blister.Pills.Where(x => AlreadyCutAdvanced.All(y => y.Pill.Id != x.Id)).ToList();
            foreach (Pill pill in pillsToProcess)
            {
                CutProposal proposal = TryCut(pill, useAdvancedMethod: true);
                AlreadyCutAdvanced.Add(proposal);
                if (proposal.State == CutState.Failed) continue;
                else return proposal;
            }

            return null;

        }

        public CutProposal GetNextSuccessfulCut
        {
            get { return AlreadyCut.FirstOrDefault(proposal => proposal.State != CutState.Failed); }
        }

        public List<CutProposal> GetSuccesfullyCuts
        {
            get { return AlreadyCut.Where(proposal => proposal.State == CutState.Succeed).ToList(); }
        }

        /*
        public CutProposal NewNextCut(Blister blisterTotCut, bool omitFixed = true)
        {
            // Maybe this cutter should hold all cutProposal
            Blister = blisterTotCut;
            List<Pill> fixedPills = new List<Pill>(Blister.Pills.Count);

            for (int i = 0; i < Blister.Pills.Count; i++)
            {
                Pill currentPill = Blister.Pills[i];
                CutProposal proposal = TryCut(currentPill);
                if (omitFixed && Grasper.HasPossibleJaw(proposal.BestCuttingData))
                {
                    fixedPills.Add(currentPill);
                    continue;
                }
                  
                proposal.ValidateCut(Grasper); // need to reimplement HasActiveAnchor!!!

                if (proposal.State == CutState.Failed) continue;
                else
                {
                    log.Info(String.Format("Cut Path found for pill {0} after checking {1} pills", currentPill.Id, i));
                    return proposal;
                }
            }
            foreach (Pill currentPill in fixedPills)
            {
                CutProposal proposal = TryCut(currentPill);
                proposal.ValidateCut(Grasper); // need to reimplement HasActiveAnchor!!!
                if (proposal.State == CutState.Failed) continue;
                else
                {
                    log.Info(String.Format("Cut Path found for pill {0} after checking {1} pills", currentPill.Id, i));
                    return proposal;
                }
            }

        }
        */

        public CutProposal TryCut(Pill pillToCut, bool useAdvancedMethod = false)
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
            if (useAdvancedMethod) cuttingData = GenerateAdvancedCuttingData(samples: 30);
            else cuttingData = GenerateSimpleCuttingData_v2();

            if (cuttingData.Count > 0)
            {
                return new CutProposal(pillToCut, cuttingData, CutState.Succeed);
            }
            return new CutProposal(pillToCut, cuttingData, CutState.Failed);
        }

        #region CUT STUFF
        private List<CutData> GenerateSimpleCuttingData_v2()
        {
            log.Debug(String.Format("Obstacles count {0}", WorkingObstacles.Count));
            List<CutData> cuttingData = new List<CutData>();
            // Stage I - naive Cutting
            cuttingData.AddRange(PolygonBuilder_v2(GenerateIsoCurvesStage1()));
            log.Info(String.Format(">>>After STAGE_1: {0} cutting possibilities<<<", cuttingData.Count));
            cuttingData.AddRange(PolygonBuilder_v2(GenerateIsoCurvesStage2()));
            log.Info(String.Format(">>>After STAGE_2: {0} cutting possibilities<<<", cuttingData.Count));
            IEnumerable<IEnumerable<LineCurve>> isoLines = GenerateIsoCurvesStage3a(1, 2.0);
            foreach (List<LineCurve> isoLn in isoLines)
            {
                cuttingData.AddRange(PolygonBuilder_v2(isoLn));
            }
            //TODO: Tu mozna sprawdzać kolizję z łapkami dogenerowac niekolizyjne cięcia.

            //PolygonBuilder_v2(GenerateIsoCurvesStage3a(1, 2.0));
            log.Info(String.Format(">>>After STAGE_3: {0} cutting possibilities<<<", cuttingData.Count));
            return cuttingData;
        }

        private List<CutData> GenerateAdvancedCuttingData(int samples = 30)
        {
            List<CutData> cuttingData = new List<CutData>();
            // If all Stages 1-3 failed, start looking more!
            List<List<LineCurve>> isoLinesStage4 = GenerateIsoCurvesStage4(samples, Setups.IsoRadius);
            if (isoLinesStage4.Count != 0)
            {
                List<List<LineCurve>> RaysCombinations = (List<List<LineCurve>>)isoLinesStage4.CartesianProduct();

                //   for (int i = 0; i < RaysCombinations.Count; i++)
                Parallel.ForEach(RaysCombinations, RaysCombination =>
                {

                    if (RaysCombination.Count > 0)
                    {
                        cuttingData.AddRange(PolygonBuilder_v2(RaysCombination));
                    }
                });

            }
            return cuttingData;
        }

        #endregion

        #region Polygon Builder Stuff

        // All methods will generate full Rays, without trimming to blister! PoligonBuilder is responsible for trimming.
        private List<LineCurve> GenerateIsoCurvesStage0()
        {

            List<LineCurve> isoLines = new List<LineCurve>(Pill.samplePoints.Count);
            for (int i = 0; i < Pill.samplePoints.Count; i++)
            {
                Vector3d direction = Vector3d.CrossProduct((Pill.connectionLines[i].PointAtEnd - Pill.connectionLines[i].PointAtStart), Vector3d.ZAxis);
                //direction = StraigtenVector(direction);
                LineCurve isoLine = Geometry.GetIsoLine(Pill.samplePoints[i], direction, Setups.IsoRadius, WorkingObstacles);
                if (isoLine == null) continue;
                isoLines.Add(isoLine);
            }
            return isoLines;
        }

        private List<LineCurve> GenerateIsoCurvesStage1()
        {

            List<LineCurve> isoLines = new List<LineCurve>(Pill.samplePoints.Count);
            for (int i = 0; i < Pill.samplePoints.Count; i++)
            {
                Vector3d direction = Vector3d.CrossProduct((Pill.connectionLines[i].PointAtEnd - Pill.connectionLines[i].PointAtStart), Vector3d.ZAxis);
                //direction = StraigtenVector(direction);
                LineCurve isoLine = Geometry.GetIsoLine(Pill.samplePoints[i], direction, Setups.IsoRadius, WorkingObstacles);
                if (isoLine == null) continue;
                isoLines.Add(isoLine);
            }
            return isoLines;
        }

        private List<LineCurve> GenerateIsoCurvesStage2()
        {
            List<LineCurve> isoLines = new List<LineCurve>(Pill.samplePoints.Count);
            for (int i = 0; i < Pill.samplePoints.Count; i++)
            {
                Vector3d direction = Vector3d.CrossProduct((Pill.proxLines[i].PointAtEnd - Pill.proxLines[i].PointAtStart), Vector3d.ZAxis);
                //direction = StraigtenVector(direction);
                LineCurve isoLine = Geometry.GetIsoLine(Pill.samplePoints[i], direction, Setups.IsoRadius, WorkingObstacles);
                if (isoLine == null) continue;
                isoLines.Add(isoLine);
            }
            return isoLines;
        }

        private List<LineCurve> GenerateIsoCurvesStage3()
        {
            List<LineCurve> isoLines = new List<LineCurve>(Pill.samplePoints.Count);
            for (int i = 0; i < Pill.samplePoints.Count; i++)
            {
                Vector3d direction = Vector3d.CrossProduct((Pill.proxLines[i].PointAtEnd - Pill.proxLines[i].PointAtStart), Vector3d.ZAxis);
                Vector3d direction2 = Vector3d.CrossProduct((Pill.connectionLines[i].PointAtEnd - Pill.connectionLines[i].PointAtStart), Vector3d.ZAxis);
                //Vector3d sum_direction = StraigtenVector(direction + direction2);
                Vector3d sum_direction = direction + direction2;
                LineCurve isoLine = Geometry.GetIsoLine(Pill.samplePoints[i], sum_direction, Setups.IsoRadius, WorkingObstacles);
                if (isoLine == null) continue;
                isoLines.Add(isoLine);
            }
            return isoLines;
        }

        private List<List<LineCurve>> GenerateIsoCurvesStage3a(int raysCount, double stepAngle)
        {
            List<List<LineCurve>> isoLines = new List<List<LineCurve>>(Pill.samplePoints.Count);
            for (int i = 0; i < Pill.samplePoints.Count; i++)
            {
                Vector3d direction = Vector3d.CrossProduct((Pill.proxLines[i].PointAtEnd - Pill.proxLines[i].PointAtStart), Vector3d.ZAxis);
                Vector3d direction2 = Vector3d.CrossProduct((Pill.connectionLines[i].PointAtEnd - Pill.connectionLines[i].PointAtStart), Vector3d.ZAxis);
                //Vector3d sum_direction = StraigtenVector(direction + direction2);
                Vector3d sum_direction = direction + direction2;
                double stepAngleInRadians = ExtraMath.ToRadians(stepAngle);
                if (!sum_direction.Rotate(-raysCount * stepAngleInRadians, Vector3d.ZAxis)) continue;
                //List<double>rotationAngles = Enumerable.Range(-raysCount, (2 * raysCount) + 1).Select(x => x* RhinoMath.ToRadians(stepAngle)).ToList();
                List<LineCurve> currentIsoLines = new List<LineCurve>((2 * raysCount) + 1);
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
            return (List<List<LineCurve>>)Combinators.Combinators.CartesianProduct(isoLines);
        }

        private List<List<LineCurve>> GenerateIsoCurvesStage4(int count, double radius)
        {
            // Obstacles need to be calculated or updated earlier
            List<List<LineCurve>> isoLines = new List<List<LineCurve>>();
            for (int i = 0; i < Pill.samplePoints.Count; i++)
            {
                Circle cir = new Circle(Pill.samplePoints[i], radius);
                List<LineCurve> iLines = new List<LineCurve>();
                IEnumerable<double> parts = Enumerable.Range(0, 30).Select(x => x * ((Math.PI) / 29));
                List<Point3d> pts = parts.Select(part => cir.PointAt(part)).ToList();


                //ArcCurve arc = new ArcCurve(new Arc(cir, new Interval(0, Math.PI)));
                //double[] t = arc.DivideByCount(count, false);
                for (int j = 0; j < pts.Count; j++)
                // for (int j = 0; j < t.Length; j++)
                {
                    //Point3d Pt = arc.PointAt(t[j]);
                    Point3d Pt = pts[j];
                    LineCurve ray = Geometry.GetIsoLine(Pill.samplePoints[i], Pt - Pill.samplePoints[i], Setups.IsoRadius, WorkingObstacles);
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
            Geometry.FlipIsoRays(Pill.OrientationCircle, ray);
            Tuple<List<Curve>, List<Curve>> result = Geometry.TrimWithRegion(ray, Pill.blister.Outline);
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
        private List<CutData> PolygonBuilder_v2(List<LineCurve> rays)
        {
            List<CutData> cuttingData = new List<CutData>();
            // Trim incoming rays and build current working full ray aray.
            List<LineCurve> trimedRays = new List<LineCurve>(rays.Count);
            List<LineCurve> fullRays = new List<LineCurve>(rays.Count);
            foreach (LineCurve ray in rays)
            {
                LineCurve trimed_ray = TrimIsoCurve(ray);
                if (trimed_ray == null) continue;
                trimedRays.Add(trimed_ray);
                fullRays.Add(ray);
                //  file.Objects.AddCurve(trimed_ray);
                // file.Objects.AddLine(ray.Line);
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
                CutData afterVerificationSeperate = VerifyPath(pLinecurrentTimmedIsoRays);
                localCutData.Add(afterVerificationSeperate);
                log.Debug(String.Format("STAGE 1: Pass. {0}", afterVerificationSeperate != null ? "Cut FOUND" : "Cut NOT found"));
                //log.Debug(String.Format("RAYS KURWA : {0}", combinations[combId].Count));
                // STAGE 2: Looking for 1 (ONE) continouse cutpath...
                // Generate Continouse Path, If there is one curve in combination, PathBuilder will return that curve, so it can be checked.
                PolylineCurve curveToCheck = PathBuilder(currentTimmedIsoRays);

                // If PathBuilder retun any curve... (ONE)
                if (curveToCheck != null)
                {
                    //  file.Objects.AddCurve(curveToCheck);
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
                    if (cutData == null) continue;
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
