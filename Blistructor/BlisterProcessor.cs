using System;
using System.Collections.Generic;
using System.Linq;
using log4net;
#if PIXEL
using Pixel.Rhino.Geometry;
using Pixel.Rhino.Geometry.Intersect;
#else
using Rhino.Geometry;
using Rhino.Geometry.Intersect;
#endif
using Newtonsoft.Json.Linq;

namespace Blistructor
{
    class BlisterProcessor
    {
        private static readonly ILog log = LogManager.GetLogger("Cutter.BlisterCutter2");
        public List<Blister> Queue = new List<Blister>();
        public List<CuttedBlister> Cutted = new List<CuttedBlister>();
        public Anchor anchor;
        private int loopTolerance = 5;

        public BlisterProcessor(Blister blisterToCut)
        {
            Queue.Add(blisterToCut);
        }
        #region PROPERTIES   
        /*
        public List<List<LineCurve>> GetCuttingLines
        {
            get
            {
                List<List<LineCurve>> cuttingLines = new List<List<LineCurve>>(Cutted.Count);
                foreach (Blister subBlister in Cutted)
                {
                    cuttingLines.Add(subBlister.GetCuttingLines());
                }
                return cuttingLines;
            }
        }
        */
        public List<PolylineCurve> GetCuttedPolygons
        {
            get
            {
                List<PolylineCurve> polygons = new List<PolylineCurve>(Cutted.Count);
                foreach (Blister subBlister in Cutted)
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
            // Check if blister is correctly allign
            if (!anchor.IsBlisterStraight(Setups.MaxBlisterPossitionDeviation)) return CuttingState.CTR_WRONG_BLISTER_POSSITION;
            log.Info(String.Format("=== Start Cutting ==="));
            int initialPillCount = Queue[0].Pills.Count;
            if (Queue[0].ToTight) return CuttingState.CTR_TO_TIGHT;
            if (Queue[0].LeftPillsCount == 1) return CuttingState.CTR_ONE_PILL;
            if (!anchor.ApplyAnchorOnBlister()) return CuttingState.CTR_ANCHOR_LOCATION_ERR;

            int n = 0; // control
                       // Main Loop
            while (Queue.Count > 0)
            {
                //if (n > mainLimit && mainLimit != -1) break;
                // Extra control to not loop forever...
                if (n > initialPillCount + loopTolerance) break;
                log.Info(String.Format(String.Format("<<<<<<Blisters Count: Queue: {0}, Cutted {1}>>>>>>>>>>>>>>>>>>>>>>>>", Queue.Count, Cutted.Count)));
                // InnerLoop - Queue Blisters

                for (int i = 0; i < Queue.Count; i++)
                {
                    Blister subBlister = Queue[i];
                    log.Info(String.Format("{0} pills left to cut on on Blister:{1}", subBlister.Pills.Count, i));
                    if (subBlister.IsDone)
                    {
                        log.Info("Blister is already cutted or is to tight for cutting.");
                        continue;
                    }
                    // In tuple I have | CutOut Blister | Current Updated Blister | Extra Blisters to Cut (recived by spliting currentBlister) 
                    Cutter cutter = new Cutter(subBlister);
                    CutProposal cutProposal;
                    try
                    {
                        cutProposal = cutter.CutNext(onlyAnchor: false);
                    }
                    catch (Exception)
                    {
                        log.Error("!!!Cannot cut blister Anymore!!!");
                        return CuttingState.CTR_FAILED;
                    }

                    if (cutProposal.HasGrasperCollisions())
                    {
                        subBlister.RemoveCollision(CutProposals);
                        cutProposal = cutter.CutNext(onlyAnchor: true);
                    }
    
                    CuttedBlister cutOut = cutProposal.GetCutoutAndRemoveFomBlister();

                    // If anything was cutted, add to list
                    if (cutOut != null)
                    {
                        Pill cuttedPill = cutOut.Pills[0];
                        if (!cuttedPill.IsAnchored)
                        {
                            log.Debug("Anchor - Update Pred Line");

                            anchor.Update(cutOut);
                        }
                        if (cuttedPill.IsAnchored && cuttedPill.State != PillState.Alone && CuttablePillsLeft == 2) anchor.FindNewAnchorAndApplyOnBlister(cutOut);
                        log.Debug("Adding new CutOut subBlister to Cutted list");
                        Cutted.Add(cutOut);
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
                        Point3d lastKnifePossition = cutOut.Pill.Center;
                        if (lastKnifePossition.X != double.NaN) subBlister.SortPillsByPointDirection(lastKnifePossition, false);
                    }
                    if (Queue.Count() >= 2) break;
                }
                n++;
            }

            if (initialPillCount == Cutted.Count) return CuttingState.CTR_SUCCESS;
            else return CuttingState.CTR_FAILED;
        }
    }
}
