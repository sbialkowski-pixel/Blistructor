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
    public class BlisterCutter 
    {
        public readonly SubBlister CutOut;
        public readonly SubBlister Current;
        public readonly List<SubBlister> ExtraBlisters;
        public readonly CutState State;

        private readonly SubBlister _blisterTotCut;


        public BlisterCutter(SubBlister blisterTotCut)
        {
            _blisterTotCut = blisterTotCut;
        }

        private void CutNext()
        {

        }
    }

    public class SubBlister
    {

        private static readonly ILog log = LogManager.GetLogger("Cutter.SubBlister");

        internal Blister blister;
        private readonly bool toTight = false;
        private PolylineCurve outline;
        private List<Pill> pills;
        public List<PolylineCurve> irVoronoi;


        private SubBlister(Blister _blister)
        {
            blister = _blister;
        }

        /// <summary>
        /// Internal constructor for non-Outline stuff
        /// </summary>
        /// <param name="outline">SubBlister Shape</param>
        private SubBlister(PolylineCurve outline, Blister blister): this(blister)
        {
            pills = new List<Pill>();
            Geometry.UnifyCurve(outline);
            this.outline = outline;
        }

        /// <summary>
        /// Contructor mostly to create cut out blisters with one cell
        /// </summary>
        /// <param name="pillsOutline"></param>
        /// <param name="outline"></param>
        public SubBlister(Pill pillsOutline, PolylineCurve outline, Blister blister) : this(outline, blister)
        {
            this.pills = new List<Pill>(1) { pillsOutline };
        }

        /// <summary>
        /// New SubBlister based on already existing cells and outline.
        /// </summary>
        /// <param name="pillsOutline">Existing cells</param>
        /// <param name="outline">SubBlister edge outline</param>
        public SubBlister(List<Pill> pillsOutline, PolylineCurve outline, Blister blister) : this(outline, blister)
        {
            log.Debug("Creating new SubBlister");
            this.pills = new List<Pill>(pillsOutline.Count);
            // Loop by all given cells
            foreach (Pill pill in pillsOutline)
            {
                if (pill.State == PillState.Cutted) continue;
                // If cell is not cutOut, check if belong to this SubBlister.
                if (Geometry.InclusionTest(pill, this))
                {
                    pill.SubBlister = this;
                    this.pills.Add(pill);
                }

            }
            log.Debug(String.Format("Instantiated {0} cells on SubBlister", pills.Count));
            if (LeftCellsCount == 1) return;
            log.Debug("Sorting Cells");
            // Order by CoordinateIndicator so it means Z-ordering.
            SortPillsByCoordinates(true);
            //  this.cells = cells.OrderBy(cell => cell.CoordinateIndicator).Reverse().ToList();
            // Rebuild cells connectivity.
            log.Debug("Creating ConncectivityData");
            CreateConnectivityData();
        }

        /// <summary>
        /// New initial SubBlister with Cells creation base on pills outlines.
        /// </summary>
        /// <param name="pillsOutline">Pills outline</param>
        /// <param name="outline">SubBlister edge outline</param>
        public SubBlister(List<PolylineCurve> pillsOutline, Polyline outline, Blister blister) : this(pillsOutline, outline.ToPolylineCurve(), blister)
        {
        }

        /// <summary>
        /// New initial SubBlister with Cells creation base on pills outlines.
        /// </summary>
        /// <param name="pillsOutline">Pills outline</param>
        /// <param name="outline">SubBlister edge outline</param>
        public SubBlister(List<PolylineCurve> pillsOutline, PolylineCurve outline, Blister blister) : this(outline, blister)
        {
            log.Debug("Creating new SubBlister");
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
            log.Debug(String.Format("Instantiated {0} pills on SubBlister", pills.Count));
            // If only 1 cell, finish here.
            if (pills.Count <= 1) return;
            // NOTE: Cells Sorting move to SubBlister nd controled in BListructor...
            // Order by Corner distance. First Two set as possible Anchor.
            // log.Debug("Sorting Cells");
            // cells = cells.OrderBy(cell => cell.CornerDistance).ToList();
            // for (int i = 0; i < 2; i++)
            //{
            //     cells[i].PossibleAnchor = true;
            // }
            toTight = AreCellsOverlapping();
            log.Info(String.Format("Is to tight? : {0}", toTight));
            if (toTight) return;
            irVoronoi = Geometry.IrregularVoronoi(pills, Outline.ToPolyline(), 50, 0.05);
            CreateConnectivityData();
        }


        #region PROPERTIES

        public int LeftCellsCount
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

        public List<int> LeftCellsIndices
        {
            get
            {
                List<int> indices = new List<int>();
                if (pills.Count == 0) return indices;
                foreach (Pill cell in pills)
                {
                    if (cell.State == PillState.Queue) indices.Add(cell.id);
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
                else if (LeftCellsCount < 1) return true;
                else return false;
            }
        }

        public List<Pill> Pills { get { return pills; } }

        public Pill PillByID(int id)
        {
            List<Pill> a = pills.Where(cell => cell.id == id).ToList();
            if (a.Count == 1) return a[0];
            return null;
        }

        public bool HasActiveAnchor
        {
            get
            {
                return pills.Any(cell => cell.IsAnchored);
            }
        }

        //public List<Cell> OrderedCells { get { return orderedCells; } set { orderedCells = value; } }

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

        private CutResult cutCell(int cell_list_id, bool ommitAnchor)
        {
           
            Pill currentCell = pills[cell_list_id];
            if (ommitAnchor && currentCell.IsAnchored)
            {
                return new CutResult(CutState.Failed);
            }
            CutState tryCutState = currentCell.TryCut();
            if (tryCutState != CutState.Alone)
            {
                currentCell.State = PillState.Alone;
                log.Info(String.Format("Cell {0}. That was last cell on SubBlister.", currentCell.id));
                return new CutResult(this, null, new List<SubBlister>(), CutState.Alone);
            }
            if (tryCutState != CutState.Failed)
            {
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="worldObstacles"></param>
        /// <returns> CutResult with : CutOut SubBlister, Current Updated SubBlister and Extra Blisters to Cut (recived by spliting currentBlister)</returns>
        public CutResult CutNext()
        {
            log.Debug(String.Format("There is still {0} cells on SubBlister", pills.Count));
            // Try cutting only AnchorInactive cells
            for (int i = 0; i < pills.Count; i++)
            {
                Pill currentPill = pills[i];
                if (currentPill.IsAnchored) continue;
                CutState tryCutState = currentPill.TryCut(true);

                if (tryCutState != CutState.Failed)
                {
                    CutResult data = CuttedCellProcessing(currentPill, tryCutState, i);
                    if (data.State == CutState.Failed)
                    {
                        continue;
                    }
                    else
                    {
                        log.Info(String.Format("Cut Path found for pill {0} after checking {1} pills", currentPill.id, i));
                        return data;
                    }
                }
                else
                {
                    continue;
                }
            }
            // If nothing, try to cut anchored ones...
            log.Warn("No cutting data generated for whole SubBlister. Try to find cutting data in anchored ...");

            for (int i = 0; i < pills.Count; i++)
            {
                Pill currentPill = pills[i];
                if (!currentPill.IsAnchored) continue;
                CutState tryCutState = currentPill.TryCut(false);
                if (currentPill.IsAnchored && tryCutState != CutState.Failed)
                {
                    CutResult data = CuttedCellProcessing(currentPill, tryCutState, i);
                    if (data.State == CutState.Failed)
                    {
                        continue;
                    }
                    else
                    {
                        log.Info(String.Format("Cut Path found for pill {0} after checking {1} pills", currentPill.id, i));
                        return data;
                    }
                }
                else
                {
                    continue;
                }
            }
            log.Warn("No cutting data generated for whole SubBlister.");
            return new CutResult(null, this, new List<SubBlister>());
        }
        /*
        public CutResult CheckIfAlone()
        {
            if (tryCutState != CutState.Alone)
            {
                currentCell.State = PillState.Alone;
                log.Info(String.Format("Cell {0}. That was last cell on SubBlister.", currentCell.id));

                return new CutResult(this, null, new List<SubBlister>(), CutState.Alone);
            }
        }
        */

        private CutResult CuttedCellProcessing(Pill foundCell, CutState foundCellState, int locationIndex)
        {
            List<SubBlister> newBlisters = new List<SubBlister>();
            // If on SubBlister was only one cell, after cutting is status change to Alone, so just return it, without any leftover blisters. 
            if (foundCellState == CutState.Alone)
            {
                foundCell.State = PillState.Alone;
                log.Info(String.Format("Cell {0}. That was last cell on SubBlister.", foundCell.id));

                return new CutResult(this, null, newBlisters, CutState.Alone);
            }

            log.Info(String.Format("Cell {0}. That was NOT last cell on SubBlister.", foundCell.id));

            // Inspect leftovers.
            foreach (PolylineCurve leftover in foundCell.bestCuttingData.BlisterLeftovers)
            {
                SubBlister newBli = new SubBlister(pills, leftover, blister);
                if (!newBli.CheckConnectivityIntegrity(foundCell)) return new CutResult();
                if (!newBli.HasActiveAnchor)
                {
                    log.Warn("No Anchor found for this leftover. Skip this cutting.");
                    return new CutResult();
                }
            }
            return new CutResult(this, null, null, CutState.Proposal);
        }

        public CutResult ApplyCut(Pill foundCell, CutState foundCellState, int locationIndex)
        {
            List<SubBlister> newBlisters = new List<SubBlister>();
            // Ok. If cell is not alone, and Anchor requerments are met. Set cell status as Cutted, and remove all connection with this cell.
            if (foundCellState == CutState.Cutted)
            {
                foundCell.State = PillState.Cutted;
                foundCell.RemoveConnectionData();
            }

            log.Info("Updating current SubBlister outline. Creating cutout SubBlister to store.");

            // If more cells are on SubBlister, replace outline of current SubBlister by first curve from the list...
            // Update current SubBlister outline
            Outline = foundCell.bestCuttingData.BlisterLeftovers[0];
            // If all was ok, Create new SubBlister with cutted Outline
            SubBlister cutted = new SubBlister(foundCell, foundCell.bestCuttingData.Polygon, blister);
            // Remove this cell from current SubBlister
            pills.RemoveAt(locationIndex);
            // Deal with more then one leftover
            // Remove other cells which are not belong to this SubBlister anymore...
            log.Debug("Remove all cells which are not belong to this SubBlister anymore.");
            log.Debug(String.Format("Before removal {0}", pills.Count));
            List<Pill> removerdCells = new List<Pill>(pills.Count);
            for (int i = 0; i < pills.Count; i++)
            {
                // If cell is no more inside this SubBlister, remove it.
                if (!Geometry.InclusionTest(pills[i],this))
                {
                    // check if cell is aimed to cut. For 100% all cells in SubBlister should be Queue.. If not it;s BUGERSON
                    if (pills[i].State != PillState.Queue) continue;
                    removerdCells.Add(pills[i]);
                    pills.RemoveAt(i);
                    i--;
                }
            }
            // Check if any form remaining cells in current SubBlister has Active anchore. /It is not alone/ If doesent, return nulllllls 
            //  if (!this.HasActiveAnchor) return Tuple.Create<SubBlister, SubBlister, List<SubBlister>>(null, null, null);

            //  log.Debug(String.Format("After removal {0} - Removed Cells {1}", cells.Count, removerdCells.Count));
            //  log.Debug(String.Format("Loop by Leftovers  [{0}] (ommit first, it is current SubBlister) and create new blisters.", foundCell.bestCuttingData.BlisterLeftovers.Count - 1));
            //int cellCount = 0;
            // Loop by Leftovers (ommit first, it is current SubBlister) and create new blisters.
            //List<SubBlister> newBlisters = new List<SubBlister>();
            for (int j = 1; j < foundCell.bestCuttingData.BlisterLeftovers.Count; j++)
            {
                PolylineCurve blisterLeftover = foundCell.bestCuttingData.BlisterLeftovers[j];
                SubBlister newBli = new SubBlister(removerdCells, blisterLeftover, blister);
                // Verify if new SubBlister is attachetd to anchor
                //    if (!newBli.HasActiveAnchor) return Tuple.Create<SubBlister, SubBlister, List<SubBlister>>(null, null, null);
                //cellCount += newBli.Cells.Count;
                newBlisters.Add(newBli);
            }
            return new CutResult(cutted, this, newBlisters);
        }

        /// <summary>
        /// Check if PillsOutlines (with knife wird appiled) are not intersecting. 
        /// </summary>
        /// <returns>True if any cell intersect with other.</returns>
        protected bool AreCellsOverlapping()
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
        /// Check Connectivity (AdjacingCell) against some cell planned to be cutout.
        /// </summary>
        /// <param name="pillToCut"></param>
        /// <returns>True if all ok, false if there is inconsistency.</returns>
        protected bool CheckConnectivityIntegrity(Pill pillToCut)
        {
            if (pills.Count > 1)
            {
                foreach (Pill pill in pills)
                {

                    if (pill.adjacentPills.Count == 1)
                    {
                        if (pill.adjacentPills[0].id == pillToCut.id)
                        {
                            log.Warn("Adjacent Cells Checker - Found alone Outline in multi-pills SubBlister. Skip this cutting.");
                            return false;
                        }
                    }
                }

                // Check for SubBlister integrity
                List<HashSet<int>> setsList = new List<HashSet<int>>();
                // Add initileze set
                HashSet<int> initSet = new HashSet<int>();
                initSet.Add(pills[0].id);
                initSet.UnionWith(pills[0].GetAdjacentPillsIds());
                setsList.Add(initSet);
                for (int i = 1; i < pills.Count; i++)
                {
                    List<int> pillsIds = pills[i].GetAdjacentPillsIds();
                    // Remove foundCell Id
                    pillsIds.Remove(pillToCut.id);
                    pillsIds.Add(pills[i].id);
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

                // If only one setList, its ok. If more, try to merge, if not possible, SubBlister is not consistent...
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
        /// Iterate throught cells and compute interconnectring data between them. 
        /// </summary>
        public void CreateConnectivityData()
        {
            log.Debug("Creating Conectivity Data.");
            foreach (Pill currentPill in pills)
            {
                // If current cell is cut out... go to next one.
                if (currentPill.State == PillState.Cutted) continue;
                // log.Debug(String.Format("Checking cell: {0}", currentCell.id));
                List<Point3d> currentMidPoints = new List<Point3d>();
                List<Curve> currentConnectionLines = new List<Curve>();
                List<Pill> currenAdjacentPills = new List<Pill>();
                foreach (Pill proxPill in pills)
                {
                    // If proxCell is cut out or cutCell is same as proxCell, next cell...
                    if (proxPill.State == PillState.Cutted || proxPill.id == currentPill.id) continue;
                    // log.Debug(String.Format("Checking cell: {0}", currentCell.id));
                    Line line = new Line(currentPill.PillCenter, proxPill.PillCenter);
                    Point3d midPoint = line.PointAt(0.5);
                    double t;
                    if (currentPill.voronoi.ClosestPoint(midPoint, out t, 2.000))
                    {
                        // log.Debug(String.Format("Checking cell: {0}", currentCell.id));
                        currenAdjacentPills.Add(proxPill);
                        currentConnectionLines.Add(new LineCurve(line));
                        currentMidPoints.Add(midPoint);
                    }
                }
                log.Debug(String.Format("CELL ID: {0} - Adjacent:{1}, Conection {2} Proxy {3}", currentPill.id, currenAdjacentPills.Count, currentConnectionLines.Count, currentMidPoints.Count));
                currentPill.AddConnectionData(currenAdjacentPills, currentConnectionLines, currentMidPoints);
            }
        }

        #region PREVIEW STUFF FOR DEBUG MOSTLY

        public List<PolylineCurve> GetCuttingPath()
        {
            // !!!============If cell is anchor it probably doesn't have cutting stuff... To validate===========!!!!
            if (pills[0].bestCuttingData == null) return new List<PolylineCurve>();

            // cells[0].bestCuttingData.GenerateBladeFootPrint();
            return pills[0].bestCuttingData.Path;
        }

        public List<LineCurve> GetCuttingLines()
        {
            // !!!============If cell is anchor it probably doesn't have cutting stuff... To validate===========!!!!
            if (pills[0].bestCuttingData == null) return new List<LineCurve>();

            // cells[0].bestCuttingData.GenerateBladeFootPrint();
            return pills[0].bestCuttingData.bladeFootPrint;
        }
        public List<LineCurve> GetIsoRays()
        {
            // !!!============If cell is anchor it probably doesn't have cutting stuff... To validate===========!!!!
            if (pills[0].bestCuttingData == null) return new List<LineCurve>();
            // cells[0].bestCuttingData.GenerateBladeFootPrint();
            return pills[0].bestCuttingData.IsoSegments;
            //return cells[0].bestCuttingData.IsoRays;

        }
        public List<PolylineCurve> GetLeftOvers()
        {
            if (pills[0].bestCuttingData == null) return new List<PolylineCurve>();
            return pills[0].bestCuttingData.BlisterLeftovers;
        }
        public List<PolylineCurve> GetAllPossiblePolygons()
        {
            // !!!============If cell is anchor it probably doesn't have cutting stuff... To validate===========!!!!
            // cells[0].bestCuttingData.GenerateBladeFootPrint();
            if (pills[0].bestCuttingData == null) return new List<PolylineCurve>();
            return pills[0].bestCuttingData.BlisterLeftovers;
        }

        #endregion
    }
}
