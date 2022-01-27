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
    public class CutProposal
    {
        private static readonly ILog log = LogManager.GetLogger("Cutter.PillCutProposals");
        protected List<CutData> _cuttingData;
        protected CutData _bestCuttingData;
        protected readonly Pill _pill;

        #region CONTRUCTORS
        public CutProposal(Pill proposedPillToCut, List<CutData> cuttingData, CutState state)
        {
            // if _pill.state is cutted, dont do normal stuff, just rewrite fields...
            _pill = proposedPillToCut;
            State = state;
            _cuttingData = SortCuttingData(cuttingData);
            _bestCuttingData = FindBestCut();
        }
        protected CutProposal(CutProposal proposal)
        {
            _pill = proposal._pill;
            _cuttingData = proposal._cuttingData;
            _bestCuttingData = proposal._bestCuttingData;
            State = proposal.State;
        }
        #endregion

        #region PROPERTIES
        public CutState State { get; private set; }
        public CutData BestCuttingData { get => _bestCuttingData; }

        public Pill Pill { get => _pill; }

        public Blister Blister { get => _pill.blister; }

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
                Blister newBli = new Blister(Pill.blister.Pills, leftover, Pill.blister._workspace);
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

        /// <summary>
        /// Get Cutout and remove Pill and any connection data for that pill from current blister. 
        /// </summary>
        /// <returns></returns>
        public CuttedBlister GetCutoutAndRemoveFomBlister()
        {
            switch (State)
            {
                case CutState.Failed:
                    throw new Exception("Cannot apply cutting on failed CutStates proposal. Big mistake!!!!");
                case CutState.Last:
                    return new CuttedBlister(_pill, BestCuttingData, Blister._workspace);
                case CutState.Succeed:
                    // Update Pill & Create CutOut
                    log.Debug("Removing Connection data from cutted Pill. Updating pill status to Cutted");
                    _pill.State = PillState.Cutted;
                    _pill.RemoveConnectionData();
                    //Update Current

                    int locationIndex = Blister.Pills.FindIndex(pill => pill.Id == _pill.Id);
                    Blister.Pills.RemoveAt(locationIndex);

                    return new CuttedBlister(_pill, BestCuttingData, Blister._workspace);
                default:
                    throw new NotImplementedException($"This state {State} is not implemented!");
            }
        }

        /// <summary>
        /// !!!! This method has to be used after GetCutoutAndRemoveFomBlister!!!!
        /// Apply all leftovers on current blister.
        /// </summary>
        /// <returns>List of blister, where firts element is Curent Updated blister. Other elemnts are parts of current blister after cutting.</returns>
        public List<Blister> GetLeftoversAndUpdateCurrentBlister()
        {
            switch (State)
            {
                case CutState.Failed:
                    throw new Exception("Cannot apply cutting on failed CutStates proposal. Big mistake!!!!");

                case CutState.Last:
                    return new List<Blister>();
                case CutState.Succeed:
                    log.Debug("Updating current Blister outline and remove cutted Pill from blister");
                    Blister.Outline = BestCuttingData.BlisterLeftovers[0];
                    // Case if Blister is splited because of this cut.
                    log.Debug("Remove all cells which are not belong to this Blister anymore.");
                    List<Pill> removerdPills = new List<Pill>(Blister.Pills.Count);
                    for (int i = 0; i < Blister.Pills.Count; i++)
                    {
                        // If cell is no more inside this Blister, remove it.
                        if (!Geometry.InclusionTest(Blister.Pills[i], Blister))
                        {
                            // check if cell is aimed to cut. For 100% all cells in Blister should be Queue.
                            if (Blister.Pills[i].State != PillState.Queue)
                            {
                                throw new Exception($"Found Pill with state {Blister.Pills[i].State} in queued blister. All pills should have stat QUEUED!. Unknow error.");
                            }
                            //Remove pill reference to current blister
                            Blister.Pills[i].blister = null;
                            removerdPills.Add(Blister.Pills[i]);
                            Blister.Pills.RemoveAt(i);
                            i--;
                        }
                    }
                    List<Blister> leftovers = new List<Blister>(BestCuttingData.BlisterLeftovers.Count);

                    leftovers.Add(Blister);

                    for (int j = 1; j < BestCuttingData.BlisterLeftovers.Count; j++)
                    {
                        PolylineCurve blisterLeftover = BestCuttingData.BlisterLeftovers[j];
                        Blister newBli = new Blister(removerdPills, blisterLeftover, Blister._workspace);
                        // Verify if new Blister is attachetd to anchor
                        if (newBli.HasPossibleAnchor)
                        {
                        };
                        leftovers.Add(newBli);
                    }
                    List<Pill> abandonePills = removerdPills.Where(pill => pill.blister == null).ToList();
                    if (abandonePills.Count > 0)
                    {
                        throw new Exception($"Abandone pills after applying cutting data: {abandonePills.Count}");
                    }
                    return leftovers;
                default:
                    throw new NotImplementedException($"This state {State} is not implemented!");
            }
        }

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

        #region PREVIEW STUFF FOR DEBUG MOSTLY

        public List<PolylineCurve> GetCuttingPath()
        {
            // !!!============If cell is anchor it probably doesn't have cutting stuff... To validate===========!!!!
            if (BestCuttingData == null) return new List<PolylineCurve>();

            // cells[0].bestCuttingData.GenerateBladeFootPrint();
            return BestCuttingData.Path;
        }

        public List<LineCurve> GetCuttingLines()
        {
            // !!!============If cell is anchor it probably doesn't have cutting stuff... To validate===========!!!!
            if (BestCuttingData == null) return new List<LineCurve>();

            // cells[0].bestCuttingData.GenerateBladeFootPrint();
            return BestCuttingData.bladeFootPrint;
        }
        public List<LineCurve> GetIsoRays()
        {
            // !!!============If cell is anchor it probably doesn't have cutting stuff... To validate===========!!!!
            if (BestCuttingData == null) return new List<LineCurve>();
            // cells[0].bestCuttingData.GenerateBladeFootPrint();
            return BestCuttingData.IsoSegments;
            //return cells[0].bestCuttingData.IsoRays;

        }
        public List<PolylineCurve> GetLeftOvers()
        {
            if (BestCuttingData == null) return new List<PolylineCurve>();
            return BestCuttingData.BlisterLeftovers;
        }
        public List<PolylineCurve> GetAllPossiblePolygons()
        {
            // !!!============If cell is anchor it probably doesn't have cutting stuff... To validate===========!!!!
            if (BestCuttingData == null) return new List<PolylineCurve>();
            return BestCuttingData.BlisterLeftovers;
        }

        #endregion
    }

}
