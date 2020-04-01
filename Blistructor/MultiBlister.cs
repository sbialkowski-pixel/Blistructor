using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

using Rhino.Geometry;
using log4net;

namespace Blistructor
{ 
    public class MultiBlister
    {
        private static readonly ILog log = LogManager.GetLogger("Main.Blistructor");
        public PolylineCurve mainOutline;
        public List<PolylineCurve> pillsss;
        /*
      private PolylineCurve mainOutline;
      private PolylineCurve aaBBox;
      private PolylineCurve maBBox;
      private LineCurve aaGuideLine;
      private LineCurve maGuideLine;
            */
        private int loopTolerance = 5;
        public List<Blister> Queue;
        public List<Blister> Cutted;
        public Point3d knifeLastPoint = new Point3d();
        public Anchor anchor;
        public List<Curve> worldObstacles;
        int cellLimit;
        int mainLimit;

        public MultiBlister(int Limit1, int Limit2)
        {
            cellLimit = Limit2;
            mainLimit = Limit1;
            Queue = new List<Blister>();
            Cutted = new List<Blister>();
        }

        public int CuttableCellsLeft
        {
            get
            {
                int counter = 0;
                foreach (Blister blister in Queue)
                {
                    counter += blister.Cells.Select(cell => cell.State).Where(state => state == CellState.Queue || state == CellState.Alone).ToList().Count;
                }
                return counter;
            }
        }

        #region PROPERTIES   

        public List<PolylineCurve> GetCuttedPolygons
        {
            get
            {
                List<PolylineCurve> polygons = new List<PolylineCurve>(Cutted.Count);
                foreach (Blister blister in Cutted)
                {
                    polygons.Add(blister.Outline);
                }
                return polygons;
            }
        }


        public List<List<LineCurve>> GetCuttingLines
        {
            get
            {
                List<List<LineCurve>> cuttingLines = new List<List<LineCurve>>(Cutted.Count);
                foreach (Blister blister in Cutted)
                {
                    cuttingLines.Add(blister.GetCuttingLines());
                }
                return cuttingLines;
            }
        }

        #endregion

        private void Initialise(List<PolylineCurve> Pills, PolylineCurve Blister)
        {
            // Initialize Lists
            Queue = new List<Blister>();
            Cutted = new List<Blister>();
            // 

            // Build initial blister
            Blister initialBlister = new Blister(Pills, Blister);
            initialBlister.SortCellsByCoordinates(true);
            Queue.Add(initialBlister);
            anchor = new Anchor(this);
            log.Debug(String.Format("New blistructor with {0} Queue blisters", Queue.Count));
            // World Obstacles
           // Line cartesianLimitLine = new Line(new Point3d(0, -Setups.BlisterCartesianDistance, 0), Vector3d.XAxis, 1.0);
            //cartesianLimitLine.Extend(Setups.IsoRadius, Setups.IsoRadius);
            
           worldObstacles = new List<Curve>() { anchor.cartesianLimitLine };

        }

        public JObject CutBlister(string pillsMask, string blisterMask)
        {
            // Do Contuter stuff here for pills and blister then    CutBlister(pills, blister)
            List<Curve> pills = GetContursBasedOnBinaryImage(pillsMask, 0.0);
            List<Curve> blisters = GetContursBasedOnBinaryImage(blisterMask, 0.0); // This should be 1 element list....
            if (blisters.Count != 1) return null;
            ApplyCalibrationData(pills);
            ApplyCalibrationData(blisters);

            // process pills
            List<PolylineCurve> outPills = new List<PolylineCurve>();
            foreach (Curve crv in pills)
            {
                NurbsCurve nCrv = (NurbsCurve)crv;
                Curve fitCurve = nCrv.Fit(3, Setups.CurveFitTolerance, 0.0);
                outPills.Add(fitCurve.ToPolyline(Setups.CurveDistanceTolerance, 0.0, 0.05, 1000.0));
            }

            NurbsCurve bliNCrv = (NurbsCurve)blisters[0];
            Curve fitBliCurve = bliNCrv.Fit(3, Setups.CurveFitTolerance, 0.0);
            PolylineCurve blister = fitBliCurve.ToPolyline(Setups.CurveDistanceTolerance, 0.0, 0.05, 1000.0);

            // TO remove
            mainOutline = blister;
            pillsss = outPills;

            JObject cuttingResult = CutBlister(outPills, blister);
            return cuttingResult;
            // return null;
        }
        public JObject CutBlister(List<Polyline> pills, Polyline blister)
        {
            List<PolylineCurve> convPills = new List<PolylineCurve>();
            foreach (Polyline pline in pills)
            {
                convPills.Add(pline.ToPolylineCurve());
            }
            return CutBlister(convPills, blister.ToPolylineCurve());
        }

        public JObject CutBlister(List<PolylineCurve> pills, PolylineCurve blister)
        {
            // Prepare basic stuff
            JObject cuttingResult = PrepareEmptyJSON();
            CuttingState status = CuttingState.CTR_UNSET;

            // Pills and blister are already curve objects
            Initialise(pills, blister);
            cuttingResult["PillsDetected"] = pills.Count;


            try
            {
                status = PerformCut(mainLimit, cellLimit);

                // status = CuttingState.CTR_UNSET;
            }
            catch (Exception ex)
            {
                status = CuttingState.CTR_OTHER_ERR;
                log.Error("PerformCut Error catcher", ex);
            }

            cuttingResult["Status"] = status.ToString();
            cuttingResult["PillsCutted"] = Cutted.Count;
            // If all alright, populate by cutting data
            if (status == CuttingState.CTR_SUCCESS)
            {

                JArray allCuttingInstruction = new JArray();
                foreach (Blister bli in Cutted)
                {
                    allCuttingInstruction.Add(bli.Cells[0].GetJSON());
                }
                cuttingResult["CuttingData"] = allCuttingInstruction;
            }
            cuttingResult["AnchorLocation"] = anchor.GetJSON();


            return cuttingResult;
        }

