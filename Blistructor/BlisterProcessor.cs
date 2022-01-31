﻿using System;
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
            if (!Grasper.ApplyAnchorOnBlister()) return CuttingState.CTR_ANCHOR_LOCATION_ERR;

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

                    /*
                    CutProposal cutProposal;
                    try
                    {
                        cutProposal = cutter.CutNext(blisterToCut, onlyAnchor: false);
                    }
                    catch (Exception)
                    {
                        log.Error("!!!Cannot cut blister anymore!!!");
                        return CuttingState.CTR_FAILED;
                    }
                    */

                    // Cut -> Validating -> Cut
                    /* CO trzeba walidowac: 
                     * - Czy wycinek "ma" łapkę
                     * - Czy pozostałosci po cięciu mają się czego trzymać (każdy musi miec po łapce). Ważne, to trzeba sprawdzić symulując apliakcję cięcia.
                     * - Czy cięcie nie powoduje kolizji z łapką w pozostałcyh kawałakach blistra
                     * - Czy pozostałe cześci blistra są integralne: kazda tableta musi mieć co najmniej 1 sąsiada...
                     * -
                     */


                    CutProposal cutProposal;
                    do
                    {
                        cutProposal = cutter.CutNextt(blisterToCut);
                        if (cutProposal == null)
                        {
                            //All pills are cut, lower requerments (try cut fixed pils - with jaws) 
                            cutProposal = cutter.GetNextSuccessfulCut;  
                        }
                        else
                        {
                            // Checking only Pill which are not fixed by Jaw
                            if (!Grasper.HasPossibleJaw(cutProposal.BestCuttingData)) continue;
                        }
                        if (!cutProposal.ValidateConnectivityIntegrityInLeftovers()) continue;
                        if (!cutProposal.ValidateJawExistanceInLeftovers()) continue;

                    } while (cutProposal != null);


                    /*TODO: Sprawdzenie kolizji łapek z juz zaproponowanym cięciem.
                    if (cutProposal.HasGrasperCollisions())
                    {
                        blisterToCut.RemoveCollision(CutProposals);
                        cutProposal = cutter.CutNext(blisterToCut, onlyAnchor: true);
                    }
                    */

                    CutBlister chunk = cutProposal.GetCutChunkAndRemoveItFomBlister();

                    // If anything was cut, add to list
                    if (chunk != null)
                    {
                        Pill cuttedPill = chunk.Pills[0];
                        if (!cuttedPill.IsAnchored)
                        {
                            log.Debug("Anchor - Update grasper prediction Line");

                            Grasper.Update(chunk);
                        }
                        if (cuttedPill.IsAnchored && cuttedPill.State != PillState.Alone && CuttablePillsLeft == 2) Grasper.FindNewAnchorAndApplyOnBlister(chunk);
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
                    if (Queue.Count() == 0) break;
                    if (Queue.Count() > 0)
                    {
                        Point3d lastKnifePossition = chunk.Pill.Center;
                        if (lastKnifePossition.X != double.NaN) blisterToCut.SortPillsByPointDirection(lastKnifePossition, false);
                    }
                    if (Queue.Count() >= 2) break;
                }
                n++;
            }

            if (initialPillCount == Chunks.Count) return CuttingState.CTR_SUCCESS;
            else return CuttingState.CTR_FAILED;
        }
    }
}
