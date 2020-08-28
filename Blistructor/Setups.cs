using System;
using System.ComponentModel;
using System.Globalization;

namespace Blistructor
{
    static class Setups
    {

        // All stuff in mm.
        // IMAGE
        // Later this will be taken from calibration data
        #region BLISTER EXTRA CALIBRATION

        /// <summary>
        /// Extra Blister move on X (Blister local Coordiante System). Calibration appluy in order
        /// </summary>
        public static readonly double CalibrationVectorX = GetEnvironmentVariableWithDefault("BLISTER_CALIBRATION_LOCAL_X", 133.48341);
        
        /// <summary>
        /// Extra Blister move on Y (Blister local Coordiante System).
        /// </summary>
        public static readonly double CalibrationVectorY = GetEnvironmentVariableWithDefault("BLISTER_CALIBRATION_LOCAL_Y", 127.952386);

        /// <summary>
        /// Extra Blister uniform scale (Blister local Coordiante System).
        /// </summary>
        public static readonly double Spacing = GetEnvironmentVariableWithDefault("BLISTER_CALIBRATION_SPACING", 0.15645);

        /// <summary>
        ///  Extra Blister rotate around WorldXY (Blister local Coordiante System).
        /// </summary>
        public static readonly double Rotate = GetEnvironmentVariableWithDefault("BLISTER_CALIBRATION_ROTATE", 0.021685);
        #endregion

        # region GENERAL TOLERANCES
        public static readonly double MaxBlisterPossitionDeviation = GetEnvironmentVariableWithDefault("BLISTER_POSITION_MAX_DEVIATION", 1.1);
        public static readonly double GeneralTolerance = GetEnvironmentVariableWithDefault("TOLERANCE_GENERAL", 1e-5);
        public static readonly double CurveReduceTolerance = GetEnvironmentVariableWithDefault("TOLERANCE_CURVE_REDUCTION", 1.0);
        //public static readonly double CurveDistanceTolerance = 0.05;  // Curve tO polyline distance tolerance.
        public static readonly double IntersectionTolerance = GetEnvironmentVariableWithDefault("TOLERANCE_INTERSECTION", 1e-5);
        public static readonly double ColinearTolerance = GetEnvironmentVariableWithDefault("TOLERANCE_COLINEAR", 1e-3);
        #endregion

        #region BLADE STUFF
        public static readonly double BladeLength = GetEnvironmentVariableWithDefault("BLADE_CUT_LENGTH", 44.0);
        public static readonly double BladeTol = GetEnvironmentVariableWithDefault("BLADE_CUT_TOLERANCE", 2.0);
        public static readonly double BladeWidth = GetEnvironmentVariableWithDefault("BLADE_CUT_WIDTH", 3.0);
        public static readonly double BladeGlobalX = GetEnvironmentVariableWithDefault("BLADE_GLOBAL_X", 204.5);
        public static readonly double BladeGlobalY = GetEnvironmentVariableWithDefault("BLADE_GLOBAL_Y", 199.0);
  
        //Axis (Cartesian Global) to calculate angles. 
        public static readonly string BladeRotationAxis = GetEnvironmentVariableWithDefault("BLADE_ROTATION_AXIS", "X");
        // Knife cutting angles is calculated base od Global Cartesian X axis. Extra Rotation (in radians) if other angles are need. 
        public static readonly double BladeRotationCalibration = GetEnvironmentVariableWithDefault("BLADE_EXTRA_ROTATION", Math.PI / 2);
        #endregion

        #region CARTESIAN/JAW
        public static readonly double CartesianPickModeAngle = GetEnvironmentVariableWithDefault("CARTESIAN_PICK_MODE_ANGLE", Math.PI / 6);
        public static readonly double JawWidth = GetEnvironmentVariableWithDefault("CARTESIAN_JAW_WIDTH", 5.5); 
        public static readonly double JawDepth = GetEnvironmentVariableWithDefault("CARTESIAN_JAW_DEPTH",  3.0); 

        public static readonly double BlisterCartesianDistance = GetEnvironmentVariableWithDefault("CARTESIAN_SAFE_DISTANCE_TO_BLISTER", 3.5);  
        public static readonly double CartesianMaxWidth = GetEnvironmentVariableWithDefault("CARTESIAN_JAWS_MAX_RANGE", 85.0);  
        public static readonly double CartesianMinWidth = GetEnvironmentVariableWithDefault("CARTESIAN_JAWS_MIN_RANGE", 10.0);

        public static readonly double CartesianPivotJawVectorX = GetEnvironmentVariableWithDefault("CARTESIAN_PIVOT_JAW_X", 109.375);  
        public static readonly double CartesianPivotJawVectorY = GetEnvironmentVariableWithDefault("CARTESIAN_PIVOT_JAW_Y", 19.5);
        #endregion
        //OTHER

        public static readonly double BlisterGlobalX = GetEnvironmentVariableWithDefault("BLISTER_GLOBAL_X", 112.4);
        public static readonly double BlisterGlobalY = GetEnvironmentVariableWithDefault("BLISTER_GLOBAL_Y", 422.1);
        public static readonly double IsoRadius = GetEnvironmentVariableWithDefault("RAY_LENGTH", 2000.0); 
        public static readonly double MinimumCutOutSize = GetEnvironmentVariableWithDefault("CUTOUT_MIN_SIZE",35.0);

        // SIMPLIFY PATH TOLERANCES

        public static readonly double AngleTolerance = GetEnvironmentVariableWithDefault("SIMPLIFY_ANGLE", (0.5 * Math.PI) * 0.2);
        // if path segment is shorter then this, it will be collapsed
        public static readonly double CollapseTolerance = GetEnvironmentVariableWithDefault("SIMPLIFY_COLLAPSE_DISTANCE", 1.0);

        public static readonly double SnapDistance = GetEnvironmentVariableWithDefault("SIMPLIFY_TOLERANCE_SNAP_DISTANCE", 1.0);


        private static T GetEnvironmentVariableWithDefault<T> ( string variable, T defaultValue)
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
    }

}
