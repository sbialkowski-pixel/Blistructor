using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Pixel.Rhino.Geometry;
using ExtraMath = Pixel.Rhino.RhinoMath;

namespace Blistructor
{

    static public class Setups
    {
        public static bool CreateChunkDebugFile = false;
        public static bool CreateBlisterDebugFile = false;
        public static bool CreatePillsDebugFiles = false;
        public static bool CreateCutterDebugFiles = false;

        #region CALIBRATOR DATA
        public static double PixelSpacing { get; private set; }
        public static Vector3d ZeroPosition { get; private set; }

        #endregion

        #region GENERAL TOLERANCES 
        public static double GeneralTolerance { get; private set; }
        public static double IntersectionTolerance { get; private set; }
        public static double ColinearTolerance { get; private set; }
        public static double MaxBlisterPossitionDeviation { get; private set; }
        #endregion

        #region SIMPLIFY/SMOOTH TOLERANCES
        public static double CurveReduceTolerance { get; private set; }
        public static double CurveSmoothTolerance { get; private set; }
        public static double AngleTolerance { get; private set; }
        // if path segment is shorter then this, it will be collapsed
        public static double CollapseTolerance { get; private set; }
        public static double SnapDistance { get; private set; }
        #endregion

        #region BLADE STUFF
        public static double BladeLength { get; private set; }
        public static double BladeTol { get; private set; }
        public static double BladeWidth { get; private set; }
        public static Vector3d BladeGlobal { get; private set; }

        //Axis (Cartesian Global) to calculate angles. 
        public static string BladeRotationAxis { get; private set; }
        // Knife cutting angles is calculated base od Global Cartesian X axis. Extra Rotation (in radians) if other angles are need. 
        public static double BladeRotationCalibration { get; private set; }
        #endregion

        #region CARTESIAN
        public static double CartesianPickModeAngle { get; private set; }
        public static double JawWidth { get; private set; }
        public static double JawDepth { get; private set; }
        public static double BlisterCartesianDistance { get; private set; }
        public static double CartesianMaxWidth { get; private set; }
        public static double CartesianMinWidth { get; private set; }
        public static double CartesianJawYLimit { get; private set; }
        public static Vector3d CartesianPivotJawVector { get; private set; }
        public static double JawKnifeAdditionalSafeDistance { get; private set; }
        public static double JawPillSafeDistance { get; private set; }
        #endregion

        #region GLOBA COORDINATE SYSTEM
        public static string BlisterGlobalSystem { get; private set; }
        public static Vector3d BlisterGlobal { get; private set; }
        public static Vector3d BlisterGlobalPick { get; private set; }
        #endregion

        #region GENERAL CONTROL
        public static double IsoRadius { get; private set; }
        public static double MinimumCutOutSize { get; private set; }
        public static double MinimumInterPillDistance { get; private set; }
        public static bool TrimBlisterToXAxis { get; private set; }
        public static double SegmentationScoreTreshold { get; private set; }
        #endregion

        public static string DebugDir = "D:\\PIXEL\\Blistructor\\DebugModels";

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
            #region CALIBRATOR DATA
            PixelSpacing = setup.GetValue<double>("pixelSpacing", PixelSpacing);
            List<double> ZeroPositionValues = setup.GetValue<List<double>>("zeroPositionCalibration", new List<double>() { ZeroPosition.X, ZeroPosition.Y });
            ZeroPosition = new Vector3d(ZeroPositionValues[0], ZeroPositionValues[1], 0);
            #endregion
            #region GENERAL TOLERANCES
            GeneralTolerance = setup.GetValue<double>("generalTolerance", GeneralTolerance);
            IntersectionTolerance = setup.GetValue<double>("intersectionTolerance", IntersectionTolerance);
            ColinearTolerance = setup.GetValue<double>("colinearTolerance", ColinearTolerance);

