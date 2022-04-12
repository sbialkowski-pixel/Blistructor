using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using log4net;
#if PIXEL
using Pixel.Rhino.FileIO;
using Pixel.Rhino.DocObjects;
using Pixel.Rhino.Geometry;
using Pixel.Rhino.Geometry.Intersect;
#else
using Rhino.Geometry;
using Rhino.Geometry.Intersect;
#endif
using Newtonsoft.Json.Linq;

namespace Blistructor
{
    public class CutBlister : Blister
    {
        private static readonly ILog log = LogManager.GetLogger("Cutter.CuttedPill");
        /// <summary>
        /// Confirmed CutData (BestCutData from CutPpoposal)
        /// </summary>
        public CutData CutData { get; set; }

        // public bool HasJaws { get; set; }

        public bool IsLast { get; set; }

        //public bool OpenJaw1 { get; set; }
        //public bool OpenJaw2 { get; set; }

        public List<JawPoint> Jaws { get; set; }


        public CutBlister(Blister blister) : base(blister.Pills[0], blister.Outline)
        {
            Jaws = new List<JawPoint>();
            CutData = null;
            IsLast = false;
        }
        public CutBlister(Pill cuttedPill, CutData cutData) : base(cuttedPill, cutData.Polygon)
        {
            CutData = cutData;
            Jaws = new List<JawPoint>();
            IsLast = false;
        }
        public Pill Pill { get => Pills[0]; }

        public JObject GetDisplayJSON(Grasper grasper)
        {
            JObject data = Pill.GetDisplayJSON();

            Point3d jaw2 = ((Point)Geometry.ReverseCalibration(new Point(grasper.Jaws[0].Location), Setups.ZeroPosition, Setups.PixelSpacing, Setups.CartesianPickModeAngle)).Location;
            Point3d jaw1 = ((Point)Geometry.ReverseCalibration(new Point(grasper.Jaws[1].Location), Setups.ZeroPosition, Setups.PixelSpacing, Setups.CartesianPickModeAngle)).Location;

            JArray anchorPossitions = new JArray() {
                new JArray() { jaw2.X, jaw2.Y },
                new JArray() { jaw1.X, jaw1.Y }
            };
            data.Add("anchors", anchorPossitions);

            // Add displayCut data
            if (CutData != null) data.Add("displayCut", CutData.GetDisplayJSON(grasper.Jaws[0].Location));
            else data.Add("displayCut", new JArray());
            return data;
        }

        public JObject GetJSON(Grasper grasper)
        {
            JObject data = Pill.GetJSON();

            //Get JAW2
            data.Add("openJaw", new JArray(Jaws.Select(jaw => jaw.Orientation.ToString().ToLower())));

            // Add Cutting Instruction
            //Point3d Jaw1_Local = grasper.Jaws[0].Location;
            Point3d Jaw2_Local = grasper.Jaws.Where(jaw => jaw.Orientation == JawSite.JAW_2).First().Location;

            if (CutData != null) data.Add("cutInstruction", CutData.GetJSON(Jaw2_Local));
            else data.Add("cutInstruction", new JArray());
            return data;
        }
    }

    public class Blister
    {
        private static readonly ILog log = LogManager.GetLogger("Cutter.Blister");
        public bool ToTight { get; private set; } = false;
        public PolylineCurve Outline { get; set; }
        public List<Pill> Pills { get; private set; }
        public Graph Graph { get; private set; }

      //  public List<PolylineCurve> Voronoi { get; private set; }
        protected string UUID;


        #region CONSTRUCTORS
        /// <summary>
        /// Internal constructor for non-Outline stuff
        /// </summary>
        /// <param name="outline">Blister Shape</param>
        private Blister(PolylineCurve outline)
        {
            Pills = new List<Pill>();
            Geometry.UnifyCurve(outline);
            this.Outline = outline;
            UUID = Guid.NewGuid().ToString();
        }

        /// <summary>
        /// Contructor mostly to create cut out blisters with one pill
        /// </summary>
        /// <param name="pillsOutline"></param>
        /// <param name="outline"></param>
        public Blister(Pill pillsOutline, PolylineCurve outline) : this(outline)
        {
            this.Pills = new List<Pill>(1) { pillsOutline };
        }

