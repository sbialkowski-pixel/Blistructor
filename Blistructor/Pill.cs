using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;

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
    public class CuttedPill
    {
        private static readonly ILog log = LogManager.GetLogger("Cutter.CuttedPill");
        private PillCutProposals _approvedProposal;

        public CuttedPill(PillCutProposals approvedProposal)
        {
            _approvedProposal = approvedProposal;
        }

        private Blister Blister { get => _approvedProposal.CutOut; }
        private Pill Pill { get => _approvedProposal.Pill; }

        private List<AnchorPoint> Anchors { get => Blister._workspace.anchor.anchors; }

        public JObject GetDisplayJSON()
        {
            JObject data = Pill.GetDisplayJSON();
            // Add displayCut data
            if (_approvedProposal.BestCuttingData != null) data.Add("displayCut", _approvedProposal.BestCuttingData.GetDisplayJSON(Anchors[0].location));
            else data.Add("displayCut", new JArray());
            return data;
        }

        public JObject GetJSON()
        {
            JObject data = Pill.GetJSON();
            Point3d Jaw1_Local = Blister._workspace.anchor.anchors[0].location;
            // Add Cutting Instruction
            if (_approvedProposal.BestCuttingData != null) data.Add("cutInstruction", _approvedProposal.BestCuttingData.GetJSON(Jaw1_Local));
            else data.Add("cutInstruction", new JArray());
            return data;
        }
    }

    public class PillCutProposals
    {
        private static readonly ILog log = LogManager.GetLogger("Cutter.PillCutProposals");
        protected List<CutData> _cuttingData;
        protected CutData _bestCuttingData;
        protected readonly Pill _pill;
        public Blister CutOut { get; private set; }
        public Blister Current { get; private set; }
        public List<Blister> ExtraBlisters { get; private set; }

        public PillCutProposals(Pill proposedPillToCut, List<CutData> cuttingData, CutState state)
        {
            // if _pill.state is cutted, dont do normal stuff, just rewrite fields...
            _pill = proposedPillToCut;
            State = state;
            _cuttingData = SortCuttingData(cuttingData);
            _bestCuttingData = FindBestCut();
        }

        protected PillCutProposals(PillCutProposals proposal)
        {
            _pill = proposal._pill;
            _cuttingData = proposal._cuttingData;
            _bestCuttingData = proposal._bestCuttingData;
            State = proposal.State;
        }

        //protected PillCutProposals(PillCutter cutter) : this(cutter.Pill, cutter.CuttingData, cutter.State)
        //{ }
   
        #region PROPERTIES
        public CutState State { get; private set; }
        public CutData BestCuttingData { get => _bestCuttingData; }

        public Pill Pill { get => _pill; }

        public Blister Blister { get => _pill.subBlister; }

        public List<PolylineCurve> GetPaths()
        {
            List<PolylineCurve> output = new List<PolylineCurve>();
            foreach (CutData cData in _cuttingData)
            {
                output.AddRange(cData.Path);
            }
            return output;
        }
        #endregion

        /// <summary>
        /// Sort cutted data, so the best is first on list.
        /// </summary>
        private List<CutData> SortCuttingData(List<CutData> cuttingData)
        {
            // Order by number of cuts to be performed.
            return cuttingData.OrderBy(x => x.EstimatedCuttingCount * x.Polygon.GetBoundingBox(false).Area * x.BlisterLeftovers.Select(y => y.PointCount).Sum()).ToList();
        }

        /// <summary>
        /// Get best Cutting Data from all generated and asign it to /bestCuttingData/ field.
        /// </summary>
        private CutData FindBestCut()
        {
            foreach (CutData cData in _cuttingData)
            {
                if (!cData.GenerateBladeFootPrint()) continue;
                return cData;
            }
            return null;
        }

        /// <summary>
        /// Check if proposed Cuting is Valid. If not update State property to FALSE
        /// </summary>
        public void ValidateCut()
        {
            // Inspect leftovers.
            foreach (PolylineCurve leftover in BestCuttingData.BlisterLeftovers)
            {
                Blister newBli = new Blister(Pill.subBlister.Pills, leftover, Pill.subBlister._workspace);
                if (!newBli.CheckConnectivityIntegrity(_pill))
                {
                    log.Warn("CheckConnectivityIntegrity failed. Propsed cut cause inconsistency in leftovers");
                    State = CutState.Failed;
                    return;
                }
                // BEFORE THIS I NEED TO UPDATE ANCHORS.
                // If after cutting none pill in leftovers has HasPosibleAnchor false, this mean BAAAAAD
                if (!newBli.HasActiveAnchor)
                {
                    log.Warn("No Anchor found for this leftover. Skip this cutting.");
                    State = CutState.Failed;
                    return;
                }
            }
        }

        public void Approve()
        {
            if (State == CutState.Failed)
            {
                throw new Exception("Cannot approve failed cut. Big mistake!!!!");
            }

            ExtraBlisters = new List<Blister>();
            if (State == CutState.Alone)
            {
                CutOut = Blister;
                Current = null;
                return;
            }

            #region Update Pill
            log.Debug("Removing Connection data from cutted Pill. Updating pill status to Cutted");
            _pill.State = PillState.Cutted;
            _pill.RemoveConnectionData();
            #endregion

            #region Update Current
            log.Debug("Updating current Blister outline and remove cutted Pill from blister");
            Blister.Outline = BestCuttingData.BlisterLeftovers[0];
            int locationIndex = Blister.Pills.FindIndex(pill => pill.Id == _pill.Id);
            Blister.Pills.RemoveAt(locationIndex);

            // Case if Blister is splited because of this cut.
            log.Debug("Remove all cells which are not belong to this Blister anymore.");
            List<Pill> removerdPills = new List<Pill>(Blister.Pills.Count);
            for (int i = 0; i < Blister.Pills.Count; i++)
            {
                // If cell is no more inside this Blister, remove it.
                if (!Geometry.InclusionTest(Blister.Pills[i], Blister))
                {
                    // check if cell is aimed to cut. For 100% all cells in Blister should be Queue.. If not it;s BUGERSON
                    if (Blister.Pills[i].State != PillState.Queue) continue;
                    removerdPills.Add(Blister.Pills[i]);
                    Blister.Pills.RemoveAt(i);
                    i--;
                }
            }
            #endregion
           
            #region create CutOut
            CutOut = new Blister(_pill, BestCuttingData.Polygon, Blister._workspace);
            #endregion

            #region Leftovers
            for (int j = 1; j < BestCuttingData.BlisterLeftovers.Count; j++)
            {
                PolylineCurve blisterLeftover = BestCuttingData.BlisterLeftovers[j];
                Blister newBli = new Blister(removerdPills, blisterLeftover, Blister._workspace);
                // Verify if new Blister is attachetd to anchor
                if (newBli.HasPossibleAnchor)
                {
                };
                ExtraBlisters.Add(newBli);
            }
            #endregion
        }
        /// <summary>
        /// Computes restricted area for graspers to ommit collision with knife.
        /// </summary>
        /// <returns>List of restricted areas for graspers as BoundingBoxes</returns>
        public List<BoundingBox> ComputGrasperRestrictedAreas()
        {
            // Thicken paths from cutting data anch check how this influance 
            List<BoundingBox> allRestrictedArea = new List<BoundingBox>(_bestCuttingData.Segments.Count);
            foreach (PolylineCurve ply in _bestCuttingData.Segments)
            {
                //Create upperLine - max distance where Jaw can operate
                LineCurve uppeLimitLine = new LineCurve(new Line(new Point3d(-Setups.IsoRadius, Setups.JawDepth, 0), Vector3d.XAxis, 2 * Setups.IsoRadius));

                //Create lowerLimitLine - lower line where for this segment knife can operate
                double knifeY = ply.ToPolyline().OrderBy(pt => pt.Y).First().Y;
                LineCurve lowerLimitLine = new LineCurve(new Line(new Point3d(-Setups.IsoRadius, knifeY, 0), Vector3d.XAxis, 2 * Setups.IsoRadius));

                //Check if knife semgnet intersect with Upper line = knife-jwa colision can occure
                List<IntersectionEvent> checkIntersect = Intersection.CurveCurve(uppeLimitLine, ply, Setups.IntersectionTolerance);

                // If intersection occures, any
                if (checkIntersect.Count > 0)
                {

                    PolylineCurve extPly = (PolylineCurve)ply.Extend(CurveEnd.Both, 100);

                    // create knife "impact area"
                    PolylineCurve knifeFootprint = Geometry.PolylineThicken(extPly, Setups.BladeWidth / 2);

                    if (knifeFootprint == null) continue;

                    LineCurve cartesianLimitLine = Anchor.CreateCartesianLimitLine();
                    // Split knifeFootprint by upper and lower line
                    List<PolylineCurve> splited = (List<PolylineCurve>)Geometry.SplitRegion(knifeFootprint, cartesianLimitLine).Select(crv => (PolylineCurve)crv);

                    if (splited.Count != 2) continue;

                    PolylineCurve forFurtherSplit = splited.OrderBy(pline => pline.CenterPoint().Y).Last();

                    LineCurve upperCartesianLimitLine = new LineCurve(cartesianLimitLine);

                    splited = (List<PolylineCurve>)Geometry.SplitRegion(forFurtherSplit, upperCartesianLimitLine).Select(crv => (PolylineCurve)crv);

                    if (splited.Count != 2) continue;

                    PolylineCurve grasperRestrictedArea = splited.OrderBy(pline => pline.CenterPoint().Y).First();

                    // After spliting, there is area where knife can operate.
                    // Transform ia to Interval as min, max values where jaw should not appear

                    BoundingBox grasperRestrictedAreaBBox = grasperRestrictedArea.GetBoundingBox(false);
                    /*
                    Interval restrictedInterval = new Interval(grasperRestrictedAreaBBox.Min.Y,
                        grasperRestrictedAreaBBox.Max.Y);
                    restrictedInterval.MakeIncreasing();
                    allRestrictedArea.Add(restrictedInterval);
                    
                    */
                    allRestrictedArea.Add(grasperRestrictedAreaBBox);
                }
            }
            return allRestrictedArea;
        }

    }

    public class PillCutter
    {
        private static readonly ILog log = LogManager.GetLogger("Cutter.PillCutter");
        private List<CutData> CuttingData { get; set; }
        private Blister Blister { get; set;}

        private Pill Pill { get; set; }

        public PillCutter(Blister blisterTotCut)
        {
            Blister = blisterTotCut;
        }

        public PillCutProposals CutNext(bool onlyAnchor = false)
        {
            if (!onlyAnchor)
            {
                for (int i = 0; i < Blister.Pills.Count; i++)
                {
                    Pill currentPill = Blister.Pills[i];
                    if (currentPill.IsAnchored) continue;
                    PillCutProposals proposal = TryCut(currentPill);
                    proposal.ValidateCut();
                    if (proposal.State == CutState.Failed) continue;
                    else
                    {
                        log.Info(String.Format("Cut Path found for pill {0} after checking {1} pills", currentPill.Id, i));
                        return proposal;
                    }
                }
                // If nothing, try to cut anchored ones...
                log.Warn("No cutting data generated for whole Blister. Try to find cutting data in anchored...");
            }
            for (int i = 0; i < Blister.Pills.Count; i++)
            {
                Pill currentPill = Blister.Pills[i];
                if (!currentPill.IsAnchored) continue;
                PillCutProposals proposal = TryCut(currentPill);
                proposal.ValidateCut();
                if (proposal.State == CutState.Failed) continue;
                else
                {
                    log.Info(String.Format("Cut Path found for pill {0} after checking {1} anchored pills", currentPill.Id, i));
                    return proposal;
                }
            }
            log.Warn("No cutting data generated for whole Blister.");
            throw new Exception("No cutting data generated for whole Blister.");
        }

        public PillCutProposals TryCut(Pill pillToCut)
        {
            CuttingData = new List<CutData>();
            Pill = pillToCut;
            log.Info(String.Format("Trying to cut Outline id: {0} with status: {1}", pillToCut.Id, pillToCut.State));
            // If Outline is cutted, dont try to cut it again... It supose to be in cutted blisters list...
            if (pillToCut.State == PillState.Cutted)
            {
               // State = CutState.Succeed;
                return new PillCutProposals(pillToCut, CuttingData, CutState.Succeed);
            }

            // If Outline is not surrounded by other Outline, update data
            log.Debug(String.Format("Check if Outline is alone on blister: No. adjacent pills: {0}", pillToCut.adjacentPills.Count));
            if (pillToCut.adjacentPills.Count == 0)
            {
                //state = PillState.Alone;
                log.Debug("This is last Outline on blister.");
                //State = CutState.Alone;
                //return;
                return new PillCutProposals(pillToCut, CuttingData, CutState.Alone);
            }

            // If still here, try to cut 
            log.Debug("Perform cutting data generation");
            if (GenerateSimpleCuttingData_v2())
            {
                //State = CutState.Succeed;
               // return;
                return new PillCutProposals(pillToCut, CuttingData, CutState.Succeed);

            }
            //State = CutState.Failed;
            //return;
            return new PillCutProposals(pillToCut, CuttingData, CutState.Failed);
        }

        #region CUT STUFF
        private bool GenerateSimpleCuttingData_v2()
        {
            Pill.UpdateObstacles();

            log.Debug(String.Format("Obstacles count {0}", Pill.obstacles.Count));
            CuttingData = new List<CutData>();
            // Stage I - naive Cutting
            // Get cutting Directions
            //PolygonBuilder_v2(GenerateIsoCurvesStage0());
            //log.Info(String.Format(">>>After STAGE_0: {0} cuttng possibilietes<<<", cuttingData.Count));
            PolygonBuilder_v2(GenerateIsoCurvesStage1());
            log.Info(String.Format(">>>After STAGE_1: {0} cuttng possibilietes<<<", CuttingData.Count));
            PolygonBuilder_v2(GenerateIsoCurvesStage2());
            log.Info(String.Format(">>>After STAGE_2: {0} cuttng possibilietes<<<", CuttingData.Count));
            IEnumerable<IEnumerable<LineCurve>> isoLines = GenerateIsoCurvesStage3a(1, 2.0);
            foreach (List<LineCurve> isoLn in isoLines)
            {
                PolygonBuilder_v2(isoLn);
            }

            //PolygonBuilder_v2(GenerateIsoCurvesStage3a(1, 2.0));
            log.Info(String.Format(">>>After STAGE_3: {0} cuttng possibilietes<<<", CuttingData.Count));
            if (CuttingData.Count > 0) return true;
            else return false;
        }

        private bool GenerateAdvancedCuttingData()
        {
            // If all Stages 1-3 failed, start looking more!
            if (CuttingData.Count == 0)
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
            if (CuttingData.Count > 0) return true;
            else return false;
        }

        #endregion

        #region Polygon Builder Stuff

        // All methods will generat full Rays, without trimming to blister! PoligonBuilder is responsible for trimming.
        private List<LineCurve> GenerateIsoCurvesStage0()
        {

            List<LineCurve> isoLines = new List<LineCurve>(Pill.samplePoints.Count);
            for (int i = 0; i < Pill.samplePoints.Count; i++)
            {
                Vector3d direction = Vector3d.CrossProduct((Pill.connectionLines[i].PointAtEnd - Pill.connectionLines[i].PointAtStart), Vector3d.ZAxis);
                //direction = StraigtenVector(direction);
                LineCurve isoLine = Geometry.GetIsoLine(Pill.samplePoints[i], direction, Setups.IsoRadius, Pill.obstacles);
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
                LineCurve isoLine = Geometry.GetIsoLine(Pill.samplePoints[i], direction, Setups.IsoRadius, Pill.obstacles);
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
                LineCurve isoLine = Geometry.GetIsoLine(Pill.samplePoints[i], direction, Setups.IsoRadius, Pill.obstacles);
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
                LineCurve isoLine = Geometry.GetIsoLine(Pill.samplePoints[i], sum_direction, Setups.IsoRadius, Pill.obstacles);
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
                    LineCurve isoLine = Geometry.GetIsoLine(Pill.samplePoints[i], sum_direction, Setups.IsoRadius, Pill.obstacles);
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
                ArcCurve arc = new ArcCurve(new Arc(cir, new Interval(0, Math.PI)));
                double[] t = arc.DivideByCount(count, false);
                for (int j = 0; j < t.Length; j++)
                {
                    Point3d Pt = arc.PointAt(t[j]);
                    LineCurve ray = Geometry.GetIsoLine(Pill.samplePoints[i], Pt - Pill.samplePoints[i], Setups.IsoRadius, Pill.obstacles);
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
            Tuple<List<Curve>, List<Curve>> result = Geometry.TrimWithRegion(ray, Pill.subBlister.Outline);
            if (result.Item1.Count < 1) return outLine;
            // log.Debug("After trimming.");
            foreach (Curve crv in result.Item1)
            {
                PointContainment test = Pill.subBlister.Outline.Contains(crv.PointAtNormalizedLength(0.5), Plane.WorldXY, 0.1);
                if (test == PointContainment.Inside) return (LineCurve)crv;

            }
            return outLine;
        }

        /// <summary>
        /// Generates closed polygon around Outline based on rays (cutters) combination
        /// </summary>
        /// <param name="rays"></param>
        private void PolygonBuilder_v2(List<LineCurve> rays)
        {
            /*
             String path = String.Format("D:\\PIXEL\\Blistructor\\DebugModels\\PolygonBuilder_Rays_{0}.3dm", id);
             File3dm file = new File3dm();
             if (File.Exists(path)) { 
                file = File3dm.Read(path); 
                 file.Objects.AddCurve(subBlister.Outline);
                 file.Objects.AddCurve(pillOffset);
                 this.obstacles.ForEach(crv => file.Objects.AddCurve(crv)) ;
             }
             */
            // file.Dump();
            // Trim incomming rays and build current working full ray aray.
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
                    cutData.Obstacles = Pill.obstacles;
                    CuttingData.Add(cutData);
                }
            }
            // file.Write(path, 6);
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
            /*
            String path = String.Format("D:\\PIXEL\\Blistructor\\DebugModels\\VerifyPath_{0}.3dm", id);
            File3dm file = new File3dm();
            if (File.Exists(path))
            {
                file = File3dm.Read(path);
            }
            */


            log.Debug(string.Format("Verify path. Segments: {0}", pathCrv.Count));
            if (pathCrv == null) return null;
            // Check if this curves creates closed polygon with blister edge.
            List<Curve> splitters = pathCrv.Cast<Curve>().ToList();
            List<Curve> splited_blister = Geometry.SplitRegion(Pill.subBlister.Outline, splitters);
            // If after split there is less then 2 region it means nothing was cutted and bliseter stays unchanged
            if (splited_blister == null) return null;
            //splitters.ForEach(crv => file.Objects.AddCurve(crv));
            //splited_blister.ForEach(crv => file.Objects.AddCurve(crv));
            // file.Write(path, 6);
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
            foreach (Pill pill in Pill.subBlister.Pills)
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

    public class Pill
    {
        private static readonly ILog log = LogManager.GetLogger("Cutter.Pill");

        public int Id { get;  }

        // Parent Blister
        internal Blister subBlister;

        // States
        public List<AnchorPoint> Anchors;
        public bool possibleAnchor;

        // Pill Stuff
        public PolylineCurve Outline;
        public PolylineCurve Offset;

        private Point3d Center;

        // Connection and Adjacent Stuff
        public PolylineCurve voronoi;
        //!!connectionLines, proxLines, adjacentPills, samplePoints <- all same sizes, and order!!
        public List<Curve> connectionLines;
        public List<Curve> proxLines;
        public List<Pill> adjacentPills;
        public List<Point3d> samplePoints;

        public List<Curve> obstacles;

        public Pill(Pill pill)
        {
            Id = pill.Id;
            subBlister = pill.subBlister;
            Anchors = pill.Anchors;
            possibleAnchor = pill.possibleAnchor;
            Outline = pill.Outline;
            Offset = pill.Offset;
            Center = pill.Center;
        }

        public Pill(int id, PolylineCurve pill, Blister subblister)
        {
            Id = id;
            State = PillState.Queue;
            subBlister = subblister;
            // Prepare all needed Pill properties
            Outline = pill;
            // Make Pill curve oriented in proper direction.
            Geometry.UnifyCurve(Outline);

            //Anchor = new AnchorPoint();
            Anchors = new List<AnchorPoint>(2);
            possibleAnchor = false;

            Center = Outline.ToPolyline().CenterPoint();

            // Create Outline offset
            Curve ofCur = Outline.Offset(Plane.WorldXY, Setups.BladeWidth / 2);
            if (ofCur == null)
            {
                log.Error("Incorrect Outline offseting");
                throw new InvalidOperationException("Incorrect Outline offseting");
            }
            else
            {
                Offset = (PolylineCurve)ofCur;
            }
        }

        #region PROPERTIES
        public Point3d PillCenter
        {
            //get { return pillProp.Centroid; } //
            get { return Center; } //

        }

        public Blister SubBlister
        {
            set
            {
                subBlister = value;
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

        public PillState State { get; set; }

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

        public bool IsAnchored
        {
            get
            {
                return Anchors.Any(anchor => anchor.state == AnchorState.Active);
            }
        }

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

        public void AddConnectionData(List<Pill> pills, List<Curve> lines, List<Point3d> midPoints)
        {
            adjacentPills = new List<Pill>();
            samplePoints = new List<Point3d>();
            connectionLines = new List<Curve>();

            EstimateOrientationCircle();
            int[] ind = Geometry.SortPtsAlongCurve(midPoints, OrientationCircle);

            foreach (int id in ind)
            {
                adjacentPills.Add(pills[id]);
                connectionLines.Add(lines[id]);
            }
            proxLines = new List<Curve>();
            foreach (Pill pill in adjacentPills)
            {
                Point3d ptA, ptB;
                if (Offset.ClosestPoints(pill.Offset, out ptA, out ptB))
                {
                    LineCurve proxLine = new LineCurve(ptA, ptB);
                    proxLines.Add(proxLine);
                    Point3d samplePoint = proxLine.Line.PointAt(0.5);   //PointAtNormalizedLength(0.5);
                    samplePoints.Add(samplePoint);
                }
            }
        }

        public void RemoveConnectionData()
        {
            for (int i = 0; i < adjacentPills.Count; i++)
            {
                adjacentPills[i].RemoveConnectionData(Id);
            }
        }

        /// <summary>
        /// Call from adjacent Outline
        /// </summary>
        /// <param name="pillId">ID of Pill which is executing this method</param>
        protected void RemoveConnectionData(int pillId)
        {
            for (int i = 0; i < adjacentPills.Count; i++)
            {
                if (adjacentPills[i].Id == pillId)
                {
                    adjacentPills.RemoveAt(i);
                    connectionLines.RemoveAt(i);
                    proxLines.RemoveAt(i);
                    samplePoints.RemoveAt(i);
                    i--;
                }
            }
            SortData();
        }

        public List<int> GetAdjacentPillsIds()
        {
            return adjacentPills.Select(pill => pill.Id).ToList();
        }
        public void EstimateOrientationCircle()
        {
            double circle_radius = Outline.GetBoundingBox(false).Diagonal.Length / 2;
            OrientationCircle = (new Circle(PillCenter, circle_radius)).ToNurbsCurve();
            Geometry.EditSeamBasedOnCurve(OrientationCircle, subBlister.Outline);
        }

        public void SortData()
        {
            EstimateOrientationCircle();
            int[] sortingIndexes = Geometry.SortPtsAlongCurve(samplePoints, OrientationCircle);

            samplePoints = sortingIndexes.Select(index => samplePoints[index]).ToList();
            connectionLines = sortingIndexes.Select(index => connectionLines[index]).ToList();
            proxLines = sortingIndexes.Select(index => proxLines[index]).ToList();
            adjacentPills = sortingIndexes.Select(index => adjacentPills[index]).ToList();
        }

        // Get ProxyLines without lines pointed as Id
        public List<Curve> GetUniqueProxy(int id)
        {
            List<Curve> proxyLines = new List<Curve>();
            for (int i = 0; i < adjacentPills.Count; i++)
            {
                if (adjacentPills[i].Id != id)
                {
                    proxyLines.Add(proxLines[i]);
                }
            }
            return proxyLines;
        }

        public Dictionary<int, Curve> GetUniqueProxy_v2(int id)
        {
            Dictionary<int, Curve> proxData = new Dictionary<int, Curve>();

            //List<Curve> proxyLines = new List<Curve>();
            for (int i = 0; i < adjacentPills.Count; i++)
            {
                if (adjacentPills[i].Id != id)
                {
                    proxData.Add(adjacentPills[i].Id, proxLines[i]);
                    // proxyLines.Add(proxLines[i]);
                }
            }
            return proxData;
        }

        public void UpdateObstacles()
        {
            obstacles = BuildObstacles_v2();
        }
        private List<Curve> BuildObstacles_v2()
        {
            List<Curve> worldObstacles = new List<Curve>() { this.subBlister._workspace.anchor.cartesianLimitLine };
            // TODO: Adding All Pils Offsets as obstaces...
            List<Curve> limiters = new List<Curve> { Offset };
            if (worldObstacles != null) limiters.AddRange(worldObstacles);
            Dictionary<int, Curve> uniquePillsOffset = new Dictionary<int, Curve>();

            for (int i = 0; i < adjacentPills.Count; i++)
            {
                // limiters.Add(adjacentPills[i].pillOffset);
                Dictionary<int, Curve> proxDict = adjacentPills[i].GetUniqueProxy_v2(Id);
                uniquePillsOffset[adjacentPills[i].Id] = adjacentPills[i].Offset;
                //List<Curve> prox = adjacentPills[i].GetUniqueProxy(id);
                foreach (KeyValuePair<int, Curve> prox_crv in proxDict)
                {
                    uniquePillsOffset[prox_crv.Key] = subBlister.PillByID(prox_crv.Key).Offset;

                    if (Geometry.CurveCurveIntersection(prox_crv.Value, proxLines).Count == 0)
                    {
                        limiters.Add(prox_crv.Value);
                    }
                }
            }
            limiters.AddRange(uniquePillsOffset.Values.ToList());
            return Geometry.RemoveDuplicateCurves(limiters);
        }
        private List<Curve> BuildObstacles()
        {
            List<Curve> limiters = new List<Curve> { Offset };
            for (int i = 0; i < adjacentPills.Count; i++)
            {
                limiters.Add(adjacentPills[i].Offset);
                List<Curve> prox = adjacentPills[i].GetUniqueProxy(Id);
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
        public JObject GetDisplayJSON()
        {
            List<AnchorPoint>  anchors = subBlister._workspace.anchor.anchors;
            JObject data = new JObject();
            data.Add("pillIndex", this.Id);
            //Pill
            JArray pillDisplayData = new JArray();
            PolylineCurve imagePill = (PolylineCurve)Geometry.ReverseCalibration(Outline, Setups.ZeroPosition, Setups.PixelSpacing, Setups.CartesianPickModeAngle);

            foreach (Point3d pt in imagePill.ToPolyline())
            {
                pillDisplayData.Add(new JArray() { pt.X, pt.Y });
            }
            data.Add("processingPill", pillDisplayData);
            Point3d jaw2 = ((Point)Geometry.ReverseCalibration(new Point(anchors[0].location), Setups.ZeroPosition, Setups.PixelSpacing, Setups.CartesianPickModeAngle)).Location;
            Point3d jaw1 = ((Point)Geometry.ReverseCalibration(new Point(anchors[1].location), Setups.ZeroPosition, Setups.PixelSpacing, Setups.CartesianPickModeAngle)).Location;

            JArray anchorPossitions = new JArray() {
                new JArray() { jaw2.X, jaw2.Y },
                new JArray() { jaw1.X, jaw1.Y }
            };
            data.Add("anchors", anchorPossitions);
            return data;
        }

        public JObject GetJSON()
        {
            Point3d Jaw1_Local = subBlister._workspace.anchor.anchors[0].location;
            //Get JAW2
            Jaw1_Local = subBlister._workspace.anchor.anchors.Where(anchor => anchor.orientation == AnchorSite.JAW_2).First().location;

            JObject data = new JObject();
            data.Add("pillIndex", this.Id);
            // Add Anchor Data <- to be implement.
            data.Add("openJaw", new JArray(Anchors.Select(anchor => anchor.orientation.ToString().ToLower())));
            return data;
        }
    }
}
