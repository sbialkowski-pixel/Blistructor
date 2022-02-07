using System;
using System.Collections.Generic;
using System.Linq;

#if PIXEL
using Pixel.Rhino.Geometry;
#else
using Rhino;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;
using ExtraMath = Rhino.RhinoMath;
#endif

using log4net;

namespace Blistructor
{
    public class CutProposal
    {
        private static readonly ILog log = LogManager.GetLogger("Cutter.PillCutProposals");
        private List<CutData> CuttingData { get; set; }

        #region CONTRUCTORS
        public CutProposal(Pill proposedPillToCut, List<CutData> cuttingData, CutState state)
        {
            // if _pill.state is CUT, just rewrite fields...
            Pill = proposedPillToCut;
            State = state;
            CuttingData = SortCuttingData(cuttingData);
            BestCuttingData = FindBestCut();
        }
        protected CutProposal(CutProposal proposal)
        {
            Pill = proposal.Pill;
            CuttingData = proposal.CuttingData;
            BestCuttingData = proposal.BestCuttingData;
            State = proposal.State;
        }
        #endregion

        #region PROPERTIES
        public CutState State { get; private set; }
        public CutData BestCuttingData { get; private set; }

        public Pill Pill { get; private set; }

        public Blister Blister { get => Pill.blister; }

        public List<PolylineCurve> GetPaths()
        {
            List<PolylineCurve> output = new List<PolylineCurve>();
            foreach (CutData cData in CuttingData)
            {
                output.AddRange(cData.Path);
            }
            return output;
        }
        #endregion

        /// <summary>
        /// Sort cutting data, so the best is first on list.
        /// </summary>
        private List<CutData> SortCuttingData(List<CutData> cuttingData)
        {
            // Order by number of cuts to be performed.
            return cuttingData.OrderBy(x => x.EstimatedCuttingCount * x.Polygon.GetBoundingBox(false).Area * x.BlisterLeftovers.Select(y => y.PointCount).Sum()).ToList();
        }

        /// <summary>
        /// Get best Cutting Data from all generated and assign it to /bestCuttingData/ field.
        /// </summary>
        private CutData FindBestCut()
        {
            foreach (CutData cData in CuttingData)
            {
                if (!cData.GenerateBladeFootPrint()) continue;
                return cData;
            }
            return null;
        }

        /// <summary>
        /// Check Pills connection intrgrity in each leftoves after current cutting.
        /// </summary>
        /// <returns> If there is no integrity, update State property to FALSE and return false.</returns>
        public bool ValidateConnectivityIntegrityInLeftovers()
        {
            // Inspect leftovers.
            foreach (PolylineCurve leftover in BestCuttingData.BlisterLeftovers)
            {
                Blister newBli = new Blister(Pill.blister.Pills, leftover);
                if (!newBli.CheckConnectivityIntegrity(Pill))
                {
                    log.Warn("CheckConnectivityIntegrity failed. Proposed cut cause inconsistency in leftovers");
                    State = CutState.Failed;
                    return false;
                }
                // BEFORE THIS I NEED TO UPDATE ANCHORS.
                // If after cutting none pill in leftovers has HasPosibleAnchor false, this mean BAAAAAD
                /*
                if (!newBli.HasActiveAnchor)
                {
                    log.Warn("No Anchor found for this leftover. Skip this cutting.");
                    State = CutState.Failed;
                    return;
                }
                */
            }
            return true;
        }


        public bool ValidateJawExistanceInLeftovers(Grasper grasper)
        {
            List<Interval> currentJawPosibleIntervals = grasper.GetJawPossibleIntervals();

            List<Interval> cutImpactIntervals = Grasper.ComputCutImpactInterval(BestCuttingData);
            Interval blisterImpactInterval = Grasper.ComputeTotalCutImpactInterval(BestCuttingData, cutImpactIntervals);
            // Cut not influancing grasper
            if (!blisterImpactInterval.IsValid) return true;
            // If this cut will remove whole jawPossibleLocation line, its is not good, at least it is last blister...
            if (blisterImpactInterval.IncludesInterval(Grasper.IntervalsInterval(currentJawPosibleIntervals), true)) return false;

            // Check for collision between current Jaws and cutImpactIntervals.
            List<JawPoint> currentJaws = Grasper.FindJawPoints(currentJawPosibleIntervals);
            List<Interval> currentJawsInterval = Grasper.GetRestrictedIntervals(currentJaws);
            if (Grasper.CollisionCheck(currentJawsInterval, cutImpactIntervals)) return false;
            //TODO: Update currentJawPosibleIntervals and chack if all Leftovers has Jaw.


            List<Interval> futureJawPosibleIntervals = Grasper.ApplyCutOnGrasperLocation(currentJawPosibleIntervals, BestCuttingData);
            List<LineCurve> futureJawPosibleLocation = Grasper.ConvertIntervalsToLines(futureJawPosibleIntervals);

            foreach (PolylineCurve leftover in BestCuttingData.BlisterLeftovers)
            {
                if (!Grasper.HasPlaceForJaw(futureJawPosibleLocation, leftover)) return false;
            }
            return true;
        }

