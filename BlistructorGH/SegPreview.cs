using System;
using System.Collections;
using System.Collections.Generic;

using Rhino;
using Rhino.Geometry;

using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

// <Custom using>
using System.IO;
using System.Linq;
using System.Reflection;
using System.ComponentModel;
using System.Globalization;

using Px = BlistructorGH;
using Blistructor;
using Newtonsoft.Json.Linq;

using PxGeo = Pixel.Rhino.Geometry;
using PxGeoI = Pixel.Rhino.Geometry.Intersect;
using ExtraMath = Pixel.Rhino.RhinoMath;

// </Custom using>


namespace Blistructor.SegPrev
{
    public class Script_Instance
    {
        /* Wypełniacz zeny linie sie zgadzały
         * 
         * 
         * 
         * 
         * 
         * 
         * 
         * 
         * 
         * 
         * 
         * 
         * 
         * 
         * 
         * 
         * 
         * 
         * 
         * 
         * 
         * 
         * 
         * 
         * 
         * 
         * 
         * 
         * 
         * 
         * 
         * 
         * 
         */
        private void RunScript(string jsonData, ref object o_blister, ref object o_pills, ref object cut_inst)
        {
            // <Custom code>
            // BASIC STUFF
           
            Blistructor.Logger.Setup();
            Blistructor.Logger.ClearAllLogFile();

            //Blister structor = new Blister();

            //JObject json = structor.CutBlister(jsonData);

            //cut_inst = json.ToString();

            Dictionary<string, string> jsonCategoryMap = new Dictionary<string, string>
                {
                    { "blister", "blister" },
                    { "Outline", "tabletka" }
                };
            Tuple<JObject, JArray> data = ParseJson(jsonData);
            JObject setup = data.Item1;
            JArray content = data.Item2;
            Setups.ApplySetups(setup);
            // Parse JSON. Item1 -> Blister, Item2 -> Pills
            Tuple<PxGeo.PolylineCurve, List<PxGeo.PolylineCurve>> pLines = GetContursBasedOnJSON(content, jsonCategoryMap);

            // Simplyfy paths on blister and pills
            PxGeo.PolylineCurve blister = SimplifyContours2(pLines.Item1, Setups.CurveReduceTolerance, Setups.CurveSmoothTolerance);
            List<PxGeo.PolylineCurve> pills = pLines.Item2.Select(pill => SimplifyContours2(pill, Setups.CurveReduceTolerance, Setups.CurveSmoothTolerance)).ToList();

            // Apply calibration on blister and pills
            // Vector3d calibrationVector = new Vector3d(rX, calibrationVectorY, 0);
            Blistructor.Geometry.ApplyCalibration(blister, Setups.ZeroPosition, Setups.PixelSpacing, Setups.CartesianPickModeAngle);
            pills.ForEach(pill => Blistructor.Geometry.ApplyCalibration(pill, Setups.ZeroPosition, Setups.PixelSpacing));
            if (Setups.TrimBlisterToXAxis)
            {
                List<PxGeo.Curve> result = Blistructor.Geometry.SplitRegion(blister, new PxGeo.LineCurve(new PxGeo.Line(new PxGeo.Point3d(-1000, -0.2, 0), PxGeo.Vector3d.XAxis, 2000)));
                if (result != null) blister = (PxGeo.PolylineCurve)result.OrderByDescending(c => c.Area()).ToList()[0];
            }


            //PxGeo.PolylineCurve Pill = Px.Convert.ToPix(Outline).ToPolylineCurve();
            // PxGeo.PolylineCurve Blister = Px.Convert.ToPix(blister).ToPolylineCurve();
            o_blister = Px.Convert.ToRh(blister);
            List<PolylineCurve> rh_pills = pills.Select(pill => Px.Convert.ToRh(pill)).ToList();
            o_pills = rh_pills;


            // </Custom code>
        }
        // <Custom additional code>
        public Tuple<JObject, JArray> ParseJson(string json)
        {
            JToken data = JToken.Parse(json);
            return new Tuple<JObject, JArray>(data.GetValue<JObject>("setup", null), data.GetValue<JArray>("content", null));
        }

        private PxGeo.PolylineCurve SimplifyContours2(PxGeo.PolylineCurve curve, double reductionTolerance = 0.0, double smoothTolerance = 0.0)
        {
            PxGeo.Polyline pline = curve.ToPolyline();
            pline.ReduceSegments(reductionTolerance);
            pline.Smooth(smoothTolerance);
            return pline.ToPolylineCurve();
        }

