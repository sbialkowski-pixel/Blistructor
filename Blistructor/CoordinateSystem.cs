using System;
using System.Collections.Generic;
using System.Linq;

#if PIXEL
using Pixel.Rhino;
using Pixel.Rhino.Geometry;
using Pixel.Rhino.Geometry.Intersect;
#else
using Rhino;
using Rhino.Geometry;
#endif

#if DEBUG
using Pixel.Rhino.FileIO;
using Pixel.Rhino.DocObjects;
#endif
using log4net;
using Newtonsoft.Json.Linq;


namespace Blistructor
{
    /// <summary>
    /// Class containing all tools needed to transform between local and global Coordinate Systems (CS) with Cartesian PICK-WORK mode switching.
    /// </summary>
    static class CoordinateSystem
    {

        /// <summary>
        /// Transform point from global (machine) to local (algorithm working area - image) coordinates.
        /// </summary>
        /// <param name="point">Point3d in global CS to transform</param>
        /// <param name="csLoc">Beginning of local CS given in global CS</param>
        /// <param name="csAngle">Angle between local-global X axis in radians</param>
        /// <returns>Point3d in local CS</returns>
        public static Point3d GlobalLocalTransform(Point3d point, Point3d csLoc, double csAngle)
        {
            Vector3d pt = point - csLoc;
            double newX = pt.X * Math.Cos(csAngle) + pt.Y * Math.Sin(csAngle);
            double newY = -pt.X * Math.Sin(csAngle) + pt.Y * Math.Cos(csAngle);
            return new Point3d(newX, newY, 0);
        }

        /// <summary>
        /// Transform point from local (algorithm working area - image) to global (machine)  coordinates.
        /// </summary>
        /// <param name="point">Point3d in local CS to transform</param>
        /// <param name="csLoc">Beginning of local CS given in global CS</param>
        /// <param name="csAngle">>Angle between local-global X axis in radians</param>
        /// <returns>Point3d in global CS</returns>
        public static Point3d LocalGlobalTransform(Point3d point, Point3d csLoc, double csAngle)
        {
            //Vector3d pt = point - csLoc;
            double newX = point.X * Math.Cos(csAngle) - point.Y * Math.Sin(csAngle);
            double newY = point.X * Math.Sin(csAngle) + point.Y * Math.Cos(csAngle);
            Point3d pt = new Point3d(newX, newY, 0);
            return pt + csLoc;
        }

        /// <summary>
        /// Computes translation vector between WORK-PICK position.
        /// </summary>
        /// <param name="pivotJawVector">Vector between Jaws rotation pivot and Jaws2 point</param>
        /// <param name="pickAngle">Angle between PICK-WORK mode in radians</param>
        /// <returns>Vector3d as translation vector between WORK-PICK position</returns>
        public static Vector3d CartesianWorkPickVector(Vector3d pivotJawVector, double pickAngle)
        {
            Point3d pt = GlobalLocalTransform((Point3d)pivotJawVector, new Point3d(0, 0, 0), pickAngle);
            return (Vector3d)pt - pivotJawVector;
        }

        /// <summary>
        /// Computes translation vector between PICK-WORK position.
        /// </summary>
        /// <param name="pivotJawVector">Vector between Jaws rotation pivot and Jaws2 point</param>
        /// <param name="pickAngle">Angle between PICK-WORK mode in radians</param>
        /// <returns>Vector3d as translation vector between PICK-WORK position</returns>
        public static Vector3d CartesianPickWorkVector(Vector3d pivotJawVector, double pickAngle)
        {
            Vector3d vec = CartesianWorkPickVector(pivotJawVector, pickAngle);
            vec.Reverse();
            return vec;
        }

        /// <summary>
        /// Compute 
        /// </summary>
        /// <param name="localJaw1">Jaw1 location in local CS</param>
        /// <param name="blisterCSLocation"></param>
        /// <param name="blisterCSangle">Angle between PICK-WORK mode in radians</param>
        /// <param name="pivotJawVector">Vector between Jaws rotation pivot and Jaws2 point</param>
        /// <returns>Point3d </returns>
        public static Point3d CartesianGlobalJaw1L(Point3d localJaw1, Point3d blisterCSLocation, double blisterCSangle, Vector3d pivotJawVector)
        {
            Point3d globalJaw1 = LocalGlobalTransform(localJaw1, blisterCSLocation, blisterCSangle);
            Vector3d correctionVector = CartesianWorkPickVector(pivotJawVector, -blisterCSangle);
            return globalJaw1 - correctionVector;
        }

        /// <summary>
        /// Compute blister position in global (machine) CS, assuming Cartesian is in PICK mode,
        /// </summary>
        /// <returns>Point3d in global CS</returns>
        private static Point3d ComputeGlobalBlisterPossition()
        {
            Vector3d correctionVector = CartesianWorkPickVector(Setups.CartesianPivotJawVector, -Setups.CartesianPickModeAngle);
            return new Point3d(Setups.BlisterGlobalPick + correctionVector);
        }

        /// <summary>
        /// Compute Jaw location in global CS 
        /// </summary>
        /// <param name="anchors"></param>
        /// <returns>List of Point3d with JAws in global CS</returns>
        public static List<Point3d> ComputeGlobalAnchors(List<AnchorPoint> anchors)
        {
            // Compute Global location for JAW_1
            Point3d blisterCS;
            if (Setups.BlisterGlobalSystem == "PICK") blisterCS = ComputeGlobalBlisterPossition();
            else if (Setups.BlisterGlobalSystem == "WORK") blisterCS = new Point3d(Setups.BlisterGlobal);
            else throw new NotImplementedException("PICK or WORK mode for blister coordinate system has to be chosen");
            
            List<Point3d> globalAnchors = new List<Point3d>(anchors.Count);

            foreach (AnchorPoint anchor in anchors)
            {
                //NOTE: Zamiana X, Y, należy sprawdzić czy to jest napewno dobrze. Wg. moich danych i opracowanej logiki tak...
                Point3d flipedPoint = new Point3d(anchor.location.Y, anchor.location.X, 0);
                Point3d globalJawLocation = CartesianGlobalJaw1L(flipedPoint, blisterCS, Setups.CartesianPickModeAngle, Setups.CartesianPivotJawVector);
                globalAnchors.Add(globalJawLocation);
            }
            return globalAnchors;
        }

    }
}
