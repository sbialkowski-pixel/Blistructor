using System;
using System.Collections.Generic;
using System.Linq;
using log4net;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;
using Newtonsoft.Json.Linq;

namespace Blistructor
{
    
    public class Blister
    {
        
        private static readonly ILog log = LogManager.GetLogger("Main.Blister");


        private bool toTight = false;
        private PolylineCurve outline;
        //private PolylineCurve bBox;
        //private Point3d minPoint;
        //private LineCurve guideLine;
        private List<Cell> cells;
        public List<PolylineCurve> irVoronoi;

        /// <summary>
        /// Internal constructor for non-pill stuff
        /// </summary>
        /// <param name="outline">Blister Shape</param>
        private Blister(PolylineCurve outline)
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
        public Blister(Cell _cells, PolylineCurve outline) : this(outline)
        {
            this.cells = new List<Cell>(1) { _cells };
        }

        /// <summary>
        /// New blister based on already existing cells and outline.
        /// </summary>
        /// <param name="cells">Existing cells</param>
        /// <param name="outline">Blister edge outline</param>
        public Blister(List<Cell> _cells, PolylineCurve outline) : this(outline)
        {
            log.Debug("Creating new blister");
            this.cells = new List<Cell>(_cells.Count);
            // Loop by all given cells
            foreach (Cell cell in _cells)
            {
                if (cell.State == CellState.Cutted) continue;
                // If cell is not cutOut, check if belong to this blister.
                if (this.InclusionTest(cell))
                {
                    cell.Blister = this;
                    this.cells.Add(cell);
                }

            }
            log.Debug(String.Format("Instantiated {0} cells on blister", cells.Count));
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
        /// New initial blister with Cells creation base on pills outlines.
        /// </summary>
        /// <param name="pills">Pills outline</param>
        /// <param name="outline">Blister edge outline</param>
        public Blister(List<PolylineCurve> pills, Polyline outline) : this(pills, outline.ToPolylineCurve())
        {
        }

        /// <summary>
        /// New initial blister with Cells creation base on pills outlines.
        /// </summary>
        /// <param name="pills">Pills outline</param>
        /// <param name="outline">Blister edge outline</param>
        public Blister(List<PolylineCurve> pills, PolylineCurve outline) : this(outline)
        {
            log.Debug("Creating new blister");
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
            log.Debug(String.Format("Instantiated {0} cells on blister", cells.Count));
            // If only 1 cell, finish here.
            if (cells.Count <= 1) return;
            // NOTE: Cells Sorting move to BLister nd controled in BListructor...
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
          /*
        public DataTree<Curve> GetObstacles
        {
            get
            {
                DataTree<Curve> out_data = new DataTree<Curve>();
                //List<List<Curve>> out_data = new List<List<Curve>>();
                //  if (cells.Count == 0) return out_data;
                for (int i = 0; i < cells.Count; i++)
                {
                    GH_Path path = new GH_Path(i);
                    if (cells[i].obstacles == null) cells[i].obstacles = cells[i].BuildObstacles_v2(null);
                    //   if (cells[i].obstacles.Count == 0) out_data.AddRang(new List<Curve>());
                    out_data.AddRange(cells[i].obstacles, path);
                }
                return out_data;
            }
        }
        public DataTree<Point3d> GetSamplePoints
        {
            get
            {
                DataTree<Point3d> out_data = new DataTree<Point3d>();
                //List<List<Curve>> out_data = new List<List<Curve>>();
                for (int i = 0; i < cells.Count; i++)
                {
                    GH_Path path = new GH_Path(i);
                    out_data.AddRange(cells[i].samplePoints, path);
                }
                return out_data;
            }
        }
        public DataTree<Curve> GetConnectionLines
        {
            get
            {
                DataTree<Curve> out_data = new DataTree<Curve>();
                //List<List<Curve>> out_data = new List<List<Curve>>();
                for (int i = 0; i < cells.Count; i++)
                {
                    GH_Path path = new GH_Path(i);
                    out_data.AddRange(cells[i].connectionLines, path);
                }
                return out_data;
            }
        }
        public DataTree<Curve> GetProxyLines
        {
            get
            {
                DataTree<Curve> out_data = new DataTree<Curve>();
                //List<List<Curve>> out_data = new List<List<Curve>>();
                for (int i = 0; i < cells.Count; i++)
                {
                    GH_Path path = new GH_Path(i);
                    out_data.AddRange(cells[i].proxLines, path);
                }
                return out_data;
            }
        }
       * /
        /*
        public Point3d MinPoint
        {
            get { return minPoint; }
        }

        public LineCurve GuideLine
        {
            get { return guideLine; }
        }
        */

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
                if (cells.Select(cell => cell.Anchor.state).Where(state => state == AnchorState.Active).ToList().Count > 0) return true;
                else return false;
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
        /// <returns> In tuple I have | CutOut Blister | Current Updated Blister | Extra Blisters to Cut (recived by spliting currentBlister)</returns>
        public Tuple<Blister, Blister, List<Blister>> CutNext(List<Curve> worldObstacles)
        {
            //int counter = 0;
            log.Debug(String.Format("There is still {0} cells on blister", cells.Count));

            // counter = 0;
            // Try cutting only AnchorInactive cells
            for (int i = 0; i < cells.Count; i++)
            //   foreach (Cell currentCell in cells)
            {
                Cell currentCell = cells[i];
                CutState tryCutState = currentCell.TryCut(true, worldObstacles);
                if (tryCutState != CutState.Failed)
                {
                    Tuple<Blister, Blister, List<Blister>> data = CuttedCellProcessing(currentCell, tryCutState, i);
                    if (data.Item1 == null && data.Item2 == null && data.Item3 == null)
                    {
                        //NOTE: Omija tabletke tóra jest ok do wyciecia i teni jakość z dupy. W zaziaku ze zmianami w  CuttedCellProcessing????    Żle sie ustawiaja statusy czy anchor jest aktywny czy nie.... dlatego....
                        log.Info("Tutej!!!");
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
            log.Info("Olaaaa!!!");
            // If nothing, try to cut anchored ones...
            log.Warn("No cutting data generated for whole blister. Try to find cutting data in anchored ...");
            //counter = 0;
            for (int i = 0; i < cells.Count; i++)
            //foreach (Cell currentCell in cells)
            {
                Cell currentCell = cells[i];
                CutState tryCutState = currentCell.TryCut(false, worldObstacles);
                if (currentCell.Anchor.state == AnchorState.Active && tryCutState != CutState.Failed)
                {
                    Tuple<Blister, Blister, List<Blister>> data = CuttedCellProcessing(currentCell, tryCutState, i);
                    if (data.Item1 == null && data.Item2 == null && data.Item3 == null)
                    {
                        //  counter++;
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
                    //counter++;
                    continue;
                }

            }
            log.Warn("No cutting data generated for whole blister.");

            return Tuple.Create<Blister, Blister, List<Blister>>(null, this, new List<Blister>());

        }

        private Tuple<Blister, Blister, List<Blister>> CuttedCellProcessing(Cell foundCell, CutState foundCellState, int locationIndex)
        {

            List<Blister> newBlisters = new List<Blister>();
            /*
            if (foundCell == null)
            {
                log.Warn("No cutting data generated for whole blister.");

                return Tuple.Create<Blister, Blister, List<Blister>>(null, this, newBlisters);
            }
            */
            // If on blister was only one cell, after cutting is status change to Alone, so just return it, without any leftover blisters. 
            if (foundCellState == CutState.Alone)
            {
                foundCell.State = CellState.Alone;
                //foundCell.RemoveConnectionData();
                log.Info(String.Format("Cell {0}. That was last cell on blister.", foundCell.id));
                return Tuple.Create<Blister, Blister, List<Blister>>(this, null, newBlisters);
            }

            log.Info(String.Format("Cell {0}. That was NOT last cell on blister.", foundCell.id));

            // Chceck if after cutting all parts hase anchor point, so none will fall of...
            foreach (PolylineCurve leftover in foundCell.bestCuttingData.BlisterLeftovers)
            {
                bool hasActiveAnchor = false;
                for (int i = 0; i < cells.Count; i++)
                {
                    if (i == locationIndex) continue;
                    if (!InclusionTest(cells[i], leftover)) continue;
                    if (cells[i].Anchor.state == AnchorState.Active)
                    {
                        hasActiveAnchor = true;
                        break;
                    }
                }
                log.Info(String.Format("hasActiveAnchor -> {0}", hasActiveAnchor));

                if (!hasActiveAnchor) return Tuple.Create<Blister, Blister, List<Blister>>(null, null, null);

            }

            // Ok. If cell is not alone, and Anchor requerments are met. Set cell status as Cutted, and remove all connection with this cell.
            if (foundCellState == CutState.Cutted)
            {
                foundCell.State = CellState.Cutted;
                foundCell.RemoveConnectionData();
            }


            log.Info("Updating current blister outline. Creating cutout blister to store.");

            // If more cells are on blister, replace outline of current blister by first curve from the list...
            // Update current blister outline
            Outline = foundCell.bestCuttingData.BlisterLeftovers[0];
            // If all was ok, Create new blister with cutted pill
            Blister cutted = new Blister(foundCell, foundCell.bestCuttingData.Polygon);
            // Remove this cell from current blister
            cells.RemoveAt(locationIndex);
            // Deal with more then one leftover
            // Remove other cells which are not belong to this blister anymore...
            log.Debug("Remove all cells which are not belong to this blister anymore.");
            log.Debug(String.Format("Before removal {0}", cells.Count));
            List<Cell> removerdCells = new List<Cell>(cells.Count);
            for (int i = 0; i < cells.Count; i++)
            {
                // If cell is no more insied this blister, remove it.
                if (!InclusionTest(cells[i]))
                {
                    // check if cell is aimed to cut. For 100% all cells in blister should be Queue.. If not it;s BUGERSON
                    if (cells[i].State != CellState.Queue) continue;
                    removerdCells.Add(cells[i]);
                    cells.RemoveAt(i);
                    i--;
                }
            }
            // Check if any form remaining cells in current blister has Active anchore. /It is not alone/ If doesent, return nulllllls 
            //  if (!this.HasActiveAnchor) return Tuple.Create<Blister, Blister, List<Blister>>(null, null, null);

            //  log.Debug(String.Format("After removal {0} - Removed Cells {1}", cells.Count, removerdCells.Count));
            //  log.Debug(String.Format("Loop by Leftovers  [{0}] (ommit first, it is current blister) and create new blisters.", foundCell.bestCuttingData.BlisterLeftovers.Count - 1));
            //int cellCount = 0;
            // Loop by Leftovers (ommit first, it is current blister) and create new blisters.
            for (int j = 1; j < foundCell.bestCuttingData.BlisterLeftovers.Count; j++)
            {
                PolylineCurve blisterLeftover = foundCell.bestCuttingData.BlisterLeftovers[j];
                Blister newBli = new Blister(removerdCells, blisterLeftover);
                // Verify if new blister is attachetd to anchor
                //    if (!newBli.HasActiveAnchor) return Tuple.Create<Blister, Blister, List<Blister>>(null, null, null);
                //cellCount += newBli.Cells.Count;
                newBlisters.Add(newBli);
            }

            return Tuple.Create<Blister, Blister, List<Blister>>(cutted, this, newBlisters);
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
            RegionContainment test = Curve.PlanarClosedCurveRelationship(Outline, testCurve, Plane.WorldXY, Setups.OverlapTolerance);
            if (test == RegionContainment.BInsideA) return true;
            else return false;
        }

        public bool InclusionTest(Curve testCurve, Curve Region)
        {
            RegionContainment test = Curve.PlanarClosedCurveRelationship(Region, testCurve, Plane.WorldXY, Setups.OverlapTolerance);
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
                    CurveIntersections inter = Intersection.CurveCurve(cells[i].pillOffset, cells[j].pillOffset, Setups.IntersectionTolerance, Setups.OverlapTolerance);
                    if (inter.Count > 0)
                    {
                        return true;
                    }
                }
            }
            return false;
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
                    LineCurve line = new LineCurve(currentCell.PillCenter, proxCell.PillCenter);
                    Point3d midPoint = line.PointAtNormalizedLength(0.5);
                    double t;
                    if (currentCell.voronoi.ClosestPoint(midPoint, out t, 2.000))
                    {
                        currenAdjacentCells.Add(proxCell);
                        currentConnectionLines.Add(line);
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