        /// <summary>
        /// New Blister based on already existing cells and Outline.
        /// </summary>
        /// <param name="pillsOutline">Existing cells</param>
        /// <param name="outline">Blister edge Outline</param>
        public Blister(List<Pill> pillsOutline, PolylineCurve outline) : this(outline)
        {
            log.Debug("Creating new Blister");
            this.Pills = new List<Pill>(pillsOutline.Count);
            // Loop by all given cells
            foreach (Pill pill in pillsOutline)
            {
                if (pill.State == PillState.Cut) continue;
                // If cell is not cutOut, check if belong to this Blister.
                if (Geometry.InclusionTest(pill, this))
                {
                    pill.blister = this;
                    this.Pills.Add(pill);
                }

            }
            log.Debug(String.Format("Instantiated {0} cells on Blister", Pills.Count));
            if (LeftPillsCount == 1) return;
            log.Debug("Creating ConncectivityData");
            Graph = new VoronoiGraph(this);
            Pills.ForEach(pill => pill.Voronoi = (PolylineCurve)Graph.Voronoi[(object)pill.Id]);
            CreateConnectivityData();
        }

        /// <summary>
        /// New initial Blister with Pills creation base on Pills outlines.
        /// </summary>
        /// <param name="pillsOutline">Pills Outline</param>
        /// <param name="outline">Blister edge Outline</param>
        public Blister(List<PolylineCurve> pillsOutline, PolylineCurve outline) : this(outline)
        {
            log.Debug("Creating new Blister");
            // Cells Creation
            Pills = new List<Pill>(Pills.Count);
            for (int pillId = 0; pillId < pillsOutline.Count; pillId++)
            {
                if (pillsOutline[pillId].IsClosed)
                {
                    Pill pill = new Pill(pillId, pillsOutline[pillId], this);
                    //  cell.SetDistance(guideLine);
                    Pills.Add(pill);
                }
            }
            log.Debug(String.Format("Instantiated {0} Pills on Blister", Pills.Count));
            // If only 1 cell, finish here.
            if (Pills.Count <= 1) return;

            ToTight = ArePillsOverlapping();
            log.Info(String.Format("Is to tight? : {0}", ToTight));
            if (ToTight) return;
            Graph = new VoronoiGraph(this);

            Pills.ForEach(pill => pill.Voronoi = (PolylineCurve)Graph.Voronoi[(object)pill.Id]);
            //Pills.ForEach(pill => pill.IrVoronoi = (PolylineCurve)Graph.IrVoronoi[(object)pill.Id]);
            CreateConnectivityData();
        }
        #endregion

        #region PROPERTIES

        public int LeftPillsCount
        {
            get
            {
                int count = 0;
                if (Pills.Count > 0)
                {
                    foreach (Pill pill in Pills)
                    {
                        if (pill.State == PillState.Queue) count++;
                    }
                }
                return count;
            }
        }

        public List<int> LeftPillsIndices
        {
            get
            {
                List<int> indices = new List<int>();
                if (Pills.Count == 0) return indices;
                foreach (Pill cell in Pills)
                {
                    if (cell.State == PillState.Queue) indices.Add(cell.Id);
                }
                return indices;
            }
        }

        public bool IsDone
        {
            get
            {
                if (ToTight) return true;
                else if (LeftPillsCount < 1) return true;
                else return false;
            }
        }

        public List<Curve> GetPillsOutline(bool offset)
        {
            if (offset) return Pills.Select(pill => (Curve)pill.Offset).ToList();
            else return Pills.Select(pill => (Curve)pill.Outline).ToList();
        }

        public List<Curve> GetPillsOutline(double offset)
        {
            return Pills.Select(pill => (Curve)pill.GetCustomOffset(offset)).ToList();
        }
        /// <summary>
        /// Get Pill by its unique ID.
        /// </summary>
        /// <param name="id">Pill ID</param>
        /// <returns>Pill</returns>
        public Pill PillByID(int id)
        {
            List<Pill> a = Pills.Where(cell => cell.Id == id).ToList();
            if (a.Count == 1) return a[0];
            return null;
        }

