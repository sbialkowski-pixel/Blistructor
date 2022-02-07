using System;
using System.Collections.Generic;
using System.Linq;
using log4net;
#if PIXEL
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

        public bool HasJaws { get; set; }

        //public bool OpenJaw1 { get; set; }
        //public bool OpenJaw2 { get; set; }

        public List<JawPoint> Jaws { get; set; }

        public CutBlister(Pill cuttedPill, CutData cutData) : base(cuttedPill, cutData.Polygon)
        {
            CutData = cutData;
            Jaws = new List<JawPoint>();
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

        private readonly bool toTight = false;
        private PolylineCurve outline;
        private List<Pill> pills;
        public List<PolylineCurve> irVoronoi;


        #region CONSTRUCTORS
        /// <summary>
        /// Internal constructor for non-Outline stuff
        /// </summary>
        /// <param name="outline">Blister Shape</param>
        private Blister(PolylineCurve outline) //: this(anchor)
        {
            pills = new List<Pill>();
            Geometry.UnifyCurve(outline);
            this.outline = outline;
        }

        /// <summary>
        /// Contructor mostly to create cut out blisters with one pill
        /// </summary>
        /// <param name="pillsOutline"></param>
        /// <param name="outline"></param>
        public Blister(Pill pillsOutline, PolylineCurve outline) : this(outline)
        {
            this.pills = new List<Pill>(1) { pillsOutline };
        }

        /// <summary>
        /// New Blister based on already existing cells and outline.
        /// </summary>
        /// <param name="pillsOutline">Existing cells</param>
        /// <param name="outline">Blister edge outline</param>
        public Blister(List<Pill> pillsOutline, PolylineCurve outline) : this(outline)
        {
            log.Debug("Creating new Blister");
            this.pills = new List<Pill>(pillsOutline.Count);
            // Loop by all given cells
            foreach (Pill pill in pillsOutline)
            {
                if (pill.State == PillState.Cut) continue;
                // If cell is not cutOut, check if belong to this Blister.
                if (Geometry.InclusionTest(pill, this))
                {
                    pill.Blister = this;
                    this.pills.Add(pill);
                }

            }
            log.Debug(String.Format("Instantiated {0} cells on Blister", pills.Count));
            if (LeftPillsCount == 1) return;
            log.Debug("Sorting Cells");
            // Order by CoordinateIndicator so it means Z-ordering.
            SortPillsByCoordinates(true);
            //  this.cells = cells.OrderBy(cell => cell.CoordinateIndicator).Reverse().ToList();
            // Rebuild cells connectivity.
            log.Debug("Creating ConncectivityData");
            CreateConnectivityData();
        }

        /// <summary>
        /// New initial Blister with Cells creation base on pills outlines.
        /// </summary>
        /// <param name="pillsOutline">Pills outline</param>
        /// <param name="outline">Blister edge outline</param>
        public Blister(List<PolylineCurve> pillsOutline, Polyline outline) : this(pillsOutline, outline.ToPolylineCurve())
        {
        }

        /// <summary>
        /// New initial Blister with Cells creation base on pills outlines.
        /// </summary>
        /// <param name="pillsOutline">Pills outline</param>
        /// <param name="outline">Blister edge outline</param>
        public Blister(List<PolylineCurve> pillsOutline, PolylineCurve outline) : this(outline)
        {
            log.Debug("Creating new Blister");
            // Cells Creation
            pills = new List<Pill>(pills.Count);
            for (int cellId = 0; cellId < pillsOutline.Count; cellId++)
            {
                if (pillsOutline[cellId].IsClosed)
                {
                    Pill pill = new Pill(cellId, pillsOutline[cellId], this);
                    //  cell.SetDistance(guideLine);
                    pills.Add(pill);
                }
            }
            log.Debug(String.Format("Instantiated {0} pills on Blister", pills.Count));
            // If only 1 cell, finish here.
            if (pills.Count <= 1) return;
            // NOTE: Cells Sorting move to Blister nd controled in BListructor...
            // Order by Corner distance. First Two set as possible Anchor.
            // log.Debug("Sorting Cells");
            // cells = cells.OrderBy(cell => cell.CornerDistance).ToList();
            // for (int i = 0; i < 2; i++)
            //{
            //     cells[i].PossibleAnchor = true;
            // }
            toTight = ArePillsOverlapping();
            log.Info(String.Format("Is to tight? : {0}", toTight));
            if (toTight) return;
            irVoronoi = Geometry.IrregularVoronoi(pills, Outline.ToPolyline(), 50, 0.05);
            CreateConnectivityData();
        }
        #endregion

        #region PROPERTIES

        public int LeftPillsCount
        {
            get
            {
                int count = 0;
                if (pills.Count > 0)
                {
                    foreach (Pill pill in pills)
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
                if (pills.Count == 0) return indices;
                foreach (Pill cell in pills)
                {
                    if (cell.State == PillState.Queue) indices.Add(cell.Id);
                }
                return indices;
            }
        }

        public List<Curve> GetPills(bool offset)
        {
            List<Curve> pillsOutline = new List<Curve>();
            foreach (Pill cell in pills)
            {
                if (offset) pillsOutline.Add(cell.Offset);
                else pillsOutline.Add(cell.Outline);
            }
            return pillsOutline;
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

        public List<Pill> Pills { get { return pills; } }

        /// <summary>
        /// Get Pill by its unique ID.
        /// </summary>
        /// <param name="id">Pill ID</param>
        /// <returns>Pill</returns>
        public Pill PillByID(int id)
        {
            List<Pill> a = pills.Where(cell => cell.Id == id).ToList();
            if (a.Count == 1) return a[0];
            return null;
        }

        public PolylineCurve Outline { get { return outline; } set { outline = value; } }

        //public PolylineCurve BBox { get { return bBox; } }

        public bool ToTight { get { return toTight; } }

        #endregion

        #region SORTS
        /// <summary>
        /// Z-Ordering, Descending
        /// </summary>
        public void SortPillsByCoordinates(bool reverse)
        {
            pills = pills.OrderBy(pill => pill.CoordinateIndicator).ToList();
            if (reverse) pills.Reverse();
        }

        public void SortPillsByPointDirection(Point3d pt, bool reverse)
        {
            pills = pills.OrderBy(pill => pill.GetDirectionIndicator(pt)).ToList();
            if (reverse) pills.Reverse();
        }

        public void SortPillsByPointDistance(Point3d pt, bool reverse)
        {
            pills = pills.OrderBy(pill => pill.GetDistance(pt)).ToList();
            if (reverse) pills.Reverse();
        }
        public void SortPillsByPointsDistance(List<Point3d> pts, bool reverse)
        {
            pills = pills.OrderBy(pill => pill.GetClosestDistance(pts)).ToList();
            if (reverse) pills.Reverse();
        }

        public void SortPillsByCurveDistance(Curve crv, bool reverse)
        {
            pills = pills.OrderBy(pill => pill.GetDistance(crv)).ToList();
            if (reverse) pills.Reverse();
        }

        public void SortPillsByCurvesDistance(List<Curve> crvs, bool reverse)
        {
            pills = pills.OrderBy(pill => pill.GetClosestDistance(crvs)).ToList();
            if (reverse) pills.Reverse();
        }

        #endregion


        /// <summary>
        /// Check if PillsOutlines (with knife wird appiled) are not intersecting. 
        /// </summary>
        /// <returns>True if any pill intersect with other.</returns>
        protected bool ArePillsOverlapping()
        {
            // output = false;
            for (int i = 0; i < pills.Count; i++)
            {
                for (int j = i + 1; j < pills.Count; j++)
                {
                    List<IntersectionEvent> inter = Intersection.CurveCurve(pills[i].Offset, pills[j].Offset, Setups.IntersectionTolerance);
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
            if (pills.Count > 1)
            {
                foreach (Pill pill in pills)
                {

                    if (pill.adjacentPills.Count == 1)
                    {
                        if (pill.adjacentPills[0].Id == pillToCut.Id)
                        {
                            log.Warn("Adjacent Cells Checker - Found alone Outline in multi-pills Blister. Skip this cutting.");
                            return false;
                        }
                    }
                }

                // Check for Blister integrity
                List<HashSet<int>> setsList = new List<HashSet<int>>();
                // Add initileze set
                HashSet<int> initSet = new HashSet<int>();
                initSet.Add(pills[0].Id);
                initSet.UnionWith(pills[0].GetAdjacentPillsIds());
                setsList.Add(initSet);
                for (int i = 1; i < pills.Count; i++)
                {
                    List<int> pillsIds = pills[i].GetAdjacentPillsIds();
                    // Remove foundCell Id
                    pillsIds.Remove(pillToCut.Id);
                    pillsIds.Add(pills[i].Id);
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
        /// Iterate throught pills and compute interconnectring data between them. 
        /// </summary>
        public void CreateConnectivityData()
        {
            log.Debug("Creating Conectivity Data.");
            foreach (Pill currentPill in pills)
            {
                // If current pill is cut out... go to next one.
                if (currentPill.State == PillState.Cut) continue;
                // log.Debug(String.Format("Checking pill: {0}", currentCell.id));
                List<Point3d> currentMidPoints = new List<Point3d>();
                List<Curve> currentConnectionLines = new List<Curve>();
                List<Pill> currenAdjacentPills = new List<Pill>();
                foreach (Pill proxPill in pills)
                {
                    // If proxCell is cut out or cutCell is same as proxCell, next cell...
                    if (proxPill.State == PillState.Cut || proxPill.Id == currentPill.Id) continue;
                    // log.Debug(String.Format("Checking pill: {0}", currentCell.id));
                    Line line = new Line(currentPill.Center, proxPill.Center);
                    Point3d midPoint = line.PointAt(0.5);
                    double t;
                    if (currentPill.Voronoi.ClosestPoint(midPoint, out t, 2.000))
                    {
                        // log.Debug(String.Format("Checking pill: {0}", currentCell.id));
                        currenAdjacentPills.Add(proxPill);
                        currentConnectionLines.Add(new LineCurve(line));
                        currentMidPoints.Add(midPoint);
                    }
                }
                log.Debug(String.Format("Pill ID: {0} - Adjacent:{1}, Conection {2} Proxy {3}", currentPill.Id, currenAdjacentPills.Count, currentConnectionLines.Count, currentMidPoints.Count));
                currentPill.AddConnectionData(currenAdjacentPills, currentConnectionLines, currentMidPoints);
            }
        }
    }
}