            MaxBlisterPossitionDeviation = setup.GetValue<double>("maxBlisterPossitionDeviation", MaxBlisterPossitionDeviation);
            #endregion
            #region SIMPLIFY/SMOOTH TOLERANCES
            // SIMPLIFY PATH TOLERANCES
            CurveReduceTolerance = setup.GetValue<double>("contourReduceTolerance", CurveReduceTolerance);
            CurveSmoothTolerance = setup.GetValue<double>("contourSmoothTolerance", CurveSmoothTolerance);
            AngleTolerance = setup.GetValue<double>("simplyfyAngleTolerance", AngleTolerance);
            // if path segment is shorter then this, it will be collapsed
            CollapseTolerance = setup.GetValue<double>("simplyfyCollapseTolerance", CollapseTolerance);
            SnapDistance = setup.GetValue<double>("simplyfySnapDistance", SnapDistance);
            #endregion
            #region BLADE STUFF
            BladeLength = setup.GetValue<double>("bladeCutLength", BladeLength);
            BladeTol = setup.GetValue<double>("bladeCutTol", BladeTol);
            BladeWidth = setup.GetValue<double>("bladeCutWidth", BladeWidth);

            List<double> BladeGlobalValues = setup.GetValue<List<double>>("bladeGlobalPosition", new List<double>() { BladeGlobal.X, BladeGlobal.Y });
            BladeGlobal = new Vector3d(BladeGlobalValues[0], BladeGlobalValues[1], 0);

            BladeRotationAxis = setup.GetValue<string>("bladeRotationAxis", BladeRotationAxis);
            BladeRotationCalibration = ExtraMath.ToRadians(setup.GetValue<double>("bladeRotationCalibration", BladeRotationCalibration));
            #endregion
            #region CARTESIAN
            CartesianPickModeAngle = ExtraMath.ToRadians(setup.GetValue<double>("cartesianPickModeAngle", CartesianPickModeAngle));
            JawWidth = setup.GetValue<double>("cartesianJawWidth", JawWidth);
            JawDepth = setup.GetValue<double>("cartesianJawDepth", JawDepth);
            BlisterCartesianDistance = setup.GetValue<double>("cartesianBlisterSafeDistance", BlisterCartesianDistance);
            CartesianMaxWidth = setup.GetValue<double>("cartesianJawMaxRange", CartesianMaxWidth);
            CartesianMinWidth = setup.GetValue<double>("cartesianJawMinRange", CartesianMinWidth);
            CartesianJawYLimit = setup.GetValue<double>("cartesianJawYLimit", CartesianJawYLimit);
            List<double> CartesianPivotJawVectorValues = setup.GetValue<List<double>>("cartesianPivotJawVector", new List<double>() { CartesianPivotJawVector.X, CartesianPivotJawVector.Y });
            CartesianPivotJawVector = new Vector3d(CartesianPivotJawVectorValues[0], CartesianPivotJawVectorValues[1], 0);
            JawKnifeAdditionalSafeDistance = setup.GetValue<double>("jawKnifeAdditionalSafeDistance", JawKnifeAdditionalSafeDistance);
            JawPillSafeDistance = setup.GetValue<double>("jawPillSafeDistance", JawPillSafeDistance);
            #endregion
            #region GLOBAL COORDINATE SYSTEM
            BlisterGlobalSystem = setup.GetValue<string>("blisterGlobalSystem", BlisterGlobalSystem);

            List<double> BlisterGlobalValues = setup.GetValue<List<double>>("blisterGlobalPosition", new List<double>() { BlisterGlobal.X, BlisterGlobal.Y });
            BlisterGlobal = new Vector3d(BlisterGlobalValues[0], BlisterGlobalValues[1], 0);

            List<double> BlisterGlobalPickValues = setup.GetValue<List<double>>("blisterGlobalPickPosition", new List<double>() { BlisterGlobalPick.X, BlisterGlobalPick.Y });
            BlisterGlobalPick = new Vector3d(BlisterGlobalPickValues[0], BlisterGlobalPickValues[1], 0);
            #endregion
            #region GENERAL CONTROL
            IsoRadius = setup.GetValue<double>("rayLength", IsoRadius);
            MinimumCutOutSize = setup.GetValue<double>("minimumCutOutSize", MinimumCutOutSize);
            MinimumInterPillDistance = setup.GetValue<double>("minimumInterPillDistance", MinimumInterPillDistance);
            TrimBlisterToXAxis = setup.GetValue<bool>("trimBlisterToXAxis", TrimBlisterToXAxis);
            SegmentationScoreTreshold = setup.GetValue<double>("segmentationScoreTreshold", SegmentationScoreTreshold);
            #endregion
        }

