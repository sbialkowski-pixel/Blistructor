﻿using System;
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

        #region CALIBRATOR DATA
        public static double PixelSpacing = GetEnvironmentVariableWithDefault("PIXEL_SPACING", 1.0);
        public static Vector3d ZeroPosition = new Vector3d(GetEnvironmentVariableWithDefault("ZERO_POSITION_X", 0), GetEnvironmentVariableWithDefault("ZERO_POSITION_Y", 0), 0);
        #endregion

        #region GENERAL TOLERANCES 
        public static double GeneralTolerance = GetEnvironmentVariableWithDefault("TOLERANCE_GENERAL", 1e-5);

        //public static readonly double CurveDistanceTolerance = 0.05;  // Curve tO polyline distance tolerance.
        public static double IntersectionTolerance = GetEnvironmentVariableWithDefault("TOLERANCE_INTERSECTION", 1e-5);
        public static double ColinearTolerance = GetEnvironmentVariableWithDefault("TOLERANCE_COLINEAR", 1e-3);
        public static double MaxBlisterPossitionDeviation = GetEnvironmentVariableWithDefault("BLISTER_POSITION_MAX_DEVIATION", 1.1);
        #endregion

        #region SIMPLIFY/SMOOTH TOLERANCES
        public static double CurveReduceTolerance = GetEnvironmentVariableWithDefault("TOLERANCE_CURVE_REDUCTION", 2.0);
        public static double CurveSmoothTolerance = GetEnvironmentVariableWithDefault("TOLERANCE_CURVE_SMOOTH", 1.0);
        public static double AngleTolerance = GetEnvironmentVariableWithDefault("SIMPLIFY_ANGLE", 0.1 * Math.PI);
        // if path segment is shorter then this, it will be collapsed
        public static double CollapseTolerance = GetEnvironmentVariableWithDefault("SIMPLIFY_COLLAPSE_DISTANCE", 1.0);

        public static double SnapDistance = GetEnvironmentVariableWithDefault("SIMPLIFY_TOLERANCE_SNAP_DISTANCE", 1.0);
        #endregion

        #region BLADE STUFF
        public static double BladeLength = GetEnvironmentVariableWithDefault("BLADE_CUT_LENGTH", 44.0);
        public static double BladeTol = GetEnvironmentVariableWithDefault("BLADE_CUT_TOLERANCE", 2.0);
        public static double BladeWidth = GetEnvironmentVariableWithDefault("BLADE_CUT_WIDTH", 3.0);
        public static Vector3d BladeGlobal = new Vector3d(GetEnvironmentVariableWithDefault("BLADE_GLOBAL_X", 200), GetEnvironmentVariableWithDefault("BLADE_GLOBAL_Y", 199.0), 0);

        //Axis (Cartesian Global) to calculate angles. 
        public static string BladeRotationAxis = GetEnvironmentVariableWithDefault("BLADE_ROTATION_AXIS", "X");
        // Knife cutting angles is calculated base od Global Cartesian X axis. Extra Rotation (in radians) if other angles are need. 
    
        public static double BladeRotationCalibration = ExtraMath.ToRadians(GetEnvironmentVariableWithDefault("BLADE_EXTRA_ROTATION", 0));
        #endregion

        #region CARTESIAN
        public static double CartesianPickModeAngle = ExtraMath.ToRadians(GetEnvironmentVariableWithDefault("CARTESIAN_PICK_MODE_ANGLE", 30));
        public static double JawWidth = GetEnvironmentVariableWithDefault("CARTESIAN_JAW_WIDTH", 5.5);
        public static double JawDepth = GetEnvironmentVariableWithDefault("CARTESIAN_JAW_DEPTH", 3.0);
        public static double BlisterCartesianDistance = GetEnvironmentVariableWithDefault("CARTESIAN_SAFE_DISTANCE_TO_BLISTER", 3.5);
        public static double CartesianMaxWidth = GetEnvironmentVariableWithDefault("CARTESIAN_JAWS_MAX_RANGE", 85.0);
        public static double CartesianMinWidth = GetEnvironmentVariableWithDefault("CARTESIAN_JAWS_MIN_RANGE", 10.0);
        public static double CartesianJawYLimit = GetEnvironmentVariableWithDefault("CARTESIAN_JAW_Y_LIMIT", 45.0);
        public static Vector3d CartesianPivotJawVector = new Vector3d(GetEnvironmentVariableWithDefault("CARTESIAN_PIVOT_JAW_X", 112.4), GetEnvironmentVariableWithDefault("CARTESIAN_PIVOT_JAW_Y", 19.5), 0);
        #endregion

        #region GLOBA COORDINATE SYSTEM
        public static string BlisterGlobalSystem = GetEnvironmentVariableWithDefault("BLISTER_GLOBAL_SYSTEM", "PICK");
        public static Vector3d BlisterGlobal = new Vector3d(GetEnvironmentVariableWithDefault("BLISTER_GLOBAL_X", 108.1), GetEnvironmentVariableWithDefault("BLISTER_GLOBAL_Y", 411.5), 0);
        public static Vector3d BlisterGlobalPick = new Vector3d(GetEnvironmentVariableWithDefault("BLISTER_GLOBAL_PICK_X", 113), GetEnvironmentVariableWithDefault("BLISTER_GLOBAL_PICK_Y", 358), 0);
        #endregion

        #region GENERAL CONTROL
        public static double IsoRadius = GetEnvironmentVariableWithDefault("RAY_LENGTH", 2000.0);

        public static double MinimumCutOutSize = GetEnvironmentVariableWithDefault("CUTOUT_MIN_SIZE", 25.0);

        public static bool TrimBlisterToXAxis = GetEnvironmentVariableWithDefault("TRIM_BLISTER_X_AXIS", true);

        public static double JawKnifeAdditionalSafeDistance = GetEnvironmentVariableWithDefault("JAW_KNIFE_ADDITIONAL_SAFE_DISTANCE", 1.00);

        public static double SegmentationScoreTreshold = GetEnvironmentVariableWithDefault("SEGMENTATION_SCORE_TRESHOLD", 0.9);

        #endregion

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
            List<double> CartesianPivotJawVectorValues = setup.GetValue<List<double>>("cartesianPivotJawVector", new List<double>(){ CartesianPivotJawVector.X, CartesianPivotJawVector.Y });
            CartesianPivotJawVector = new Vector3d(CartesianPivotJawVectorValues[0], CartesianPivotJawVectorValues[1], 0);
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
            TrimBlisterToXAxis = setup.GetValue<bool>("trimBlisterToXAxis", TrimBlisterToXAxis);
            JawKnifeAdditionalSafeDistance = setup.GetValue<double>("jawKnifeAdditionalSafeDistance", JawKnifeAdditionalSafeDistance);
            SegmentationScoreTreshold = setup.GetValue<double>("segmentationScoreTreshold", SegmentationScoreTreshold);
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