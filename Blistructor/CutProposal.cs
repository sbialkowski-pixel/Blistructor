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
        internal List<CutData> CuttingData { get; set; }

        private List<CutData> AlreadyCutData { get; set; }

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
        public CutState State { get; internal set; }
        public CutData BestCuttingData { get; private set; }

        public Pill Pill { get; private set; }

        public Blister Blister { get => Pill.blister; }

        public CutValidator Validator { get; set; }

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

        public CutData NextCutData()
        {
            //Blister.Pills
            List<CutData> proceedCUtData = CuttingData.Where(x => AlreadyCutData.All(y => y.UUID != x.UUID)).ToList();
            foreach (CutData cData in proceedCUtData)
            {
                AlreadyCutData.Add(cData);
                if (!cData.GenerateBladeFootPrint()) continue;
                return cData;
            }
            return null;
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
        /// Get Chunk and remove Pill and any connection data for that pill from current blister. 
        /// </summary>
        /// <returns></returns>
        public CutBlister GetCutChunkAndRemoveItFomBlister()
        {
            switch (State)
            {
                case CutState.Rejected:
                case CutState.Failed:
                    throw new Exception("Cannot apply cutting on failed CutStates proposal. Big mistake!!!!");
                case CutState.Last:
                    CutBlister cBlister = new CutBlister(Blister);
                    cBlister.IsLast = true;
                    return cBlister;
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
        /// <returns>Tuple with Current blister and list of leftover blister</returns>
        public (Blister CurrentBluster, List<Blister> Leftovers) GetLeftoversAndUpdateCurrentBlister()
        {
            switch (State)
            {
                case CutState.Failed:
                    throw new Exception("Cannot apply cutting on failed CutStates proposal. Big mistake!!!!");
                case CutState.Last:
                    // return   new List<Blister>();
                    return (CurrentBluster: null, Leftovers: new List<Blister>());
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

                    //leftovers.Add(Blister);

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
                    return (CurrentBluster: Blister, Leftovers: leftovers);
                default:
                    throw new NotImplementedException($"This state {State} is not implemented!");
            }
        }

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
