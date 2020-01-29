using System;
using System.Collections;
using System.Collections.Generic;

using Rhino;
using Rhino.Geometry;

using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

// <Custom using>
using Grasshopper.Kernel.Geometry.Voronoi;
using Grasshopper.Kernel.Geometry.Delaunay;

using Rhino.Geometry.Intersect;
using System.Linq;
using Combinators;
using Conturer;
// </Custom using>

/// Unique namespace, so visual studio won't throw any errors about duplicate definitions.
namespace Blistructor
{
    /// <summary>
    /// This class will be instantiated on demand by the Script component.
    /// </summary>
    public class Script_Instance : GH_ScriptInstance
    {

        /// This method is added to prevent compiler errors when opening this file in visual studio (code) or rider.
        public override void InvokeRunScript(IGH_Component owner, object rhinoDocument, int iteration, List<object> inputs, IGH_DataAccess DA)
        {
            throw new NotImplementedException();
        }

        #region Utility functions
        /// <summary>Print a String to the [Out] Parameter of the Script component.</summary>
        /// <param name="text">String to print.</param>
        private void Print(string text) { /* Implementation hidden. */ }
        /// <summary>Print a formatted String to the [Out] Parameter of the Script component.</summary>
        /// <param name="format">String format.</param>
        /// <param name="args">Formatting parameters.</param>
        private void Print(string format, params object[] args) { /* Implementation hidden. */ }
        /// <summary>Print useful information about an object instance to the [Out] Parameter of the Script component. </summary>
        /// <param name="obj">Object instance to parse.</param>
        private void Reflect(object obj) { /* Implementation hidden. */ }
        /// <summary>Print the signatures of all the overloads of a specific method to the [Out] Parameter of the Script component. </summary>
        /// <param name="obj">Object instance to parse.</param>
        private void Reflect(object obj, string method_name) { /* Implementation hidden. */ }
        #endregion
        #region Members
        /// <summary>Gets the current Rhino document.</summary>
        private readonly RhinoDoc RhinoDocument;
        /// <summary>Gets the Grasshopper document that owns this script.</summary>
        private readonly GH_Document GrasshopperDocument;
        /// <summary>Gets the Grasshopper script component that owns this script.</summary>
        private readonly IGH_Component Component;
        /// <summary>
        /// Gets the current iteration count. The first call to RunScript() is associated with Iteration==0.
        /// Any subsequent call within the same solution will increment the Iteration count.
        /// </summary>
        private readonly int Iteration;
        #endregion
        /// <summary>
        /// This procedure contains the user code. Input parameters are provided as regular arguments,
        /// Output parameters as ref arguments. You don't have to assign output parameters,
        /// they will have a default value.
        /// </summary>

        #region Runscript

        private void RunScript(List<Curve> Pills, Polyline Blister, int cellId, int iter1, int iter2, ref object A, ref object B, ref object C, ref object D, ref object E, ref object F, ref object G, ref object H, ref object I, ref object AA)
        {
            // <Custom code>
            bool prev_blister = true;
            Blistructor blister = new Blistructor(Pills, Blister);
            blister.CreateConnectivityData();
            //blister.getCuttingInstructions(iter1, iter2);

            List<NurbsCurve> pills = new List<NurbsCurve>();
            List<PolylineCurve> polygons = new List<PolylineCurve>();
            List<PolylineCurve> path = new List<PolylineCurve>();
            List<PolylineCurve> blisters = new List<PolylineCurve>();
            //List<LineCurve> bladeFootPrint = new  List<LineCurve>();
            DataTree<LineCurve> bladeFootPrint = new DataTree<LineCurve>();



            if (!blister.toTight && prev_blister)
            {
                //for (int i = 0; i < blister.orderedCells.Count; i++)
                //{
                //    Cell cell = blister.orderedCells[i];
                //    // foreach(Cell cell in blister.orderedCells){
                //    polygons.Add(cell.bestCuttingData.Polygon);
                //    blisters.Add(cell.bestCuttingData.NewBlister);
                //    path.Add(cell.bestCuttingData.Path);
                //    //cell.bestCuttingData.GetInstructions();
                //    if (cell.bestCuttingData.bladeFootPrint.Count > 0)
                //    {
                //        bladeFootPrint.AddRange(cell.bestCuttingData.bladeFootPrint, new GH_Path(i));
                //    }
                //    pills.Add(cell.pill);
                //}
                //A = polygons;
                //B = path;
                //C = blisters;
                //D = pills;
                //F = bladeFootPrint;

                // E = blister.cells[cellId].GetTrimmedIsoRays();
                //E = blister.irVoronoi;
                //
                //G = blister.cells[cellId].temp;
                H = blister.cells[cellId].proxLines;
                //I = blister.cells[cellId].GetPaths();
                I = blister.cells[cellId].samplePoints;
                //AA = blister.cells[cellId].orientationCircle;
            }
            else
            {
                Print("TO TIGHT PILLS ARRANGMENT. I'M DONE HERE. BYEBYE...");
            }
            // </Custom code>
        }
        #endregion

        #region Additional
        // <Custom additional code>
        
        public class Blistructor2
        {
            public List<Blister> blister;
            public Point3d knifeLastPoint = new Point3d();
            
            public Blistructor2(string maskPath, Polyline Blister)
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

            public Blistructor2(List<Curve> Pills, Polyline Blister)
            {
                blister.Add(new Blister(Pills, Blister));
            }


        }


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
                    // Order by Corner distance. First Two set as possible Anchor.
                    cells = cells.OrderBy(cell => cell.CornerDistance).ToList();
                    for (int i = 0; i < 2; i++)
                    {
                        cells[i].possible_anchor = true;
                    }
                    cells = cells.OrderBy(cell => cell.CoordinateIndicator).Reverse().ToList();
                    //cells = cells.OrderBy(cell => cell.CornerDistance).Reverse().ToList();
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

