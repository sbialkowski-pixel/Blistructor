using System;
using Rhino.Geometry;

namespace Blistructor
{
    static class Setups
    {
        // All stuff in mm.
        // IMAGE
        // Later this will be taken from calibration data
        public const double CalibrationVectorX = 133.48341;
        public const double CalibrationVectorY = 127.952386;
        public const double Spacing = 0.15645;
        public const double Rotate = 0.021685;

        // GENERAL TOLERANCES
        public const double MaxBlisterPossitionDeviation = 1.1;
        public const double GeneralTolerance = 0.0001;
        public const double CurveFitTolerance = 0.2;
        public const double CurveDistanceTolerance = 0.05;  // Curve tO polyline distance tolerance.
        public const double IntersectionTolerance = 0.0001;
        public const double OverlapTolerance = 0.0001;
        
        // BLADE STUFF
        public const double BladeLength = 44.0;
        public const double BladeTol = 2.0;
        public const double BladeWidth = 3.0;
        public const double BladeGlobalX = 204.5;
        public const double BladeGlobalY = 199.0;
        public const double BladeRotationCalibration = Math.PI / 2;

        // CARTESIAN/JAW
        public const double CartesianPickModeAngle = Math.PI/6;
        public const double CartesianThickness = 6.0;
        public const double CartesianDepth = 3.5;
        public const double BlisterCartesianDistance = 3.0;
        public const double CartesianMaxWidth = 85.0;
        public const double CartesianMinWidth = 10.0;
        public const double CartesianPivotJawVectorX = 109.375;
        public const double CartesianPivotJawVectorY = 19.5;
        //OTHER

        public const double BlisterGlobalX = 112.4;
        public const double BlisterGlobalY = 422.1;
        public const double IsoRadius = 2000.0;
        public const double MinimumCutOutSize = 35.0;

        // SIMPLIFY PATH TOLERANCES
        public const double AngleTolerance = (0.5 * Math.PI) * 0.2;
        public const double CollapseTolerance = 1.0; // if path segment is shorter then this, it will be collapsed
        public const double SnapDistance = 1.0; // if path segment is shorter then this, it will be collapsed
    }

}