        #endregion

        #region SORTS

        public void SortPillsByComplex(Grasper grasper)
        {
            double sort(Pill pill)
            {
                double jawDistance = pill.GetClosestDistance(grasper.Jaws.Select(jaw => jaw.Location).ToList());
                return  pill.NeighbourCount*( pill.CoordinateIndicator + jawDistance);
            }
            Pills.OrderBy(pill => sort(pill)).ToList();
        }

        /// <summary>
        /// Z-Ordering.
        ///<param name="descending">If true, Pills will be sorted descending.</param>
        /// </summary>
        public void SortPillsByCoordinates(bool descending)
        {
            Pills = Pills.OrderBy(pill => pill.CoordinateIndicator).ToList();
            if (descending) Pills.Reverse();
        }

        /// <summary>
        /// Sort Pills based on external Point (pt). By default, sorting is done ascending prioritizing Y direction. 
        /// </summary>
        /// <param name="pt">Point of reference.</param>
        /// <param name="descending">If true, Pills will be sorted descending.</param>
        public void SortPillsByPointDirection(Point3d pt, bool descending)
        {
            Pills = Pills.OrderBy(pill => pill.GetDirectionIndicator(pt)).ToList();
            if (descending) Pills.Reverse();
        }

        public void SortPillsByPointDistance(Point3d pt, bool descending)
        {
            Pills = Pills.OrderBy(pill => pill.GetDistance(pt)).ToList();
            if (descending) Pills.Reverse();
        }
        public void SortPillsByPointsDistance(List<Point3d> pts, bool descending)
        {
            Pills = Pills.OrderBy(pill => pill.GetClosestDistance(pts)).ToList();
            if (descending) Pills.Reverse();
        }

        public void SortPillsByCurveDistance(Curve crv, bool descending)
        {
            Pills = Pills.OrderBy(pill => pill.GetDistance(crv)).ToList();
            if (descending) Pills.Reverse();
        }

        public void SortPillsByCurvesDistance(List<Curve> crvs, bool descending)
        {
            Pills = Pills.OrderBy(pill => pill.GetClosestDistance(crvs)).ToList();
            if (descending) Pills.Reverse();
        }

        #endregion


