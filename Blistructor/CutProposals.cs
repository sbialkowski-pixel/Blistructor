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
    public class CutProposal : IEquatable<CutProposal>
    {
        private static readonly ILog log = LogManager.GetLogger("Cutter.CutProposal");

        public CutData Data { get; private set; }
        private CutValidator Validator { get; set; }

        internal Guid UUID { private set; get; }

        #region CONTRUCTORS
        public CutProposal(Pill proposedPillToCut, CutData cutData, Grasper grasper, CutState state)
        {
            UUID = Guid.NewGuid();
            Pill = proposedPillToCut;
            State = state;
            Data = cutData;
            if (cutData != null)
                Validator = new CutValidator(Data, grasper);
        }
        #endregion

        #region PROPERTIES
        public Pill Pill { get; private set; }
        public Blister Blister { get => Pill.blister; }
        public CutState State { get; internal set; }
        #endregion

        public bool Equals(CutProposal other)
        {
            if (other == null) return false;
            return (this.UUID.Equals(other.UUID));
        }

        #region VALIDATOR
        /// <summary>
        /// Check if Cut has any impact on Grasper.
        /// </summary>
        /// <returns></returns>
        public bool HasCutAnyImpactOnJaws
        {
            get { return Validator.HasCutAnyImpactOnJaws; }
        }

        /// <summary>
        /// Check Connectivity (AdjacingPills) against pill planned to be cut.
        /// </summary>
        /// <param name="state">Proposal state, if validation fails</param>
        /// <returns>True if all ok, false if there is inconsistency.</returns>
        public bool CheckConnectivityIntegrityInLeftovers(CutState state = CutState.None)
        {
            if (State == CutState.Last) return true;
            bool result = Validator.CheckConnectivityIntegrityInLeftovers(Pill, Blister);
            if (!result && state != CutState.None) State = state;
            return result;
        }

        /// <summary>
        /// Check for collision between current Jaws and cutImpactIntervals.
        /// </summary>
        /// <param name="state">Proposal state, if validation fails</param>        
        /// <returns></returns>
        public bool CheckJawsCollision(CutState state = CutState.None)
        {
            bool result = Validator.CheckJawsCollision();
            if (!result && state != CutState.None) State = state;
            return result;
        }

        /// <summary>
        /// If this cut will remove whole jawPossibleLocation line, its is not good, at least if it is not last blister.
        /// </summary>
        /// <param name="state">Proposal state, if validation fails</param>        
        /// <returns>True if any Jaw can occure. False if whole jawPossibleLocation line is removed.</returns>
        public bool CheckJawsExistance(CutState state = CutState.None)
        {
            if (State == CutState.Last) return true;
            bool result = Validator.CheckJawsExistance();
            if (!result && state != CutState.None) State = state;
            return result;
        }

        /// <summary>
        /// Create futureJawPosibleIntervals based on cutData and check if leftoves has place for Jaws.
        /// </summary>
        /// <param name="state">Proposal state, if validation fails</param>        
        /// <returns></returns>
        public bool CheckJawExistanceInLeftovers(CutState state = CutState.None)
        {
            if (State == CutState.Last) return true;
            bool result = Validator.CheckJawsExistance();
            if (!result && state != CutState.None) State = state;
            return result;
        }

        /// <summary>
        /// Check if this cut proposition vlilates distance limit  between Jaws.
        /// </summary>
        /// <param name="state">Proposal state, if validation fails</param>        
        /// <returns></returns>
        public bool CheckJawLimitVoilations(CutState state = CutState.None)
        {
            bool result = Validator.CheckJawLimitVoilations();
            if (!result && state != CutState.None) State = state;
            return result;
        }

        /// <summary>
        /// Check if after applying cut, CutBLister can handle any Jaw.
        /// This is to check if second last piece can be hold by any Jaw.
        /// </summary>
        /// <param name="state">Proposal state, if validation fails</param>        
        /// <returns>True if Jaw can exist on this chuck after applying cut</returns>
        public bool CheckJawExistanceInCut(CutState state = CutState.None)
        {
            bool result = Validator.CheckJawExistanceInCut();
            if (!result && state != CutState.None) State = state;
            return result;
        }
        #endregion

        #region APPROVAL
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

                    return new CutBlister(Pill, Data);
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
                    Blister.Outline = Data.BlisterLeftovers[0];
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
                    List<Blister> leftovers = new List<Blister>(Data.BlisterLeftovers.Count);

                    //leftovers.Add(Blister);

                    for (int j = 1; j < Data.BlisterLeftovers.Count; j++)
                    {
                        PolylineCurve blisterLeftover = Data.BlisterLeftovers[j];
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

        #endregion
    }

}