        private CuttingState PerformCut(int mainLimit, int cellLimit)
        {
            log.Info(String.Format("=== Start Cutting ==="));
            int initialPillCount = Queue[0].Cells.Count;
            if (Queue[0].ToTight) return CuttingState.CTR_TO_TIGHT;
            if (Queue[0].LeftCellsCount == 1) return CuttingState.CTR_ONE_PILL;
            if (!anchor.ApplyAnchorOnBlister()) return CuttingState.CTR_ANCHOR_LOCATION_ERR;
            //return CuttingState.CTR_ANCHOR_LOCATION_ERR;

            int n = 0; // control
                       // Main Loop
            while (Queue.Count > 0)
            {
                if (n > mainLimit && mainLimit != -1) break;
                // Extra control to not loop forever...
                if (n > initialPillCount + loopTolerance) break;
                log.Info(String.Format(String.Format("<<<<<<<<<<<<<<<Blisters Count: Queue: {0}, Cutted {1}>>>>>>>>>>>>>>>>>>>>>>>>", Queue.Count, Cutted.Count)));
                // InnerLoop - Queue Blisters

                for (int i = 0; i < Queue.Count; i++)
                {
                    if (Queue == null) continue;
                    Blister blister = Queue[i];
                    log.Info(String.Format("{0} cells left to cut on on Blister:{1}", blister.Cells.Count, i));
                    if (blister.IsDone)
                    {
                        log.Info("Blister is already cutted or is to tight for cutting.");
                        continue;
                    }
                    // In tuple I have | CutOut Blister | Current Updated Blister | Extra Blisters to Cut (recived by spliting currentBlister) 
                    Tuple<Blister, Blister, List<Blister>> result = blister.CutNext(worldObstacles);
                    log.Debug(String.Format("Cutting Result: Cutout: {0} - Current Blister {1} - New Blisters {2}.", result.Item1, result.Item2, result.Item3.Count));
                    // If anything was cutted, add to list
                    if (result.Item1 != null)
                    {
                        Cell cuttedCell = result.Item1.Cells[0];
                        if (cuttedCell.Anchor.state == AnchorState.Inactive)
                        {
                            log.Info("Anchor - Update Pred Line");

                            anchor.Update(result.Item1);
                        }
                        if (cuttedCell.Anchor.state == AnchorState.Active && cuttedCell.State != CellState.Alone && CuttableCellsLeft == 2) anchor.FindNewAnchorAndApplyOnBlister(result.Item1);
                        log.Info("Adding new CutOut blister to Cutted list");
                        Cutted.Add(result.Item1);
                    }
                    else
                    {
                        log.Error("!!!Cannot cut blister Anymore!!!");
                        return CuttingState.CTR_FAILED;
                    }
                    // override current bluster, if null , remove it from Queue list
                    if (result.Item2 == null)
                    {
                        log.Info("Current blister is empty. Removing from Queue");
                        Queue.RemoveAt(i);
                        i--;
                        break;
                    }
                    else
                    {
                        log.Info("Updating blister");
                        blister = result.Item2;
                        // Sort Pills by last Knife Possition -> Last Pill Centre
                        // Point3d lastKnifePossition = Cutted.Last().Cells[0].bestCuttingData.GetLastKnifePossition();
                        Point3d lastKnifePossition = Cutted.Last().Cells[0].PillCenter;
                        if (lastKnifePossition.X != double.NaN) blister.SortCellsByPointDirection(lastKnifePossition, false);
                        //if (lastKnifePossition.X != double.NaN) blister.SortCellsByCoordinates(true);

                    }
                    // Add extra blsters if any was created
                    if (result.Item3.Count != 0)
                    {
                        log.Info("Adding new blister(s) to Queue");
                        Queue.AddRange(result.Item3);
                        break;
                    }

                }
                n++;
            }

            if (initialPillCount == Cutted.Count) return CuttingState.CTR_SUCCESS;
            else return CuttingState.CTR_FAILED;
        }


        private JObject PrepareEmptyJSON()
        {
            JObject data = new JObject();
            data.Add("Status", null);
            data.Add("PillsDetected", null);
            data.Add("PillsCutted", null);
            data.Add("AnchorLocation", null);
            data.Add("CuttingData", new JArray());
            return data;
        }

        private List<Curve> GetContursBasedOnBinaryImage(string imagePath, double tol)
        {
            List<List<int[]>> allPoints = Conturer.getContours(imagePath, tol);
            List<Curve> finalContours = new List<Curve>();
            foreach (List<int[]> conturPoints in allPoints)
            {
                Polyline pLine = new Polyline(conturPoints.Count);
                foreach (int[] rawPoint in conturPoints)
                {
                    Point3d point = new Point3d(rawPoint[0], rawPoint[1], 0);
                    pLine.Add(point);
                }
                pLine.Add(pLine.First);
                PolylineCurve ppLine = pLine.ToPolylineCurve();

                finalContours.Add((Curve)ppLine.Rebuild(pLine.Count, 3, true));
            }
            return finalContours;
        }

        private void ApplyCalibrationData(List<Curve> curves)
        {
            // Get reveresed calibraion vector
            Vector3d vector = new Vector3d(-Setups.CalibrationVectorX, -Setups.CalibrationVectorY, 0);
            foreach (Curve crv in curves)
            {
                crv.Scale(Setups.Spacing);
                crv.Translate(vector);
                crv.Rotate(Setups.Rotate, Vector3d.ZAxis, new Point3d(0, 0, 0));
            }
        }


    }
}
