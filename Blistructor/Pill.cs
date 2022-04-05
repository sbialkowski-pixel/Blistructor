using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;

#if PIXEL
using Pixel.Rhino;
using Pixel.Rhino.FileIO;
using Pixel.Rhino.DocObjects;
using Pixel.Rhino.Geometry;
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
    public class Pill : IEquatable<Pill>
    {
        private static readonly ILog log = LogManager.GetLogger("Cutter.Pill");

        public int Id { get;  }

        // Parent Blister
        internal Blister blister ;

        //public bool PossibleAnchor { get; set; }

        // Pill Stuff
        public PolylineCurve Outline { get; private set; }
        public PolylineCurve Offset { get; private set; }

        public Point3d Center { get; private set; }

        // Connection and Adjacent Stuff
        public PolylineCurve Voronoi { get; set; }
        //!!connectionLines, proxLines, adjacentPills, samplePoints <- all same sizes, and order!!
        public List<Curve> connectionLines;
        public List<Curve> proxLines;
        public List<Pill> adjacentPills;
        public List<Point3d> samplePoints;

        public List<Curve> obstacles;

        public Pill(Pill pill)
        {
            Id = pill.Id;
            Outline = (PolylineCurve) pill.Outline.Duplicate();
            Offset = (PolylineCurve) pill.Offset.Duplicate();
            Voronoi = (PolylineCurve) pill.Voronoi.Duplicate();
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

            Center = Outline.ToPolyline().CenterPoint();

            // Create Outline offset
            Offset = GetCustomOffset(Setups.BladeWidth / 2);
        }

        #region PROPERTIES
        public double CoordinateIndicator
        {
            get
            {
                return Center.X + (Center.Y * 10);
            }
        }

        public double NeighbourCount
        {
            get { return adjacentPills.Count; }
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

        public bool Equals(Pill other)
        {
            if (other == null) return false;
            return (this.Id.Equals(other.Id));
        }
        public PolylineCurve GetCustomOffset(double offset)
        {
            Curve ofCur = Outline.Offset(Plane.WorldXY, offset);
            if (ofCur == null)
            {
                log.Error("Incorrect Outline offseting");
                throw new InvalidOperationException("Incorrect Outline offseting");
            }
            else
            {
                return (PolylineCurve)ofCur;
            }
        }

        #region DISTANCES
        public double GetDirectionIndicator(Point3d pt)
        {
            Vector3d vec = pt - this.Center;
            return Math.Abs(vec.X) + Math.Abs(vec.Y) * 10;
        }
        public double GetDistance(Point3d pt)
        {
            return pt.DistanceTo(this.Center);
        }
        public double GetClosestDistance(List<Point3d> pts)
        {
            return pts.Select(p => p.DistanceTo(this.Center)).OrderBy(d => d).First();
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
            // Add extra offset 
           //obstacles = obstacles.Select(Outline => Outline.Offset(Plane.WorldXY, Setups.BladeTol)).ToList();
        }
        private List<Curve> BuildObstacles_v2()
        {
            List<Curve> limiters = new List<Curve> { Offset };
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
            return data;
        }

        public JObject GetJSON()
        {
            // Point3d Jaw1_Local = Anchors[0].Location;
            //Get JAW2
            //Jaw1_Local = Anchors.Where(anchor => anchor.Orientation == JawSite.JAW_2).First().Location;

            JObject data = new JObject
            {
                { "pillIndex", this.Id }
            };
            // Add Anchor Data <- to be implement.
            // data.Add("openJaw", new JArray(Anchors.Select(anchor => anchor.Orientation.ToString().ToLower())));
            return data;
        }

        public void GenerateDebugGeometryFile(int runId)
        {
            string runIdString = $"{runId:00}";
            Directory.CreateDirectory(Path.Combine(Setups.DebugDir, runIdString));
            string fileName = $"pill_{Id:00}.3dm";
            string filePath = Path.Combine(Setups.DebugDir, runIdString, fileName);
            File3dm file = new File3dm();
            
            Layer pillLayer = new Layer();
            pillLayer.Name = $"pill_{this.Id}";
            pillLayer.Index = 0;
            file.AllLayers.Add(pillLayer);
            ObjectAttributes pillAttributes = new ObjectAttributes();
            pillAttributes.LayerIndex = pillLayer.Index;
            
            file.Objects.AddCurve(this.Outline, pillAttributes);
            file.Objects.AddCurve(this.Offset, pillAttributes);
            file.Objects.AddCurve(this.Voronoi, pillAttributes);
            file.Objects.AddPoint(this.Center, pillAttributes);

            Layer connLayer = new Layer
            {
                Name = $"connection_{this.Id}",
                Index = 1
            };
            file.AllLayers.Add(connLayer);
            ObjectAttributes connAttributes = new ObjectAttributes();
            connAttributes.LayerIndex = connLayer.Index;
            connectionLines.ForEach(crv => file.Objects.AddCurve(crv, connAttributes));
            proxLines.ForEach(crv => file.Objects.AddCurve(crv, connAttributes));
            file.Objects.AddPoints(samplePoints, connAttributes);

            file.Write(filePath, 6);
        }
    }
}
