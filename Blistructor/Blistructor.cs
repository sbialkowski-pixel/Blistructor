using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;

namespace Blistructor
{
    public class Blistructor
    {

        public bool toTight = false;

        public PolylineCurve blister;
        public PolylineCurve blisterBBox;
        public List<Cell> cells;
        public List<Cell> orderedCells;
        public List<PolylineCurve> irVoronoi;
        public Point3d minPoint;
        public LineCurve guideLine;

        public Blistructor(string maskPath, Polyline Blister)
        {
            /*
            Conturer.Conturer cont = new Conturer.Conturer();
            List<List<int[]>> allPoints = cont.getContours(maskPath, 0.05);
            DataTree<Point3d> rawContours = new DataTree<Point3d>();

            for (int i = 0; i < allPoints.Count; i++ ){
              GH_Path path = new GH_Path(i);
              List<int[]> pointList = allPoints[i];
              for (int j = 0; j < pointList.Count; j++ ){
                Point3d point = new Point3d(pointList[j][0], -pointList[j][1], 0);
                rawContours.Add(point, path);
              }
            }
            initialize(rawContours, Blister);
      */
        }

        public Blistructor(List<Curve> Pills, Polyline Blister)
        {
            Initialize(Pills, Blister);
        }

        public List<NurbsCurve> GetPills
        {
            get
            {
                List<NurbsCurve> pills = new List<NurbsCurve>();
                foreach (Cell cell in cells)
                {
                    pills.Add(cell.pill);
                }
                return pills;
            }

        }

        private void Initialize(List<Curve> Pills, Polyline Blister)
        {
            //isDone = false;

            minPoint = new Point3d(0, double.MaxValue, 0);
            cells = new List<Cell>(Pills.Count);
            if (Pills.Count > 1)
            {
                // Prepare all needed Blister data
                blister = Blister.ToPolylineCurve();
                BoundingBox blisterBB = blister.GetBoundingBox(false);
                Rectangle3d rect = new Rectangle3d(Plane.WorldXY, blisterBB.Min, blisterBB.Max);
                blisterBBox = rect.ToPolyline().ToPolylineCurve();
                // Find lowest mid point on Blister Bounding Box
                foreach (Line edge in blisterBBox.ToPolyline().GetSegments())
                {
                    if (edge.PointAt(0.5).Y < minPoint.Y)
                    {
                        minPoint = edge.PointAt(0.5);
                        guideLine = new LineCurve(edge);
                    }
                }

                // Cells Creation
                cells = new List<Cell>(Pills.Count);
                for (int cellId = 0; cellId < Pills.Count; cellId++)
                {
                    if (Pills[cellId].IsClosed)
                    {
                        Cell cell = new Cell(cellId, Pills[cellId]);
                        cell.SetDistance(guideLine);
                        cells.Add(cell);
                    }
                }
                cells = cells.OrderBy(cell => cell.CoordinateIndicator).Reverse().ToList();
                toTight = AreCellsOverlapping();
                if (!toTight)
                {
                    irVoronoi = Geometry.IrregularVoronoi(cells, Blister, 50, 0.05);
                }
            }
        }

        public int CellsLeft
        {
            get
            {
                int count = 0;
                if (cells.Count > 0)
                {
                    foreach (Cell cell in cells)
                    {
                        if (!cell.removed) count++;
                    }
                }
                return count;
            }
        }

        public bool isDone
        {
            get
            {
                if (toTight) return true;
                else if (CellsLeft <= 1) return true;
                else return false;
            }
        }