        public Tuple<PxGeo.PolylineCurve, List<PxGeo.PolylineCurve>> GetContursBasedOnJSON(JArray content, Dictionary<string, string> jsonCategoryMap)
        {
            //JArray data = JArray.Parse(json);
            if (content.Count == 0)
            {
                string message = String.Format("JSON - Input JSON contains no data, or data are not Array type");
                throw new NotSupportedException(message);
            }

            List<PxGeo.PolylineCurve> pills = new List<PxGeo.PolylineCurve>();
            List<PxGeo.PolylineCurve> blister = new List<PxGeo.PolylineCurve>();

            foreach (JObject obj_data in content)
            {
                JArray contours = (JArray)obj_data["contours"];
                // Can be more then one contour, MaskRCNN can detect small shit withing bboxes, so have to remove them by filtering areas. 
                List<PxGeo.Polyline> tempContours = new List<PxGeo.Polyline>();
                foreach (JArray contour in contours)
                {
                    // Get contour and create Polyline
                    PxGeo.Polyline pline = new PxGeo.Polyline(contour.Count);
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
                    throw new InvalidOperationException(message);
                }
                PxGeo.Polyline finalPline = tempContours.OrderByDescending(contour => contour.Area()).First();


                if ((string)obj_data["category"] == jsonCategoryMap["Outline"]) pills.Add(finalPline.ToPolylineCurve());
                else if ((string)obj_data["category"] == jsonCategoryMap["blister"]) blister.Add(finalPline.ToPolylineCurve());
                else
                {
                    string message = String.Format("JSON - Incorrect category for countour");
                    throw new InvalidOperationException(message);
                }
            }

            if (blister.Count != 1)
            {
                string message = String.Format("JSON - {0} blister objects found! Need just one object.", blister.Count);
                throw new NotSupportedException(message);
            }
            return Tuple.Create(blister[0], pills);
        }

        static class Setups
        {

            public static double PixelSpacing = GetEnvironmentVariableWithDefault("PIXEL_SPACING", 1.0);
            public static PxGeo.Vector3d ZeroPosition = new PxGeo.Vector3d(GetEnvironmentVariableWithDefault("ZERO_POSITION_X", 0), GetEnvironmentVariableWithDefault("ZERO_POSITION_Y", 0), 0);

            #region GENERAL TOLERANCES
            public static double MaxBlisterPossitionDeviation = GetEnvironmentVariableWithDefault("BLISTER_POSITION_MAX_DEVIATION", 1.1);
            public static double GeneralTolerance = GetEnvironmentVariableWithDefault("TOLERANCE_GENERAL", 1e-5);

            //public static readonly double CurveDistanceTolerance = 0.05;  // Curve tO polyline distance tolerance.
            public static double IntersectionTolerance = GetEnvironmentVariableWithDefault("TOLERANCE_INTERSECTION", 1e-5);
            public static double ColinearTolerance = GetEnvironmentVariableWithDefault("TOLERANCE_COLINEAR", 1e-3);
            #endregion

            #region BLADE STUFF

            public static double BladeLength = GetEnvironmentVariableWithDefault("BLADE_CUT_LENGTH", 44.0);
            public static double BladeTol = GetEnvironmentVariableWithDefault("BLADE_CUT_TOLERANCE", 2.0);
            public static double BladeWidth = GetEnvironmentVariableWithDefault("BLADE_CUT_WIDTH", 3.0);
            public static PxGeo.Vector3d BladeGlobal = new PxGeo.Vector3d(GetEnvironmentVariableWithDefault("BLADE_GLOBAL_X", 200), GetEnvironmentVariableWithDefault("BLADE_GLOBAL_Y", 199.0), 0);


            //Axis (Cartesian Global) to calculate angles. 
            public static string BladeRotationAxis = GetEnvironmentVariableWithDefault("BLADE_ROTATION_AXIS", "X");
            // Knife cutting angles is calculated base od Global Cartesian X axis. Extra Rotation (in radians) if other angles are need. 

            public static double BladeRotationCalibration = ExtraMath.ToRadians(GetEnvironmentVariableWithDefault("BLADE_EXTRA_ROTATION", 0));
            #endregion

            #region CARTESIAN/