        /// <summary>
        /// Get Chunk and remove Pill and any connection data for that pill from current blister. 
        /// </summary>
        /// <returns></returns>
        public CutBlister GetCutChunkAndRemoveItFomBlister()
        {
            switch (State)
            {
                case CutState.Failed:
                    throw new Exception("Cannot apply cutting on failed CutStates proposal. Big mistake!!!!");
                case CutState.Last:
                    return new CutBlister(Pill, BestCuttingData);
                case CutState.Succeed:
                    // Update Pill & Create CutOut
                    log.Debug("Removing Connection data from cut Pill. Updating pill status to Cut");
                    Pill.State = PillState.Cut;
                    Pill.RemoveConnectionData();
                    //Update Current

                    int locationIndex = Blister.Pills.FindIndex(pill => pill.Id == Pill.Id);
                    Blister.Pills.RemoveAt(locationIndex);

                    return new CutBlister(Pill, BestCuttingData);
                default:
                    throw new NotImplementedException($"This state {State} is not implemented!");
            }
        }

        /// <summary>
        /// !!!! This method has to be used after GetCutoutAndRemoveFomBlister!!!!
        /// Apply all leftovers on current blister.
        /// </summary>
        /// <returns>List of blister, where first element is Current Updated blister. Other elements are parts of current blister after cutting.</returns>
        public List<Blister> GetLeftoversAndUpdateCurrentBlister()
        {
            switch (State)
            {
                case CutState.Failed:
                    throw new Exception("Cannot apply cutting on failed CutStates proposal. Big mistake!!!!");
                case CutState.Last:
                    return new List<Blister>();
                case CutState.Succeed:
                    log.Debug("Updating current Blister outline and remove cut Pill from blister");
                    Blister.Outline = BestCuttingData.BlisterLeftovers[0];
                    // Case if Blister is split because of this cut.
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
                                throw new Exception($"Found Pill with state {Blister.Pills[i].State} in queued blister. All pills should have status QUEUED!. Unknown error.");
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
                        Blister newBli = new Blister(removerdPills, blisterLeftover);
                        // Verify if new Blister is attached to anchor
                        //if (newBli.HasPossibleAnchor)
                        //{
                        //};
                        leftovers.Add(newBli);
                    }
                    List<Pill> abandonePills = removerdPills.Where(pill => pill.blister == null).ToList();
                    if (abandonePills.Count > 0)
                    {
                        throw new Exception($"Abandon pills after applying cutting data: {abandonePills.Count}");
                    }
                    return leftovers;
                default:
                    throw new NotImplementedException($"This state {State} is not implemented!");
            }
        }

        /*
        public List<BoundingBox> ComputGrasperRestrictedAreas()
        {
            // Thicken paths from cutting data and check how this influence 
            List<BoundingBox> allRestrictedArea = new List<BoundingBox>(_bestCuttingData.Segments.Count);
            foreach (PolylineCurve ply in _bestCuttingData.Segments)
            {
                //Create upperLine - max distance where Jaw can operate
                LineCurve uppeLimitLine = new LineCurve(new Line(new Point3d(-Setups.IsoRadius, Setups.JawDepth, 0), Vector3d.XAxis, 2 * Setups.IsoRadius));

                //Create lowerLimitLine - lower line where for this segment knife can operate
                double knifeY = ply.ToPolyline().OrderBy(pt => pt.Y).First().Y;
                LineCurve lowerLimitLine = new LineCurve(new Line(new Point3d(-Setups.IsoRadius, knifeY, 0), Vector3d.XAxis, 2 * Setups.IsoRadius));

                //Check if knife segment intersect with Upper line = knife-jaw collision can occur
                List<IntersectionEvent> checkIntersect = Intersection.CurveCurve(uppeLimitLine, ply, Setups.IntersectionTolerance);

                // If intersection occurs, any
                if (checkIntersect.Count > 0)
                {

                    PolylineCurve extPly = (PolylineCurve)ply.Extend(CurveEnd.Both, 100);

                    // create knife "impact area"
                    PolylineCurve knifeFootprint = Geometry.PolylineThicken(extPly, Setups.BladeWidth / 2);

                    if (knifeFootprint == null) continue;

                    LineCurve cartesianLimitLine = Grasper.CreateCartesianLimitLine();
                    // Split knifeFootprint by upper and lower line
                    List<PolylineCurve> splited = (List<PolylineCurve>)Geometry.SplitRegion(knifeFootprint, cartesianLimitLine).Select(crv => (PolylineCurve)crv);

                    if (splited.Count != 2) continue;

                    PolylineCurve forFurtherSplit = splited.OrderBy(pline => pline.CenterPoint().Y).Last();

                    LineCurve upperCartesianLimitLine = new LineCurve(cartesianLimitLine);

                    splited = (List<PolylineCurve>)Geometry.SplitRegion(forFurtherSplit, upperCartesianLimitLine).Select(crv => (PolylineCurve)crv);

                    if (splited.Count != 2) continue;

                    PolylineCurve grasperRestrictedArea = splited.OrderBy(pline => pline.CenterPoint().Y).First();

                    // After split, there is area where knife can operate.
                    // Transform into Interval as min, max values where jaw should not appear

                    BoundingBox grasperRestrictedAreaBBox = grasperRestrictedArea.GetBoundingBox(false);
                    allRestrictedArea.Add(grasperRestrictedAreaBBox);
                }
            }
            return allRestrictedArea;
        }
*/

        #region PREVIEW STUFF FOR DEBUG MOSTLY
        /*
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
        */
        #endregion
    }

}
