using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
#if PIXEL
using Pixel.Rhino.Geometry;
#else
using Rhino.Geometry;
#endif
using log4net;

// TODO: -WIP-: Przejechanie wszystkich blistrów i sprawdzenie jak działa -> szukanie błedów
// TODO: -DONE?-: Adaptacyje anchory -> Aktualizacja anchorów (ich przemieszczania) wraz z procesem ciecia np. przypadek blistra 19.

// TODO: AdvancedCutting -> blister 19.

//
// TODO: Zle sie wyliczaja Anchor Pointy - trzeba dodac grasperPredLine na górze i na dole  i brac mina z obu extremalnych konców...
// TODO: Setups trzeba zmianic na JSON'a

// TODO: -DONE-: (Część bedzie w logach) Obsługa braku możliwości technicznych pociecia (Za ciasno, za skomplikowany, nie da sie wprowadzić noża, pocięty kawałek wiekszy niż 34mm..)
// TODO: -WIP-: Adaptacyjna kolejność ciecia - po każdej wycietej tabletce, nalezało by przesortowac cell tak aby wubierał najbliższe - Nadal kolejnosc ciecia jest do kitu ...
// TODO: -DONE???-: Generowanie JSONA, Obsługa wyjątków, lista errorów. struktura pliku
// TODO: "ładne" logowanie produkcyjne jak i debugowe.
// TODO: Posprzątanie w klasach.
// TODO: -DONE???-: PredAnchorLine nie updatuje sie w przypadku gdzi na końcu jest wiecej niż jeden blister...
// TODO: -DONE- W przypadku Blstra 28 -> Tab10, linia ciecia nieszczeslicwi przecina blister 4 razy... wywala sie na tym cąła logika 

/*States:
 * CTR_SUCCESS -> Cutting successful.
 * CTR_TO_TIGHT -> Pills are to tight. Cutting aborted.
 * CTR_ONE_PILL -> One pill on blister only. Nothing to do.
 * CTR_FAILED -> Cutting Failed. Cannot Found cutting paths for all pills. Blister is to complicated or it is uncuttable.
 * CTR_ANCHOR_LOCATION_ERR: Blister side to small to pick by both graspers or No place for graspers.
 */

namespace Blistructor
{
    public class MultiBlister
    {
        private static readonly ILog log = LogManager.GetLogger("Main.Blistructor");
        public PolylineCurve mainOutline;
        public List<PolylineCurve> pillsss;

        private int loopTolerance = 5;
        public List<Blister> Queue;
        public List<Blister> Cutted;

        private JObject cuttingResult;

        //  public Point3d knifeLastPoint = new Point3d();
        public Anchor anchor;
        public List<Curve> worldObstacles;
        int cellLimit;
        int mainLimit;

        public MultiBlister()
        {
            cellLimit = -1;
            mainLimit = -1;
            Queue = new List<Blister>();
            Cutted = new List<Blister>();
            cuttingResult = PrepareEmptyJSON();
        }

