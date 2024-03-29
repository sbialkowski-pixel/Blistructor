﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;

#if PIXEL
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

namespace Blistructor
{
    public class Pill : IEquatable<Pill>
    {
        private static readonly ILog log = LogManager.GetLogger("Cutter.Pill");

        #region Properties
        internal Blister ParentBlister { get; set; }
        public int Id { get; }

        public PillState State { get; set; }

        /// <summary>
        /// Pill Outline deviation form Circle. 1.0 means pill shape is circular; 0.0 means is not circular at all.
        /// </summary>
        public double CircleDeviation { get; private set; }
        /// <summary>
        /// Pill shape
        /// </summary>
        public PolylineCurve Outline { get; private set; }
        /// <summary>
        /// Pill shape offset by Sapfe distance (Setups.BladeWidth/2)
        /// </summary>
        public PolylineCurve Offset { get; private set; }
        /// <summary>
        /// Pill center (vertex avrage)
        /// </summary>
        public Point3d Center { get; private set; }
        /// <summary>
        /// Circle calculated based on Outline BBox. It is not same circle, as used in CircleDeviation property!
        /// </summary>
        public PolylineCurve OrientationCircle { get; private set; }

        // Connection and Adjacent Stuff
        public PolylineCurve IrVoronoi { get; set; }
        public PolylineCurve Voronoi { get; set; }
        #endregion
        #region Properties - Connection
        //!!ConnectionLines, ProxLines, AdjacentPills, SamplePoints <- all same sizes, and order!!
        public List<Pill> AdjacentPills { get; internal set; }
        public List<Curve> ConnectionLines { get; internal set; }
        public List<Curve> ProxLines { get; internal set; }
        public List<Point3d> SamplePoints { get; internal set; }
        public List<Curve> Obstacles { get; internal set; }

        #endregion

        public Pill(Pill pill)
        {
            Id = pill.Id;
            State = pill.State;
            Outline = (PolylineCurve)pill.Outline.Duplicate();
            Offset = (PolylineCurve)pill.Offset.Duplicate();
            Voronoi = (PolylineCurve)pill.Voronoi.Duplicate();
            IrVoronoi = (PolylineCurve)pill.IrVoronoi.Duplicate();
            OrientationCircle = (PolylineCurve)pill.OrientationCircle.Duplicate();
            AdjacentPills = new List<Pill>();
            Center = pill.Center;
        }

        public Pill(int id, PolylineCurve pill, Blister blister)
        {
            Id = id;
            State = PillState.Queue;
            this.ParentBlister = blister;
            // Prepare all needed Pill properties
            Outline = pill;
            // Make Pill curve oriented in proper direction.
            Geometry.UnifyCurve(Outline);

            Center = Outline.ToPolyline().CenterPoint();
            // Create Outline offset
            Offset = GetCustomOffset(Setups.MinimumInterPillDistance / 2);
            CircleDeviation = Geometry.DeviationFromCircle(Outline.ToPolyline());
        }

        #region PROPERTIES - OTHER
        public double CoordinateIndicator
        {
            get
            {
                return Center.X + (Center.Y * 10);
            }
        }

        public double NeighbourCount
        {
            get { return AdjacentPills.Count; }
        }
        public List<int> GetAdjacentPillsIds
        {
            get { return AdjacentPills.Select(pill => pill.Id).ToList(); }
        }
        #endregion

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

        public void AddConnectionData(List<Pill> pills, List<Curve> lines)
        {
            AdjacentPills = new List<Pill>();
            SamplePoints = new List<Point3d>();
            ConnectionLines = new List<Curve>();

            List<Point3d> midPoints = lines.Select(line => line.PointAt(0.5)).ToList();
            EstimateOrientationCircle();
            int[] ind = Geometry.SortPtsAlongCurve(midPoints, OrientationCircle);

            foreach (int id in ind)
            {
                AdjacentPills.Add(pills[id]);
                ConnectionLines.Add(lines[id]);
            }
            ProxLines = new List<Curve>();
            foreach (Pill pill in AdjacentPills)
            {
                Point3d ptA, ptB;
                if (Offset.ClosestPoints(pill.Offset, out ptA, out ptB))
                {
                    LineCurve proxLine = new LineCurve(ptA, ptB);
                    ProxLines.Add(proxLine);
                    Point3d samplePoint = proxLine.Line.PointAt(0.5);   //PointAtNormalizedLength(0.5);
                    SamplePoints.Add(samplePoint);
                }
            }
        }

        public void RemoveConnectionData()
        {
            for (int i = 0; i < AdjacentPills.Count; i++)
            {
                AdjacentPills[i].RemoveConnectionData(Id);
            }
        }

