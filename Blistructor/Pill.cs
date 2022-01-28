using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;

#if PIXEL
using Pixel.Rhino;
using Pixel.Rhino.FileIO;
using Pixel.Rhino.Geometry;
using Pixel.Rhino.Geometry.Intersect;
using ExtraMath = Pixel.Rhino.RhinoMath;
#else
using Rhino;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;
using ExtraMath = Rhino.RhinoMath;
#endif

using log4net;
using Newtonsoft.Json.Linq;

using Combinators;
using Pixel.Rhino.DocObjects;

namespace Blistructor
{
    public class Pill
    {
        private static readonly ILog log = LogManager.GetLogger("Cutter.Pill");

        public int Id { get;  }

        // Parent Blister
        internal Blister blister ;

        // States
        public List<AnchorPoint> Anchors;
        public bool possibleAnchor;

        // Pill Stuff
        public PolylineCurve Outline { get; private set; }
        public PolylineCurve Offset { get; private set; }

        public Point3d Center { get; private set; }

        // Connection and Adjacent Stuff
        public PolylineCurve voronoi;
        //!!connectionLines, proxLines, adjacentPills, samplePoints <- all same sizes, and order!!
        public List<Curve> connectionLines;
        public List<Curve> proxLines;
        public List<Pill> adjacentPills;
        public List<Point3d> samplePoints;

        public List<Curve> obstacles;

        public Pill(Pill pill)
        {
            Id = pill.Id;
            blister = pill.blister;
            Anchors = pill.Anchors;
            possibleAnchor = pill.possibleAnchor;
            Outline = pill.Outline;
            Offset = pill.Offset;
            Center = pill.Center;
        }

        public Pill(int id, PolylineCurve pill, Blister subblister)
        {
            Id = id;
            State = PillState.Queue;
            blister = subblister;
            // Prepare all needed Pill properties
            Outline = pill;
            // Make Pill curve oriented in proper direction.
            Geometry.UnifyCurve(Outline);

            //Anchor = new AnchorPoint();
            Anchors = new List<AnchorPoint>(2);
            possibleAnchor = false;

            Center = Outline.ToPolyline().CenterPoint();

            // Create Outline offset
            Curve ofCur = Outline.Offset(Plane.WorldXY, Setups.BladeWidth / 2);
            if (ofCur == null)
            {
                log.Error("Incorrect Outline offseting");
                throw new InvalidOperationException("Incorrect Outline offseting");
            }
            else
            {
                Offset = (PolylineCurve)ofCur;
            }
        }

        #region PROPERTIES
        public Blister Blister
        {
            set
            {
                blister = value;
                EstimateOrientationCircle();
                SortData();
            }
        }

        public double CoordinateIndicator
        {
            get
            {
                return Center.X + Center.Y * 100;
            }
        }

        public NurbsCurve OrientationCircle { get; private set; }

        public PillState State { get; set; }

        #endregion
        /*
        public List<LineCurve> GetTrimmedIsoRays()
        {
            List<LineCurve> output = new List<LineCurve>();
            foreach (CutData cData in cuttingData)
            {
                output.AddRange(cData.TrimmedIsoRays);
            }
            return output;
        }
        */

        public bool IsAnchored
        {
            get
            {
                return Anchors.Any(anchor => anchor.state == AnchorState.Active);
            }
        }

        #region DISTANCES
        public double GetDirectionIndicator(Point3d pt)
        {
            Vector3d vec = pt - this.Center;
            return Math.Abs(vec.X) + Math.Abs(vec.Y) * 100;
        }
        public double GetDistance(Point3d pt)
        {
            return pt.DistanceTo(this.Center);
        }
        public double GetClosestDistance(List<Point3d> pts)
        {
            PointCloud ptC = new PointCloud(pts);
            int closestIndex = ptC.ClosestPoint(this.Center);
            return this.Center.DistanceTo(pts[closestIndex]);
        }
        public double GetDistance(Curve crv)
        {
            double t;
            crv.ClosestPoint(this.Center, out t);
            return crv.PointAt(t).DistanceTo(this.Center);
        }
        public double GetClosestDistance(List<Curve> crvs)
        {
            List<Point3d> ptc = new List<Point3d>();
            foreach (Curve crv in crvs)
            {
                double t;
                crv.ClosestPoint(this.Center, out t);
                ptc.Add(crv.PointAt(t));
            }
            return GetClosestDistance(ptc);
        }