        /// <summary>
        /// Check if PillsOutlines (with knife wird appiled) are not intersecting. 
        /// </summary>
        /// <returns>True if any pill intersect with other.</returns>
        protected bool ArePillsOverlapping()
        {
            // output = false;
            for (int i = 0; i < Pills.Count; i++)
            {
                for (int j = i + 1; j < Pills.Count; j++)
                {
                    List<IntersectionEvent> inter = Intersection.CurveCurve(Pills[i].Offset, Pills[j].Offset, Setups.IntersectionTolerance);
                    if (inter.Count > 0)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Check Connectivity (AdjacingPills) against some pill planned to be cutout.
        /// </summary>
        /// <param name="pillToCut"></param>
        /// <returns>True if all ok, false if there is inconsistency.</returns>
        internal bool CheckConnectivityIntegrity(Pill pillToCut)
        {
            if (Pills.Count > 1)
            {
                foreach (Pill pill in Pills)
                {

                    if (pill.AdjacentPills.Count == 1)
                    {
                        if (pill.AdjacentPills[0].Id == pillToCut.Id)
                        {
                            log.Warn("Adjacent Cells Checker - Found alone Outline in multi-Pills Blister. Skip this cutting.");
                            return false;
                        }
                    }
                }

                // Check for Blister integrity
                List<HashSet<int>> setsList = new List<HashSet<int>>();
                // Add initileze set
                HashSet<int> initSet = new HashSet<int>();
                initSet.Add(Pills[0].Id);
                initSet.UnionWith(Pills[0].GetAdjacentPillsIds());
                setsList.Add(initSet);
                for (int i = 1; i < Pills.Count; i++)
                {
                    List<int> pillsIds = Pills[i].GetAdjacentPillsIds();
                    // Remove foundCell Id
                    pillsIds.Remove(pillToCut.Id);
                    pillsIds.Add(Pills[i].Id);
                    // check if smallSet fit to any bigger sets in list.
                    bool added = false;
                    for (int j = 0; j < setsList.Count; j++)
                    {
                        // If yes, add it and break forloop.
                        if (setsList[j].Overlaps(pillsIds))
                        {
                            setsList[j].UnionWith(pillsIds);
                            added = true;
                            break;
                        }
                    }
                    // if not added, create ne big set based on small one.
                    if (!added)
                    {
                        HashSet<int> newSet = new HashSet<int>();
                        newSet.UnionWith(pillsIds);
                        setsList.Add(newSet);
                    }
                }

                // If only one setList, its ok. If more, try to merge, if not possible, Blister is not consistent...
                if (setsList.Count > 1)
                {
                    // Create finalSet
                    HashSet<int> finalSet = new HashSet<int>();
                    finalSet.UnionWith(setsList[0]);
                    // Remove form setList all sets which are alredy added to finalSet, in this case first one
                    setsList.RemoveAt(0);
                    // Try 3 times. Why 3, i dont know. Just like that...
                    int initsetsListCount = setsList.Count;
                    for (int m = 0; m < 3; m++)
                    {
                        for (int k = 0; k < setsList.Count; k++)
                        {
                            if (finalSet.Overlaps(setsList[k]))
                            {
                                // if overlaped, add to finalSet
                                finalSet.UnionWith(setsList[k]);
                                // Remove form setList all sets which are alredy added to finalSet, in this case first one
                                setsList.RemoveAt(k);
                                k--;
                            }
                        }
                        // If all sets are merged, break;
                        if (setsList.Count == 0) break;
                        // If after second runf setList is same, means no change, brake;
                        if (m > 2 && setsList.Count == initsetsListCount) break;
                    }
                    if (setsList.Count > 0)
                    {
                        log.Warn("Adjacent Pills Checker - Pill AdjacentConnection not cconsistent. Skip this cutting.");
                        return false;
                    }
                    else return true;
                }
                else return true;
            }
            else
            {
                return true;
            }

        }

        /// <summary>
        /// Iterate throught Pills and compute interconnectring data between them. 
        /// </summary>
        public void CreateConnectivityData()
        {
            log.Debug("Creating Conectivity Data.");
            foreach (Pill currentPill in Pills)
            {
                // If current pill is cut out... go to next one.
                if (currentPill.State == PillState.Cut) continue;
                // log.Debug(String.Format("Checking pill: {0}", currentCell.id));
                List<Point3d> currentMidPoints = new List<Point3d>();
                List<Curve> currentConnectionLines = new List<Curve>();
                List<Pill> currenAdjacentPills = Graph.GetAdjacentPills(currentPill.Id);
                foreach (Pill proxPill in currenAdjacentPills)
                {
                    if (proxPill.State == PillState.Cut || proxPill.Id == currentPill.Id) continue;
                    Line line = new Line(currentPill.Center, proxPill.Center);
                    currentConnectionLines.Add(new LineCurve(line));
                }
                log.Debug(String.Format("Pill ID: {0} - Adjacent:{1}, Conection {2} Proxy {3}", currentPill.Id, currenAdjacentPills.Count, currentConnectionLines.Count, currentMidPoints.Count));
                currentPill.AddConnectionData(currenAdjacentPills, currentConnectionLines);
            }
        }

        public void GenerateDebugGeometryFile(int runId)
        {
            string runIdString = $"{runId:00}";
            Directory.CreateDirectory(Path.Combine(Setups.DebugDir, runIdString));
            string fileName = $"blister.3dm";
            string filePath = Path.Combine(Setups.DebugDir, runIdString, fileName);
            File3dm file = new File3dm();

            Layer pillLayer = new Layer();
            pillLayer.Name = $"blister";
            pillLayer.Index = 0;
            file.AllLayers.Add(pillLayer);
            ObjectAttributes pillAttributes = new ObjectAttributes();
            pillAttributes.LayerIndex = pillLayer.Index;

            file.Objects.AddCurve(this.Outline, pillAttributes);

            file.Write(filePath, 6);
        }
    }
}