            public bool IsDone
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
                if (!IsDone)
                {
                    CreateConnectivityData();
                    orderedCells = new List<Cell>();
                    PolylineCurve currentBlister = new PolylineCurve(blister);
                    int n = 0; // control
                    while (!IsDone)
                    {
                        if (n > iter1) break;

                        //if(n > cells.Count) break;

                        bool advancedCutting = true;
                        bool anyCutted = false;
                        // Simple cutting
                        for (int cellId = 0; cellId < iter2; cellId++)
                        //for (int cellId = 0; cellId < cells.Count; cellId++)
                        {
                            Cell currentCell = cells[cellId];
                            //if (!currentCell.removed && !currentCell.possible_anchor)
                            if (!currentCell.removed)
                            {
                                //currentCell.SortData(currentBlister);
                                if (currentCell.GenerateSimpleCuttingData(currentBlister))
                                {
                                    currentCell.PolygonSelector();
                                    // HERE GET INSTRUCTION!!!
                                    currentBlister = currentCell.bestCuttingData.NewBlister;
                                    currentCell.RemoveConnectionData(currentBlister);
                                    cells[cellId].removed = true;
                                    orderedCells.Add(currentCell);
                                    advancedCutting = false;
                                    anyCutted = true;
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
                foreach (Cell currentCell in cells)
                {
                    // If current cell is removed... go to next one.
                    if (currentCell.removed) continue;
                    List<Point3d> currentMidPoints = new List<Point3d>();
                    List<Curve> currentConnectionLines = new List<Curve>();
                    List<Cell> currenAdjacentCells = new List<Cell>();
                    foreach (Cell proxCell in cells)
                    {
                        // If proxCell is removed or currCell is same as proxCell, next cell...
                        if (proxCell.removed || proxCell.id == currentCell.id) continue;
                        LineCurve line = new LineCurve(currentCell.pillCenter, proxCell.pillCenter);
                        Point3d midPoint = line.PointAtNormalizedLength(0.5);
                        double t;
                        if (currentCell.voronoi.ClosestPoint(midPoint, out t, 1.000))
                        {
                            currenAdjacentCells.Add(proxCell);
                            currentConnectionLines.Add(line);
                            currentMidPoints.Add(midPoint);
                        }
                    }
                    currentCell.AddConnectionData(currenAdjacentCells, currentConnectionLines, currentMidPoints, blister);
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

        public class Blister
        {
            public bool ToTight = false;
            private PolylineCurve outline;
            private PolylineCurve bBox;
            private Point3d minPoint;
            private LineCurve guideLine;
            private List<Cell> cells;
            private List<Cell> orderedCells;
            public List<PolylineCurve> irVoronoi;

            public Blister(List<Curve> pills, Polyline outline)
            {
                Initialize(pills, outline.ToPolylineCurve());
            }
            
            public Blister(List<Curve> pills, PolylineCurve outline)
            {
                Initialize(pills, outline);
            }

            private void Initialize(List<Curve> pills, PolylineCurve outline)
            {
                minPoint = new Point3d(0, double.MaxValue, 0);
                cells = new List<Cell>(pills.Count);
                if (pills.Count > 1)
                {
                    // Prepare all needed Blister data
                    this.outline = outline;
                    BoundingBox blisterBB = Outline.GetBoundingBox(false);
                    Rectangle3d rect = new Rectangle3d(Plane.WorldXY, blisterBB.Min, blisterBB.Max);
                    BBox = rect.ToPolyline().ToPolylineCurve();
                    // Find lowest mid point on Blister Bounding Box
                    foreach (Line edge in BBox.ToPolyline().GetSegments())
                    {
                        if (edge.PointAt(0.5).Y < minPoint.Y)
                        {
                            minPoint = edge.PointAt(0.5);
                            guideLine = new LineCurve(edge);
                        }
                    }

                    // Cells Creation
                    cells = new List<Cell>(pills.Count);
                    for (int cellId = 0; cellId < pills.Count; cellId++)
                    {
                        if (pills[cellId].IsClosed)
                        {
                            Cell cell = new Cell(cellId, pills[cellId]);
                            cell.SetDistance(guideLine);
                            cells.Add(cell);
                        }
                    }
                    // Order by Corner distance. First Two set as possible Anchor.
                    cells = cells.OrderBy(cell => cell.CornerDistance).ToList();
                    for (int i = 0; i < 2; i++)
                    {
                        cells[i].possible_anchor = true;
                    }
                    cells = cells.OrderBy(cell => cell.CoordinateIndicator).Reverse().ToList();
                    //cells = cells.OrderBy(cell => cell.CornerDistance).Reverse().ToList();
                    ToTight = AreCellsOverlapping();
                    if (!ToTight)
                    {
                        irVoronoi = Geometry.IrregularVoronoi(cells, Outline.ToPolyline(), 50, 0.05);
                    }
                }
            }

            public Point3d MinPoint
            {
                get { return minPoint; }
            }
            
            public LineCurve GuideLine
            {
                get { return guideLine; }
            }
            public int LeftCellsCount
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
            
            public List<int> LeftCellsIndices
            {
                get
                {
                    List<int> indices = new List<int>();
                    if (cells.Count == 0) return indices;
                    foreach ( Cell cell in cells)
                    {
                        if (!cell.removed) indices.Add(cell.id);
                    }
                    return indices;
                }
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

            public bool IsDone
            {
                get
                {
                    if (ToTight) return true;
                    else if (LeftCellsCount <= 1) return true;
                    else return false;
                }
            }

            public List<Cell> Cells { get { return cells; } set { cells = value; } }
            
            public List<Cell> OrderedCells { get { return orderedCells; } set { orderedCells = value; } }
            
            public PolylineCurve Outline { get { return outline; } set { outline = value; } }
           
            public PolylineCurve BBox { get { return bBox; } set { bBox = value; } }

            public void CutNext()
            {

            }

            public bool InclusionTest(Cell testCell)
            {
                return InclusionTest(testCell.pillOffset);
            }
            
            public bool InclusionTest(Curve testCurve)
            {
                RegionContainment test = Curve.PlanarClosedCurveRelationship(Outline, testCurve, Plane.WorldXY, Setups.OverlapTolerance);
                if (test == RegionContainment.BInsideA) return true;
                else return false;
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

            public void CreateConnectivityData()
            {
                foreach (Cell currentCell in cells)
                {
                    // If current cell is removed... go to next one.
                    if (currentCell.removed) continue;
                    List<Point3d> currentMidPoints = new List<Point3d>();
                    List<Curve> currentConnectionLines = new List<Curve>();
                    List<Cell> currenAdjacentCells = new List<Cell>();
                    foreach (Cell proxCell in cells)
                    {
                        // If proxCell is removed or currCell is same as proxCell, next cell...
                        if (proxCell.removed || proxCell.id != currentCell.id) continue;
                        LineCurve line = new LineCurve(currentCell.pillCenter, proxCell.pillCenter);
                        Point3d midPoint = line.PointAtNormalizedLength(0.5);
                        double t;
                        if (currentCell.voronoi.ClosestPoint(midPoint, out t, 1.000))
                        {
                            currenAdjacentCells.Add(proxCell);
                            currentConnectionLines.Add(line);
                            currentMidPoints.Add(midPoint);
                        }
                    }
                    currentCell.AddConnectionData(currenAdjacentCells, currentConnectionLines, currentMidPoints, outline);
                }
            }

        }

        public class Cell
        {

            public int id;

            // States
            public bool removed = false;
            //public bool removable = false;
            //public bool final_anchor = false;
            public bool possible_anchor = false;
            public double CornerDistance = 0;
            public double GuideDistance = 0;

            // Pill Stuff
            public NurbsCurve pill;
            public NurbsCurve pillOffset;
            public Point3d pillCenter;
            private AreaMassProperties pillProp;
            public NurbsCurve orientationCircle;

            // Connection and Adjacent Stuff
            public Curve voronoi;
            //!!connectionLines, proxLines, adjacentCells, samplePoints <- all same sizes, and order!!
            public List<Curve> connectionLines;
            public List<Curve> proxLines;
            public List<Cell> adjacentCells;
            public List<Point3d> samplePoints;

            public List<Curve> obstacles;

            public List<Curve> temp = new List<Curve>();
            // public List<Curve> temp2 = new List<Curve>();
            public List<CutData> cuttingData;
            public CutData bestCuttingData;
            // Int with best cutting index and new Blister for this cutting.

            public Cell(int _id, Curve _pill)
            {
                id = _id;
                // Prepare all needed Pill properties
                pill = _pill.ToNurbsCurve();
                pill = pill.Rebuild(pill.Points.Count, 3, true);
                CurveOrientation orient = pill.ClosedCurveOrientation(Vector3d.ZAxis);
                if (orient == CurveOrientation.Clockwise)
                {
                    pill.Reverse();
                }
                pillProp = AreaMassProperties.Compute(pill);
                pillCenter = pillProp.Centroid;

                // Create pill offset
                Curve[] ofCur = pill.Offset(Plane.WorldXY, Setups.BladeWidth / 2, 0.001, CurveOffsetCornerStyle.Sharp);
                if (ofCur.Length == 1)
                {
                    pillOffset = ofCur[0].ToNurbsCurve();
                }
                else
                {
                    throw new InvalidOperationException();
                }
            }

            public double CoordinateIndicator
            {
                get
                {
                    return pillCenter.X + pillCenter.Y * 10000;
                }
            }

            public List<LineCurve> GetTrimmedIsoRays()
            {
                List<LineCurve> output = new List<LineCurve>();
                foreach (CutData cData in cuttingData)
                {
                    output.AddRange(cData.TrimmedIsoRays);
                }
                return output;
            }

            public List<PolylineCurve> GetPaths()
            {
                List<PolylineCurve> output = new List<PolylineCurve>();
                foreach (CutData cData in cuttingData)
                {
                    output.Add(cData.Path);
                }
                return output;
            }

            public void SetDistance(LineCurve guideLine)
            {
                double t;
                guideLine.ClosestPoint(pillCenter, out t);
                GuideDistance = pillCenter.DistanceTo(guideLine.PointAt(t));
                double distance_A = pillCenter.DistanceTo(guideLine.PointAtStart);
                double distance_B = pillCenter.DistanceTo(guideLine.PointAtEnd);
                //Rhino.RhinoApp.WriteLine(String.Format("Dist_A: {0}, Dist_B: {1}", distance_A, distance_B));
                CornerDistance = Math.Min(distance_A, distance_B);

                //CornerDistance = distance_A + distance_B;
                //CornerDistance = pillCenter.DistanceTo(guideLine.PointAtStart);
            }

            public void AddConnectionData(List<Cell> cells, List<Curve> lines, List<Point3d> midPoints, Curve blister)
            {
                adjacentCells = new List<Cell>();
                samplePoints = new List<Point3d>();
                connectionLines = new List<Curve>();

                EstimateOrientationCircle(blister);
                int[] ind = Geometry.SortPtsAlongCurve(midPoints, orientationCircle);

                foreach (int id in ind)
                {
                    adjacentCells.Add(cells[id]);
                    connectionLines.Add(lines[id]);
                }
                proxLines = new List<Curve>();
                foreach (Cell cell in adjacentCells)
                {
                    Point3d ptA, ptB;
                    if (pillOffset.ClosestPoints(cell.pillOffset, out ptA, out ptB))
                    {
                        LineCurve proxLine = new LineCurve(ptA, ptB);
                        proxLines.Add(proxLine);
                        Point3d samplePoint = proxLine.PointAtNormalizedLength(0.5);
                        samplePoints.Add(samplePoint);
                    }
                }
            }

            public void RemoveConnectionData(PolylineCurve blister)
            {
                for (int i = 0; i < adjacentCells.Count; i++)
                {
                    adjacentCells[i].RemoveConnectionData(id, blister);
                }

            }

            protected void RemoveConnectionData(int cellId, Curve blister)
            {
                for (int i = 0; i < adjacentCells.Count; i++)
                {
                    if (adjacentCells[i].id == cellId)
                    {
                        adjacentCells.RemoveAt(i);
                        connectionLines.RemoveAt(i);
                        proxLines.RemoveAt(i);
                        samplePoints.RemoveAt(i);
                        i--;
                    }
                }
                SortData(blister);
            }

            public void EstimateOrientationCircle(Curve blister)
            {
                double circle_radius = pill.GetBoundingBox(false).Diagonal.Length / 2;
                orientationCircle = (new Circle(pillCenter, circle_radius)).ToNurbsCurve();
                Geometry.EditSeamBasedOnCurve(orientationCircle, blister);
            }

            public void SortData(Curve blister)
            {
                EstimateOrientationCircle(blister);
                int[] sortingIndexes = Geometry.SortPtsAlongCurve(samplePoints, orientationCircle);

                samplePoints = sortingIndexes.Select(index => samplePoints[index]).ToList();
                connectionLines = sortingIndexes.Select(index => connectionLines[index]).ToList();
                proxLines = sortingIndexes.Select(index => proxLines[index]).ToList();
                adjacentCells = sortingIndexes.Select(index => adjacentCells[index]).ToList();

                //samplePoints = samplePoints.Zip(sortingIndexes, Tuple.Create).OrderBy(i => i.Item2).Select(i => i.Item1).ToList();
                //connectionLines = connectionLines.Zip(sortingIndexes, Tuple.Create).OrderBy(i => i.Item2).Select(i => i.Item1).ToList();
                // proxLines = proxLines.Zip(sortingIndexes, Tuple.Create).OrderBy(i => i.Item2).Select(i => i.Item1).ToList();
                // adjacentCells = adjacentCells.Zip(sortingIndexes, Tuple.Create).OrderBy(i => i.Item2).Select(i => i.Item1).ToList();
            }

            // Get ProxyLines without lines pointed as Id
            public List<Curve> GetUniqueProxy(int id)
            {
                List<Curve> proxyLines = new List<Curve>();
                for (int i = 0; i < adjacentCells.Count; i++)
                {
                    if (adjacentCells[i].id != id)
                    {
                        proxyLines.Add(proxLines[i]);
                    }
                }
                return proxyLines;
            }

            public List<Curve> BuildObstacles()
            {
                List<Curve> limiters = new List<Curve>
        {
          pillOffset
          };
                for (int i = 0; i < adjacentCells.Count; i++)
                {
                    limiters.Add(adjacentCells[i].pillOffset);

                    List<Curve> prox = adjacentCells[i].GetUniqueProxy(id);
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

            public bool GenerateSimpleCuttingData(PolylineCurve blister)
            {
                // Initialise new Arrays
                obstacles = BuildObstacles();
                cuttingData = new List<CutData>();
                // Stage I - naive Cutting
                // Get cutting Directions

                List<LineCurve> isoLinesStage1 = GenerateIsoCurvesStage1(blister);
                if (isoLinesStage1.Count > 0)
                {
                    // Check each Ray seperatly
                    foreach (LineCurve ray in isoLinesStage1)
                    {
                        PolylineCurve newRay = Geometry.SnapToPoints(ray, blister, Setups.SnapDistance);
                        CutData cData = VerifyContinousPath(newRay, blister);
                        if (cData != null)
                        {
                            cData.TrimmedIsoRays = new List<LineCurve> { ray };
                            cuttingData.Add(cData);
                        }
                    }
                    PolygonBuilder(isoLinesStage1, blister);
                }
                //if (cuttingData.Count > 0) return true;
                List<LineCurve> isoLinesStage2 = GenerateIsoCurvesStage2(blister);
                if (isoLinesStage2.Count > 0)
                {
                    foreach (LineCurve ray in isoLinesStage2)
                    {
                        PolylineCurve newRay = Geometry.SnapToPoints(ray, blister, Setups.SnapDistance);
                        CutData cData = VerifyContinousPath(newRay, blister);
                        if (cData != null)
                        {
                            cData.TrimmedIsoRays = new List<LineCurve>() { ray };
                            cuttingData.Add(cData);
                        }
                    }
                    PolygonBuilder(isoLinesStage2, blister);
                }
                //if (cuttingData.Count > 0) return true;
                List<LineCurve> isoLinesStage3 = GenerateIsoCurvesStage3(blister);
                if (isoLinesStage3.Count > 0)
                {
                    foreach (LineCurve ray in isoLinesStage3)
                    {
                        PolylineCurve newRay = Geometry.SnapToPoints(ray, blister, Setups.SnapDistance);
                        CutData cData = VerifyContinousPath(newRay, blister);
                        if (cData != null)
                        {
                            cData.TrimmedIsoRays = new List<LineCurve> { ray };
                            cuttingData.Add(cData);
                        }
                    }
                    PolygonBuilder(isoLinesStage3, blister);
                }
                if (cuttingData.Count > 0) return true;
                else return false;
            }

            public bool GenerateAdvancedCuttingData(PolylineCurve blister)
            {
                // If all Stages 1-3 failed, start looking more!
                if (cuttingData.Count == 0)
                {
                    // if (cuttingData.Count == 0){
                    List<List<LineCurve>> isoLinesStage4 = GenerateIsoCurvesStage4(60, Setups.IsoRadius, blister);
                    if (isoLinesStage4.Count != 0)
                    {
                        List<List<LineCurve>> RaysCombinations = (List<List<LineCurve>>)isoLinesStage4.CartesianProduct();
                        for (int i = 0; i < RaysCombinations.Count; i++)
                        {
                            if (RaysCombinations[i].Count > 0)
                            {
                                PolygonBuilder(RaysCombinations[i], blister);
                            }
                        }
                    }
                }
                if (cuttingData.Count > 0) return true;
                else return false;
            }

            public void PolygonSelector()
            {
                cuttingData = cuttingData.OrderBy(x => x.CuttingCount).ToList();
                List<CutData> selected = cuttingData.Where(x => x.CuttingCount == cuttingData[0].CuttingCount).ToList();
                selected = selected.OrderBy(x => x.GetPerimeter()).ToList();
                bestCuttingData = selected[0];
                // bestCuttingData = cuttingData[0];
            }

            public void PolygonSelector2()
            {
                //List<PolylineCurve> output = new List<PolylineCurve>();


                // List<CutData> selected = cuttingData.Where(x => x.Count == cuttingData[0].Count).ToList();
                //foreach(CutData cData in selected){
                //  output.Add(cData.Path);
                // }
                // Here some more filtering, if more polygons hase same number of cutting segments...
                // Dummy get first...
                // if (selected.Count > 0){
                //   bestCuttingData = selected[0];
                // }
                // return output;
            }

            #region Polygon Builder Stuff

            private List<LineCurve> GenerateIsoCurvesStage1(PolylineCurve blister)
            {
                List<LineCurve> isoLines = new List<LineCurve>(samplePoints.Count);
                for (int i = 0; i < samplePoints.Count; i++)
                {
                    Vector3d direction = Vector3d.CrossProduct((connectionLines[i].PointAtEnd - connectionLines[i].PointAtStart), Vector3d.ZAxis);
                    //direction = StraigtenVector(direction);
                    LineCurve isoLine = Geometry.GetIsoLine(samplePoints[i], direction, Setups.IsoRadius, obstacles);
                    if (isoLine != null)
                    {
                        LineCurve t_ray = TrimIsoCurve(isoLine, samplePoints[i], blister);
                        if (t_ray != null)
                        {
                            isoLines.Add(t_ray);
                        }
                    }
                }
                return isoLines;
            }

            private List<LineCurve> GenerateIsoCurvesStage2(PolylineCurve blister)
            {
                List<LineCurve> isoLines = new List<LineCurve>(samplePoints.Count);
                for (int i = 0; i < samplePoints.Count; i++)
                {
                    Vector3d direction = Vector3d.CrossProduct((proxLines[i].PointAtEnd - proxLines[i].PointAtStart), Vector3d.ZAxis);
                    //direction = StraigtenVector(direction);
                    LineCurve isoLine = Geometry.GetIsoLine(samplePoints[i], direction, Setups.IsoRadius, obstacles);
                    if (isoLine != null)
                    {
                        LineCurve t_ray = TrimIsoCurve(isoLine, samplePoints[i], blister);
                        if (t_ray != null)
                        {
                            isoLines.Add(t_ray);
                        }
                    }
                }
                return isoLines;
            }

            private List<LineCurve> GenerateIsoCurvesStage3(PolylineCurve blister)
            {
                List<LineCurve> isoLines = new List<LineCurve>(samplePoints.Count);
                for (int i = 0; i < samplePoints.Count; i++)
                {
                    Vector3d direction = Vector3d.CrossProduct((proxLines[i].PointAtEnd - proxLines[i].PointAtStart), Vector3d.ZAxis);
                    Vector3d direction2 = Vector3d.CrossProduct((connectionLines[i].PointAtEnd - connectionLines[i].PointAtStart), Vector3d.ZAxis);
                    //Vector3d sum_direction = StraigtenVector(direction + direction2);
                    Vector3d sum_direction = direction + direction2;
                    LineCurve isoLine = Geometry.GetIsoLine(samplePoints[i], sum_direction, Setups.IsoRadius, obstacles);
                    if (isoLine != null)
                    {
                        LineCurve t_ray = TrimIsoCurve(isoLine, samplePoints[i], blister);
                        if (t_ray != null)
                        {
                            isoLines.Add(t_ray);
                        }
                    }
                }
                return isoLines;
            }

            private List<List<LineCurve>> GenerateIsoCurvesStage4(int count, double radius, PolylineCurve blister)
            {
                // Obstacles need to be calculated or updated earlier
                List<List<LineCurve>> isoLines = new List<List<LineCurve>>();
                for (int i = 0; i < samplePoints.Count; i++)
                {
                    Circle cir = new Circle(samplePoints[i], radius);
                    List<LineCurve> iLines = new List<LineCurve>();
                    ArcCurve arc = new ArcCurve(new Arc(cir, new Interval(0, Math.PI)));
                    double[] t = arc.DivideByCount(count, false);
                    for (int j = 0; j < t.Length; j++)
                    {
                        Point3d Pt = arc.PointAt(t[j]);
                        LineCurve ray = Geometry.GetIsoLine(samplePoints[i], Pt - samplePoints[i], Setups.IsoRadius, obstacles);
                        if (ray != null)
                        {
                            LineCurve t_ray = TrimIsoCurve(ray, samplePoints[i], blister);
                            if (t_ray != null)
                            {
                                iLines.Add(t_ray);
                            }
                        }
                    }
                    isoLines.Add(iLines);
                }
                return isoLines;
            }

            private Vector3d StraigtenVector(Vector3d vec)
            {
                Vector3d direction = vec;
                double angle = Vector3d.VectorAngle(vec, Vector3d.XAxis);
                if (angle <= Setups.AngleTolerance || angle >= Math.PI - Setups.AngleTolerance)
                {
                    direction = Vector3d.XAxis;
                }
                else if (angle <= (0.5 * Math.PI) + Setups.AngleTolerance && angle > (0.5 * Math.PI) - Setups.AngleTolerance)
                {
                    direction = Vector3d.YAxis;
                }
                return direction;
            }

            private LineCurve TrimIsoCurve(LineCurve ray, Point3d samplePoint, PolylineCurve blister)
            {
                LineCurve outLine = null;
                if (ray != null)
                {
                    Geometry.FlipIsoRays(orientationCircle, ray);
                    Tuple<List<Curve>, List<Curve>> result = Geometry.TrimWithRegion(ray, blister);
                    if (result.Item1.Count >= 1)
                    {
                        foreach (Curve crv in result.Item1)
                        {
                            double t;
                            if (crv.ClosestPoint(samplePoint, out t, 0.1))
                            {
                                LineCurve line = (LineCurve)crv;
                                if (line != null)
                                {
                                    //  flipIsoRays(orientationCircle, line);
                                    outLine = line;
                                }
                            }
                        }
                    }
                }
                return outLine;
            }
            /*
                private List<LineCurve> trimIsoCurves(List<LineCurve> rays, Curve blister){
                  List<LineCurve> outLine = new List<LineCurve>(rays.Count);
                  foreach (LineCurve ray in rays){
                    LineCurve line = trimIsoCurve(ray, blister);
                    if (line != null){
                      outLine.Add(line);
                    }
                  }
                  return outLine;
                }
              */

            private void PolygonBuilder(List<LineCurve> rays, PolylineCurve blister)
            {
                //List<LineCurve> cutters = trimIsoCurves(rays, blister);
                //Generate Combinations array
                List<List<LineCurve>> combinations = Combinators.Combinators.UniqueCombinations(rays, 2);
                // Loop over combinations
                for (int combId = 0; combId < combinations.Count; combId++)
                {
                    // Generate Path
                    // W tej cześci należt zmienic duzo, aby można było w trakcie wycinaia otrzymac dwa kawałki blista osobnego...
                    PolylineCurve curveToCheck = PathBuilder(combinations[combId]);
                    // HEREEEEEE !!!!! temp.Add

                    if (curveToCheck != null)
                    {
                        //temp.Add(curveToCheck);
                        // Remove very short segments
                        Polyline pLineToCheck = curveToCheck.ToPolyline();
                        pLineToCheck.DeleteShortSegments(Setups.CollapseTolerance);
                        // Look if end of cutting line is close to existing point on blister. If tolerance is smaller snap to this point
                        curveToCheck = pLineToCheck.ToPolylineCurve();
                        curveToCheck = Geometry.SnapToPoints(curveToCheck, blister, Setups.SnapDistance);
                        temp.Add(curveToCheck);
                        //curveToCheck.RemoveShortSegments(Setups.CollapseTolerance);
                        // Verify if path is cuttable
                        CutData cutData = VerifyContinousPath(curveToCheck, blister);
                        if (cutData != null)
                        {
                            cutData.TrimmedIsoRays = combinations[combId];
                            cuttingData.Add(cutData);
                        }
                    }
                }
            }

            private PolylineCurve PathBuilder(List<LineCurve> cutters)
            {
                PolylineCurve pLine = null;
                // If more curves, generate one!
                if (cutters.Count > 1)
                {
                    // Perform intersectiona based on combination array.
                    List<CurveIntersections> intersectionsData = new List<CurveIntersections>();
                    for (int interId = 1; interId < cutters.Count; interId++)
                    {
                        CurveIntersections inter = Intersection.CurveCurve(cutters[interId - 1], cutters[interId], Setups.IntersectionTolerance, Setups.OverlapTolerance);
                        // If no intersection, at any curve, break all testing process
                        if (inter.Count == 0)
                        {
                            break;
                        }
                        else
                        {
                            //If exist, Store it
                            intersectionsData.Add(inter);
                        }
                    }
                    // If intersection are equal to curveCount-1, this mean, all cuvre where involve.. so..
                    if (intersectionsData.Count == cutters.Count - 1)
                    {
                        //Create JoinedCurve from all interesection data
                        List<Point3d> polyLinePoints = new List<Point3d> { cutters[0].PointAtStart };
                        for (int i = 0; i < intersectionsData.Count; i++)
                        {
                            polyLinePoints.Add(intersectionsData[i][0].PointA);
                        }
                        polyLinePoints.Add(cutters[cutters.Count - 1].PointAtEnd);
                        pLine = new PolylineCurve(polyLinePoints);
                    }
                }
                return pLine;
            }
            // TODO: Test that stuff...
            private CutData VerifyPartialPath(List<LineCurve> cutters, Curve blister)
            {

                List<CurveIntersections> intersectionsData = new List<CurveIntersections>();
                List<Point3d> intersectionPoints = new List<Point3d>();
                for (int interId = 0; interId < cutters.Count; interId++)
                {
                    CurveIntersections inter = Intersection.CurveCurve(cutters[interId], blister, Setups.IntersectionTolerance, Setups.OverlapTolerance);
                    if (inter.Count == 0)
                    {
                        break;
                    }
                    else
                    {
                        //If exist, Store it
                        intersectionsData.Add(inter);
                        for (int i = 0; i < inter.Count; i++)
                        {
                            intersectionPoints.Add(inter[i].PointA);
                            intersectionPoints.Add(inter[i].PointB);
                        }
                    }
                }
                // Close Curve
                intersectionPoints.Add(intersectionPoints[0]);
                // Sort points around pill
                Point3d[] sortedInterPoints = Geometry.SortPtsAlongCurve(intersectionPoints.ToArray(), orientationCircle);
                // Generate Pline...
                PolylineCurve pLine = new PolylineCurve(sortedInterPoints);
                return null;
            }

            private CutData VerifyContinousPath(PolylineCurve pCrv, Curve blister)
            {
                CutData data = null;
                if (pCrv != null)
                {
                    // Check if curve is not self-intersecting
                    CurveIntersections selfChecking = Intersection.CurveSelf(pCrv, Setups.IntersectionTolerance);
                    if (selfChecking.Count == 0)
                    {
                        // Błędne załozenia w przypadki przeciacia blistra na 2 części
                        // Check if this curve creates closed polygon with blister edge.
                        CurveIntersections blisterInter = Intersection.CurveCurve(pCrv, blister, Setups.IntersectionTolerance, Setups.OverlapTolerance);
                        // If both ends of Plyline cuts blister, it will create close polygon.
                        if (blisterInter.Count == 2)
                        {

                            // Get part of blister which is between Plyline ends.
                            double[] ts = new double[blisterInter.Count];
                            for (int i = 0; i < blisterInter.Count; i++)
                            {
                                ts[i] = blisterInter[i].ParameterB;
                            }
                            List<Curve> commonParts = new List<Curve>(2) { blister.Trim(ts[0], ts[1]), blister.Trim(ts[1], ts[0]) };
                            // Look for shorter part.
                            List<Curve> blisterParts = commonParts.OrderBy(x => x.GetLength()).ToList();
                            temp.Add(pCrv);
                            //Curve common_part = commonParts.OrderBy(x => x.GetLength()).ToList()[0];

                            //Join Curve into closed polygon
                            Curve[] pCurve = Curve.JoinCurves(new Curve[2] { blisterParts[0], pCrv });
                            Polyline polygon = new Polyline();
                            if (pCurve.Length == 1)
                            {
                                pCurve[0].TryGetPolyline(out polygon);
                            }
                            //temp.Add(polygon.ToPolylineCurve());
                            // Check if polygon is closed and no. vertecies is bigger then 2
                            if (polygon.Count > 3 && polygon.IsClosed)
                            {

                                // Check if polygon is "surounding" Pill
                                PolylineCurve poly = polygon.ToPolylineCurve();
                                RegionContainment test = Curve.PlanarClosedCurveRelationship(poly, pill, Plane.WorldXY, 0.01);
                                // TODO: Check if any other pill is NOT inside this region
                                if (test == RegionContainment.BInsideA)
                                {
                                    // If yes, generate newBlister
                                    Curve[] bCurve = Curve.JoinCurves(new Curve[2] { blisterParts[1], pCrv });
                                    Polyline newBlister = new Polyline();
                                    if (bCurve.Length == 1)
                                    {
                                        bCurve[0].TryGetPolyline(out newBlister);
                                    }
                                    data = new CutData(polygon.ToPolylineCurve(), newBlister.ToPolylineCurve(), pCrv);
                                }
                            }
                        }
                    }
                }
                return data;
            }
            #endregion

            public void GetInstructions()
            {
                //TO IMPLEMENT
            }

        }

        public class CutData
        {
            // public List<PolylineCurve> Paths = new List<PolylineCurve>();
            // public List<List<LineCurve>> IsoRays = new List<List<LineCurve>>();
            // public List<PolylineCurve> Polygons = new List<PolylineCurve>();

            private List<LineCurve> trimmedIsoRays;
            private List<LineCurve> isoRays;
            public PolylineCurve Path;
            public PolylineCurve Polygon;
            public PolylineCurve NewBlister;
            public List<LineCurve> bladeFootPrint;

            public CutData()
            {
                TrimmedIsoRays = new List<LineCurve>();
                IsoRays = new List<LineCurve>();
                bladeFootPrint = new List<LineCurve>();
            }

            public CutData(PolylineCurve polygon, PolylineCurve newBlister, PolylineCurve path)
            {
                Path = path;
                Polygon = polygon;
                NewBlister = newBlister;
                TrimmedIsoRays = new List<LineCurve>();
                IsoRays = new List<LineCurve>();
                bladeFootPrint = new List<LineCurve>();
                GenerateBladeFootPrint();
            }

            public double GetCuttingLength
            {
                get
                {
                    return Path.ToPolyline().Length;
                }
            }

            public int CuttingCount
            {
                get
                {
                    int count = 0;
                    foreach (Line line in Path.ToPolyline().GetSegments())
                    {
                        count += GetCuttingPartsCount(line);
                    }
                    return count;
                }
            }

            public int Count
            {
                get
                {
                    return Polygon.PointCount - 1;
                }
            }

            public List<LineCurve> TrimmedIsoRays
            {
                set
                {
                    trimmedIsoRays = value;
                }
                get
                {
                    return trimmedIsoRays;
                }
            }

            public List<LineCurve> IsoRays
            {
                set
                {
                    isoRays = value;
                }
                get
                {
                    return isoRays;
                }
            }

            public bool Cuttable
            {
                get
                {
                    if (isoRays.Count > 0) return true;
                    else return false;
                }
            }

            public void GenerateBladeFootPrint()
            {

                if (Polygon != null && Path != null)
                {
                    Polyline path = Path.ToPolyline();
                    if (path.SegmentCount > 0)
                    {
                        Line[] segments = path.GetSegments();
                        for (int i = 0; i < segments.Length; i++)
                        {
                            Line seg = segments[i];
                            // First Segment. End point is on the blister Edge
                            if (i < segments.Length - 1)
                            {
                                int parts = GetCuttingPartsCount(seg);
                                Point3d cutStartPt = seg.To + (seg.UnitTangent * Setups.BladeTol);
                                for (int j = 0; j < parts; j++)
                                {
                                    Point3d cutEndPt = cutStartPt + (seg.UnitTangent * -Setups.BladeLength);
                                    Line cutPrint = new Line(cutStartPt, cutEndPt);
                                    bladeFootPrint.Add(new LineCurve(cutPrint));
                                    cutStartPt = cutEndPt + (seg.UnitTangent * Setups.BladeTol);
                                }
                            }

                            // Last segment.
                            if (i == segments.Length - 1)
                            {
                                int parts = GetCuttingPartsCount(seg);
                                Point3d cutStartPt = seg.From - (seg.UnitTangent * Setups.BladeTol);
                                for (int j = 0; j < parts; j++)
                                {
                                    Point3d cutEndPt = cutStartPt + (seg.UnitTangent * Setups.BladeLength);
                                    Line cutPrint = new Line(cutStartPt, cutEndPt);
                                    bladeFootPrint.Add(new LineCurve(cutPrint));
                                    cutStartPt = cutEndPt - (seg.UnitTangent * Setups.BladeTol);
                                }
                            }
                        }
                    }
                    else
                    {
                        //Errror
                    }
                }
            }

            private int GetCuttingPartsCount(Line line)
            {
                return (int)Math.Ceiling(line.Length / (Setups.BladeLength - (2 * Setups.BladeTol)));
            }

            public double GetArea()
            {
                AreaMassProperties prop = AreaMassProperties.Compute(Polygon);
                return prop.Area;
            }

            public double GetPerimeter()
            {
                return Polygon.GetLength();
            }
        }

        static class Setups
        {
            public const double GeneralTolerance = 0.0001;
            public const double IntersectionTolerance = 0.0001;
            public const double OverlapTolerance = 0.0001;
            public const double BladeLength = 44.0;
            public const double BladeTol = 2.0;
            public const double BladeWidth = 3.0;
            public const double CartesianThicknes = 3.0;
            public const double IsoRadius = 1000.0;
            public const double AngleTolerance = (0.5 * Math.PI) * 0.2;
            public const double CollapseTolerance = 1.0; // if path segment is shorter then this, it will be collapsed
            public const double SnapDistance = 0.5; // if path segment is shorter then this, it will be collapsed
        }

        static class Geometry
        {
            public static PolylineCurve SnapToPoints(PolylineCurve moving, PolylineCurve stationary, double tolerance)
            {
                Polyline pMoving = moving.ToPolyline();
                PointCloud fixedPoints = new PointCloud(stationary.ToPolyline());
                // Check start point
                int s_index = fixedPoints.ClosestPoint(pMoving.First);
                if (s_index != -1)
                {
                    if (fixedPoints[s_index].Location.DistanceTo(pMoving.First) < tolerance)
                    {
                        pMoving[0] = fixedPoints[s_index].Location;
                    }
                }
                // Check end point
                int e_index = fixedPoints.ClosestPoint(pMoving.Last);
                if (e_index != -1)
                {
                    if (fixedPoints[e_index].Location.DistanceTo(pMoving.Last) < tolerance)
                    {
                        pMoving[pMoving.Count - 1] = fixedPoints[e_index].Location;
                    }
                }
                return pMoving.ToPolylineCurve();

            }

            public static PolylineCurve SnapToPoints(LineCurve moving, PolylineCurve stationary, double tolerance)
            {
                return Geometry.SnapToPoints(new PolylineCurve(new Point3d[] { moving.PointAtStart, moving.PointAtEnd }), stationary, tolerance);
            }

            public static void FlipIsoRays(NurbsCurve guideCrv, LineCurve crv)
            {
                Curve temp = crv.Extend(CurveEnd.Both, 10000, CurveExtensionStyle.Line);
                LineCurve extended = new LineCurve(temp.PointAtStart, temp.PointAtEnd);
                Point3d guidePt, crvPt;
                if (guideCrv.ClosestPoints(extended, out guidePt, out crvPt))
                {
                    double guide_t;
                    if (guideCrv.ClosestPoint(guidePt, out guide_t, 1.0))
                    {
                        Vector3d guide_v = guideCrv.TangentAt(guide_t);
                        Vector3d crv_v = crv.Line.UnitTangent;
                        if (guide_v * crv_v < 0.01)
                        {
                            crv.Reverse();
                        }
                    }
                }
            }

            public static LineCurve GetIsoLine(Point3d source, Vector3d direction, double radius, List<Curve> obstacles)
            {

                LineCurve rayA = new LineCurve(new Line(source, direction, radius));
                LineCurve rayB = new LineCurve(new Line(source, -direction, radius));

                List<LineCurve> rays = new List<LineCurve> { rayA, rayB };
                List<Point3d> pts = new List<Point3d>(2);

                foreach (LineCurve ray in rays)
                {
                    SortedList<double, Point3d> interData = new SortedList<double, Point3d>();
                    for (int obId = 0; obId < obstacles.Count; obId++)
                    {
                        CurveIntersections inter = Intersection.CurveCurve(obstacles[obId], ray, Setups.IntersectionTolerance, Setups.OverlapTolerance);
                        if (inter.Count > 0)
                        {
                            foreach (IntersectionEvent cross in inter)
                            {
                                interData.Add(cross.ParameterB, cross.PointB);
                            }
                        }
                    }
                    LineCurve rayent = new LineCurve(ray);
                    if (interData.Count > 0)
                    {
                        pts.Add(interData[interData.Keys[0]]);
                    }
                    else
                    {
                        pts.Add(rayent.PointAtEnd);
                    }
                }
                LineCurve isoLine = new LineCurve(pts[0], pts[1]);
                if (isoLine.GetLength() >= Setups.BladeLength) return isoLine;
                else return null;
            }

            public static void EditSeamBasedOnCurve(Curve editCrv, Curve baseCrv)
            {
                Point3d thisPt, otherPt;
                if (editCrv.ClosestPoints(baseCrv, out thisPt, out otherPt))
                {
                    double this_t;
                    editCrv.ClosestPoint(thisPt, out this_t);
                    editCrv.ChangeClosedCurveSeam(this_t);
                }
            }

            public static List<Curve> RemoveDuplicateCurves(List<Curve> crvs)
            {
                List<Curve> uniqueCurves = new List<Curve>();
                for (int i = 0; i < crvs.Count; i++)
                {
                    bool unique = true;
                    for (int j = i + 1; j < crvs.Count; j++)
                    {
                        if (GeometryBase.GeometryEquals(crvs[i], crvs[j]))
                        {
                            unique = false;
                        }
                    }
                    if (unique)
                    {
                        uniqueCurves.Add(crvs[i]);
                    }
                }
                return uniqueCurves;
            }

            public static int[] SortPtsAlongCurve(List<Point3d> pts, Curve crv)
            {//out Point3d[] points
                int L = pts.Count;//points = pts.ToArray();
                int[] iA = new int[L]; double[] tA = new double[L];
                for (int i = 0; i < L; i++)
                {
                    double t;
                    crv.ClosestPoint(pts[i], out t);
                    iA[i] = i; tA[i] = t;
                }
                Array.Sort(tA, iA);// Array.Sort(tA, iA);//Array.Sort(tA, points);
                return iA;
            }

            public static Point3d[] SortPtsAlongCurve(Point3d[] pts, Curve crv)
            {
                int L = pts.Length;
                Point3d[] points = pts;
                int[] iA = new int[L]; double[] tA = new double[L];
                for (int i = 0; i < L; i++)
                {
                    double t;
                    crv.ClosestPoint(pts[i], out t);
                    iA[i] = i; tA[i] = t;
                }
                Array.Sort(tA, points);
                return points;
            }

            public static Polyline ConvexHull(List<Point3d> pts)
            {
                List<GH_Point> po = new List<GH_Point>();
                foreach (Point3d pt in pts)
                {
                    po.Add(new GH_Point(pt));
                }
                return Grasshopper.Kernel.Geometry.ConvexHull.Solver.ComputeHull(po);
            }

            public static List<CurveIntersections> CurveCurveIntersection(Curve baseCrv, List<Curve> otherCrv)
            {
                List<CurveIntersections> allIntersections = new List<CurveIntersections>(otherCrv.Count);
                for (int i = 0; i < otherCrv.Count; i++)
                {
                    CurveIntersections inter = Intersection.CurveCurve(baseCrv, otherCrv[i], Setups.IntersectionTolerance, Setups.OverlapTolerance);
                    if (inter.Count > 0)
                    {
                        allIntersections.Add(inter);
                    }
                }
                return allIntersections;
            }

            public static List<List<CurveIntersections>> CurveCurveIntersection(List<Curve> baseCrv, List<Curve> otherCrv)
            {
                List<List<CurveIntersections>> allIntersections = new List<List<CurveIntersections>>(baseCrv.Count);
                for (int i = 0; i < baseCrv.Count; i++)
                {
                    List<CurveIntersections> currentInter = new List<CurveIntersections>(otherCrv.Count);
                    for (int j = 0; j < otherCrv.Count; j++)
                    {
                        currentInter.Add(Intersection.CurveCurve(baseCrv[i], otherCrv[j], Setups.IntersectionTolerance, Setups.OverlapTolerance));
                    }
                    allIntersections.Add(currentInter);
                }
                return allIntersections;
            }

            public static List<CurveIntersections> MultipleCurveIntersection(List<Curve> curves)
            {
                List<CurveIntersections> allIntersections = new List<CurveIntersections>();
                for (int i = 0; i < curves.Count; i++)
                {
                    for (int j = i + 1; j < curves.Count; j++)
                    {
                        CurveIntersections inter = Intersection.CurveCurve(curves[i], curves[j], Setups.IntersectionTolerance, Setups.OverlapTolerance);
                        if (inter.Count > 0)
                        {
                            allIntersections.Add(inter);
                        }
                    }
                }
                return allIntersections;
            }

            public static Tuple<List<Curve>, List<Curve>> TrimWithRegion(Curve crv, Curve region)
            {
                List<Curve> inside = new List<Curve>();
                List<Curve> outside = new List<Curve>();
                CurveIntersections inter = Intersection.CurveCurve(crv, region, 0.001, 0.001);
                if (inter.Count > 0)
                {
                    List<double> t_param = new List<double>();
                    foreach (IntersectionEvent i in inter)
                    {
                        t_param.Add(i.ParameterA);
                    }
                    t_param.Sort();
                    Curve[] splitedCrv = crv.Split(t_param);
                    if (splitedCrv.Length > 0)
                    {
                        foreach (Curve part_crv in splitedCrv)
                        {
                            Point3d testPt = part_crv.PointAtNormalizedLength(0.5);
                            PointContainment result = region.Contains(testPt, Plane.WorldXY, 0.0001);
                            if (result == PointContainment.Inside) inside.Add(part_crv);
                            else if (result == PointContainment.Outside) outside.Add(part_crv);
                            else if (result == PointContainment.Unset) throw new InvalidOperationException("Unset");
                            else throw new InvalidOperationException("Trim Failed");
                        }
                    }
                    else throw new InvalidOperationException("Trim Failed on Split");
                }
                // IF no intersection...
                else
                {
                    Point3d testPt = crv.PointAtNormalizedLength(0.5);
                    PointContainment result = region.Contains(testPt, Plane.WorldXY, 0.0001);
                    if (result == PointContainment.Inside) inside.Add(crv);
                    else if (result == PointContainment.Outside) outside.Add(crv);
                    else if (result == PointContainment.Unset) throw new InvalidOperationException("Unset");
                    else throw new InvalidOperationException("Trim Failed");
                }
                return Tuple.Create(inside, outside);
            }

            public static Tuple<List<Curve>, List<Curve>> TrimWithRegions(Curve crv, List<Curve> regions)
            {
                List<Curve> inside = new List<Curve>();
                List<Curve> outside = new List<Curve>();
                List<CurveIntersections> inter = CurveCurveIntersection(crv, regions);
                SortedList<double, Point3d> data = new SortedList<double, Point3d>();
                foreach (CurveIntersections crvInter in inter)
                {
                    foreach (IntersectionEvent inEv in crvInter)
                    {
                        data.Add(inEv.ParameterA, inEv.PointA);
                    }
                }
                List<Curve> splitedCrv = crv.Split(data.Keys).ToList<Curve>();
                // If ther is intersection...
                if (splitedCrv.Count > 0)
                {
                    // Look for all inside parts of cure and move them to inside list.
                    for (int i = 0; i < splitedCrv.Count; i++)
                    {
                        Curve part_crv = splitedCrv[i];
                        Point3d testPt = part_crv.PointAtNormalizedLength(0.5);
                        foreach (Curve region in regions)
                        {
                            PointContainment result = region.Contains(testPt, Plane.WorldXY, 0.0001);
                            if (result == PointContainment.Inside)
                            {
                                inside.Add(part_crv);
                                splitedCrv.RemoveAt(i);
                                i--;
                                break;
                            }

                        }
                    }
                    // add leftovers to outside list
                    outside.AddRange(splitedCrv);
                    // IF no intersection...
                }
                else
                {
                    foreach (Curve region in regions)
                    {
                        Point3d testPt = crv.PointAtNormalizedLength(0.5);
                        PointContainment result = region.Contains(testPt, Plane.WorldXY, 0.0001);
                        if (result == PointContainment.Inside)
                        {
                            inside.Add(crv);
                        }
                    }
                    if (inside.Count == 0)
                    {
                        outside.Add(crv);
                    }
                }
                return Tuple.Create(inside, outside);
            }

            public static List<Curve> SplitRegion(Curve region, Curve splittingCurve)
            {
                List<double> region_t_params = new List<double>();
                List<double> splitter_t_params = new List<double>();
                CurveIntersections intersection = Intersection.CurveCurve(splittingCurve, region, Setups.IntersectionTolerance, Setups.OverlapTolerance);
                if (!region.IsClosed)
                {
                    return null;
                }

                if (intersection == null)
                {
                    return null;
                }
                if (intersection.Count % 2 != 0 || intersection.Count == 0)
                {
                    return null;
                }
                foreach (IntersectionEvent inter in intersection)
                {
                    splitter_t_params.Add(inter.ParameterA);
                    region_t_params.Add(inter.ParameterB);
                }
                splitter_t_params.Sort();
                region_t_params.Sort();
                Curve[] splited_splitter = splittingCurve.Split(splitter_t_params);
                List<Curve> sCurve = new List<Curve>();
                foreach (Curve crv in splited_splitter)
                {
                    Point3d testPt = crv.PointAtNormalizedLength(0.5);
                    PointContainment result = region.Contains(testPt, Plane.WorldXY, 0.0001);
                    if (result == PointContainment.Inside)
                    {
                        sCurve.Add(crv);
                    }
                }

                // If ther is only one splitter
                List<Curve> pCurve = new List<Curve>();
                if (sCurve.Count == 1 && region_t_params.Count == 2)
                {
                    List<Curve> splited_region = region.Split(region_t_params).ToList();
                    if (splited_region.Count == 2)
                    {
                        foreach (Curve out_segment in splited_region)
                        {
                            Curve[] temp = Curve.JoinCurves(new List<Curve>() { out_segment, sCurve[0] });
                            if (temp.Length != 1)
                            {
                                break;
                            }
                            if (temp[0].IsClosed)
                            {
                                pCurve.Add(temp[0]);
                            }
                        }
                    }
                }
                else
                {
                    // Use recursieve option
                    pCurve.AddRange(SplitRegion(region, sCurve));
                }
                return pCurve;
            }

            public static List<Curve> SplitRegion(Curve region, List<Curve> splitters)
            {
                //List<PolylineCurve> out_regions = new List<PolylineCurve>();
                List<Curve> temp_regions = new List<Curve>();
                temp_regions.Add(region);

                foreach (PolylineCurve splitter in splitters)
                {
                    List<Curve> current_temp_regions = new List<Curve>();
                    foreach (Curve current_region in temp_regions)
                    {
                        List<Curve> choped_region = SplitRegion(current_region, splitter);
                        if (choped_region != null)
                        {
                            foreach (Curve _region in choped_region)
                            {
                                Curve[] c_inter = Curve.CreateBooleanIntersection(_region, region, Setups.GeneralTolerance);
                                foreach (Curve inter_curve in c_inter)
                                {
                                    current_temp_regions.Add(inter_curve);
                                }
                            }
                        }
                        else
                        {
                            if (region.Contains(AreaMassProperties.Compute(current_region).Centroid, Plane.WorldXY, Setups.GeneralTolerance) == PointContainment.Inside)
                            {
                                current_temp_regions.Add(current_region);
                            }
                        }
                    }
                    temp_regions = new List<Curve>(current_temp_regions);
                }
                return temp_regions;
            }

            public static List<PolylineCurve> IrregularVoronoi(List<Cell> cells, Polyline blister, int resolution = 50, double tolerance = 0.05)
            {
                Grasshopper.Kernel.Geometry.Node2List n2l = new Grasshopper.Kernel.Geometry.Node2List();
                List<Grasshopper.Kernel.Geometry.Node2> outline = new List<Grasshopper.Kernel.Geometry.Node2>();
                foreach (Cell cell in cells)
                {
                    Point3d[] pts;
                    cell.pill.DivideByCount(resolution, false, out pts);
                    foreach (Point3d pt in pts)
                    {
                        n2l.Append(new Grasshopper.Kernel.Geometry.Node2(pt.X, pt.Y));
                    }
                }

                foreach (Point3d pt in blister)
                {
                    outline.Add(new Grasshopper.Kernel.Geometry.Node2(pt.X, pt.Y));
                }

                Connectivity del_con = Grasshopper.Kernel.Geometry.Delaunay.Solver.Solve_Connectivity(n2l, 0.001, true);
                List<Cell2> voronoi = Grasshopper.Kernel.Geometry.Voronoi.Solver.Solve_Connectivity(n2l, del_con, outline);

                List<PolylineCurve> vCells = new List<PolylineCurve>();
                for (int i = 0; i < cells.Count; i++)
                {
                    List<Point3d> pts = new List<Point3d>();
                    for (int j = 0; j < resolution - 1; j++)
                    {
                        int glob_index = (i * (resolution - 1)) + j;
                        // vor.Add(voronoi[glob_index].ToPolyline());
                        Point3d[] vert = voronoi[glob_index].ToPolyline().ToArray();
                        foreach (Point3d pt in vert)
                        {
                            PointContainment result = cells[i].pill.Contains(pt, Rhino.Geometry.Plane.WorldXY, 0.0001);
                            if (result == PointContainment.Outside)
                            {
                                pts.Add(pt);
                            }
                        }
                    }

                    Polyline poly = new Polyline(SortPtsAlongCurve(Point3d.CullDuplicates(pts, 0.001), cells[i].pill));
                    poly.Add(poly[0]);
                    poly.ReduceSegments(tolerance);
                    vCells.Add(new PolylineCurve(poly));
                    cells[i].voronoi = new PolylineCurve(poly);
                }
                return vCells;
            }
        }
        // </Custom additional code>
        #endregion

    }
}