        #endregion

        /*
        public void SetDistance(LineCurve guideLine)
        {
            double t;
            guideLine.ClosestPoint(PillCenter, out t);
            GuideDistance = PillCenter.DistanceTo(guideLine.PointAt(t));
            double distance_A = PillCenter.DistanceTo(guideLine.PointAtStart);
            double distance_B = PillCenter.DistanceTo(guideLine.PointAtEnd);
            //Rhino.RhinoApp.WriteLine(String.Format("Dist_A: {0}, Dist_B: {1}", distance_A, distance_B));
            CornerDistance = Math.Min(distance_A, distance_B);

            //CornerDistance = distance_A + distance_B;
            //CornerDistance = pillCenter.DistanceTo(guideLine.PointAtStart);
        }
        */

        public void AddConnectionData(List<Pill> pills, List<Curve> lines, List<Point3d> midPoints)
        {
            adjacentPills = new List<Pill>();
            samplePoints = new List<Point3d>();
            connectionLines = new List<Curve>();

            EstimateOrientationCircle();
            int[] ind = Geometry.SortPtsAlongCurve(midPoints, OrientationCircle);

            foreach (int id in ind)
            {
                adjacentPills.Add(pills[id]);
                connectionLines.Add(lines[id]);
            }
            proxLines = new List<Curve>();
            foreach (Pill pill in adjacentPills)
            {
                Point3d ptA, ptB;
                if (Offset.ClosestPoints(pill.Offset, out ptA, out ptB))
                {
                    LineCurve proxLine = new LineCurve(ptA, ptB);
                    proxLines.Add(proxLine);
                    Point3d samplePoint = proxLine.Line.PointAt(0.5);   //PointAtNormalizedLength(0.5);
                    samplePoints.Add(samplePoint);
                }
            }
        }

        public void RemoveConnectionData()
        {
            for (int i = 0; i < adjacentPills.Count; i++)
            {
                adjacentPills[i].RemoveConnectionData(Id);
            }
        }

        /// <summary>
        /// Call from adjacent Outline
        /// </summary>
        /// <param name="pillId">ID of Pill which is executing this method</param>
        protected void RemoveConnectionData(int pillId)
        {
            for (int i = 0; i < adjacentPills.Count; i++)
            {
                if (adjacentPills[i].Id == pillId)
                {
                    adjacentPills.RemoveAt(i);
                    connectionLines.RemoveAt(i);
                    proxLines.RemoveAt(i);
                    samplePoints.RemoveAt(i);
                    i--;
                }
            }
            SortData();
        }

        public List<int> GetAdjacentPillsIds()
        {
            return adjacentPills.Select(pill => pill.Id).ToList();
        }
        public void EstimateOrientationCircle()
        {
            double circle_radius = Outline.GetBoundingBox(false).Diagonal.Length / 2;
            OrientationCircle = (new Circle(Center, circle_radius)).ToNurbsCurve();
            Geometry.EditSeamBasedOnCurve(OrientationCircle, blister.Outline);
        }

        public void SortData()
        {
            EstimateOrientationCircle();
            int[] sortingIndexes = Geometry.SortPtsAlongCurve(samplePoints, OrientationCircle);

            samplePoints = sortingIndexes.Select(index => samplePoints[index]).ToList();
            connectionLines = sortingIndexes.Select(index => connectionLines[index]).ToList();
            proxLines = sortingIndexes.Select(index => proxLines[index]).ToList();
            adjacentPills = sortingIndexes.Select(index => adjacentPills[index]).ToList();
        }

        // Get ProxyLines without lines pointed as Id
        public List<Curve> GetUniqueProxy(int id)
        {
            List<Curve> proxyLines = new List<Curve>();
            for (int i = 0; i < adjacentPills.Count; i++)
            {
                if (adjacentPills[i].Id != id)
                {
                    proxyLines.Add(proxLines[i]);
                }
            }
            return proxyLines;
        }

