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
        public Blister Blister { get => Pill.ParentBlister; }
        public CutState State { get; internal set; }
        #endregion

        public bool Equals(CutProposal other)
        {
            if (other == null) return false;
            return (this.UUID.Equals(other.UUID));
        }

        //Metoda do oceny ile miejsca zostaje po cięciu. Przeniesć do validatora
        //Dodać do alidatora metodę do oceny ceica w celu sortowania
        //Validator powinien miec wszystko co potrznbne do validoacji i oceny ciecia. 
        /// <summary>
        /// Evaluate how Cut influance JawLocationPossible.
        /// </summary>
        /// <returns> 0.0 means Cut remove whole JawLocationPossible, 1.0 means no impact on JawsLocation</returns>
        public double EvaluateFutureJawPosibleIntervalsRange()
        {
            if (!Validator.HasCutAnyImpactOnJaws) return 1.0;
            List<Interval> futureJawPosibleIntervals = Grasper.ApplyCutOnGrasperLocation(Validator.CurrentJawPosibleIntervals, Validator.BlisterImpactInterval);
            double rangeLength =  Grasper.IntervalsInterval(futureJawPosibleIntervals).Length;
            double realLength = futureJawPosibleIntervals.Select(inter => inter.Length).Sum();
            return rangeLength / realLength;
            //return futureJawPosibleIntervals.Select(inter => inter.Length).Sum();
        }

        public double EvaluateCutQuality()
        {
            return Data.EstimatedCuttingCount * Data.Polygon.GetBoundingBox(false).Area * Data.BlisterLeftovers.Select(y => y.PointCount).Sum();
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
                    //Pill.State = PillState.Cut;
                    //Pill.RemoveConnectionData();
                    //Update Current
                    Blister.Pills.Remove(Pill);
                    // New Pill based on existing does not have anny connection relations.
                    Pill cutPill = new Pill(Pill) { State = PillState.Cut };

                    return new CutBlister(cutPill, Data);
                default:
                    throw new NotImplementedException($"This state {State} is not implemented!");
            }
        }


        /// <summary>
        /// !!!! This method has to be used after GetCutoutAndRemoveFomBlister!!!!
        /// Apply all leftovers on current blister.
        /// </summary>
        /// <returns>Tuple with Current blister and list of leftover blister</returns>
        public (Blister CurrentBlister, List<Blister> Leftovers) GetLeftoversAndUpdateCurrentBlister()
        {
            switch (State)
            {
                case CutState.Failed:
                    throw new Exception("Cannot apply cutting on failed CutStates proposal. Big mistake!!!!");
                case CutState.Last:
                    return (CurrentBlister: null, Leftovers: new List<Blister>());
                case CutState.Succeed:
                    log.Debug("Updating current Blister Outline and remove cut Pill from blister");

                    // Remove parentBLitser form all pills
                    Blister.Pills.ForEach(pill => pill.ParentBlister = null);
                    List<int> pillCountPerLeftover = new List<int>(Blister.Pills.Count);
                    List<Blister> newBlisters = new List<Blister>(Data.BlisterLeftovers.Count);
                    foreach (PolylineCurve leftover in Data.BlisterLeftovers)
                    {
                        Blister newBli = new Blister(Blister.Pills, leftover);
                        newBlisters.Add(newBli);
                    }
                    if (!newBlisters.Zip(pillCountPerLeftover, (blister, count) => blister.Pills.Count == count).All(pred=> pred == true))
                    {
                        throw new Exception($"Probabely Leftovers are overlapping. Same pill belongs to more then one leftover!");
                    }



                    /*
                    Blister.Outline = Data.BlisterLeftovers[0];
                    // Case if Blister is split because of this cut.
                    log.Debug("Remove all cells which are not belong to this Blister anymore.");
                    List<Pill> removerdPills = new List<Pill>(Blister.Pills.Count);
                    List<Pill> validPills = new List<Pill>(Blister.Pills.Count);
                    for (int i = 0; i < Blister.Pills.Count; i++)
                    {
                        // If cell is no more inside this Blister, remove it.
                        if (!Geometry.InclusionTest(Blister.Pills[i], Blister))
                        {
                            // check if cell is aimed to cut. For 100% all cells in Blister should be Queue.
                            if (Blister.Pills[i].State != PillState.Queue)
                            {
                                throw new Exception($"Found Pill with state {Blister.Pills[i].State} in queued blister. All Pills should have status QUEUED!. Unknown error.");
                            }
                            //Remove pill reference to current blister
                            Blister.Pills[i].blister = null;
                            removerdPills.Add(Blister.Pills[i]);
                            Blister.Pills.RemoveAt(i);
                            i--;
                        }
                        else
                        {
                            validPills.Add(Blister.Pills[i]);
                        }
                    }
                    Blister newCurrentBli = new Blister(validPills, Data.BlisterLeftovers[0]);

                    List<Blister> leftovers = new List<Blister>(Data.BlisterLeftovers.Count);

                    //leftovers.Add(Blister);

                    for (int j = 1; j < Data.BlisterLeftovers.Count; j++)
                    {
                        PolylineCurve blisterLeftover = Data.BlisterLeftovers[j];
                        Blister newBli = new Blister(removerdPills, blisterLeftover);
                        leftovers.Add(newBli);
                    }
                    */
                   // List<Pill> abandonePills = removerdPills.Where(pill => pill.blister == null).ToList();
                   // if (abandonePills.Count > 0)
                   // {
                   //     throw new Exception($"Abandon Pills after applying cutting data: {abandonePills.Count}");
                   // }

                    Blister currentBlister = newBlisters[0];
                    newBlisters.RemoveAt(0);

                    return (CurrentBlister: currentBlister, Leftovers: newBlisters);
                default:
                    throw new NotImplementedException($"This state {State} is not implemented!");
            }
        }

        #endregion
    }

}