        public void getCuttingInstructions(int iter1, int iter2)
        {
            if (!isDone)
            {
                CreateConnectivityData();
                orderedCells = new List<Cell>();
                PolylineCurve currentBlister = new PolylineCurve(blister);
                int n = 0; // control
                while (!isDone)
                {
                    if (n > iter1) break;

                    //if(n > cells.Count) break;

                    bool advancedCutting = true;
                    // Simple cutting
                    for (int cellId = 0; cellId < iter2; cellId++)
                    //for (int cellId = 0; cellId < cells.Count; cellId++)
                    {
                        Cell currentCell = cells[cellId];
                        if (!currentCell.removed)
                        {
                            if (currentCell.GenerateSimpleCuttingData(currentBlister))
                            {
                                currentCell.PolygonSelector();
                                // HERE GET INSTRUCTION!!!
                                currentBlister = currentCell.bestCuttingData.NewBlister;
                                currentCell.RemoveConnectionData();
                                cells[cellId].removed = true;
                                orderedCells.Add(currentCell);
                                advancedCutting = false;
                                break;
                            }
                        }
                    }
                    /*
                    if (advancedCutting){
                      for (int cellId = 0; cellId < cells.Count; cellId++)
                      {
                        Cell currentCell = cells[cellId];
                        if (!currentCell.removed){
                          if(currentCell.generateAdvancedCuttingData(currentBlister)){
                            currentCell.polygonSelector();
                            // HERE GET INSTRUCTION!!!
                            currentBlister = currentCell.bestCuttingData.NewBlister;
                            currentCell.RemoveConnectionData();
                            cells[cellId].removed = true;
                            orderedCells.Add(currentCell);
                            advancedCutting = false;
                            break;
                          }
                        }
                      }
                    }
                    */
                    n++;
                }
                // Add last cell
                if (CellsLeft == 1)
                {
                    foreach (Cell cell in cells)
                    {
                        if (!cell.removed)
                        {
                            CutData data = new CutData
                            {
                                Polygon = orderedCells[orderedCells.Count - 1].bestCuttingData.NewBlister
                            };
                            cell.bestCuttingData = data;
                            orderedCells.Add(cell);
                        }
                    }
                }
            }
        }

        public void EstimateOrder()
        {
        }

        public List<Curve> EstimateCartesian()
        {
            //   public List<Point3d> estimateCartesian(){
            //Get last 2 cells...
            List<Curve> temp = new List<Curve>(2);
            List<Point3d> anchorPoints = new List<Point3d>(2);
            List<Cell> lastCells = new List<Cell> { orderedCells[orderedCells.Count - 1], orderedCells[orderedCells.Count - 2] };
            //List<Curve> lastPills = new List<Curve> {(Curve) orderedCells[orderedCells.Count - 1].pill , (Curve) orderedCells[orderedCells.Count - 2].pill};

            var xform = Rhino.Geometry.Transform.Translation(0, Setups.CartesianThicknes, 0);
            //// tempGuideLine.Transform(xform);

            foreach (Cell cell in lastCells)
            {
                LineCurve tempGuideLine = new LineCurve(guideLine);
                tempGuideLine.Transform(xform);
                Tuple<List<Curve>, List<Curve>> result = Geometry.TrimWithRegion(tempGuideLine, cell.bestCuttingData.Polygon);
                if (result.Item1.Count == 1)
                {
                    Tuple<List<Curve>, List<Curve>> result2 = Geometry.TrimWithRegion(result.Item1[0], cell.pill);
                    temp.AddRange(result2.Item2);
                }
            }
            return temp;
        }


        // Create Connectivity Data
        public void CreateConnectivityData()
        {
            for (int currentCellId = 0; currentCellId < cells.Count; currentCellId++)
            {
                List<Point3d> currentMidPoints = new List<Point3d>();
                List<Curve> currentConnectionLines = new List<Curve>();
                List<Cell> currenAdjacentCells = new List<Cell>();
                for (int proxCellId = 0; proxCellId < cells.Count; proxCellId++)
                {
                    if (proxCellId != currentCellId)
                    {
                        LineCurve line = new LineCurve(cells[currentCellId].pillCenter, cells[proxCellId].pillCenter);
                        Point3d midPoint = line.PointAtNormalizedLength(0.5);
                        double t;
                        if (cells[currentCellId].voronoi.ClosestPoint(midPoint, out t, 1.000))
                        {
                            currenAdjacentCells.Add(cells[proxCellId]);
                            currentConnectionLines.Add(line);
                            currentMidPoints.Add(midPoint);
                        }
                    }
                }
                cells[currentCellId].AddConnectionData(currenAdjacentCells, currentConnectionLines, currentMidPoints, blister);
            }
        }

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
    }

}
