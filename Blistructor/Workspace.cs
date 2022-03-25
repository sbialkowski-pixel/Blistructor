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
// TODO: "ładne" logowanie produkcyjne jak i debugowe.


namespace Blistructor
{
    public class Workspace
    {
        private static readonly ILog log = LogManager.GetLogger("Cutter.Main");

        public Workspace()
        {
        }
        
        public JObject CutBlister(string JSON)
        {
            try
            {
                Tuple<JObject, JArray> data = ParseJson(JSON);
                JObject setup = data.Item1;
                JArray content = data.Item2;
                Setups.ApplySetups(setup);
                // Parse JSON. Item1 -> Blister, Item2 -> Pills
                Tuple<PolylineCurve, List<PolylineCurve>> pLines = GetContursBasedOnJSON(content);

                // Simplyfy paths on blister and pills
                PolylineCurve blister = Geometry.SimplifyContours2(pLines.Item1, Setups.CurveReduceTolerance, Setups.CurveSmoothTolerance);
                List<PolylineCurve> pills = pLines.Item2.Select(pill => Geometry.SimplifyContours2(pill, Setups.CurveReduceTolerance, Setups.CurveSmoothTolerance)).ToList();

                // Apply calibration on blister and pills
                // Vector3d calibrationVector = new Vector3d(rX, calibrationVectorY, 0);
                Geometry.ApplyCalibration(blister, Setups.ZeroPosition, Setups.PixelSpacing, Setups.CartesianPickModeAngle);
                pills.ForEach(pill => Geometry.ApplyCalibration(pill, Setups.ZeroPosition, Setups.PixelSpacing, Setups.CartesianPickModeAngle));
                
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
                return CutBlisterWorker(pills.Select(pline => pline.ToPolylineCurve()).ToList(), blister.ToPolylineCurve());
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
                return CutBlisterWorker(pills, blister);
            }
            catch (Exception ex)
            {
                return PrepareStatus(CuttingState.CTR_OTHER_ERR, "Unhandled Exception.", ex);
            }
        }

        private JObject CutBlisterWorker(List<PolylineCurve> pillsOutlines, PolylineCurve blisterOutlines)
        {
             // Trim Blister to X axis
            if (Setups.TrimBlisterToXAxis)
            {
                LineCurve xAxis = new LineCurve(new Line(new Point3d(-1000, -0.2, 0), Vector3d.XAxis, 2000));
                List<Curve> result = Geometry.SplitRegion(blisterOutlines, xAxis);
                if (result != null) blisterOutlines = (PolylineCurve)result.OrderByDescending(c => c.Area()).ToList()[0];
            }

            BlisterProcessor cutter = new BlisterProcessor(pillsOutlines, blisterOutlines);
            CuttingState status = cutter.PerformCut();
            JObject cuttingResult = PrepareStatus(status);
            cuttingResult.Merge(PrepareEmptyJSON());
            cuttingResult["pillsDetected"] = pillsOutlines.Count;
            cuttingResult["pillsCutted"] = cutter.Chunks.Count;
            cuttingResult["jawsLocation"] = cutter.Grasper.GetJSON();

            JArray allCuttingInstruction = new JArray();
            JArray allDisplayInstruction = new JArray();
            foreach (CutBlister bli in cutter.Chunks)
            {
                allCuttingInstruction.Add(bli.GetJSON(cutter.Grasper));
                allDisplayInstruction.Add(bli.GetDisplayJSON(cutter.Grasper));
            }
            cuttingResult["cuttingData"] = allCuttingInstruction;
            cuttingResult["displayData"] = allDisplayInstruction;
 
            return cuttingResult;
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

        public Tuple<JObject, JArray> ParseJson(string json)
        {
            JToken data = JToken.Parse(json);
            return new Tuple<JObject, JArray>(data.GetValue<JObject>("setup", null), data.GetValue<JArray>("content", null));
        }

        public Tuple<PolylineCurve, List<PolylineCurve>> GetContursBasedOnJSON(JArray content)
        {
            Dictionary<string, string> jsonCategoryMap = new Dictionary<string, string>
                {
                    { "blister", "blister" },
                    { "Outline", "tabletka" }
                };

            if (content.Count == 0)
            {
                string message = string.Format("JSON - Input JSON contains no data, or data are not Array type");
                log.Error(message);
                throw new NotSupportedException(message);
            }

            List<PolylineCurve> pills = new List<PolylineCurve>();
            List<PolylineCurve> blister = new List<PolylineCurve>();

            foreach (JObject obj_data in content)
            {
                // Check detected object score, if is lower then ScoreThreshold, omit this contour.
                if ((double)obj_data["score"] < Setups.SegmentationScoreTreshold) continue;

                //Build contours
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
                    string message = string.Format("JSON - None closed polyline for contour set. Incorrect contour data.");
                    log.Error(message);
                    throw new InvalidOperationException(message);
                }
                // Filtering areas
                Polyline finalPline = tempContours.OrderByDescending(contour => contour.Area()).First();

                if ((string)obj_data["category"] == jsonCategoryMap["Outline"]) pills.Add(finalPline.ToPolylineCurve());
                else if ((string)obj_data["category"] == jsonCategoryMap["blister"]) blister.Add(finalPline.ToPolylineCurve());
                else
                {
                    string message = string.Format("JSON - Incorrect category for countour");
                    log.Error(message);
                    throw new InvalidOperationException(message);
                }
            }

            if (blister.Count != 1)
            {
                string message = string.Format("JSON - {0} blister objects found! Need just one object.", blister.Count);
                log.Error(message);
                throw new NotSupportedException(message);
            }
            return Tuple.Create(blister[0], pills);
        }
    }
}