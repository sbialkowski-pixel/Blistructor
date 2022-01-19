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
// TODO: AdvancedCutting -> blister 19.

//
// TODO: Zle sie wyliczaja Anchor Pointy - trzeba dodac grasperPredLine na górze i na dole  i brac mina z obu extremalnych konców...

// TODO: -WIP-: Adaptacyjna kolejność ciecia - po każdej wycietej tabletce, nalezało by przesortowac cell tak aby wubierał najbliższe - Nadal kolejnosc ciecia jest do kitu ...
// TODO: "ładne" logowanie produkcyjne jak i debugowe.
// TODO: Posprzątanie w klasach.

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
        private static readonly ILog log = LogManager.GetLogger("Cutter.Main");

        public PolylineCurve mainOutline;

        private int loopTolerance = 5;
        public List<SubBlister> Queue;
        public List<SubBlister> Cutted;

        //  private JObject cuttingResult;

        public Anchor anchor;
        public List<Curve> worldObstacles;


        public MultiBlister()
        {
            // Logger.Setup();
            Queue = new List<SubBlister>();
            Cutted = new List<SubBlister>();
            // cuttingResult = PrepareEmptyJSON();
        }

        public int CuttableCellsLeft
        {
            get
            {
                int counter = 0;
                foreach (SubBlister subBlister in Queue)
                {
                    counter += subBlister.Cells.Select(cell => cell.State).Where(state => state == CellState.Queue || state == CellState.Alone).ToList().Count;
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
                foreach (SubBlister subBlister in Cutted)
                {
                    polygons.Add(subBlister.Outline);
                }
                return polygons;
            }
        }


        public List<List<LineCurve>> GetCuttingLines
        {
            get
            {
                List<List<LineCurve>> cuttingLines = new List<List<LineCurve>>(Cutted.Count);
                foreach (SubBlister subBlister in Cutted)
                {
                    cuttingLines.Add(subBlister.GetCuttingLines());
                }
                return cuttingLines;
            }
        }

        //public JObject GetLastCutting { get{ return cuttingResult;}}


        #endregion

        private void InitialiseNewCut()
        {
            // Init Logger
            // Logger.Setup();

            // Initialize Lists
            Queue = new List<SubBlister>();
            Cutted = new List<SubBlister>();

            //cuttingResult = PrepareEmptyJSON();
        }

        private void InitialiseNewBlister(List<PolylineCurve> pills, PolylineCurve blister)
        {
            SubBlister initialBlister = new SubBlister(pills, blister);
            initialBlister.SortCellsByCoordinates(true);
            Queue.Add(initialBlister);
            anchor = new Anchor(this);


            // World Obstacles
            // Line cartesianLimitLine = new Line(new Point3d(0, -Setups.BlisterCartesianDistance, 0), Vector3d.XAxis, 1.0);
            //cartesianLimitLine.Extend(Setups.IsoRadius, Setups.IsoRadius);

            worldObstacles = new List<Curve>() { anchor.cartesianLimitLine };
            log.Info(String.Format("New blister with {0} pills", pills.Count));
        }

        //double pixelSpacing = 1.0, double calibrationVectorX = 0.0, double calibrationVectorY = 0.0
        public JObject CutBlister(string JSON)
        {
            try
            {
                InitialiseNewCut();
                Dictionary<string, string> jsonCategoryMap = new Dictionary<string, string>
                {
                    { "blister", "blister" },
                    { "pill", "tabletka" }
                };
                Tuple<JObject, JArray> data = ParseJson(JSON);
                JObject setup = data.Item1;
                JArray content = data.Item2;
                Setups.ApplySetups(setup);
                // Parse JSON. Item1 -> SubBlister, Item2 -> Pills
                Tuple<PolylineCurve, List<PolylineCurve>> pLines = GetContursBasedOnJSON(content, jsonCategoryMap);

                // Simplyfy paths on blister and pills
                PolylineCurve blister = SimplifyContours2(pLines.Item1, Setups.CurveReduceTolerance, Setups.CurveSmoothTolerance);
                List<PolylineCurve> pills = pLines.Item2.Select(pill => SimplifyContours2(pill, Setups.CurveReduceTolerance, Setups.CurveSmoothTolerance)).ToList();

                // Apply calibration on blister and pills
                // Vector3d calibrationVector = new Vector3d(rX, calibrationVectorY, 0);
                Geometry.ApplyCalibration(blister, Setups.ZeroPosition, Setups.PixelSpacing, Setups.CartesianPickModeAngle);
                pills.ForEach(pill => Geometry.ApplyCalibration(pill, Setups.ZeroPosition, Setups.PixelSpacing, Setups.CartesianPickModeAngle));
                if (Setups.TrimBlisterToXAxis)
                {
                    List<Curve> result = Geometry.SplitRegion(blister, new LineCurve(new Line(new Point3d(-1000, -0.2, 0), Vector3d.XAxis, 2000)));
                    if (result != null) blister = (PolylineCurve)result.OrderByDescending(c => c.Area()).ToList()[0];
                }
                return CutBlisterWorker(pills, blister);
            }

            catch (AnchorException ex)
            {
                log.Error("Anchor Exception.", ex);
                return PrepareStatus(CuttingState.CTR_WRONG_BLISTER_POSSITION, "Anchor Exception.", ex);
            }

            catch (Exception ex)
            {
                log.Error("Unhandled Exception.", ex);
                return PrepareStatus(CuttingState.CTR_OTHER_ERR, "Unhandled Exception.", ex);
            }
        }

        public JObject CutBlister(List<Polyline> pills, Polyline blister)
        {
            try
            {
                InitialiseNewCut();
                return CutBlisterWorker(pills, blister);
            }
            catch (Exception ex)
            {
                return PrepareStatus(CuttingState.CTR_OTHER_ERR, "Unhandled Exception.", ex);
            }
        }

        public JObject CutBlister(List<PolylineCurve> pills, PolylineCurve blister)
        {
            try
            {
                InitialiseNewCut();
                return CutBlisterWorker(pills, blister);
            }
            catch (Exception ex)
            {
                return PrepareStatus(CuttingState.CTR_OTHER_ERR, "Unhandled Exception.", ex);
            }
        }

        private JObject CutBlisterWorker(List<Polyline> pills, Polyline blister)
        {
            return CutBlisterWorker(pills.Select(pline => pline.ToPolylineCurve()).ToList(), blister.ToPolylineCurve());
        }

        private JObject CutBlisterWorker(List<PolylineCurve> pills, PolylineCurve blister)
        {

            //  CuttingState status = CuttingState.CTR_UNSET;

            // Pills and blister are already curve objects
            // InitialiseNewBlister(pills, blister);
            // cuttingResult["pillsDetected"] = pills.Count;
            /*
            try
            {
                status = PerformCut();
            }
            catch (Exception ex)
            {
                status = CuttingState.CTR_OTHER_ERR;
                log.Error("PerformCut Error", ex);
            }
            */

            InitialiseNewBlister(pills, blister);
            CuttingState status = PerformCut();
            JObject cuttingResult = PrepareStatus(status);
            cuttingResult.Merge(PrepareEmptyJSON());
            cuttingResult["pillsDetected"] = pills.Count;
            cuttingResult["pillsCutted"] = Cutted.Count;
            cuttingResult["jawsLocation"] = anchor.GetJSON();
            // If all alright, populate by cutting data
            if (status == CuttingState.CTR_SUCCESS)
            {

                JArray allCuttingInstruction = new JArray();
                JArray allDisplayInstruction = new JArray();
                foreach (SubBlister bli in Cutted)
                {
                    // Pass to JsonCretors JAW_1 Local coordinate for proper global coordinates calculation...
                    allCuttingInstruction.Add(bli.Cells[0].GetJSON(anchor.anchors[0].location));
                    allDisplayInstruction.Add(bli.Cells[0].GetDisplayJSON(anchor.anchors));
                }
                cuttingResult["cuttingData"] = allCuttingInstruction;
                cuttingResult["displayData"] = allDisplayInstruction;
            }
            return cuttingResult;
        }

        private CuttingState PerformCut()
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
                //if (n > mainLimit && mainLimit != -1) break;
                // Extra control to not loop forever...
                if (n > initialPillCount + loopTolerance) break;
                log.Info(String.Format(String.Format("<<<<<<Blisters Count: Queue: {0}, Cutted {1}>>>>>>>>>>>>>>>>>>>>>>>>", Queue.Count, Cutted.Count)));
                // InnerLoop - Queue Blisters

                for (int i = 0; i < Queue.Count; i++)
                {
                    if (Queue == null) continue;
                    SubBlister subBlister = Queue[i];
                    log.Info(String.Format("{0} cells left to cut on on SubBlister:{1}", subBlister.Cells.Count, i));
                    if (subBlister.IsDone)
                    {
                        log.Info("SubBlister is already cutted or is to tight for cutting.");
                        continue;
                    }
                    // In tuple I have | CutOut SubBlister | Current Updated SubBlister | Extra Blisters to Cut (recived by spliting currentBlister) 
                    CutResult result = subBlister.CutNext(worldObstacles);
                    log.Debug(String.Format("Cutting Result: Cutout: {0} - Current SubBlister {1} - New Blisters {2}.", result.CutOut, result.Current, result.ExtraBlisters.Count));
                    // If anything was cutted, add to list
                    if (result.CutOut != null)
                    {
                        Cell cuttedCell = result.CutOut.Cells[0];
                        if (!cuttedCell.IsAnchored)
                        {
                            log.Debug("Anchor - Update Pred Line");

                            anchor.Update(result.CutOut);
                        }
                        if (cuttedCell.IsAnchored && cuttedCell.State != CellState.Alone && CuttableCellsLeft == 2) anchor.FindNewAnchorAndApplyOnBlister(result.CutOut);
                        log.Debug("Adding new CutOut subBlister to Cutted list");
                        Cutted.Add(result.CutOut);
                    }
                    else
                    {
                        log.Error("!!!Cannot cut blister Anymore!!!");
                        return CuttingState.CTR_FAILED;
                    }
                    // override current bluster, if null , remove it from Queue list
                    if (result.Current == null)
                    {
                        log.Info("Current subBlister is empty. Removing from Queue");
                        Queue.RemoveAt(i);
                        i--;
                        break;
                    }
                    else
                    {
                        log.Debug("Updating subBlister");
                        subBlister = result.Current;
                        // Sort Pills by last Knife Possition -> Last Pill Centre
                        // Point3d lastKnifePossition = Cutted.Last().Cells[0].bestCuttingData.GetLastKnifePossition();
                        Point3d lastKnifePossition = Cutted.Last().Cells[0].PillCenter;
                        if (lastKnifePossition.X != double.NaN) subBlister.SortCellsByPointDirection(lastKnifePossition, false);
                        //if (lastKnifePossition.X != double.NaN) subBlister.SortCellsByCoordinates(true);

                    }
                    // Add extra blsters if any was created
                    if (result.ExtraBlisters.Count != 0)
                    {
                        log.Debug("Adding new subBlister(s) to Queue");
                        Queue.AddRange(result.ExtraBlisters);
                        break;
                    }

                }
                n++;
            }

            if (initialPillCount == Cutted.Count) return CuttingState.CTR_SUCCESS;
            else return CuttingState.CTR_FAILED;
        }

        private JObject PrepareStatus(CuttingState stateCode, string message = "")
        {
            if (message == "") message = stateCode.GetDescription();
            JObject data = new JObject
            {
                { "status", stateCode.ToString() },
                { "message", message }
            };
            return data;
        }

        private JObject PrepareStatus(CuttingState stateCode, string message, Exception ex)
        {
            JObject data = PrepareStatus(stateCode, message);
            JObject error_data = new JObject
            {
                { "message", ex.Message },
                { "stackTrace", ex.StackTrace }
            };
            data.Add("unhandledException", error_data);
            return data;
        }

        private JObject PrepareEmptyJSON()
        {
            //JObject data = PrepareStatus(CuttingState.CTR_UNSET, "");
            JObject data = new JObject
            {
                { "pillsDetected", null },
                { "pillsCutted", null },
                { "jawsLocation", null },
                { "cuttingData", new JArray() }
            };

            return data;
        }

        private PolylineCurve SimplifyContours(PolylineCurve curve)
        {
            Polyline reduced = Geometry.DouglasPeuckerReduce(curve.ToPolyline(), 0.2);
            Point3d[] points;
            double[] param = reduced.ToPolylineCurve().DivideByLength(2.0, true, out points);
            return (new Polyline(points)).ToPolylineCurve();

        }

        private PolylineCurve SimplifyContours2(PolylineCurve curve, double reductionTolerance = 0.0, double smoothTolerance = 0.0)
        {
            Polyline pline = curve.ToPolyline();
            pline.ReduceSegments(reductionTolerance);
            pline.Smooth(smoothTolerance);
            return pline.ToPolylineCurve();
        }

        public Tuple<JObject, JArray> ParseJson(string json)
        {
            JToken data = JToken.Parse(json);
            return new Tuple<JObject, JArray>(data.GetValue<JObject>("setup", null), data.GetValue<JArray>("content", null));
        }

        //TODO: Dodanie thresholda do segmentacji. Tutaj albo w UdoneVision.
        public Tuple<PolylineCurve, List<PolylineCurve>> GetContursBasedOnJSON(JArray content, Dictionary<string, string> jsonCategoryMap)
        {
            //JArray data = JArray.Parse(json);
            if (content.Count == 0)
            {
                string message = String.Format("JSON - Input JSON contains no data, or data are not Array type");
                log.Error(message);
                throw new NotSupportedException(message);
            }

            List<PolylineCurve> pills = new List<PolylineCurve>();
            List<PolylineCurve> blister = new List<PolylineCurve>();

            foreach (JObject obj_data in content)
            {
                JArray contours = (JArray)obj_data["contours"];
                // Can be more then one contour, MaskRCNN can detect small shit withing bboxes, so have to remove them by filtering areas. 
                List<Polyline> tempContours = new List<Polyline>();
                foreach (JArray contour in contours)
                {
                    // Get contour and create Polyline
                    Polyline pline = new Polyline(contour.Count);
                    foreach (JArray cont_data in contour)
                    {
                        pline.Add((double)cont_data[0], (double)cont_data[1], 0);
                    }
                    // If Polyline is closed, proceed it further
                    if (pline.IsClosed) tempContours.Add(pline);
                }
                if (tempContours.Count == 0)
                {
                    string message = String.Format("JSON - None closed polyline for contour set. Incorrect contour data.");
                    log.Error(message);
                    throw new InvalidOperationException(message);
                }
                Polyline finalPline = tempContours.OrderByDescending(contour => contour.Area()).First();


                // If more the one contours or zero per category, return error;
                /*if (contours.Count != 1)
                {
                    string message = String.Format("JSON - Found {0} contours per object. Only one contour per object allowed", contours.Count);
                    log.Error(message);
                    throw new InvalidOperationException(message);
                }
                */



                if ((string)obj_data["category"] == jsonCategoryMap["pill"]) pills.Add(finalPline.ToPolylineCurve());
                else if ((string)obj_data["category"] == jsonCategoryMap["blister"]) blister.Add(finalPline.ToPolylineCurve());
                else
                {
                    string message = String.Format("JSON - Incorrect category for countour");
                    log.Error(message);
                    throw new InvalidOperationException(message);
                }
            }

            if (blister.Count != 1)
            {
                string message = String.Format("JSON - {0} blister objects found! Need just one object.", blister.Count);
                log.Error(message);
                throw new NotSupportedException(message);
            }
            return Tuple.Create(blister[0], pills);
        }
    }
}