        public Dictionary<int, Curve> GetUniqueProxy_v2(int id)
        {
            Dictionary<int, Curve> proxData = new Dictionary<int, Curve>();

            //List<Curve> proxyLines = new List<Curve>();
            for (int i = 0; i < adjacentPills.Count; i++)
            {
                if (adjacentPills[i].Id != id)
                {
                    proxData.Add(adjacentPills[i].Id, proxLines[i]);
                    // proxyLines.Add(proxLines[i]);
                }
            }
            return proxData;
        }

        public void UpdateObstacles()
        {
            obstacles = BuildObstacles_v2();
        }
        private List<Curve> BuildObstacles_v2()
        {
            List<Curve> worldObstacles = new List<Curve>();
            // TODO: Adding All Pils Offsets as obstaces...
            List<Curve> limiters = new List<Curve> { Offset };
            if (worldObstacles != null) limiters.AddRange(worldObstacles);
            Dictionary<int, Curve> uniquePillsOffset = new Dictionary<int, Curve>();

            for (int i = 0; i < adjacentPills.Count; i++)
            {
                // limiters.Add(adjacentPills[i].pillOffset);
                Dictionary<int, Curve> proxDict = adjacentPills[i].GetUniqueProxy_v2(Id);
                uniquePillsOffset[adjacentPills[i].Id] = adjacentPills[i].Offset;
                //List<Curve> prox = adjacentPills[i].GetUniqueProxy(id);
                foreach (KeyValuePair<int, Curve> prox_crv in proxDict)
                {
                    uniquePillsOffset[prox_crv.Key] = blister.PillByID(prox_crv.Key).Offset;

                    if (Geometry.CurveCurveIntersection(prox_crv.Value, proxLines).Count == 0)
                    {
                        limiters.Add(prox_crv.Value);
                    }
                }
            }
            limiters.AddRange(uniquePillsOffset.Values.ToList());
            return Geometry.RemoveDuplicateCurves(limiters);
        }
        private List<Curve> BuildObstacles()
        {
            List<Curve> limiters = new List<Curve> { Offset };
            for (int i = 0; i < adjacentPills.Count; i++)
            {
                limiters.Add(adjacentPills[i].Offset);
                List<Curve> prox = adjacentPills[i].GetUniqueProxy(Id);
                foreach (Curve crv in prox)
                {
                    if (Geometry.CurveCurveIntersection(crv, proxLines).Count == 0)
                    {
                        limiters.Add(crv);
                    }
                }
            }
            return Geometry.RemoveDuplicateCurves(limiters);
        }
        public JObject GetDisplayJSON()
        {
            JObject data = new JObject();
            data.Add("pillIndex", this.Id);
            //Pill
            JArray pillDisplayData = new JArray();
            PolylineCurve imagePill = (PolylineCurve)Geometry.ReverseCalibration(Outline, Setups.ZeroPosition, Setups.PixelSpacing, Setups.CartesianPickModeAngle);

            foreach (Point3d pt in imagePill.ToPolyline())
            {
                pillDisplayData.Add(new JArray() { pt.X, pt.Y });
            }
            data.Add("processingPill", pillDisplayData);
            Point3d jaw2 = ((Point)Geometry.ReverseCalibration(new Point(Anchors[0].location), Setups.ZeroPosition, Setups.PixelSpacing, Setups.CartesianPickModeAngle)).Location;
            Point3d jaw1 = ((Point)Geometry.ReverseCalibration(new Point(Anchors[1].location), Setups.ZeroPosition, Setups.PixelSpacing, Setups.CartesianPickModeAngle)).Location;

            JArray anchorPossitions = new JArray() {
                new JArray() { jaw2.X, jaw2.Y },
                new JArray() { jaw1.X, jaw1.Y }
            };
            data.Add("anchors", anchorPossitions);
            return data;
        }

        public JObject GetJSON()
        {
            Point3d Jaw1_Local = Anchors[0].location;
            //Get JAW2
            Jaw1_Local = Anchors.Where(anchor => anchor.orientation == AnchorSite.JAW_2).First().location;

            JObject data = new JObject();
            data.Add("pillIndex", this.Id);
            // Add Anchor Data <- to be implement.
            data.Add("openJaw", new JArray(Anchors.Select(anchor => anchor.orientation.ToString().ToLower())));
            return data;
        }
    }
}
