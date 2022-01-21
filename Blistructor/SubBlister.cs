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

    public class SubBlister
    {

        private static readonly ILog log = LogManager.GetLogger("Cutter.SubBlister");

        internal Blister blister;
        private readonly bool toTight = false;
        private PolylineCurve outline;
        private List<Cell> cells;
        public List<PolylineCurve> irVoronoi;

        private SubBlister(Blister _blister)
        {
            blister = _blister;
        }

        /// <summary>
        /// Internal constructor for non-pill stuff
        /// </summary>
        /// <param name="outline">SubBlister Shape</param>
        private SubBlister(PolylineCurve outline, Blister blister): this(blister)
        {
            cells = new List<Cell>();
            Geometry.UnifyCurve(outline);
            this.outline = outline;
        }

        /// <summary>
        /// Contructor mostly to create cut out blisters with one cell
        /// </summary>
        /// <param name="cells"></param>
        /// <param name="outline"></param>
        public SubBlister(Cell _cells, PolylineCurve outline, Blister blister) : this(outline, blister)
        {
            this.cells = new List<Cell>(1) { _cells };
        }

        /// <summary>
        /// New SubBlister based on already existing cells and outline.
        /// </summary>
        /// <param name="cells">Existing cells</param>
        /// <param name="outline">SubBlister edge outline</param>
        public SubBlister(List<Cell> _cells, PolylineCurve outline, Blister blister) : this(outline, blister)
        {
            log.Debug("Creating new SubBlister");
            this.cells = new List<Cell>(_cells.Count);
            // Loop by all given cells
            foreach (Cell cell in _cells)
            {
                if (cell.State == CellState.Cutted) continue;
                // If cell is not cutOut, check if belong to this SubBlister.
                if (this.InclusionTest(cell))
                {
                    cell.SubBlister = this;
                    this.cells.Add(cell);
                }

            }
            log.Debug(String.Format("Instantiated {0} cells on SubBlister", cells.Count));
            if (LeftCellsCount == 1) return;
            log.Debug("Sorting Cells");
            // Order by CoordinateIndicator so it means Z-ordering.
            SortCellsByCoordinates(true);
            //  this.cells = cells.OrderBy(cell => cell.CoordinateIndicator).Reverse().ToList();
            // Rebuild cells connectivity.
            log.Debug("Creating ConncectivityData");
            CreateConnectivityData();
        }

        /// <summary>
        /// New initial SubBlister with Cells creation base on pills outlines.
        /// </summary>
        /// <param name="pills">Pills outline</param>
        /// <param name="outline">SubBlister edge outline</param>
        public SubBlister(List<PolylineCurve> pills, Polyline outline, Blister blister) : this(pills, outline.ToPolylineCurve(), blister)
        {
        }

        /// <summary>
        /// New initial SubBlister with Cells creation base on pills outlines.
        /// </summary>
        /// <param name="pills">Pills outline</param>
        /// <param name="outline">SubBlister edge outline</param>
        public SubBlister(List<PolylineCurve> pills, PolylineCurve outline, Blister blister) : this(outline, blister)
        {
            log.Debug("Creating new SubBlister");
            // Cells Creation
            cells = new List<Cell>(pills.Count);
            for (int cellId = 0; cellId < pills.Count; cellId++)
            {
                if (pills[cellId].IsClosed)
                {
                    Cell cell = new Cell(cellId, pills[cellId], this);
                    //  cell.SetDistance(guideLine);
                    cells.Add(cell);
                }
            }
            log.Debug(String.Format("Instantiated {0} cells on SubBlister", cells.Count));
            // If only 1 cell, finish here.
            if (cells.Count <= 1) return;
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
            irVoronoi = Geometry.IrregularVoronoi(cells, Outline.ToPolyline(), 50, 0.05);
            CreateConnectivityData();
        }


        #region PROPERTIES

        public int LeftCellsCount
        {
            get
            {
                int count = 0;
                if (cells.Count > 0)
                {
                    foreach (Cell cell in cells)
                    {
                        if (cell.State == CellState.Queue) count++;
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
                if (cells.Count == 0) return indices;
                foreach (Cell cell in cells)
                {
                    if (cell.State == CellState.Queue) indices.Add(cell.id);
                }
                return indices;
            }
        }

        public List<Curve> GetPills(bool offset)
        {
            List<Curve> pills = new List<Curve>();
            foreach (Cell cell in cells)
            {
                if (offset) pills.Add(cell.pillOffset);
                else pills.Add(cell.pill);
            }
            return pills;
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

        public List<Cell> Cells { get { return cells; } }

        public Cell CellByID(int id)
        {
            List<Cell> a = cells.Where(cell => cell.id == id).ToList();
            if (a.Count == 1) return a[0];
            return null;
        }

        public bool HasActiveAnchor
        {
            get
            {
                return cells.Any(cell => cell.IsAnchored);
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
        public void SortCellsByCoordinates(bool reverse)
        {
            cells = cells.OrderBy(cell => cell.CoordinateIndicator).ToList();
            if (reverse) cells.Reverse();
        }

        public void SortCellsByPointDirection(Point3d pt, bool reverse)
        {
            cells = cells.OrderBy(cell => cell.GetDirectionIndicator(pt)).ToList();
            if (reverse) cells.Reverse();
        }

        public void SortCellsByPointDistance(Point3d pt, bool reverse)
        {
            cells = cells.OrderBy(cell => cell.GetDistance(pt)).ToList();
            if (reverse) cells.Reverse();
        }
        public void SortCellsByPointsDistance(List<Point3d> pts, bool reverse)
        {
            cells = cells.OrderBy(cell => cell.GetClosestDistance(pts)).ToList();
            if (reverse) cells.Reverse();
        }

        public void SortCellsByCurveDistance(Curve crv, bool reverse)
        {
            cells = cells.OrderBy(cell => cell.GetDistance(crv)).ToList();
            if (reverse) cells.Reverse();
        }

        public void SortCellsByCurvesDistance(List<Curve> crvs, bool reverse)
        {
            cells = cells.OrderBy(cell => cell.GetClosestDistance(crvs)).ToList();
            if (reverse) cells.Reverse();
        }

        #endregion

        /// <summary>
        /// 
        /// </summary>
        /// <param name="worldObstacles"></param>
        /// <returns> CutResult with : CutOut SubBlister, Current Updated SubBlister and Extra Blisters to Cut (recived by spliting currentBlister)</returns>
        public CutResult CutNext()
        {
            log.Debug(String.Format("There is still {0} cells on SubBlister", cells.Count));
            // Try cutting only AnchorInactive cells
            for (int i = 0; i < cells.Count; i++)
            {
                Cell currentCell = cells[i];
                CutState tryCutState = currentCell.TryCut(true);

                if (tryCutState != CutState.Failed)
                {
                    CutResult data = CuttedCellProcessing(currentCell, tryCutState, i);
                    if (data.State == CutState.Failed)
                    {
                        continue;
                    }
                    else
                    {
                        log.Info(String.Format("Cut Path found for cell {0} after checking {1} cells", currentCell.id, i));
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

            for (int i = 0; i < cells.Count; i++)
            {
                Cell currentCell = cells[i];
                CutState tryCutState = currentCell.TryCut(false);
                if (currentCell.IsAnchored && tryCutState != CutState.Failed)
                {
                    CutResult data = CuttedCellProcessing(currentCell, tryCutState, i);
                    if (data.State == CutState.Failed)
                    {
                        continue;
                    }
                    else
                    {
                        log.Info(String.Format("Cut Path found for cell {0} after checking {1} cells", currentCell.id, i));
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
                currentCell.State = CellState.Alone;
                log.Info(String.Format("Cell {0}. That was last cell on SubBlister.", currentCell.id));

                return new CutResult(this, null, new List<SubBlister>(), CutState.Alone);
            }
        }
        */

        private CutResult CuttedCellProcessing(Cell foundCell, CutState foundCellState, int locationIndex)
        {
            List<SubBlister> newBlisters = new List<SubBlister>();
            // If on SubBlister was only one cell, after cutting is status change to Alone, so just return it, without any leftover blisters. 
            if (foundCellState == CutState.Alone)
            {
                foundCell.State = CellState.Alone;
                log.Info(String.Format("Cell {0}. That was last cell on SubBlister.", foundCell.id));

                return new CutResult(this, null, newBlisters, CutState.Alone);
            }

            log.Info(String.Format("Cell {0}. That was NOT last cell on SubBlister.", foundCell.id));

            // Inspect leftovers.
            foreach (PolylineCurve leftover in foundCell.bestCuttingData.BlisterLeftovers)
            {
                SubBlister newBli = new SubBlister(cells, leftover, blister);
                if (!newBli.CheckConnectivityIntegrity(foundCell)) return new CutResult();
                if (!newBli.HasActiveAnchor)
                {
                    log.Warn("No Anchor found for this leftover. Skip this cutting.");
                    return new CutResult();
                }
            }
            return new CutResult(this, null, null, CutState.Proposal);
        }

        public CutResult ApplyCut(Cell foundCell, CutState foundCellState, int locationIndex)
        {
            List<SubBlister> newBlisters = new List<SubBlister>();
            // Ok. If cell is not alone, and Anchor requerments are met. Set cell status as Cutted, and remove all connection with this cell.
            if (foundCellState == CutState.Cutted)
            {
                foundCell.State = CellState.Cutted;
                foundCell.RemoveConnectionData();
            }

            log.Info("Updating current SubBlister outline. Creating cutout SubBlister to store.");

            // If more cells are on SubBlister, replace outline of current SubBlister by first curve from the list...
            // Update current SubBlister outline
            Outline = foundCell.bestCuttingData.BlisterLeftovers[0];
            // If all was ok, Create new SubBlister with cutted pill
            SubBlister cutted = new SubBlister(foundCell, foundCell.bestCuttingData.Polygon, blister);
            // Remove this cell from current SubBlister
            cells.RemoveAt(locationIndex);
            // Deal with more then one leftover
            // Remove other cells which are not belong to this SubBlister anymore...
            log.Debug("Remove all cells which are not belong to this SubBlister anymore.");
            log.Debug(String.Format("Before removal {0}", cells.Count));
            List<Cell> removerdCells = new List<Cell>(cells.Count);
            for (int i = 0; i < cells.Count; i++)
            {
                // If cell is no more inside this SubBlister, remove it.
                if (!InclusionTest(cells[i]))
                {
                    // check if cell is aimed to cut. For 100% all cells in SubBlister should be Queue.. If not it;s BUGERSON
                    if (cells[i].State != CellState.Queue) continue;
                    removerdCells.Add(cells[i]);
                    cells.RemoveAt(i);
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


        public bool InclusionTest(Cell testCell)
        {
            return InclusionTest(testCell.pillOffset);
        }

        public bool InclusionTest(Cell testCell, Curve Region)
        {
            return InclusionTest(testCell.pillOffset, Region);
        }

        public bool InclusionTest(Curve testCurve)
        {
            RegionContainment test = Curve.PlanarClosedCurveRelationship(Outline, testCurve);
            if (test == RegionContainment.BInsideA) return true;
            else return false;
        }

        public bool InclusionTest(Curve testCurve, Curve Region)
        {
            RegionContainment test = Curve.PlanarClosedCurveRelationship(Region, testCurve);
            if (test == RegionContainment.BInsideA) return true;
            else return false;
        }

        /// <summary>
        /// Check if PillsOutlines (with knife wird appiled) are not intersecting. 
        /// </summary>
        /// <returns>True if any cell intersect with other.</returns>
        protected bool AreCellsOverlapping()
        {
            // output = false;
            for (int i = 0; i < cells.Count; i++)
            {
                for (int j = i + 1; j < cells.Count; j++)
                {
                    List<IntersectionEvent> inter = Intersection.CurveCurve(cells[i].pillOffset, cells[j].pillOffset, Setups.IntersectionTolerance);
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
        /// <param name="cellToCut"></param>
        /// <returns>True if all ok, false if there is inconsistency.</returns>
        protected bool CheckConnectivityIntegrity(Cell cellToCut)
        {
            if (cells.Count > 1)
            {
                foreach (Cell cell in cells)
                {

                    if (cell.adjacentCells.Count == 1)
                    {
                        if (cell.adjacentCells[0].id == cellToCut.id)
                        {
                            log.Warn("Adjacent Cells Checker - Found alone pill in multi-pills SubBlister. Skip this cutting.");
                            return false;
                        }
                    }
                }

                // Check for SubBlister integrity
                List<HashSet<int>> setsList = new List<HashSet<int>>();
                // Add initileze set
                HashSet<int> initSet = new HashSet<int>();
                initSet.Add(cells[0].id);
                initSet.UnionWith(cells[0].GetAdjacentCellsIds());
                setsList.Add(initSet);
                for (int i = 1; i < cells.Count; i++)
                {
                    List<int> cellsIds = cells[i].GetAdjacentCellsIds();
                    // Remove foundCell Id
                    cellsIds.Remove(cellToCut.id);
                    cellsIds.Add(cells[i].id);
                    // check if smallSet fit to any bigger sets in list.
                    bool added = false;
                    for (int j = 0; j < setsList.Count; j++)
                    {
                        // If yes, add it and break forloop.
                        if (setsList[j].Overlaps(cellsIds))
                        {
                            setsList[j].UnionWith(cellsIds);
                            added = true;
                            break;
                        }
                    }
                    // if not added, create ne big set based on small one.
                    if (!added)
                    {
                        HashSet<int> newSet = new HashSet<int>();
                        newSet.UnionWith(cellsIds);
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
                        log.Warn("Adjacent Cells Checker - Pill AdjacentConnection not cconsistent. Skip this cutting.");
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
            foreach (Cell currentCell in cells)
            {
                // If current cell is cut out... go to next one.
                if (currentCell.State == CellState.Cutted) continue;
                // log.Debug(String.Format("Checking cell: {0}", currentCell.id));
                List<Point3d> currentMidPoints = new List<Point3d>();
                List<Curve> currentConnectionLines = new List<Curve>();
                List<Cell> currenAdjacentCells = new List<Cell>();
                foreach (Cell proxCell in cells)
                {
                    // If proxCell is cut out or cutCell is same as proxCell, next cell...
                    if (proxCell.State == CellState.Cutted || proxCell.id == currentCell.id) continue;
                    // log.Debug(String.Format("Checking cell: {0}", currentCell.id));
                    Line line = new Line(currentCell.PillCenter, proxCell.PillCenter);
                    Point3d midPoint = line.PointAt(0.5);
                    double t;
                    if (currentCell.voronoi.ClosestPoint(midPoint, out t, 2.000))
                    {
                        // log.Debug(String.Format("Checking cell: {0}", currentCell.id));
                        currenAdjacentCells.Add(proxCell);
                        currentConnectionLines.Add(new LineCurve(line));
                        currentMidPoints.Add(midPoint);
                    }
                }
                log.Debug(String.Format("CELL ID: {0} - Adjacent:{1}, Conection {2} Proxy {3}", currentCell.id, currenAdjacentCells.Count, currentConnectionLines.Count, currentMidPoints.Count));
                currentCell.AddConnectionData(currenAdjacentCells, currentConnectionLines, currentMidPoints);
            }
        }

        #region PREVIEW STUFF FOR DEBUG MOSTLY

        public List<PolylineCurve> GetCuttingPath()
        {
            // !!!============If cell is anchor it probably doesn't have cutting stuff... To validate===========!!!!
            if (cells[0].bestCuttingData == null) return new List<PolylineCurve>();

            // cells[0].bestCuttingData.GenerateBladeFootPrint();
            return cells[0].bestCuttingData.Path;
        }

        public List<LineCurve> GetCuttingLines()
        {
            // !!!============If cell is anchor it probably doesn't have cutting stuff... To validate===========!!!!
            if (cells[0].bestCuttingData == null) return new List<LineCurve>();

            // cells[0].bestCuttingData.GenerateBladeFootPrint();
            return cells[0].bestCuttingData.bladeFootPrint;
        }
        public List<LineCurve> GetIsoRays()
        {
            // !!!============If cell is anchor it probably doesn't have cutting stuff... To validate===========!!!!
            if (cells[0].bestCuttingData == null) return new List<LineCurve>();
            // cells[0].bestCuttingData.GenerateBladeFootPrint();
            return cells[0].bestCuttingData.IsoSegments;
            //return cells[0].bestCuttingData.IsoRays;

        }
        public List<PolylineCurve> GetLeftOvers()
        {
            if (cells[0].bestCuttingData == null) return new List<PolylineCurve>();
            return cells[0].bestCuttingData.BlisterLeftovers;
        }
        public List<PolylineCurve> GetAllPossiblePolygons()
        {
            // !!!============If cell is anchor it probably doesn't have cutting stuff... To validate===========!!!!
            // cells[0].bestCuttingData.GenerateBladeFootPrint();
            if (cells[0].bestCuttingData == null) return new List<PolylineCurve>();
            return cells[0].bestCuttingData.BlisterLeftovers;
        }

        #endregion
    }
}