        static Setups()
        {
#if DEBUG_FILE
            CreateChunkDebugFile = true;
            CreateBlisterDebugFile = true;
            CreatePillsDebugFiles = true;
            CreateCutterDebugFiles = true;
#endif
            #region CALIBRATOR DATA
            PixelSpacing = GetEnvironmentVariableWithDefault("PIXEL_SPACING", 1.0);
            ZeroPosition = new Vector3d(GetEnvironmentVariableWithDefault("ZERO_POSITION_X", 0), GetEnvironmentVariableWithDefault("ZERO_POSITION_Y", 0), 0);
            #endregion
            #region GENERAL TOLERANCES 
            GeneralTolerance = GetEnvironmentVariableWithDefault("TOLERANCE_GENERAL", 1e-5);
            IntersectionTolerance = GetEnvironmentVariableWithDefault("TOLERANCE_INTERSECTION", 1e-5);
            ColinearTolerance = GetEnvironmentVariableWithDefault("TOLERANCE_COLINEAR", 1e-3);
            MaxBlisterPossitionDeviation = GetEnvironmentVariableWithDefault("BLISTER_POSITION_MAX_DEVIATION", 1.1);
            #endregion
            #region SIMPLIFY/SMOOTH TOLERANCES
            CurveReduceTolerance = GetEnvironmentVariableWithDefault("TOLERANCE_CURVE_REDUCTION", 2.0);
            CurveSmoothTolerance = GetEnvironmentVariableWithDefault("TOLERANCE_CURVE_SMOOTH", 1.0);
            AngleTolerance = GetEnvironmentVariableWithDefault("SIMPLIFY_ANGLE", 0.1 * Math.PI);
            // if path segment is shorter then this, it will be collapsed
            CollapseTolerance = GetEnvironmentVariableWithDefault("SIMPLIFY_COLLAPSE_DISTANCE", 1.0);
            SnapDistance = GetEnvironmentVariableWithDefault("SIMPLIFY_TOLERANCE_SNAP_DISTANCE", 1.0);
            #endregion
            #region BLADE STUFF
            BladeLength = GetEnvironmentVariableWithDefault("BLADE_CUT_LENGTH", 44.0);
            BladeTol = GetEnvironmentVariableWithDefault("BLADE_CUT_TOLERANCE", 2.0);
            BladeWidth = GetEnvironmentVariableWithDefault("BLADE_CUT_WIDTH", 4.5);
            Vector3d BladeGlobal = new Vector3d(GetEnvironmentVariableWithDefault("BLADE_GLOBAL_X", 200), GetEnvironmentVariableWithDefault("BLADE_GLOBAL_Y", 199.0), 0);

            //Axis (Cartesian Global) to calculate angles. 
            BladeRotationAxis = GetEnvironmentVariableWithDefault("BLADE_ROTATION_AXIS", "X");
            // Knife cutting angles is calculated base od Global Cartesian X axis. Extra Rotation (in radians) if other angles are need. 
            BladeRotationCalibration = ExtraMath.ToRadians(GetEnvironmentVariableWithDefault("BLADE_EXTRA_ROTATION", 0));
            #endregion
            #region CARTESIAN
            CartesianPickModeAngle = ExtraMath.ToRadians(GetEnvironmentVariableWithDefault("CARTESIAN_PICK_MODE_ANGLE", 30));
            JawWidth = GetEnvironmentVariableWithDefault("CARTESIAN_JAW_WIDTH", 5.5);
            JawDepth = GetEnvironmentVariableWithDefault("CARTESIAN_JAW_DEPTH", 3.0);
            BlisterCartesianDistance = GetEnvironmentVariableWithDefault("CARTESIAN_SAFE_DISTANCE_TO_BLISTER", 3.5);
            CartesianMaxWidth = GetEnvironmentVariableWithDefault("CARTESIAN_JAWS_MAX_RANGE", 85.0);
            CartesianMinWidth = GetEnvironmentVariableWithDefault("CARTESIAN_JAWS_MIN_RANGE", 10.0);
            CartesianJawYLimit = GetEnvironmentVariableWithDefault("CARTESIAN_JAW_Y_LIMIT", 45.0);
            CartesianPivotJawVector = new Vector3d(GetEnvironmentVariableWithDefault("CARTESIAN_PIVOT_JAW_X", 112.4), GetEnvironmentVariableWithDefault("CARTESIAN_PIVOT_JAW_Y", 19.5), 0);
            JawKnifeAdditionalSafeDistance = GetEnvironmentVariableWithDefault("JAW_KNIFE_ADDITIONAL_SAFE_DISTANCE", 1);
            JawPillSafeDistance = GetEnvironmentVariableWithDefault("JAW_PILL_SAFE_DISTANCE", 0.0);
            #endregion
            #region GLOBA COORDINATE SYSTEM
            BlisterGlobalSystem = GetEnvironmentVariableWithDefault("BLISTER_GLOBAL_SYSTEM", "PICK");
            BlisterGlobal = new Vector3d(GetEnvironmentVariableWithDefault("BLISTER_GLOBAL_X", 108.1), GetEnvironmentVariableWithDefault("BLISTER_GLOBAL_Y", 411.5), 0);
            BlisterGlobalPick = new Vector3d(GetEnvironmentVariableWithDefault("BLISTER_GLOBAL_PICK_X", 113), GetEnvironmentVariableWithDefault("BLISTER_GLOBAL_PICK_Y", 358), 0);
            #endregion
            #region GENERAL CONTROL
            IsoRadius = GetEnvironmentVariableWithDefault("RAY_LENGTH", 2000.0);
            MinimumCutOutSize = GetEnvironmentVariableWithDefault("CUTOUT_MIN_SIZE", 25.0);
            MinimumInterPillDistance = GetEnvironmentVariableWithDefault("MINIMUM_INTER_PILL_DISTANCE", 3.0);
            TrimBlisterToXAxis = GetEnvironmentVariableWithDefault("TRIM_BLISTER_X_AXIS", true);
            SegmentationScoreTreshold = GetEnvironmentVariableWithDefault("SEGMENTATION_SCORE_TRESHOLD", 0.9);
            #endregion
        }

    }

    public static class JTokenExtention
    {
        public static T GetValue<T>(this JToken jobject, string propertyName, T defaultValue)
        {
            return jobject[propertyName] != null ? jobject[propertyName].ToObject<T>() : defaultValue;
        }
    }

}

