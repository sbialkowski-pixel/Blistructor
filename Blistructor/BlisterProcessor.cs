using System;
using System.IO;

using System.Collections.Generic;
using System.Linq;
using log4net;
#if PIXEL
using Pixel.Rhino.FileIO;
using Pixel.Rhino.Geometry;
using Pixel.Rhino.DocObjects;
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

        public BlisterProcessor(List<PolylineCurve> pillsOutlines, PolylineCurve blisterOutlines) 
        {
            Blister initialBlister = new Blister(pillsOutlines, blisterOutlines);
            initialBlister.SortPillsByCoordinates(true);
            log.Info(String.Format("New blister with {0} Pills", pillsOutlines.Count));
            Queue.Add(initialBlister);
            Grasper = new Grasper(Queue);
            initialBlister.SortPillsByComplex(Grasper);
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
            //DebugShit
            Random rnd = new Random();
            int runId = rnd.Next(0, 1000);

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
                if (Setups.CreatePillsDebugFiles) Queue[0].Pills.ForEach(pill => pill.GenerateDebugGeometryFile(runId, Chunks.Count));
                if (Setups.CreateBlisterDebugFile) Queue[0].GenerateDebugGeometryFile(runId);

                Queue = Queue.OrderBy(blister => blister.LeftPillsCount).ToList();

                //if (n > mainLimit && mainLimit != -1) break;
                // Extra control to not loop forever...
                if (n > 2 * initialPillCount) break;
                log.Info(String.Format(String.Format("<<<<<<Blisters Count: Queue: {0}, Cutted {1}>>>>>>>>>>>>>>>>>>>>>>>>", Queue.Count, Chunks.Count)));
                // InnerLoop - Queue Blisters

                for (int i = 0; i < Queue.Count; i++)
                {
                    Blister blisterToCut = Queue[i];
                    blisterToCut.SortPillsByComplex(Grasper);
                    log.Info(String.Format("{0} Pills left to cut on Blister:{1}", blisterToCut.Pills.Count, i));
                    if (blisterToCut.IsDone)
                    {
                        log.Info("Blister is already cut or is to tight for cutting.");
                        Queue.RemoveAt(i);
                        continue;
                    }

                    Cutter cutter = new Cutter(blisterToCut, Grasper, runId, Chunks.Count);
                    CutProposal cutProposal;

                    // Eech cut, update Jaws.
                    Grasper.UpdateJawsPoints();
                    // Loop on Pills
                    while (true)
                    {
                        cutProposal = cutter.CutNextPill();
                        //GET PROPOSAL
                        if (cutProposal == null)
                        {
                            //All Pills are cut, lower requerments (try cut fixed and rejected Pills where jaws can occure)
                            cutProposal = cutter.GetNextPillCut(CutState.Rejected);
                            if (cutProposal == null) cutProposal = cutter.GetNextPillCut(CutState.Fixed);
                            // If there is no successful cut proposal (all CutStates == FAIL), just end.
                            if (cutProposal == null)
                            {
                                log.Error("!!!Cannot cut blister anymore!!!");
                                return CuttingState.CTR_FAILED;
                            }
                        }
                        else if (cutProposal.State != CutState.Last)
                        {
                            // Checking only Pill which are not fixed by Jaw and cut data allows to grab it no collisions
                            if (Grasper.ContainsJaw(cutProposal.Data) && Grasper.HasPlaceForJawInCutContext(cutProposal.Data))
                            {
                                cutProposal.State = CutState.Fixed;
                                continue;
                            }
                        }

                        if (cutProposal.State != CutState.Last)
                        {
                            if ((Queue.Count - 1 + cutProposal.Data.BlisterLeftovers.Count) > 2) continue;
                            //if (!cutProposal.CheckConnectivityIntegrityInLeftovers(state: CutState.Failed)) continue;
                            if (cutProposal.HasCutAnyImpactOnJaws)
                            {
                                if (!cutProposal.CheckJawsExistance(state: CutState.Failed)) continue;
                                if (!cutProposal.CheckJawExistanceInLeftovers(state: CutState.Failed)) continue;
                                if (!cutProposal.CheckJawLimitVoilations(state: CutState.Failed)) continue;
                                if (cutProposal.State == CutState.Proposed)
                                {
                                    if (!cutProposal.CheckJawsCollision(state: CutState.Rejected)) continue;
                                }
                                else if (cutProposal.State == CutState.Rejected)
                                {
                                    cutProposal.State = CutState.Succeed;
                                    break;
                                }
                                else { continue; }
                            }
                            cutProposal.State = CutState.Succeed;
                        }
                        else
                        {
                            if (!Grasper.HasBlisterPlaceForJaw(cutProposal.Blister))
                            {
                                log.Error("!!!Cannot hold Last pill on blister!!!");
                                return CuttingState.CTR_FAILED;
                            }
                        }
                        if (cutProposal.State == CutState.Failed) continue;
                        break;
                    }

                    CutBlister chunk = cutProposal.GetCutChunkAndRemoveItFomBlister();

                    if (Setups.CreateChunkDebugFile) GenerateDebugGeometryFile(chunk, runId, Chunks.Count);

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
                    if (chunk.IsLast)
                    {
                        Queue.RemoveAt(i);
                    }
                    else
                    {
                        (Blister cBlister, List<Blister> Leftovers) = cutProposal.GetLeftoversAndUpdateCurrentBlister();
                        Queue[i] = cBlister;
                        Queue.AddRange(Leftovers);
                    }

                    // If this is last chunk in this blister, just leave this for loop, no Grapser update or Knife possition update is needed.
                    // Also if queue is empty, leave
                    if (chunk.IsLast || Queue.Count == 0) continue;

                    // If current blister has more then one pill, just apply cut on Grasper
                    // This mean: blister must have at least 2 Pills. So whatever is any other chunk have Jaw (IsLast), just apply cut on Grasper.
                    if (blisterToCut.LeftPillsCount > 1)
                    {
                        Grasper.ApplyCut(chunk);
                    }
                    else
                    {
                        // If only one pill left, this cut is second last. So pottentialy can be hold by JAW. Chceck if this cut can be hold by any Jaw OR in past cuts one Jaws has been occupied, so there is no chance that THIS cut will be hold by JAW. If no chanse, apply cut on Grasper.
                        // Additionaly if Queue ==2, the other blister must be hold so, tuch chunk mus reduce grapsers.
                        int pastLast = Chunks.Where(c_chunk => c_chunk.IsLast == true).Count();
                        if (!cutProposal.CheckJawExistanceInCut(state: CutState.None) || pastLast > 0 || Queue.Count == 2) Grasper.ApplyCut(chunk);
                    }

                    //If there is more the 2 piecies of blister, this is critical error. 
                    if (Queue.Count > 2)
                    {
                        log.Error($"!!!{Queue.Count} pieces of blister to cut. This is imposible! Cannot cut blister Anymore!!!");
                        return CuttingState.CTR_LEFTOVERS_FAILURE;
                    }

                    //TODO: Przy sortowaniu, na poczatek trzeba dawać tabletku które wykraczaja poza linie mozliwości łapek,!!!
                    // Update Knife posittion to reorder Pills for next cut.
                    //if (Queue.Count > 0)
                    //{
                    //    double sort(Pill pill)
                    //    {
                    //        double jawDistance  = pill.GetClosestDistance(Grasper.Jaws.Select(jaw => jaw.Location).ToList());
                    //        return pill.CoordinateIndicator +jawDistance;
                    //    }
                    //    //Point3d lastKnifePossition = chunk.Pill.Center;
                    //    //if (lastKnifePossition.X != double.NaN) blisterToCut.SortPillsByPointDirection(lastKnifePossition, false);
                    //    blisterToCut.Pills.OrderBy(pill => sort(pill)).ToList();
                    //}
                }
                n++;
            }

            // Assign Jaws to CutBlisters
            Grasper.UpdateJawsPoints();

            foreach (CutBlister cBLister in Chunks)
            {
                // apply Jaw on cBlister
                List<JawPoint> jaws = Grasper.ContainsJaws(cBLister);
                if (jaws.Count > 0)
                {
                    cBLister.Jaws = jaws;
                }
                // Validate Collision
                if (cBLister.CutData != null)
                {
                    if (Grasper.IsColliding(cBLister.CutData, updateJaws: false))
                    {
                        log.Error("Found collision with grasper for cut blister! Aborting!");
                        // return CuttingState.CTR_FAILED;
                    }
                }
            }
            // TODO: Validate if more then one chunk hase specific Jaw.
            if (initialPillCount == Chunks.Count) return CuttingState.CTR_SUCCESS;
            else return CuttingState.CTR_FAILED;
        }
        public void GenerateDebugGeometryFile(CutBlister chunk, int runId, int chunkId)
        {
            string runIdString = $"{runId:00}";
            string chunkIdString = $"{chunkId:00}";
            Directory.CreateDirectory(Path.Combine(Setups.DebugDir, runIdString));
            string fileName = $"{chunkId:00}_chunk.3dm";
            string filePath = Path.Combine(Setups.DebugDir, runIdString, fileName);
            File3dm file = new File3dm();
            
            Layer l_chunk = new Layer();
            l_chunk.Name = "chunk";
            l_chunk.Index = 0;
            file.AllLayers.Add(l_chunk);
            ObjectAttributes a_chunk = new ObjectAttributes();
            a_chunk.LayerIndex = l_chunk.Index;


            file.Objects.AddCurve(chunk.Outline, a_chunk);
            file.Objects.AddCurve(chunk.Pill.Outline, a_chunk);
            Grasper.JawsPossibleLocation.ForEach(crv => file.Objects.AddCurve(crv, a_chunk));
            Queue.ForEach(b => b.Pills.ForEach(p => file.Objects.AddCurve(p.Outline, a_chunk)));
            if (chunk.CutData != null)
            {
                chunk.CutData.BladeFootPrint.ForEach(crv => file.Objects.AddCurve(crv, a_chunk));
                chunk.CutData.BlisterLeftovers.ForEach(crv => file.Objects.AddCurve(crv, a_chunk));
                chunk.CutData.Path.ForEach(crv => file.Objects.AddCurve(crv, a_chunk));

                if (true)
                {
                    //Proposal part
                    Layer l_polygon = new Layer();
                    l_polygon.Name = "polygon";
                    l_polygon.Index = 1;
                    file.AllLayers.Add(l_polygon);
                    Layer l_lines = new Layer();
                    l_lines.Name = "lines";
                    l_lines.Index = 2;
                    file.AllLayers.Add(l_lines);
                    ObjectAttributes a_polygon = new ObjectAttributes();
                    a_polygon.LayerIndex = l_polygon.Index;

                    file.Objects.AddCurve(chunk.CutData.Polygon, a_polygon);
                    ObjectAttributes a_lines = new ObjectAttributes();
                    a_lines.LayerIndex = l_lines.Index;
                    chunk.CutData.IsoSegments.ForEach(line => file.Objects.AddCurve(line, a_lines));
                }
            }
            file.Write(filePath, 6);
        }
    }
}