        public MultiBlister(int Limit1, int Limit2)
        {
            cellLimit = Limit2;
            mainLimit = Limit1;
            Queue = new List<Blister>();
            Cutted = new List<Blister>();
            cuttingResult = PrepareEmptyJSON();
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

        public JObject GetLastCutting { get{ return cuttingResult;}}


        #endregion

        private void InitialiseNewCut()
        {
            // Init Logger
            Logger.Setup();

            // Initialize Lists
            Queue = new List<Blister>();
            Cutted = new List<Blister>();

            cuttingResult = PrepareEmptyJSON();
        }

        private void InitialiseNewBlister(List<PolylineCurve> Pills, PolylineCurve Blister)
        {
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

        public JObject CutBlister(string JSON)
        {
            InitialiseNewCut();
            Dictionary<string, string> jsonCategoryMap = new Dictionary<string, string>();
            jsonCategoryMap.Add("blister", "blister");
            jsonCategoryMap.Add("pill", "tabletka");

            // Item1 -> Blister, Item2 -> Pills
            Tuple<Polyline, List<Polyline>> pLines = GetContursBasedOnJSON(JSON, jsonCategoryMap);
            return CutBlisterWorker(pLines.Item2, pLines.Item1);
        }

        public JObject CutBlister(List<Polyline> pills, Polyline blister)
        {
            InitialiseNewCut();
            return CutBlisterWorker(pills, blister);
        }
        
        public JObject CutBlister(List<PolylineCurve> pills, PolylineCurve blister)
        {
            InitialiseNewCut();
            return CutBlisterWorker(pills, blister);
        }

        private JObject CutBlisterWorker(List<Polyline> pills, Polyline blister)
        {
            return CutBlisterWorker(pills.Select(pline => pline.ToPolylineCurve()).ToList() , blister.ToPolylineCurve());
        }

        private JObject CutBlisterWorker(List<PolylineCurve> pills, PolylineCurve blister)
        {
   
            CuttingState status = CuttingState.CTR_UNSET;

            // Pills and blister are already curve objects
            InitialiseNewBlister(pills, blister);
            cuttingResult["pillsDetected"] = pills.Count;

            try
            {
                status = PerformCut(mainLimit, cellLimit);
            }
            catch (Exception ex)
            {
                status = CuttingState.CTR_OTHER_ERR;
                log.Error("PerformCut Error catcher", ex);
            }

            cuttingResult["status"] = status.ToString();
            cuttingResult["pillsCutted"] = Cutted.Count;
            cuttingResult["jawsLocation"] = anchor.GetJSON();
            // If all alright, populate by cutting data
            if (status == CuttingState.CTR_SUCCESS)
            {

                JArray allCuttingInstruction = new JArray();
                foreach (Blister bli in Cutted)
                {
                    // Pass to JesonCretors JAW_1 Local coordinate for proper global coordinates calculation...
                    allCuttingInstruction.Add(bli.Cells[0].GetJSON(anchor.anchors[0].location));
                }
                cuttingResult["cuttingData"] = allCuttingInstruction;
            }
            return cuttingResult;
        }

        private CuttingState PerformCut(int mainLimit, int cellLimit)
        {
            // Check if blister is correctly allign
            if (!anchor.IsBlisterStraight(Setups.MaxBlisterPossitionDeviation)) return CuttingState.CTR_WRONG_BLISTER_POSSITION;
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

        private JObject PrepareStatus(CuttingState stateCode, string message)
        {
            JObject data = new JObject();
            data.Add("code", stateCode.ToString());
            data.Add("message", message);
            data.Add("notHandleException", new JObject());
            return data;
        }
        private JObject PrepareStatus(CuttingState stateCode, string message, Exception ex)
        {
            JObject data = PrepareStatus(stateCode, message);
            data.Add("code", stateCode.ToString());
            data.Add("message", message);

            JObject error_data = new JObject();
            error_data.Add("message", ex.Message);
            error_data.Add("stackTrace", ex.StackTrace);
            data["notHandleException"] = error_data;
            return data;
        }


        private JObject PrepareEmptyJSON()
        {
            JObject data = new JObject();
            data.Add("status", new JObject());
            data.Add("pillsDetected", null);
            data.Add("pillsCutted", null);
            data.Add("jawsLocation", null);
            data.Add("cuttingData", new JArray());
            return data;
        }

        private PolylineCurve SimplifyContours(PolylineCurve curve)
        {
            Polyline reduced = Geometry.DouglasPeuckerReduce(curve.ToPolyline(), 0.2);
            Point3d[] points;
            double[] param = reduced.ToPolylineCurve().DivideByLength(2.0, true, out points);
            return (new Polyline(points)).ToPolylineCurve();

        }

        private PolylineCurve SimplifyContours2(PolylineCurve curve)
        {
            Polyline pline = curve.ToPolyline();
            pline.Smooth(1.0);
            pline.DeleteShortSegments(Setups.CurveReduceTolerance);
            pline.MergeColinearSegments(Setups.ColinearTolerance, true);
            return pline.ToPolylineCurve();
        }

        public Tuple<Polyline, List<Polyline>> GetContursBasedOnJSON(string json, Dictionary<string, string> jsonCategoryMap)
        {
            JArray data = JArray.Parse(json);
            if (data.Count == 0)
            {
                log.Error(String.Format("JSON - Input JSON contains no data, or data are not Array type"));
                return null;
            }

            List<Polyline> pills = new List<Polyline>();
            List<Polyline> blister = new List<Polyline>();

            foreach (JObject obj_data in data)
            {
                JArray contours = (JArray)obj_data["contours"];
                // If more the one contours or zero per category, return error;
                if (contours.Count != 1)
                {
                    log.Error(String.Format("JSON - Found {0} contours per object. Only one contour per object allowed", contours.Count));
                    return null;
                } 
                // Get contour and create Polyline
                JArray points = (JArray)contours[0];
                Polyline pline = new Polyline(points.Count);
                foreach (JArray cont_data in points)
                {
                    pline.Add((double)cont_data[0], (double)cont_data[1], 0);
                }
                // Check if Polyline is closed
                if (!pline.IsClosed)
                {
                    log.Error(String.Format("JSON - Polyline is not closed. Inccorect contour data."));
                    return null;
                }
                if ((string)obj_data["category"] == jsonCategoryMap["pill"]) pills.Add(pline);
                else if ((string)obj_data["category"] == jsonCategoryMap["blister"]) blister.Add(pline);
                else {
                    string message = String.Format("JSON - Incorrect category for countour"); 
                    log.Error(message);
                    PrepareStatus(CuttingState.CTR_OTHER_ERR, message);
                    return null; }
            }

            if (blister.Count != 1) {
                string message = String.Format("JSON - {0} blister objects found! Need just one object.", blister.Count);
                log.Error(message);
                PrepareStatus(CuttingState.CTR_OTHER_ERR, message);
                return null; }
            return Tuple.Create(blister[0], pills);
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