        /// <summary>
        /// Call from adjacent Outline
        /// </summary>
        /// <param name="pillId">ID of Pill which is executing this method</param>
        protected void RemoveConnectionData(int pillId)
        {
            for (int i = 0; i < AdjacentPills.Count; i++)
            {
                if (AdjacentPills[i].Id == pillId)
                {
                    AdjacentPills.RemoveAt(i);
                    ConnectionLines.RemoveAt(i);
                    ProxLines.RemoveAt(i);
                    SamplePoints.RemoveAt(i);
                    i--;
                }
            }
            SortData();
        }

        private void EstimateOrientationCircle()
        {
            double circle_radius = Outline.GetBoundingBox(false).Diagonal.Length / 2;
            NurbsCurve orientationCircle = (new Circle(Center, circle_radius)).ToNurbsCurve();
            Geometry.EditSeamBasedOnCurve(orientationCircle, ParentBlister.Outline);
            OrientationCircle = orientationCircle.TryGetPolyline().ToPolylineCurve();

        }

        public void SortData()
        {
            EstimateOrientationCircle();
            int[] sortingIndexes = Geometry.SortPtsAlongCurve(SamplePoints, OrientationCircle);

            SamplePoints = sortingIndexes.Select(index => SamplePoints[index]).ToList();
            ConnectionLines = sortingIndexes.Select(index => ConnectionLines[index]).ToList();
            ProxLines = sortingIndexes.Select(index => ProxLines[index]).ToList();
            AdjacentPills = sortingIndexes.Select(index => AdjacentPills[index]).ToList();
        }

        // Get ProxyLines without lines pointed as Id
        public List<Curve> GetUniqueProxy(int id)
        {
            List<Curve> proxyLines = new List<Curve>();
            for (int i = 0; i < AdjacentPills.Count; i++)
            {
                if (AdjacentPills[i].Id != id)
                {
                    proxyLines.Add(ProxLines[i]);
                }
            }
            return proxyLines;
        }

        public Dictionary<int, Curve> GetUniqueProxy_v2(int id)
        {
            Dictionary<int, Curve> proxData = new Dictionary<int, Curve>();

            //List<Curve> proxyLines = new List<Curve>();
            for (int i = 0; i < AdjacentPills.Count; i++)
            {
                if (AdjacentPills[i].Id != id)
                {
                    proxData.Add(AdjacentPills[i].Id, ProxLines[i]);
                    // proxyLines.Add(ProxLines[i]);
                }
            }
            return proxData;
        }

        public void UpdateObstacles()
        {
            Obstacles = BuildObstacles_v2();
            // Add extra offset 
            //obstacles = obstacles.Select(Outline => Outline.Offset(Plane.WorldXY, Setups.BladeTol)).ToList();
        }

        private List<Curve> BuildObstacles_v2()
        {
            List<Curve> limiters = new List<Curve> { Offset };
            Dictionary<int, Curve> uniquePillsOffset = new Dictionary<int, Curve>();

            for (int i = 0; i < AdjacentPills.Count; i++)
            {
                // limiters.Add(AdjacentPills[i].pillOffset);
                Dictionary<int, Curve> proxDict = AdjacentPills[i].GetUniqueProxy_v2(Id);
                uniquePillsOffset[AdjacentPills[i].Id] = AdjacentPills[i].Offset;
                //List<Curve> prox = AdjacentPills[i].GetUniqueProxy(id);
                foreach (KeyValuePair<int, Curve> prox_crv in proxDict)
                {
                    uniquePillsOffset[prox_crv.Key] = ParentBlister.PillByID(prox_crv.Key).Offset;

                    if (Geometry.CurveCurveIntersection(prox_crv.Value, ProxLines).Count == 0)
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

        public void GenerateDebugGeometryFile(int runId, int chunkId)
        {
            // TODO: TU SIE WYWALA JAK JESt OTSta
            string runIdString = $"{runId:00}";
            string chunkIdString = $"{chunkId:00}";
            Directory.CreateDirectory(Path.Combine(Setups.DebugDir, runIdString));
            string fileName = $"{chunkIdString}_pill_{Id:00}.3dm";
            string filePath = Path.Combine(Setups.DebugDir, runIdString, fileName);
            File3dm file = new File3dm();

            Layer pillLayer = new Layer();
            pillLayer.Name = $"{chunkIdString}_pill_{this.Id}";
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
            if (ConnectionLines != null) ConnectionLines.ForEach(crv => file.Objects.AddCurve(crv, connAttributes));
            if (ConnectionLines != null) ProxLines.ForEach(crv => file.Objects.AddCurve(crv, connAttributes));
            if (SamplePoints != null) file.Objects.AddPoints(SamplePoints, connAttributes);

            file.Write(filePath, 6);
        }
    }
}