            public static double CartesianPickModeAngle = ExtraMath.ToRadians(GetEnvironmentVariableWithDefault("CARTESIAN_PICK_MODE_ANGLE", 30));
            public static double JawWidth = GetEnvironmentVariableWithDefault("CARTESIAN_JAW_WIDTH", 5.5);
            public static double JawDepth = GetEnvironmentVariableWithDefault("CARTESIAN_JAW_DEPTH", 3.0);

            public static double BlisterCartesianDistance = GetEnvironmentVariableWithDefault("CARTESIAN_SAFE_DISTANCE_TO_BLISTER", 3.5);
            public static double CartesianMaxWidth = GetEnvironmentVariableWithDefault("CARTESIAN_JAWS_MAX_RANGE", 85.0);
            public static double CartesianMinWidth = GetEnvironmentVariableWithDefault("CARTESIAN_JAWS_MIN_RANGE", 10.0);
            public static double CartesianJawYLimit = GetEnvironmentVariableWithDefault("CARTESIAN_JAW_Y_LIMIT", 15.0);


            public static PxGeo.Vector3d CartesianPivotJawVector = new PxGeo.Vector3d(GetEnvironmentVariableWithDefault("CARTESIAN_PIVOT_JAW_X", 112.4), GetEnvironmentVariableWithDefault("CARTESIAN_PIVOT_JAW_Y", 19.5), 0);
            #endregion

            //OTHER
            public static string BlisterGlobalSystem = GetEnvironmentVariableWithDefault("BLISTER_GLOBAL_System", "PICK");
            public static PxGeo.Vector3d BlisterGlobal = new PxGeo.Vector3d(GetEnvironmentVariableWithDefault("BLISTER_GLOBAL_X", 108.1), GetEnvironmentVariableWithDefault("BLISTER_GLOBAL_Y", 411.5), 0);
            public static PxGeo.Vector3d BlisterGlobalPick = new PxGeo.Vector3d(GetEnvironmentVariableWithDefault("BLISTER_GLOBAL_PICK_X", 113), GetEnvironmentVariableWithDefault("BLISTER_GLOBAL_PICK_Y", 358), 0);

            public static double IsoRadius = GetEnvironmentVariableWithDefault("RAY_LENGTH", 2000.0);
            public static double MinimumCutOutSize = GetEnvironmentVariableWithDefault("CUTOUT_MIN_SIZE", 25.0);

            // SIMPLIFY PATH TOLERANCES
            public static double CurveReduceTolerance = GetEnvironmentVariableWithDefault("TOLERANCE_CURVE_REDUCTION", 2.0);
            public static double CurveSmoothTolerance = GetEnvironmentVariableWithDefault("TOLERANCE_CURVE_SMOOTH", 1.0);
            public static double AngleTolerance = GetEnvironmentVariableWithDefault("SIMPLIFY_ANGLE", 0.1 * Math.PI);
            // if path segment is shorter then this, it will be collapsed
            public static double CollapseTolerance = GetEnvironmentVariableWithDefault("SIMPLIFY_COLLAPSE_DISTANCE", 1.0);

            public static double SnapDistance = GetEnvironmentVariableWithDefault("SIMPLIFY_TOLERANCE_SNAP_DISTANCE", 1.0);
            public static bool TrimBlisterToXAxis = GetEnvironmentVariableWithDefault("TRIM_BLISTER_X_AXIS", true);


            private static T GetEnvironmentVariableWithDefault<T>(string variable, T defaultValue)
            {
                string value = Environment.GetEnvironmentVariable(variable);
                if (value != null) return TryParse<T>(value);
                else return defaultValue;
            }

            public static T TryParse<T>(string inValue)
            {
                TypeConverter converter =
                    TypeDescriptor.GetConverter(typeof(T));

                return (T)converter.ConvertFromString(null,
                    CultureInfo.InvariantCulture, inValue);
            }