/*
 * {"setup":
    {

    "generalTolerance":1e-5,
    "intersectionTolerance":1e-5,
    "colinearTolerance": 1e-3,

    "maxBlisterPossitionDeviation": 1.1,

    "bladeCutLength": 44.0,
    "bladeCutTol":2.0,
    "bladeCutWidth" :3.0,

    "bladeGlobalPosition": [204.5,199.0],
    "bladeRotationAxis": "X",
    "bladeRotationCalibration": "Math.PI / 2",

    "cartesianPickModeAngle": "Math.PI / 6",
    "cartesianJawWidth" : 5.5, 
    "cartesianJawDepth" : 3.0 ,

    "cartesianBlisterSafeDistance": 3.5,
    "cartesianJawMaxRange": 85.0,  
    "cartesianJawMinRange" : 10.0,

    "cartesianPivotJawVector": [109.375, 19.5],
    "blisterGlobalPosition": [112.4, 422.1],
    
    "rayLength": 2000.0,
    "minimumCutOutSize": 25.0,

    "contourReduceTolerance": 2.0,
    "contourSmoothTolerance": 1.0,
    "simplyfyAngleTolerance":" 0.1 * Math.PI",
    "simplyfyCollapseTolerance": 1.0,
    "simplyfySnapDistance":1.0
},
"content":{}
}
*/