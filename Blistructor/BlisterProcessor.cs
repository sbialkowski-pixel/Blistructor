using System;
using System.Collections.Generic;
using System.Linq;
using log4net;
#if PIXEL
using Pixel.Rhino.Geometry;
#else
using Rhino.Geometry;
#endif

namespace Blistructor
{
    class BlisterProcessor
    {
        private static readonly ILog log = LogManager.GetLogger("Cutter.BlisterProcessor");
        public List<Blister> Queue = new List<Blister>();
        public List<CutBlister> Chunks = new List<CutBlister>();
        public Grasper Grasper { get; private set; }

        public BlisterProcessor()
        {

        }

        public BlisterProcessor(List<PolylineCurve> pillsOutlines, PolylineCurve blisterOutlines) : this()
        {
            Blister initialBlister = new Blister(pillsOutlines, blisterOutlines);
            initialBlister.SortPillsByCoordinates(true);
            log.Info(String.Format("New blister with {0} pills", pillsOutlines.Count));
            Queue.Add(initialBlister);
            Grasper = new Grasper(Queue);
        }

        public BlisterProcessor(Blister blisterToCut)
        {
            Queue.Add(blisterToCut);
        }
        #region PROPERTIES   
        public List<PolylineCurve> GetCuttedPolygons
        {
            get
            {
                List<PolylineCurve> polygons = new List<PolylineCurve>(Chunks.Count);
                foreach (Blister subBlister in Chunks)
                {
                    polygons.Add(subBlister.Outline);
                }
                return polygons;
            }
        }
        public int CuttablePillsLeft
        {
            get
            {
                int counter = 0;
                foreach (Blister subBlister in Queue)
                {
                    counter += subBlister.Pills.Select(pill => pill.State).Where(state => state == PillState.Queue || state == PillState.Alone).ToList().Count;
                }
                return counter;
            }
        }
        #endregion
        public CuttingState PerformCut()
        {
            // Check if blister is correctly align
            if (!Grasper.IsBlisterStraight(Setups.MaxBlisterPossitionDeviation)) return CuttingState.CTR_WRONG_BLISTER_POSSITION;
            log.Info(String.Format("=== Start Cutting ==="));
            int initialPillCount = Queue[0].Pills.Count;
            if (Queue[0].ToTight) return CuttingState.CTR_TO_TIGHT;
            if (Queue[0].LeftPillsCount == 1) return CuttingState.CTR_ONE_PILL;
            if (Grasper.Jaws == null) return CuttingState.CTR_ANCHOR_LOCATION_ERR;

            int n = 0; // control
                       // Main Loop
            while (Queue.Count > 0)
            {
                //if (n > mainLimit && mainLimit != -1) break;
                // Extra control to not loop forever...
                if (n > 2 * initialPillCount) break;
                log.Info(String.Format(String.Format("<<<<<<Blisters Count: Queue: {0}, Cutted {1}>>>>>>>>>>>>>>>>>>>>>>>>", Queue.Count, Chunks.Count)));
                // InnerLoop - Queue Blisters

                for (int i = 0; i < Queue.Count; i++)
                {
                    Blister blisterToCut = Queue[i];
                    log.Info(String.Format("{0} pills left to cut on Blister:{1}", blisterToCut.Pills.Count, i));
                    if (blisterToCut.IsDone)
                    {
                        log.Info("Blister is already cut or is to tight for cutting.");
                        continue;
                    }

                    Cutter cutter = new Cutter(Grasper.GetCartesianAsObstacle());
                    CutProposal cutProposal;
                    CutValidator validator = null;

                    // Eech cut, update Jaws.
                    Grasper.UpdateJawsPoints();

                    while (true)
                    {
                        cutProposal = cutter.CutNext(blisterToCut);
                        if (cutProposal == null)
                        {
                            //All pills are cut, lower requerments (try cut fixed pills where jaws can occure) 
                            cutProposal = cutter.GetNextSuccessfulCut;
                            // If there is no successful cut proposal (all CutStates == FAIL), just end.
                            if (cutProposal == null)
                            {
                                log.Error("!!!Cannot cut blister anymore!!!");
                                return CuttingState.CTR_FAILED;
                            }
                        }
                        else if (cutProposal.State != CutState.Last)
                        {
                            if (validator == null) validator = new CutValidator(cutProposal, Grasper);
                            // Checking only Pill which are not fixed by Jaw and cut data allows to grab it no collisions)
                            if (Grasper.ContainsJaw(cutProposal.BestCuttingData) && validator.CheckJawExistanceInCut(updateCutState: false)) continue;
                        }

                        if (cutProposal.State != CutState.Last)
                        {
                            if (validator == null) validator = new CutValidator(cutProposal, Grasper);

                            if (!validator.CheckConnectivityIntegrityInLeftovers(updateCutState: true)) continue;
                            if (validator.HasCutAnyImpactOnJaws)
                            {
                                if (!validator.CheckJawsExistance(updateCutState: true)) continue;
                                if (!validator.CheckJawExistanceInLeftovers(updateCutState: true)) continue;

                                if (!validator.CheckJawsCollision(updateCutState: true))
                                {
                                    // Only CutProposal with colission left. Just use it, Grasper will be updated, and collision will be invalid. 
                                    if (cutProposal.State == CutState.Rejected)
                                    {
                                        cutProposal.State = CutState.Succeed;
                                        break;
                                    }
                                    else continue;
                                }
                            }
                        }
                        else
                        {
                            if (!Grasper.HasBlisterPlaceForJaw(cutProposal.Blister))
                            {
                                log.Error("!!!Cannot hold Last pill on blister!!!");
                                return CuttingState.CTR_FAILED;
                            }
                            //TODO: Check if last pill has JAW. Theoretically this ValidateJawExistanceInLeftovers in provious cuts should ensure this statment, buuut.
                        }

                        if (cutProposal.State == CutState.Failed) continue;
                        break;
                    }

                    CutBlister chunk = cutProposal.GetCutChunkAndRemoveItFomBlister();

                    // If anything was cut, add to list
                    if (chunk != null)
                    {
                        log.Debug("Adding new CutOut subBlister to Chunks list");
                        Chunks.Add(chunk);
                    }
                    else
                    {
                        log.Error("!!!Cannot cut blister Anymore!!!");
                        return CuttingState.CTR_FAILED;
                    }

                    List<Blister> Leftovers = cutProposal.GetLeftoversAndUpdateCurrentBlister();

                    Queue = Leftovers;

                    // If this is last chunk in this blister, just leave this for loop, no Grapser update or Knife possition update is needed.
                    // Also if queue is empty, leave
                    if (chunk.IsLast || Queue.Count == 0) continue;

                    // If current blister has more then one pill, just apply cut on Grasper
                    // This mean: blister must have at least 2 pills. So whatever is any other chunk have Jaw (IsLast), just apply cut on Grasper.
                    if (blisterToCut.LeftPillsCount > 1)
                    {
                        Grasper.ApplyCut(chunk);
                    }
                    else
                    {
                        // If only one pill left, this cut is second last. So pottentialy can be hold by JAW. Chceck if this cut can be hold by any Jaw OR in past cuts one Jaws has been occupied, so there is no chance that THIS cut will be hold by JAW. If no chanse, apply cut on Grasper.
                        // Additionaly if Queue ==2, the other blister must be hold so, tuch chunk mus reduce grapsers.
                        int pastLast = Chunks.Where(c_chunk => c_chunk.IsLast == true).Count();
                        if (!validator.CheckJawExistanceInCut(updateCutState: false) || pastLast > 0 || Queue.Count == 2) Grasper.ApplyCut(chunk);
                    }


                    // if (Queue.Count == 1)
                    // {
                    // If current blister has more then one pill, just apply cut on Grasper
                    //   if (Queue[i].LeftPillsCount > 1) 
                    //   {
                    //      Grasper.ApplyCut(chunk);
                    //  }
                    //  else
                    //   {
                    // If only one pill left, this cut is second last. So pottentialy can be hold by JAW. Chceck if this cut can be hold by any Jaw OR in past cuts one Jaws has been occupied, so there is no chance that THIS cut will be hold by JAW. If no chanse, apply cut on Grasper
                    //       if(!validator.CheckJawExistanceInCut(updateCutState: false) || pastLast != 0) //Grasper.ApplyCut(chunk);
                    //   }
                    // }
                    // else if(Queue.Count == 2)
                    // {
                    /*
                    If blister is splited on Two, there is some remarks:
                    - current blister must have at least ONE pill exept this cut already, else leftover will be O or 1 and this point will be not reached.
                    - other blister must have also at least one pill (may be last)
                    - JAW must be also on the OTHER blister
                    - 
                    */
                    //}

                    //If there is more the 2 piecies of blister, this is critical error. 
                    if (Queue.Count > 2)
                    {
                        log.Error($"!!!{Queue.Count} pieces of blister to cut. This is imposible! Cannot cut blister Anymore!!!");
                        return CuttingState.CTR_LEFTOVERS_FAILURE;
                    }

                    // Update Knife posittion to reorder pills for next cut.
                    if (Queue.Count > 0)
                    {
                        Point3d lastKnifePossition = chunk.Pill.Center;
                        if (lastKnifePossition.X != double.NaN) blisterToCut.SortPillsByPointDirection(lastKnifePossition, false);
                    }


                }
                n++;
            }

            // Assign Jaws to CutBlisters
            Grasper.UpdateJawsPoints();

            foreach (CutBlister cBLister in Chunks)
            {
                List<JawPoint> jaws = Grasper.ContainsJaws(cBLister);
                if (jaws.Count > 0)
                {
                    //if (Grasper.IsColliding(cBLister.CutData, updateJaws: false))
                    //{
                    //    log.Error("Found collision with grasper for cut blister! Aborting!");
                    //    return CuttingState.CTR_FAILED;
                    //}
                    cBLister.Jaws = jaws;
                }
            }
            // TODO: Validate if more then one chunk hase specific Jaw.

            if (initialPillCount == Chunks.Count) return CuttingState.CTR_SUCCESS;
            else return CuttingState.CTR_FAILED;
        }
    }
}