            public static void ApplySetups(JObject setup)
            {
                PixelSpacing = setup.GetValue<double>("pixelSpacing", PixelSpacing);
                List<double> ZeroPositionValues = setup.GetValue<List<double>>("zeroPositionCalibration", new List<double>() { ZeroPosition.X, ZeroPosition.Y });
                ZeroPosition = new PxGeo.Vector3d(ZeroPositionValues[0], ZeroPositionValues[1], 0);

                GeneralTolerance = setup.GetValue<double>("generalTolerance", GeneralTolerance);
                IntersectionTolerance = setup.GetValue<double>("intersectionTolerance", IntersectionTolerance);
                ColinearTolerance = setup.GetValue<double>("colinearTolerance", ColinearTolerance);

                MaxBlisterPossitionDeviation = setup.GetValue<double>("maxBlisterPossitionDeviation", MaxBlisterPossitionDeviation);

                BladeLength = setup.GetValue<double>("bladeCutLength", BladeLength);
                BladeTol = setup.GetValue<double>("bladeCutTol", BladeTol);

                BladeWidth = setup.GetValue<double>("bladeCutWidth", BladeWidth);

                List<double> BladeGlobalValues = setup.GetValue<List<double>>("bladeGlobalPosition", new List<double>() { BladeGlobal.X, BladeGlobal.Y });
                BladeGlobal = new PxGeo.Vector3d(BladeGlobalValues[0], BladeGlobalValues[1], 0);

                BladeRotationAxis = setup.GetValue<string>("bladeRotationAxis", BladeRotationAxis);
                BladeRotationCalibration = ExtraMath.ToRadians(setup.GetValue<double>("bladeRotationCalibration", BladeRotationCalibration));

                CartesianPickModeAngle = ExtraMath.ToRadians(setup.GetValue<double>("cartesianPickModeAngle", CartesianPickModeAngle));
                JawWidth = setup.GetValue<double>("cartesianJawWidth", JawWidth);
                JawDepth = setup.GetValue<double>("cartesianJawDepth", JawDepth);

                BlisterCartesianDistance = setup.GetValue<double>("cartesianBlisterSafeDistance", BlisterCartesianDistance);
                CartesianMaxWidth = setup.GetValue<double>("cartesianJawMaxRange", CartesianMaxWidth);
                CartesianMinWidth = setup.GetValue<double>("cartesianJawMinRange", CartesianMinWidth);
                CartesianJawYLimit = setup.GetValue<double>("cartesianJawYLimit", CartesianJawYLimit);

                List<double> CartesianPivotJawVectorValues = setup.GetValue<List<double>>("cartesianPivotJawVector", new List<double>() { CartesianPivotJawVector.X, CartesianPivotJawVector.Y });
                CartesianPivotJawVector = new PxGeo.Vector3d(CartesianPivotJawVectorValues[0], CartesianPivotJawVectorValues[1], 0);

                //OTHER         public static string BlisterGlobalSystem = GetEnvironmentVariableWithDefault("BLISTER_GLOBAL_System", "PICK");

                BlisterGlobalSystem = setup.GetValue<string>("blisterGlobalSystem", BlisterGlobalSystem);

                List<double> BlisterGlobalValues = setup.GetValue<List<double>>("blisterGlobalPosition", new List<double>() { BlisterGlobal.X, BlisterGlobal.Y });
                BlisterGlobal = new PxGeo.Vector3d(BlisterGlobalValues[0], BlisterGlobalValues[1], 0);

                List<double> BlisterGlobalPickValues = setup.GetValue<List<double>>("blisterGlobalPickPosition", new List<double>() { BlisterGlobalPick.X, BlisterGlobalPick.Y });
                BlisterGlobalPick = new PxGeo.Vector3d(BlisterGlobalPickValues[0], BlisterGlobalPickValues[1], 0);

                IsoRadius = setup.GetValue<double>("rayLength", IsoRadius);
                MinimumCutOutSize = setup.GetValue<double>("minimumCutOutSize", MinimumCutOutSize);

                // SIMPLIFY PATH TOLERANCES
                CurveReduceTolerance = setup.GetValue<double>("contourReduceTolerance", CurveReduceTolerance);
                CurveSmoothTolerance = setup.GetValue<double>("contourSmoothTolerance", CurveSmoothTolerance);
                AngleTolerance = setup.GetValue<double>("simplyfyAngleTolerance", AngleTolerance);
                // if path segment is shorter then this, it will be collapsed
                CollapseTolerance = setup.GetValue<double>("simplyfyCollapseTolerance", CollapseTolerance);
                SnapDistance = setup.GetValue<double>("simplyfySnapDistance", SnapDistance);
                TrimBlisterToXAxis = setup.GetValue<bool>("trimBlisterToXAxis", TrimBlisterToXAxis);
            }
        }


        // </Custom additional code>

    }
}
