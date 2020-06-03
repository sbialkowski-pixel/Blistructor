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
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;

using Blistructor;


using log4net;
using log4net.Appender;
using log4net.Core;
using log4net.Filter;
using log4net.Layout;
using log4net.Repository.Hierarchy;

using Newtonsoft.Json.Linq;

using RhGeo = Rhino.Geometry;
using PixGeo = Pixel.Geometry;

/*
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.CV.Util;

using GH_Delanuey = Grasshopper.Kernel.Geometry.Delaunay;
using GH_Voronoi = Grasshopper.Kernel.Geometry.Voronoi;
*/


// </Custom using>

/// Unique namespace, so visual studio won't throw any errors about duplicate definitions.
namespace BlistructorGH
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

        private void RunScript(string PillsPath, string BlisterPath, Point3d calibPt, int L1,int L2, ref object LeftOvers, ref object InitData, ref object CAState, ref object QAState, ref object CuttedData, ref object JSON, ref object UnfinishedCut, ref object AnchorGuideLine, ref object JawPoints, ref object anchPred)

       // private void RunScript(List<Polyline> Pills, Polyline Blister, int cellId, int iter1, int iter2, ref object A, ref object B, ref object C, ref object D, ref object E, ref object F, ref object G, ref object H, ref object I, ref object AA)
        {
            // <Custom code>
           //string assPath = @"D:\PIXEL\Blistructor\Blistructor\bin\Release\Blistructor.dll";
           //var blistructorAssembly = Assembly.Load(File.ReadAllBytes(assPath));
           //var structorType = blistructorAssembly.GetType("Blistructor.MultiBlisterGH");


            ILog log = LogManager.GetLogger("Main");
            Logger.Setup();
            Logger.ClearAllLogFile();

            log.Info("====NEW RUN====");


            try
            {
               //dynamic structor = Activator.CreateInstance(structorType, L1, L2);
                MultiBlisterGH structor = new MultiBlisterGH(L1, L2);



                JSON = structor.CutBlister(PillsPath, BlisterPath);
                // G = structor.CutBlister(Pills, Blister);
                if (structor.Queue.Count != 0)
                {
                    InitData = structor.GetQueuePillDataGH;
                    UnfinishedCut = structor.GetUnfinishedCutDataGH;
                   LeftOvers = structor.GetLeftOversGH;
                 }
                CuttedData = structor.GetCuttedPillDataGH;
                // QAState = structor.GetQueuedAnchorStatus;
               //  CAState = structor.GetCuttedAnchorStatus;
               
               //  LeftOvers = structor.GetLeftOversGH;
                //AA = structor.pillsss;


                JawPoints = structor.anchor.anchors.Select(aP => aP.location).ToList()  ;
                AnchorGuideLine = structor.anchor.GrasperPossibleLocation;
               // anchPred = structor.anchor.GrasperPossibleLocation;

            }
            catch (Exception ex)
            {
                log.Error("Main Error catcher", ex);
                throw;
            }
            // </Custom code>
        }
        #endregion

        #region Additional
        // <Custom additional code>
         /*
        public enum CuttingState
        {
            [Description("Cutting successful")]
            CTR_SUCCESS = 0,
            [Description("Pills are to tight. Cutting aborted.")]
            CTR_TO_TIGHT = 1,
            [Description("One pill on blister only. Nothing to do.")]
            CTR_ONE_PILL = 2,
            [Description("Cutting Failed. Cannot Found cutting paths for all pills. Blister is to complicated or it is uncuttable.")]
            CTR_FAILED = 3,
            [Description("Blister side to small to pick by both graspers or No place for graspers.")]
            CTR_ANCHOR_LOCATION_ERR = 4,
            [Description("Other Error. Check log file")]
            CTR_OTHER_ERR = 5,
            [Description("Unknown")]
            CTR_UNSET = -1
        };
        */

        /*
        public class MultiBlister
        {
            private static readonly ILog log = LogManager.GetLogger("Main.Blistructor");
            public PolylineCurve mainOutline;
            public List<PolylineCurve> pillsss;

            private int loopTolerance = 5;
            public List<Blister> Queue;
            public List<Blister> Cutted;
            public Point3d knifeLastPoint = new Point3d();
            public Anchor anchor;
            public List<Curve> worldObstacles;
            int cellLimit;
            int mainLimit;

            public MultiBlister(int Limit1, int Limit2)
            {
                cellLimit = Limit2;
                mainLimit = Limit1;
                Queue = new List<Blister>();
                Cutted = new List<Blister>();
            }

            public int CuttableCellsLeft { get
                {
                    int counter = 0;
                    foreach (Blister blister in Queue)
                    {
                        counter += blister.Cells.Select(cell => cell.State).Where(state => state == CellState.Queue || state == CellState.Alone).ToList().Count;
                    }
                    return counter;
                }
            }

            #region PROPERTIES

            public DataTree<Curve> GetCuttedPillDataGH
            {
                get
                {
                    DataTree<Curve> out_data = new DataTree<Curve>();
                    //List<List<Curve>> out_data = new List<List<Curve>>();
                    for (int i = 0; i < Cutted.Count; i++)
                    {
                        Blister cuttedBlister = Cutted[i];
                        Cell cuttedCell = Cutted[i].Cells[0];
                       // if (cuttedCell.cuttingData == null) continue;
                       // if (cuttedCell.cuttingData.Count == 0) continue;
                        // BlisterStuff
                        out_data.Add(cuttedBlister.Outline, new GH_Path(i, 0, 0));
                        out_data.AddRange(cuttedBlister.GetLeftOvers(), new GH_Path(i, 0, 1));
                        // Cutting Data
                        out_data.AddRange(cuttedBlister.GetCuttingLines(), new GH_Path(i, 1, 0));
                        out_data.AddRange(cuttedBlister.GetCuttingPath(), new GH_Path(i, 1, 1));
                        out_data.AddRange(cuttedBlister.GetIsoRays(), new GH_Path(i, 1, 2));
                        if (cuttedCell.cuttingData != null) out_data.AddRange(cuttedCell.cuttingData.Select(cData => cData.Polygon), new GH_Path(i, 1, 3));
                        else out_data.AddRange(new List<Curve>(), new GH_Path(i, 1, 3));
                        // Cell Data
                        out_data.AddRange(cuttedBlister.GetPills(false), new GH_Path(i, 2, 0));
                        out_data.AddRange(cuttedCell.connectionLines, new GH_Path(i, 2, 1));
                        out_data.AddRange(cuttedCell.proxLines, new GH_Path(i, 2, 2));
                        if (cuttedCell.cuttingData != null) out_data.AddRange(cuttedCell.obstacles, new GH_Path(i, 2, 3));
                        else out_data.AddRange(new List<Curve>(), new GH_Path(i, 2, 3));
                        out_data.Add(cuttedCell.voronoi, new GH_Path(i, 2, 4));
                    }
                    return out_data;
                }
            }

            public List<PolylineCurve> GetCuttedPolygons
            {
                get
                {
                    List<PolylineCurve> polygons = new List<PolylineCurve>(Cutted.Count);
                    foreach (Blister blister in Cutted)
                    {
                        polygons.Add(blister.Outline);
                    }
                    return polygons;
                }
            }

            public DataTree<string> GetCuttedAnchorStatus
            {
                get
                {
                    DataTree<string> anchors = new DataTree<string>();
                    foreach (Blister blister in Cutted)
                    {
                        anchors.Add(blister.Cells[0].Anchor.state.ToString());
                    }
                    anchors.Graft();
                    return anchors;
                }
            }

            public DataTree<string> GetQueuedAnchorStatus
            {
                get
                {
                    DataTree<string> anchors = new DataTree<string>();
                    for (int j = 0; j < Queue.Count; j++)
                    {
                        for (int i = 0; i < Queue[j].Cells.Count; i++)
                        {
                            anchors.Add(Queue[j].Cells[i].Anchor.state.ToString(), new GH_Path(j,i));
                        }
                    }
                    return anchors;
                }
            }

            public DataTree<PolylineCurve> GetPillsGH
            {
                get
                {
                    DataTree<PolylineCurve> out_data = new DataTree<PolylineCurve>();
                    //List<List<Curve>> out_data = new List<List<Curve>>();
                    for (int i = 0; i < Cutted.Count; i++)
                    {
                        GH_Path path = new GH_Path(i);
                        out_data.Add(Cutted[i].Cells[0].pill, path);
                    }
                    return out_data;
                }
            }

            public DataTree<PolylineCurve> GetAllCuttingPolygonsGH
            {
                get
                {
                    DataTree<PolylineCurve> out_data = new DataTree<PolylineCurve>();
                    //List<List<Curve>> out_data = new List<List<Curve>>();
                    for (int i = 0; i < Cutted.Count; i++)
                    {
                        GH_Path path = new GH_Path(i);
                        if (Cutted[i].Cells[0].cuttingData == null) continue;
                        if (Cutted[i].Cells[0].cuttingData.Count == 0) continue;
                        out_data.AddRange(Cutted[i].Cells[0].cuttingData.Select(cData => cData.Polygon), path);
                    }
                    return out_data;
                }
            }


            public List<List<LineCurve>> GetCuttingLines
            {
                get
                {
                    List<List<LineCurve>> cuttingLines = new List<List<LineCurve>>(Cutted.Count);
                    foreach (Blister blister in Cutted)
                    {
                        cuttingLines.Add(blister.GetCuttingLines());
                    }
                    return cuttingLines;
                }
            }

            public DataTree<Curve> GetCuttingLinesGH
            {
                get
                {
                    DataTree<Curve> out_data = new DataTree<Curve>();
                    //List<List<Curve>> out_data = new List<List<Curve>>();
                    for (int i = 0; i < Cutted.Count; i++)
                    {
                        GH_Path path = new GH_Path(i);
                        out_data.AddRange(Cutted[i].GetCuttingLines(), path);
                    }
                    return out_data;
                }
            }

            public DataTree<PolylineCurve> GetPathGH
            {
                get
                {
                    DataTree<PolylineCurve> out_data = new DataTree<PolylineCurve>();
                    //List<List<Curve>> out_data = new List<List<Curve>>();
                    for (int i = 0; i < Cutted.Count; i++)
                    {
                        GH_Path path = new GH_Path(i);
                        out_data.AddRange(Cutted[i].GetCuttingPath(), path);
                    }
                    return out_data;
                }
            }

            public DataTree<LineCurve> GetRaysGH
            {
                get
                {
                    DataTree<LineCurve> out_data = new DataTree<LineCurve>();
                    //List<List<Curve>> out_data = new List<List<Curve>>();
                    for (int i = 0; i < Cutted.Count; i++)
                    {
                        GH_Path path = new GH_Path(i);
                        out_data.AddRange(Cutted[i].GetIsoRays(), path);
                    }
                    return out_data;
                }
            }

            public DataTree<PolylineCurve> GetLeftOversGH
            {
                get
                {
                    DataTree<PolylineCurve> out_data = new DataTree<PolylineCurve>();
                    //List<List<Curve>> out_data = new List<List<Curve>>();
                    for (int i = 0; i < Queue.Count; i++)
                    {
                        GH_Path path = new GH_Path(i);
                        out_data.Add(Queue[i].Outline, path);
                    }
                    return out_data;
                }
            }

            public DataTree<Curve> GetObstaclesGH
            {
                get
                {
                    DataTree<Curve> out_data = new DataTree<Curve>();
                    //List<List<Curve>> out_data = new List<List<Curve>>();
                    for (int i = 0; i < Cutted.Count; i++)
                    {
                        GH_Path path = new GH_Path(i);
                        if (Cutted[i].Cells[0].bestCuttingData == null) continue;
                        out_data.AddRange(Cutted[i].Cells[0].bestCuttingData.obstacles, path);
                    }
                    return out_data;
                }
            }

            public DataTree<Curve> GetUnfinishedCutDataGH
            {
                get
                {
                    DataTree<Curve> out_data = new DataTree<Curve>();
                    //List<List<Curve>> out_data = new List<List<Curve>>();
                    for (int i = 0; i < Queue[0].Cells.Count; i++)
                    {
                        Cell cell = Queue[0].Cells[i];
                        if (cell.cuttingData == null) continue;
                        if (cell.cuttingData.Count == 0) continue;
                        for (int j = 0; j < cell.cuttingData.Count; j++) {
                            out_data.AddRange(cell.cuttingData[j].IsoSegments, new GH_Path(i, j, 0));
                            out_data.AddRange(cell.cuttingData[j].Segments, new GH_Path(i, j, 1));
                            out_data.AddRange(cell.cuttingData[j].obstacles, new GH_Path(i, j, 2));
                            out_data.AddRange(cell.cuttingData[j].Path, new GH_Path(i, j, 3));
                        }
                    }
                    return out_data;
                }
            }

            public DataTree<Curve> GetQueuePillDataGH
            {
                get
                {
                    DataTree<Curve> out_data = new DataTree<Curve>();
                    //List<List<Curve>> out_data = new List<List<Curve>>();
                    for (int j = 0; j < Queue.Count; j++)
                    {
                        for (int i = 0; i < Queue[j].Cells.Count; i++)
                        {
                            Cell cell = Queue[j].Cells[i];
                            //if (cell.cuttingData == null) continue;
                            out_data.Add(cell.pill, new GH_Path(j, i, 0));
                            out_data.Add(cell.pillOffset, new GH_Path(j,i, 0));

                            out_data.AddRange(cell.proxLines, new GH_Path(j, i, 1));
                            out_data.AddRange(cell.connectionLines, new GH_Path(j, i, 2));

                            out_data.Add(cell.voronoi, new GH_Path(j, i, 3));
                        }
                    }
                    return out_data;
                }
            }


            #endregion

            private void Initialise(List<PolylineCurve> Pills, PolylineCurve Blister)
            {
                // Initialize Lists
                Queue = new List<Blister>();
                Cutted = new List<Blister>();
               // 

              // Build initial blister
                Blister initialBlister = new Blister(Pills, Blister);
                initialBlister.SortCellsByCoordinates(true);
                Queue.Add(initialBlister);
                anchor = new Anchor(this);
                log.Debug(String.Format("New blistructor with {0} Queue blisters", Queue.Count));
                // World Obstacles
                Line cartesianLimitLine = new Line(new Point3d(0, -Setups.BlisterCartesianDistance, 0), Vector3d.XAxis, 1.0);
                cartesianLimitLine.Extend(Setups.IsoRadius, Setups.IsoRadius);
                worldObstacles = new List<Curve>() { new LineCurve(cartesianLimitLine) };

            }

            public JObject CutBlister(string pillsMask, string blisterMask)
            {
                // Do Contuter stuff here for pills and blister then    CutBlister(pills, blister)
                List<Curve> pills = GetContursBasedOnBinaryImage(pillsMask, 0.0);
                List<Curve> blisters = GetContursBasedOnBinaryImage(blisterMask, 0.0); // This should be 1 element list....
                if (blisters.Count != 1) return null;
                ApplyCalibrationData(pills);
                ApplyCalibrationData(blisters);

                // process pills
                List<PolylineCurve> outPills = new List<PolylineCurve>();
                foreach (Curve crv in pills)
                {
                    NurbsCurve nCrv = (NurbsCurve)crv;
                    Curve fitCurve = nCrv.Fit(3, Setups.CurveFitTolerance, 0.0);
                    outPills.Add(fitCurve.ToPolyline(Setups.CurveDistanceTolerance, 0.0, 0.05, 1000.0));
                }

                NurbsCurve bliNCrv = (NurbsCurve)blisters[0];
                Curve fitBliCurve = bliNCrv.Fit(3, Setups.CurveFitTolerance, 0.0);
                PolylineCurve blister = fitBliCurve.ToPolyline(Setups.CurveDistanceTolerance, 0.0, 0.05, 1000.0);

                // TO remove
                mainOutline = blister;
                pillsss = outPills;

                JObject cuttingResult = CutBlister(outPills, blister);
                return cuttingResult;
              // return null;
            }
            public JObject CutBlister(List<Polyline> pills, Polyline blister)
            {
                List<PolylineCurve> convPills = new List<PolylineCurve>();
                foreach (Polyline pline in pills)
                {
                    convPills.Add(pline.ToPolylineCurve());
                }
                return CutBlister(convPills, blister.ToPolylineCurve());
            }

            public JObject CutBlister(List<PolylineCurve> pills, PolylineCurve blister)
            {
                // Prepare basic stuff
                JObject cuttingResult = PrepareEmptyJSON();
                CuttingState status = CuttingState.CTR_UNSET;

                // Pills and blister are already curve objects
                Initialise(pills, blister);
                cuttingResult["PillsDetected"] = pills.Count;


                try
                {
                    status = PerformCut(mainLimit, cellLimit);

                    // status = CuttingState.CTR_UNSET;
                }
                catch (Exception ex)
                {
                    status = CuttingState.CTR_OTHER_ERR;
                    log.Error("Main Error catcher", ex);
                }
 
                cuttingResult["Status"] = status.ToString();
                cuttingResult["PillsCutted"] = Cutted.Count;
                // If all alright, populate by cutting data
                if (status == CuttingState.CTR_SUCCESS)
                {
                    
                    JArray allCuttingInstruction = new JArray();
                    foreach (Blister bli in Cutted)
                    {
                        allCuttingInstruction.Add(bli.Cells[0].GetJSON());
                    }
                    cuttingResult["CuttingData"] = allCuttingInstruction;
                }
                cuttingResult["AnchorLocation"] = anchor.GetJSON();


                return cuttingResult;
            }

            private CuttingState PerformCut(int mainLimit, int cellLimit)
            {
                log.Info(String.Format("=== Start Cutting ==="));
                int initialPillCount = Queue[0].Cells.Count;
                if (Queue[0].ToTight) return CuttingState.CTR_TO_TIGHT;
                if (Queue[0].LeftCellsCount == 1) return CuttingState.CTR_ONE_PILL;
                if (!anchor.ApplyAnchorOnBlister()) return CuttingState.CTR_ANCHOR_LOCATION_ERR;
                //return CuttingState.CTR_ANCHOR_LOCATION_ERR;
               
                int n = 0; // control
                // Main Loop
                while (Queue.Count > 0)
                {
                    if (n > mainLimit && mainLimit != -1) break;
                    // Extra control to not loop forever...
                    if (n > initialPillCount + loopTolerance) break;
                    log.Info(String.Format(String.Format("<<<<<<<<<<<<<<<Blisters Count: Queue: {0}, Cutted {1}>>>>>>>>>>>>>>>>>>>>>>>>", Queue.Count, Cutted.Count)));
                    // InnerLoop - Queue Blisters
    
                    for (int i = 0; i < Queue.Count; i++) 
                    { 
                        if (Queue == null) continue;
                        Blister blister = Queue[i];
                        log.Info(String.Format("{0} cells left to cut on on Blister:{1}", blister.Cells.Count, i));
                        if (blister.IsDone)
                        {
                            log.Info("Blister is already cutted or is to tight for cutting.");
                            continue;
                        }
                        // In tuple I have | CutOut Blister | Current Updated Blister | Extra Blisters to Cut (recived by spliting currentBlister) 
                        Tuple<Blister, Blister, List<Blister>> result = blister.CutNext(worldObstacles) ;
                        log.Debug(String.Format("Cutting Result: Cutout: {0} - Current Blister {1} - New Blisters {2}.", result.Item1, result.Item2, result.Item3.Count));
                        // If anything was cutted, add to list
                        if (result.Item1 != null)
                        {
                            Cell cuttedCell = result.Item1.Cells[0];
                            if (cuttedCell.Anchor.state == AnchorState.Inactive) anchor.Update(result.Item1);
                            if (cuttedCell.Anchor.state == AnchorState.Active && cuttedCell.State != CellState.Alone && CuttableCellsLeft ==2 ) anchor.FindNewAnchorAndApplyOnBlister(result.Item1);
                            log.Info("Adding new CutOut blister to Cutted list");
                            Cutted.Add(result.Item1);
                        }
                        else
                        {
                            log.Error("!!!Cannot cut blister Anymore!!!");
                            return CuttingState.CTR_FAILED;
                        }
                        // override current bluster, if null , remove it from Queue list
                        if (result.Item2 == null)
                        {
                            log.Info("Current blister is empty. Removing from Queue");
                            Queue.RemoveAt(i);
                            i--;
                            break;
                        }
                        else
                        {
                            log.Info("Updating blister");
                            blister = result.Item2;
                            // Sort Pills by last Knife Possition -> Last Pill Centre
                            // Point3d lastKnifePossition = Cutted.Last().Cells[0].bestCuttingData.GetLastKnifePossition();
                            Point3d lastKnifePossition = Cutted.Last().Cells[0].PillCenter;
                            if (lastKnifePossition.X != double.NaN) blister.SortCellsByPointDirection(lastKnifePossition, false);
                            //if (lastKnifePossition.X != double.NaN) blister.SortCellsByCoordinates(true);

                        }
                        // Add extra blsters if any was created
                        if (result.Item3.Count != 0)
                        {
                            log.Info("Adding new blister(s) to Queue");
                            Queue.AddRange(result.Item3);
                            break;
                        }

                    }
                    n++;
                }

                if (initialPillCount == Cutted.Count) return CuttingState.CTR_SUCCESS;
                else return CuttingState.CTR_FAILED;
            }


            private JObject PrepareEmptyJSON()
            {
                JObject data = new JObject();
                data.Add("Status", null);
                data.Add("PillsDetected", null);
                data.Add("PillsCutted", null);
                data.Add("AnchorLocation", null);
                data.Add("CuttingData", new JArray());
                return data;
            }

            private List<Curve> GetContursBasedOnBinaryImage(string imagePath, double tol)
            {
                List<List<int[]>> allPoints = Conturer.getContours(imagePath, tol);
                List<Curve> finalContours = new List<Curve>();
                foreach (List<int[]> conturPoints in allPoints) 
                {
                    Polyline pLine = new Polyline(conturPoints.Count);
                    foreach (int[] rawPoint in conturPoints)
                    {
                        Point3d point = new Point3d(rawPoint[0], rawPoint[1], 0);
                        pLine.Add(point);
                    }
                    pLine.Add(pLine.First);
                    PolylineCurve ppLine = pLine.ToPolylineCurve();
                    
                    finalContours.Add((Curve) ppLine.Rebuild(pLine.Count, 3, true));
                }
                return finalContours;
            }

            private void ApplyCalibrationData(List<Curve> curves) 
            {
                // Get reveresed calibraion vector
                Vector3d vector = new Vector3d(-Setups.CalibrationVectorX, -Setups.CalibrationVectorY, 0);
                foreach (Curve crv in curves)
                {
                    crv.Scale(Setups.Spacing);
                    crv.Translate(vector);
                    crv.Rotate(Setups.Rotate, Vector3d.ZAxis, new Point3d(0, 0, 0));
                }
            }


        }

        */

        //    public List<Curve> EstimateCartesian()
        //    {
        //        //   public List<Point3d> estimateCartesian(){
        //        //Get last 2 cells...
        //        List<Curve> temp = new List<Curve>(2);
        //        List<Point3d> anchorPoints = new List<Point3d>(2);
        //        List<Cell> lastCells = new List<Cell> { orderedCells[orderedCells.Count - 1], orderedCells[orderedCells.Count - 2] };
        //        //List<Curve> lastPills = new List<Curve> {(Curve) orderedCells[orderedCells.Count - 1].pill , (Curve) orderedCells[orderedCells.Count - 2].pill};

        //        var xform = Rhino.Geometry.Transform.Translation(0, Setups.CartesianThicknes, 0);
        //        //// tempGuideLine.Transform(xform);

        //        foreach (Cell cell in lastCells)
        //        {
        //            LineCurve tempGuideLine = new LineCurve(guideLine);
        //            tempGuideLine.Transform(xform);
        //            Tuple<List<Curve>, List<Curve>> result = Geometry.TrimWithRegion(tempGuideLine, cell.bestCuttingData.Polygon);
        //            if (result.Item1.Count == 1)
        //            {
        //                Tuple<List<Curve>, List<Curve>> result2 = Geometry.TrimWithRegion(result.Item1[0], cell.pill);
        //                temp.AddRange(result2.Item2);
        //            }
        //        }
        //        return temp;
        //    }
        
       // public enum AnchorSite { Left = 0, Right = 1, Unset = 2};
       // public enum AnchorState { Active = 0, Inactive = 1, Cutted = 2 };
        /*
        public class AnchorPoint
        {

            public Point3d location;
            public AnchorSite orientation;
            public AnchorState state;

            public AnchorPoint()
            {
                location = new Point3d(-1,-1,-1);
                orientation = AnchorSite.Unset;
                state = AnchorState.Inactive;
            }

            public AnchorPoint(Point3d pt, AnchorSite site)
            {
                location = pt;
                orientation = site;
                state = AnchorState.Active;
            }
        }
        */

        /*
        public class Anchor
        {
            private static readonly ILog log = LogManager.GetLogger("Blistructor.Anchor");

            public LineCurve cartesianLimitLine;

            public MultiBlister mBlister;
            public PolylineCurve mainOutline;
            public PolylineCurve aaBBox;
            public PolylineCurve maBBox;
            public LineCurve GuideLine;
            //public LineCurve aaUpperLimitLine;
            //public LineCurve maGuideLine;
            //public LineCurve maUpperLimitLine;

            public List<LineCurve> GrasperPossibleLocation;
            public List<AnchorPoint> anchors;


            public Anchor(MultiBlister mBlister)
            {
                // Create Coartesin Limit Line
                Line tempLine = new Line(new Point3d(0, -Setups.BlisterCartesianDistance, 0), Vector3d.XAxis, 1.0);
                tempLine.Extend(Setups.IsoRadius, Setups.IsoRadius);
                cartesianLimitLine = new LineCurve(tempLine);

                // Get needed data
                this.mBlister = mBlister;
                GrasperPossibleLocation = new List<LineCurve>();
                //Build initial blister shape data
                mainOutline = mBlister.Queue[0].Outline;

                // Generate BBoxes
                BoundingBox blisterBB = mainOutline.GetBoundingBox(false);
                Rectangle3d rect = new Rectangle3d(Plane.WorldXY, blisterBB.Min, blisterBB.Max);
                maBBox = Geometry.MinimumAreaRectangleBF(mainOutline);
                Geometry.UnifyCurve(maBBox);
                aaBBox = rect.ToPolyline().ToPolylineCurve();
                Geometry.UnifyCurve(aaBBox);

                // Find lowest mid point on Blister AA Bounding Box
                List<Line> aaSegments = new List<Line>(aaBBox.ToPolyline().GetSegments());
                GuideLine = new LineCurve(aaSegments.OrderBy(line => line.PointAt(0.5).Y).ToList()[0]);
                // Move line to Y => 0
                GuideLine.SetStartPoint(new Point3d(GuideLine.PointAtStart.X, 0, 0));
                GuideLine.SetEndPoint(new Point3d(GuideLine.PointAtEnd.X, 0, 0));

                // Create initial predition Line
                LineCurve fullPredLine = new LineCurve(GuideLine);
                fullPredLine.Translate(Vector3d.YAxis * Setups.CartesianDepth / 2);

                // Find limits based on Blister Shape
                double[] paramT = GuideLine.DivideByCount(50, true);
                List<double> limitedParamT = new List<double>(paramT.Length);
                foreach (double t in paramT)
                {
                    double parT;
                    if (mainOutline.ClosestPoint(GuideLine.PointAt(t), out parT, Setups.CartesianDepth / 2)) limitedParamT.Add(parT);
                }
                // Find Extreme points on Blister
                List<Point3d> extremePointsOnBlister = new List<Point3d>(){
                    mainOutline.PointAt(limitedParamT.First()),
                    mainOutline.PointAt(limitedParamT.Last())
                };
                // Project this point to Predition Line
                List<double> fullPredLineParamT = new List<double>(paramT.Length);
                foreach (Point3d pt in extremePointsOnBlister)
                {
                    double parT;
                    if (fullPredLine.ClosestPoint(pt, out parT)) fullPredLineParamT.Add(parT);
                }

                // keep lines between extreme points
                fullPredLine = (LineCurve)fullPredLine.Trim(fullPredLineParamT[0], fullPredLineParamT[1]);
                // Shrink curve on both sides by half of Grasper width.

                // Move temporaly predLine to the upper position, too chceck intersection with pills.
                fullPredLine.Translate(Vector3d.YAxis * Setups.CartesianDepth / 2);
                // NOTE: Check intersection with pills (Or maybe with pillsOffset. Rethink problem)
                Tuple<List<Curve>, List<Curve>> trimResult = Geometry.TrimWithRegions(fullPredLine, mBlister.Queue[0].GetPills(false));
                // Gather all parts outsite (not in pills) shrink curve on both sides by half of Grasper width and move it back to mid position 
                foreach (Curve crv in trimResult.Item2)
                {
                    // Shrink pieces on both sides by half of Grasper width.
                    Line ln = ((LineCurve)crv).Line;
                    if (ln.Length < Setups.CartesianThickness) continue;
                    ln.Extend(-Setups.CartesianThickness / 2, -Setups.CartesianThickness / 2);
                    LineCurve cln = new LineCurve(ln);
                    //move it back to mid position
                    cln.Translate(Vector3d.YAxis * -Setups.CartesianDepth / 2);
                    // Gather 
                    GrasperPossibleLocation.Add(cln);
                }

                anchors = GetJawsPoints();
            }

          //  public void Update

            public List<AnchorPoint> GetJawsPoints()
            {
                List<AnchorPoint> extremePoints = GetExtremePoints();
                if  (extremePoints == null) return null;
                Line spectrumLine = new Line(extremePoints[0].location, extremePoints[1].location);
                if (spectrumLine.Length < Setups.CartesianMaxWidth && spectrumLine.Length > Setups.CartesianMinWidth) return extremePoints;
                
                Point3d midPoint = spectrumLine.PointAt(0.5);
                if (spectrumLine.Length > Setups.CartesianMaxWidth)
                {
                    NurbsCurve maxJawsCircle = new Circle(midPoint, Setups.CartesianMaxWidth/2).ToNurbsCurve();
                  //  List<Curve> trimResult = new List<Curve>();

                    Tuple<List<Curve>, List<Curve>>  trimResult = Geometry.TrimWithRegion(GrasperPossibleLocation.Select(crv => (Curve)crv).ToList(), maxJawsCircle);

                    if (trimResult.Item1.Count == 0) return null;
                    List<Point3d> toEvaluate = new List<Point3d>(trimResult.Item1.Count);
                    foreach (Curve ln in trimResult.Item1)
                    {
                        Point3d pt, pt2;
                        ln.ClosestPoints(maxJawsCircle, out pt, out pt2);
                        toEvaluate.Add(pt);
                    }
                    toEvaluate = toEvaluate.OrderBy(pt => pt.X).ToList();
                    Point3d leftJaw = toEvaluate.First();
                    Point3d rightJaw = toEvaluate.Last();
                    if (leftJaw.DistanceTo(rightJaw) < Setups.CartesianMinWidth) return null;
                    return new List<AnchorPoint>() {
                        new AnchorPoint(leftJaw, AnchorSite.Left),
                        new AnchorPoint(rightJaw, AnchorSite.Right)
                    };
                }
                else return null;
            }

            private List<AnchorPoint> GetExtremePoints()
            {
                if (GrasperPossibleLocation == null) return null;
                if (GrasperPossibleLocation.Count == 0) return null;
                Point3d leftJaw = GrasperPossibleLocation.OrderBy(line => line.PointAtStart.X).Select(line => line.PointAtStart).First();
                Point3d rightJaw = GrasperPossibleLocation.OrderBy(line => line.PointAtStart.X).Select(line => line.PointAtEnd).Last();
                return new List<AnchorPoint>() {
                        new AnchorPoint(leftJaw, AnchorSite.Left),
                        new AnchorPoint(rightJaw, AnchorSite.Right)
                    };
            }

            // NOTE: TO TEST AND IMPLENT
            /// <summary>
            /// Check which Anchor belongs to which cell and reset other cells anchors. 
            /// </summary>
            /// <returns></returns>
            public bool ApplyAnchorOnBlister()
            {
                // NOTE: For loop by all queue blisters.
                foreach (Blister blister in mBlister.Queue)
                {
                    if (blister.Cells == null) return false;
                    if (blister.Cells.Count == 0) return false;
                    foreach (Cell cell in blister.Cells)
                    {
                        // Reset anchors in each cell.
                        cell.Anchor = new AnchorPoint();
                        foreach (AnchorPoint pt in anchors)
                        {
                            PointContainment result = cell.voronoi.Contains(pt.location, Plane.WorldXY, Setups.IntersectionTolerance);
                            if (result == PointContainment.Inside)
                            {
                                log.Info(String.Format("Anchor appied on cell - {0} with status {1}", cell.id, pt.state));
                                cell.Anchor = pt;
                                break;
                            }
                        }

                    }
                }
                return true;
            }


            public void Update(Blister cuttedBlister)
            {
                Update(null, cuttedBlister.Cells[0].bestCuttingData.Polygon);
            }

            public void Update(PolylineCurve path, PolylineCurve polygon)
            {
                 if (polygon != null)
                {
                   Curve[] offset = polygon.Offset(Plane.WorldXY, Setups.CartesianThickness / 2, Setups.GeneralTolerance, CurveOffsetCornerStyle.Sharp);
                    if (offset.Length > 0)
                    {
                        Tuple<List<Curve>, List<Curve>> result = Geometry.TrimWithRegion(GrasperPossibleLocation.Select(crv => (Curve)crv).ToList(), offset[0]);
                        GrasperPossibleLocation = result.Item2.Select(crv => (LineCurve)crv).ToList();
                    }
                }

                if (path != null)
                {
                    PolylineCurve pathOutline = Geometry.PolylineThicken(path, Setups.BladeWidth / 2 + Setups.CartesianThickness / 2);
                    Tuple<List<Curve>,List<Curve>> result= Geometry.TrimWithRegion(GrasperPossibleLocation.Select(crv => (Curve)crv).ToList(), pathOutline);
                    GrasperPossibleLocation = result.Item2.Select(crv => (LineCurve)crv ).ToList();
                }

            }

            public void FindNewAnchorAndApplyOnBlister(Blister cuttedBlister)
            {
                Update(cuttedBlister);
                anchors = GetJawsPoints();
                ApplyAnchorOnBlister();
            }

            public JObject GetJSON()
            {
                JObject anchorPoints = new JObject();
                if (anchors.Count == 0) return anchorPoints;
                foreach (AnchorPoint pt in anchors)
                {
                    JArray pointArray = new JArray();
                    pointArray.Add(pt.location.X);
                    pointArray.Add(pt.location.Y);
                    anchorPoints.Add(pt.orientation.ToString(), pointArray);
                }
                return anchorPoints;
            }
        }

       */
      
        /*
        public class CuttedBlister : Blister
        {
            public CuttedBlister(Cell cell, PolylineCurve outline) :base(outline)
            {
               this.cells = new List<Cell>(1) { _cells };
            }
        }
        */

        /*
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

            public bool HasActiveAnchor{
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
                for (int i =0; i< cells.Count; i++)
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

         */
        //public enum CellState { Queue = 0, Cutted = 1, Alone = 2 };
        //public enum CutState { Failed = 0, Cutted = 1, Alone = 2 };

        /*
        public class Cell
        {
            private static readonly ILog log = LogManager.GetLogger("Blistructor.Cell");

            public int id;

            // Parent Blister
            private Blister blister;

            // States
            private CellState state = CellState.Queue;
            public AnchorPoint Anchor;
            //public double CornerDistance = 0;
            //public double GuideDistance = 0;

            // Pill Stuff
            public PolylineCurve pill;
            public PolylineCurve pillOffset;

            private AreaMassProperties pillProp;

            // Connection and Adjacent Stuff
            public Curve voronoi;
            //!!connectionLines, proxLines, adjacentCells, samplePoints <- all same sizes, and order!!
            public List<Curve> connectionLines;
            public List<Curve> proxLines;
            public List<Cell> adjacentCells;
            public List<Point3d> samplePoints;

            public List<Curve> obstacles;

            //public List<Curve> temp = new List<Curve>();
            // public List<Curve> temp2 = new List<Curve>();
            public List<CutData> cuttingData;
            public CutData bestCuttingData;
            // Int with best cutting index and new Blister for this cutting.

            public Cell(int _id, PolylineCurve _pill, Blister _blister)
            {
                id = _id;
                blister = _blister;
                // Prepare all needed Pill properties
                pill = _pill;
                // Make Pill curve oriented in proper direction.
                Geometry.UnifyCurve(pill);

                Anchor = new AnchorPoint();

                pillProp = AreaMassProperties.Compute(pill);

                // Create pill offset
                Curve[] ofCur = pill.Offset(Plane.WorldXY, Setups.BladeWidth / 2, 0.001, CurveOffsetCornerStyle.Sharp);
                if (ofCur == null)
                {
                    log.Error("Incorrect pill offseting");
                    throw new InvalidOperationException("Incorrect pill offseting");
                }
                if (ofCur.Length == 1)
                {
                    pillOffset = (PolylineCurve) ofCur[0];
                }
                else
                {
                    log.Error("Incorrect pill offseting");
                    throw new InvalidOperationException();
                }
            }

            #region PROPERTIES
            public Point3d PillCenter
            {
                get { return pillProp.Centroid; }
            }

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
                    return PillCenter.X + PillCenter.Y * 100;
                }
            }

            public NurbsCurve OrientationCircle { get; private set; }

            public CellState State { get; set; }

            #endregion
            
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
                    output.AddRange(cData.Path);
                }
                return output;
            }

            #region GENERAL MANAGE

            #region DISTANCES
            public double GetDirectionIndicator(Point3d pt)
            {
                Vector3d vec = pt - this.PillCenter;
                return Math.Abs(vec.X) + Math.Abs(vec.Y) * 100;
            }
            public double GetDistance(Point3d pt)
            {
                return pt.DistanceTo(this.PillCenter);
            }
            public double GetClosestDistance(List<Point3d> pts)
            {
                PointCloud ptC = new PointCloud(pts);
                int closestIndex = ptC.ClosestPoint(this.PillCenter);
                return this.PillCenter.DistanceTo(pts[closestIndex]);
            }
            public double GetDistance(Curve crv)
            {
                double t;
                crv.ClosestPoint(this.PillCenter, out t);
                return crv.PointAt(t).DistanceTo(this.PillCenter);
            }
            public double GetClosestDistance(List<Curve> crvs)
            {
                List<Point3d> ptc = new List<Point3d>();
                foreach (Curve crv in crvs)
                {
                    double t;
                    crv.ClosestPoint(this.PillCenter, out t);
                    ptc.Add(crv.PointAt(t));
                }
                return GetClosestDistance(ptc);
            }

            #endregion

            
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
            

            public void AddConnectionData(List<Cell> cells, List<Curve> lines, List<Point3d> midPoints)
            {
                adjacentCells = new List<Cell>();
                samplePoints = new List<Point3d>();
                connectionLines = new List<Curve>();

                EstimateOrientationCircle();
                int[] ind = Geometry.SortPtsAlongCurve(midPoints, OrientationCircle);

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

            public void RemoveConnectionData()
            {
                for (int i = 0; i < adjacentCells.Count; i++)
                {
                    adjacentCells[i].RemoveConnectionData(id);
                }


            }

            /// <summary>
            /// Call from adjacent cell
            /// </summary>
            /// <param name="cellId">ID of Cell which is executing this method</param>
            protected void RemoveConnectionData(int cellId)
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
                SortData();
            }

            private void EstimateOrientationCircle()
            {
                double circle_radius = pill.GetBoundingBox(false).Diagonal.Length / 2;
                OrientationCircle = (new Circle(PillCenter, circle_radius)).ToNurbsCurve();
                Geometry.EditSeamBasedOnCurve(OrientationCircle, blister.Outline);
            }

            private void SortData()
            {
                EstimateOrientationCircle();
                int[] sortingIndexes = Geometry.SortPtsAlongCurve(samplePoints, OrientationCircle);

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
            private List<Curve> GetUniqueProxy(int id)
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

            private Dictionary<int, Curve> GetUniqueProxy_v2(int id)
            {
                Dictionary<int, Curve> proxData = new Dictionary<int, Curve>();

                //List<Curve> proxyLines = new List<Curve>();
                for (int i = 0; i < adjacentCells.Count; i++)
                {
                    if (adjacentCells[i].id != id)
                    {
                        proxData.Add(adjacentCells[i].id, proxLines[i]);
                        // proxyLines.Add(proxLines[i]);
                    }
                }
                return proxData;
            }

            public List<Curve> BuildObstacles()
            {
                List<Curve> limiters = new List<Curve> { pillOffset };
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

            public List<Curve> BuildObstacles_v2(List<Curve> worldObstacles)
            {
                
                List<Curve> limiters = new List<Curve> { pillOffset };
                if (worldObstacles != null) limiters.AddRange(worldObstacles);
                Dictionary<int, Curve> uniqueCellsOffset = new Dictionary<int, Curve>();

                for (int i = 0; i < adjacentCells.Count; i++)
                {
                    // limiters.Add(adjacentCells[i].pillOffset);
                    Dictionary<int, Curve> proxDict = adjacentCells[i].GetUniqueProxy_v2(id);
                    uniqueCellsOffset[adjacentCells[i].id] = adjacentCells[i].pillOffset;
                    //List<Curve> prox = adjacentCells[i].GetUniqueProxy(id);
                    foreach (KeyValuePair<int, Curve> prox_crv in proxDict)
                    {
                        uniqueCellsOffset[prox_crv.Key] = blister.CellByID(prox_crv.Key).pillOffset;

                        if (Geometry.CurveCurveIntersection(prox_crv.Value, proxLines).Count == 0)
                        {
                            limiters.Add(prox_crv.Value);
                        }
                    }
                }
                limiters.AddRange(uniqueCellsOffset.Values.ToList());
                return Geometry.RemoveDuplicateCurves(limiters);
            }

            #endregion

            #region CUT STUFF
            public bool GenerateSimpleCuttingData_v2(List<Curve> worldObstacles)
            {
                // Initialise new Arrays
                obstacles = BuildObstacles_v2(worldObstacles);
                log.Debug(String.Format("Obstacles count {0}", obstacles.Count));
                cuttingData = new List<CutData>();
                // Stage I - naive Cutting
                // Get cutting Directions

                PolygonBuilder_v2(GenerateIsoCurvesStage1());
                log.Info(String.Format(">>>After STAGE_1: {0} cuttng possibilietes<<<", cuttingData.Count));
                PolygonBuilder_v2(GenerateIsoCurvesStage2());
                log.Info(String.Format(">>>After STAGE_2: {0} cuttng possibilietes<<<", cuttingData.Count));
                IEnumerable<IEnumerable<LineCurve>> isoLines = GenerateIsoCurvesStage3a(1, 2.0);
                foreach (List<LineCurve> isoLn in isoLines)
                {
                    PolygonBuilder_v2(isoLn);
                }

                //PolygonBuilder_v2(GenerateIsoCurvesStage3a(1, 2.0));
                log.Info(String.Format(">>>After STAGE_3: {0} cuttng possibilietes<<<", cuttingData.Count));
                if (cuttingData.Count > 0) return true;
                else return false;
            }


            public bool GenerateAdvancedCuttingData()
            {
                // If all Stages 1-3 failed, start looking more!
                if (cuttingData.Count == 0)
                {
                    // if (cuttingData.Count == 0){
                    List<List<LineCurve>> isoLinesStage4 = GenerateIsoCurvesStage4(60, Setups.IsoRadius);
                    if (isoLinesStage4.Count != 0)
                    {
                        List<List<LineCurve>> RaysCombinations = (List<List<LineCurve>>)isoLinesStage4.CartesianProduct();
                        for (int i = 0; i < RaysCombinations.Count; i++)
                        {
                            if (RaysCombinations[i].Count > 0)
                            {
                                PolygonBuilder_v2(RaysCombinations[i]);
                            }
                        }
                    }
                }
                if (cuttingData.Count > 0) return true;
                else return false;
            }

            /// <summary>
            /// Get best Cutting Data from all generated and asign it to /bestCuttingData/ field.
            /// </summary>
            public bool PolygonSelector()
            {
                // Order by number of cuts to be performed.

                cuttingData = cuttingData.OrderBy(x => x.EstimatedCuttingCount * x.Polygon.GetBoundingBox(false).Area * x.BlisterLeftovers.Select(y=> y.PointCount).Sum()).ToList();
               // cuttingData = cuttingData.OrderBy(x => x.EstimatedCuttingCount* x.GetPerimeter()).ToList();
                List<CutData> selected = cuttingData;
                // Limit only to lower number of cuts
                //List<CutData> selected = cuttingData.Where(x => x.EstimatedCuttingCount == cuttingData[0].EstimatedCuttingCount).ToList();
                //Then sort by perimeter
                //selected = selected.OrderBy(x => x.GetPerimeter()).ToList();
                foreach  ( CutData cData in selected)
                {
                   // if (!cData.RecalculateIsoSegments(OrientationCircle)) continue;
                    if (!cData.GenerateBladeFootPrint()) continue;
                    bestCuttingData = cData;
                    return true;
                }
                //Pick best one.
                //bestCuttingData = selected[0];
                //if (bestCuttingData.RecalculateIsoSegments(OrientationCircle))
                //{
                //    bestCuttingData.GenerateBladeFootPrint();
               //     return true;
               // }
                return false;
                // bestCuttingData = cuttingData[0];
            }

            public void PolygonSelector2()
            {
            }

            public CutState TryCut(bool ommitAnchor, List<Curve> worldObstacles)
            {
                log.Info(String.Format("Trying to cut cell id: {0} with status: {1}", id, state));
                // If cell is cutted, dont try to cut it again... It supose to be in cutted blisters list...
                if (state == CellState.Cutted) return CutState.Cutted;


                // If cell is not surrounded by other cell, update data
                log.Debug(String.Format("Check if cell is alone on blister: No. adjacent cells: {0}", adjacentCells.Count));
                if (adjacentCells.Count == 0)
                {
                    //state = CellState.Alone;
                    log.Debug("This is last cell on blister.");
                    return CutState.Alone;
                }
                // If cell is marekd as possible anchor, also dont try to cut
                if (ommitAnchor == true && Anchor.state == AnchorState.Active)
                {
                    log.Info("Marked as anchored. Omitting");
                    return CutState.Failed;
                }

                // If still here, try to cut 
                log.Debug("Perform cutting data generation");
                if (GenerateSimpleCuttingData_v2(worldObstacles))
                {
                   // RemoveConnectionData();
                    PolygonSelector();
                   // state = CellState.Cutted;
                    return CutState.Cutted;
                }
                else return CutState.Failed;

            }
            #endregion

            #region Polygon Builder Stuff

            // All methods will generat full Rays, without trimming to blister! PoligonBuilder is responsible for trimming.
            private List<LineCurve> GenerateIsoCurvesStage1()
            {

                List<LineCurve> isoLines = new List<LineCurve>(samplePoints.Count);
                for (int i = 0; i < samplePoints.Count; i++)
                {
                    Vector3d direction = Vector3d.CrossProduct((connectionLines[i].PointAtEnd - connectionLines[i].PointAtStart), Vector3d.ZAxis);
                    //direction = StraigtenVector(direction);
                    LineCurve isoLine = Geometry.GetIsoLine(samplePoints[i], direction, Setups.IsoRadius, obstacles);
                    if (isoLine == null) continue;
                    isoLines.Add(isoLine);
                }
                return isoLines;
            }

            private List<LineCurve> GenerateIsoCurvesStage2()
            {
                List<LineCurve> isoLines = new List<LineCurve>(samplePoints.Count);
                for (int i = 0; i < samplePoints.Count; i++)
                {
                    Vector3d direction = Vector3d.CrossProduct((proxLines[i].PointAtEnd - proxLines[i].PointAtStart), Vector3d.ZAxis);
                    //direction = StraigtenVector(direction);
                    LineCurve isoLine = Geometry.GetIsoLine(samplePoints[i], direction, Setups.IsoRadius, obstacles);
                    if (isoLine == null) continue;
                    isoLines.Add(isoLine);
                }
                return isoLines;
            }

            private List<LineCurve> GenerateIsoCurvesStage3()
            {
                List<LineCurve> isoLines = new List<LineCurve>(samplePoints.Count);
                for (int i = 0; i < samplePoints.Count; i++)
                {
                    Vector3d direction = Vector3d.CrossProduct((proxLines[i].PointAtEnd - proxLines[i].PointAtStart), Vector3d.ZAxis);
                    Vector3d direction2 = Vector3d.CrossProduct((connectionLines[i].PointAtEnd - connectionLines[i].PointAtStart), Vector3d.ZAxis);
                    //Vector3d sum_direction = StraigtenVector(direction + direction2);
                    Vector3d sum_direction = direction + direction2;
                    LineCurve isoLine = Geometry.GetIsoLine(samplePoints[i], sum_direction, Setups.IsoRadius, obstacles);
                    if (isoLine == null) continue;
                    isoLines.Add(isoLine);
                }
                return isoLines;
            }

            private IEnumerable<IEnumerable<LineCurve>> GenerateIsoCurvesStage3a(int raysCount, double stepAngle)
            {
                List<List<LineCurve>> isoLines = new List<List<LineCurve>>(samplePoints.Count);
                for (int i = 0; i < samplePoints.Count; i++)
                {
                    Vector3d direction = Vector3d.CrossProduct((proxLines[i].PointAtEnd - proxLines[i].PointAtStart), Vector3d.ZAxis);
                    Vector3d direction2 = Vector3d.CrossProduct((connectionLines[i].PointAtEnd - connectionLines[i].PointAtStart), Vector3d.ZAxis);
                    //Vector3d sum_direction = StraigtenVector(direction + direction2);
                    Vector3d sum_direction = direction + direction2;
                    double stepAngleInRadians = RhinoMath.ToRadians(stepAngle);
                    if (!sum_direction.Rotate(-raysCount * stepAngleInRadians, Vector3d.ZAxis)) continue;
                    //List<double>rotationAngles = Enumerable.Range(-raysCount, (2 * raysCount) + 1).Select(x => x* RhinoMath.ToRadians(stepAngle)).ToList();
                    List<LineCurve> currentIsoLines = new List<LineCurve>((2 * raysCount) + 1);
                    foreach (double angle in Enumerable.Range(0, (2 * raysCount) + 1)) 

                    {
                        if (!sum_direction.Rotate(stepAngleInRadians, Vector3d.ZAxis)) continue;
                        LineCurve isoLine = Geometry.GetIsoLine(samplePoints[i], sum_direction, Setups.IsoRadius, obstacles);
                        if (isoLine == null) continue;
                        currentIsoLines.Add(isoLine);
                    }
                    if (currentIsoLines.Count == 0) continue;
                    isoLines.Add(currentIsoLines);
                    //sum_direction.Rotate(RhinoMath.ToRadians(stepAngle), Vector3d.ZAxis)
                    //LineCurve isoLine = Geometry.GetIsoLine(samplePoints[i], sum_direction, Setups.IsoRadius, obstacles);
                    // if (isoLine == null) continue;
                    //isoLines.Add(isoLine);
                }
                return Combinators.Combinators.CartesianProduct(isoLines);
            }

            private List<List<LineCurve>> GenerateIsoCurvesStage4(int count, double radius)
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
                            LineCurve t_ray = TrimIsoCurve(ray);
                            //LineCurve t_ray = TrimIsoCurve(ray, samplePoints[i]);
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

            /// <summary>
            /// Trim curve 
            /// </summary>
            /// <param name="ray"></param>
            /// <param name="samplePoint"></param>
            /// <returns></returns>
            private LineCurve TrimIsoCurve(LineCurve ray)
            {
                LineCurve outLine = null;
                if (ray == null) return outLine;
                //  log.Debug("Ray not null");
                Geometry.FlipIsoRays(OrientationCircle, ray);
                Tuple<List<Curve>, List<Curve>> result = Geometry.TrimWithRegion(ray, blister.Outline);
                if (result.Item1.Count < 1) return outLine;
                // log.Debug("After trimming.");
                foreach (Curve crv in result.Item1)
                {
                    PointContainment test = blister.Outline.Contains(crv.PointAtNormalizedLength(0.5), Plane.WorldXY, 0.1);
                    if (test == PointContainment.Inside) return (LineCurve)crv;

                }
                return outLine;
            }

            /// <summary>
            /// Generates closed polygon around cell based on rays (cutters) combination
            /// </summary>
            /// <param name="rays"></param>
            private void PolygonBuilder_v2(List<LineCurve> rays)
            {
                // Trim incomming rays and build current working full ray aray.
                List<LineCurve> trimedRays = new List<LineCurve>(rays.Count);
                List<LineCurve> fullRays = new List<LineCurve>(rays.Count);
                foreach (LineCurve ray in rays)
                {
                    LineCurve trimed_ray = TrimIsoCurve(ray);
                    if (trimed_ray == null) continue;
                    trimedRays.Add(trimed_ray);
                    fullRays.Add(ray);
                }
                if (trimedRays.Count != rays.Count) log.Warn("After trimming there is less rays!");


                List<int> raysIndicies = Enumerable.Range(0, trimedRays.Count).ToList();

                //Generate Combinations array
                List<List<int>> raysIndiciesCombinations = Combinators.Combinators.UniqueCombinations(raysIndicies, 1);
                log.Debug(String.Format("Building cut data from {0} rays organized in {1} combinations", trimedRays.Count, raysIndiciesCombinations.Count));
                // Loop over combinations even with 1 ray
                foreach (List<int> combinationIndicies in raysIndiciesCombinations)
                //for (int combId = 0; combId < raysIndiciesCombinations.Count; combId++)
                {
                    List<LineCurve> currentTimmedIsoRays = new List<LineCurve>(combinationIndicies.Count);
                    List<LineCurve> currentFullIsoRays = new List<LineCurve>(combinationIndicies.Count);
                    List<PolylineCurve> pLinecurrentTimmedIsoRays = new List<PolylineCurve>(combinationIndicies.Count);
                    foreach (int combinationIndex in combinationIndicies)
                    {
                        currentTimmedIsoRays.Add(trimedRays[combinationIndex]);
                        currentFullIsoRays.Add(fullRays[combinationIndex]);
                        pLinecurrentTimmedIsoRays.Add(new PolylineCurve(new List<Point3d>() { trimedRays[combinationIndex].Line.From, trimedRays[combinationIndex].Line.To }));
                    }

                    log.Debug(String.Format("STAGE 1: Checking {0} rays.", currentTimmedIsoRays.Count));
                    List<CutData> localCutData = new List<CutData>(2);
                    // STAGE 1: Check if each ray in combination, can cut sucessfully blister.

                    // Convert LineCurve to PolylineCurve....

                    localCutData.Add(VerifyPath(pLinecurrentTimmedIsoRays));
                    log.Debug("STAGE 1: Pass.");
                    //log.Debug(String.Format("RAYS KURWA : {0}", combinations[combId].Count));
                    // STAGE 2: Looking for 1 (ONE) continouse cutpath...
                    // Generate Continouse Path, If there is one curve in combination, PathBuilder will return that curve, so it can be checked.
                    PolylineCurve curveToCheck = PathBuilder(currentTimmedIsoRays);
                    // If PathBuilder retun any curve... (ONE)
                    if (curveToCheck != null)
                    {
                        // Remove very short segments
                        Polyline pLineToCheck = curveToCheck.ToPolyline();
                        //pLineToCheck.DeleteShortSegments(Setups.CollapseTolerance);
                        // Look if end of cutting line is close to existing point on blister. If tolerance is smaller snap to this point
                        curveToCheck = pLineToCheck.ToPolylineCurve();
                        //curveToCheck = Geometry.SnapToPoints(curveToCheck, blister.Outline, Setups.SnapDistance);
                        // NOTE: straighten parts of curve????
                        localCutData.Add(VerifyPath(curveToCheck));
                        log.Debug("STAGE 2: Pass.");
                    }


                    foreach (CutData cutData in localCutData)
                    {
                        if (cutData == null) continue;
                        //cutData.TrimmedIsoRays = currentTimmedIsoRays;
                        cutData.IsoSegments = currentFullIsoRays;
                        cutData.Obstacles = obstacles;
                        cuttingData.Add(cutData);
                    }
                }
            }

            /// <summary>
            /// Takes group of separate cutting lines, and tries to find continous cuttiing path.
            /// </summary>
            /// <param name="cutters">cutting iso line.</param>
            /// <returns> Joined PolylineCurve if path was found. null if not.</returns>
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
                        if (inter.Count == 0) break;
                        //If exist, Store it
                        else intersectionsData.Add(inter);
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
                else
                {
                    pLine = new PolylineCurve(new List<Point3d> { cutters[0].Line.From, cutters[0].Line.To });
                }
                //if (pLine != null) log.Info(pLine.ClosedCurveOrientation().ToString());
                return pLine;
            }

            private CutData VerifyPath(PolylineCurve pathCrv)
            {
                return VerifyPath(new List<PolylineCurve>() { pathCrv });
            }
            private CutData VerifyPath(List<PolylineCurve> pathCrv)
            {
                log.Debug(string.Format("Verify path. Segments: {0}", pathCrv.Count));
                if (pathCrv == null) return null;
                // Check if this curves creates closed polygon with blister edge.
                List<Curve> splitters = pathCrv.Cast<Curve>().ToList();
                List<Curve> splited_blister = Geometry.SplitRegion(blister.Outline, splitters);
                // If after split there is less then 2 region it means nothing was cutted and bliseter stays unchanged
                if (splited_blister == null) return null;
                if (splited_blister.Count < 2) return null;

                log.Debug(string.Format("Blister splitited onto {0} parts", splited_blister.Count));
                Polyline pill_region = null;
                List<PolylineCurve> cutted_blister_regions = new List<PolylineCurve>();

                // Get region with pill
                foreach (Curve s_region in splited_blister)
                {
                    if (!s_region.IsValid || !s_region.IsClosed) continue;
                    RegionContainment test = Curve.PlanarClosedCurveRelationship(s_region, pill, Plane.WorldXY, Setups.GeneralTolerance);
                    if (test == RegionContainment.BInsideA) s_region.TryGetPolyline(out pill_region);
                    else if (test == RegionContainment.Disjoint)
                    {
                        Polyline cutted_blister_region = null;
                        s_region.TryGetPolyline(out cutted_blister_region);
                        cutted_blister_regions.Add(cutted_blister_region.ToPolylineCurve());
                    }
                    else return null;
                }
                if (pill_region == null) return null;
                PolylineCurve pill_region_curve = pill_region.ToPolylineCurve();

                // Chceck if only this pill is inside pill_region, After checking if pill region exists of course....
                log.Debug("Chceck if only this pill is inside pill_region.");
                foreach (Cell cell in blister.Cells)
                {
                    if (cell.id == this.id) continue;
                    RegionContainment test = Curve.PlanarClosedCurveRelationship(cell.pillOffset, pill_region_curve, Plane.WorldXY, Setups.GeneralTolerance);
                    if (test == RegionContainment.AInsideB)
                    {
                        log.Debug("More then one pill in cutout region. CutData creation failed.");
                        return null;
                    }
                }
                log.Debug("Check smallest segment size requerment.");
                // Check if smallest segment from cutout blister is smaller than some size.
                PolylineCurve pill_region_Crv = pill_region.ToPolylineCurve();
                PolylineCurve bbox = Geometry.MinimumAreaRectangleBF(pill_region_Crv);
                Line[] pill_region_segments = pill_region.GetSegments().OrderBy(line => line.Length).ToArray();
                if (pill_region_segments[0].Length > Setups.MinimumCutOutSize) return null;
                log.Debug("CutData created.");
                return new CutData(pill_region_Crv, pathCrv, cutted_blister_regions);

            }

            #endregion

            public JObject GetJSON()
            {
                JObject data = new JObject();
                data.Add("PillIndex", this.id);
                // Add Anchor Data <- to be implement.
                data.Add("Anchor", Anchor.orientation.ToString());
                // Add Cutting Instruction
                if (bestCuttingData != null) data.Add("CutInstruction", bestCuttingData.GetJSON());
                else data.Add("CutInstruction", new JArray());
                return data;
            }
        }
        */

        /*
        public class CutData
        {
            private static readonly ILog log = LogManager.GetLogger("Blistructor.CutData");
            private List<PolylineCurve> path;
            private PolylineCurve polygon;
            public List<PolylineCurve> BlisterLeftovers;
            public List<LineCurve> bladeFootPrint;
            // public List<LineCurve> bladeFootPrint2;
            public List<Curve> obstacles;
            public List<Line> isoSegments;
            public List<Line> segments;

            public CutData()
            {
                segments = new List<Line>();
                isoSegments = new List<Line>();
                bladeFootPrint = new List<LineCurve>();
            }

            private CutData(PolylineCurve polygon, List<PolylineCurve> path) : this()
            {
                this.path = path;
                this.polygon = polygon;
            }

            public CutData(PolylineCurve polygon, List<PolylineCurve> path, PolylineCurve blisterLeftover) : this(polygon, path)
            {
                BlisterLeftovers = new List<PolylineCurve>() { blisterLeftover };
                //GenerateBladeFootPrint();
            }

            public CutData(PolylineCurve polygon, List<PolylineCurve> path, List<PolylineCurve> blisterLeftovers) : this(polygon, path)
            {
                BlisterLeftovers = blisterLeftovers;
                // GenerateBladeFootPrint();
            }

            public List<PolylineCurve> Path { get { return path; } }
           
            public PolylineCurve Polygon { get { return polygon; } }
            
            public List<Curve> Obstacles { set { obstacles = value; } }

            public int EstimatedCuttingCount
            {
                get
                {
                    int count = 0;
                    foreach (PolylineCurve pline in Path)
                    {
                        foreach (Line line in pline.ToPolyline().GetSegments())
                        {
                            count += GetCuttingPartsCount(line);
                        }
                    }
                    return count;
                }
            }

            public int RealCuttingCount
            {
                get
                {
                    if (bladeFootPrint == null) return -1;
                    return bladeFootPrint.Count;
                }
            }

            public int Count
            {
                get
                {
                    return polygon.PointCount - 1;
                }
            }

            public List<LineCurve> IsoSegments
            {
                get { return isoSegments.Select(line => new LineCurve(line)).ToList(); }
                set { isoSegments = value.Select(x => x.Line).ToList(); }
            }

            public List<LineCurve> Segments { get { return segments.Select(line => new LineCurve(line)).ToList(); } }


            public bool GenerateBladeFootPrint()
            {
                if (isoSegments == null || segments == null) return false;
                if (!GenerateSegments()) return false;
                //  log.Info("Data are ok.");
                // Loop by all paths and generate Segments and IsoSegments
                for (int i =0; i< segments.Count; i++)
                {
                    List<LineCurve> footPrint = GetKnifeprintPerSegment(segments[i], isoSegments[i]);
                    if (footPrint.Count == 0) return false;
                    bladeFootPrint.AddRange(footPrint);
                }
                log.Info(String.Format("Generated {0} Blade Footpronts.", bladeFootPrint.Count));
                return true;
            }

            public bool GenerateSegments()
            {
                if (path == null) return false;
                // Loop by all paths and generate Segments
                segments = new List<Line>();
                foreach (PolylineCurve pline in Path)
                {
                    foreach (Line ln in pline.ToPolyline().GetSegments())
                    {
                        segments.Add(ln);
                    }
                }
                return true;
            }


            public bool RecalculateIsoSegments(Curve orientationGuideCurve)
            {
                if (polygon == null || path == null) return false;
                //  log.Info("Data are ok.");
                // Loop by all paths and generate Segments and IsoSegments
                segments = new List<Line>();
                isoSegments = new List<Line>();
                foreach (PolylineCurve pline in Path)
                {
                    foreach (Line ln in pline.ToPolyline().GetSegments())
                    {
                        segments.Add(ln);
                        LineCurve cIsoLn = Geometry.GetIsoLine(ln.PointAt(0.5), ln.UnitTangent, Setups.IsoRadius, obstacles);
                        if (cIsoLn == null) return false;
                        Geometry.FlipIsoRays(orientationGuideCurve, cIsoLn);
                        Line isoLn = cIsoLn.Line;
                        if (isoLn == null) throw new InvalidOperationException("Computing IsoSegment failed during BladeFootPrint Generation.");
                        isoSegments.Add(isoLn);
                    }
                }
                return true;
            }

            public List<LineCurve> GetKnifeprintPerSegment(Line segment, Line isoSegment)
            {
                List<Point3d> knifePts = new List<Point3d>();
                List<LineCurve> knifeLines = new List<LineCurve> ();
                int cutCount = GetCuttingPartsCount(segment);
                // Add Knife tolerance
                segment.Extend(Setups.BladeTol, Setups.BladeTol);

                List<double> lineT = new List<double>() { 0.0, 1.0 };
                int segmentSide = -1; // id 0 -> From side is out, 1 -> To side is out, -1 -> none is out.
                
                foreach(double t in lineT)
                {
                    Point3d exSegmentPt = segment.PointAt(t);
                    // Check if extended point is still on isoSegment line.
                    Point3d testPt = isoSegment.ClosestPoint(exSegmentPt,true);
                   // if (testPt.DistanceTo(exSegmentPt) > Setups.GeneralTolerance) return knifeLines;
                    // Check if any side of the IsoSegment is out of blister...
                    double dist = exSegmentPt.DistanceTo(isoSegment.PointAt(t));
                    if (dist > Setups.IsoRadius / 2) segmentSide = (int)t;
                }

                if (segmentSide == 0)
                {
                   segment.Flip();
                   isoSegment.Flip();
                }

                // If Middle
                if (segmentSide == -1)
                {
                    // If only one segment
                    if (cutCount == 1)
                    {
                        Point3d cutStartPt = segment.From;
                        Point3d cutEndPt = cutStartPt + (segment.UnitTangent * Setups.BladeLength);
                        LineCurve cutPrint = new LineCurve(cutStartPt, cutEndPt);
                        Point3d testPt = isoSegment.ClosestPoint(cutEndPt, true);
                        double endDist = testPt.DistanceTo(cutEndPt);
                        log.Info(String.Format("EndPointDist{0}", endDist));
                        // Check if CutPrint is not out of isoSegment, if not thak it as blase posotion
                        if (endDist < Setups.GeneralTolerance) knifeLines.Add(new LineCurve(cutStartPt, cutEndPt));
                        // Blade posidion os out of possible location, apply fix, by moving it to the center.   
                        else
                        {
                           // knifeLines.Add(new LineCurve(cutStartPt, cutEndPt));
                           double startdDist = segment.From.DistanceTo(isoSegment.From);
                           double diffDist = startdDist - endDist; // This should be positive...
                           Vector3d translateVector =  -segment.UnitTangent * (endDist + (diffDist / 2));
                           cutPrint.Translate(translateVector);
                           knifeLines.Add(new LineCurve(cutPrint));
                        }
                        
                    }
                    //if more segments, try to distribute them evenly alogn isoSegment
                    else if (cutCount > 1)
                    {
                        // SHot segment by half of the blade on both sides.
                        segment.Extend(-Setups.BladeLength / 2, -Setups.BladeLength / 2);
                        LineCurve exSegment = new LineCurve(segment);
                        // Divide segment by parts
                        double[] divT = exSegment.DivideByCount(cutCount - 1, true);
                        knifePts.AddRange(divT.Select(t => exSegment.PointAt(t)).ToList());
                        // Add bladePrints
                        knifeLines.AddRange(knifePts.Select(pt => new LineCurve(pt - (segment.UnitTangent*Setups.BladeLength/2), pt + (segment.UnitTangent * Setups.BladeLength / 2))).ToList());                                                                       
                    }
                }
                // If not Middle (assumprion, IsoSegments are very long on one side. No checking for coverage.
                else
                {
                    Point3d cutStartPt = segment.From;
                    for (int j = 0; j < cutCount; j++)
                    {
                        Point3d cutEndPt = cutStartPt + (segment.UnitTangent * Setups.BladeLength);
                        Line cutPrint = new Line(cutStartPt, cutEndPt);
                        knifeLines.Add(new LineCurve(cutPrint));
                        cutStartPt = cutEndPt - (segment.UnitTangent * Setups.BladeTol);
                    }
                }
                return knifeLines;

            }

            private int GetCuttingPartsCount(Line line)
            {
                return (int)Math.Ceiling(line.Length / (Setups.BladeLength - (2 * Setups.BladeTol)));
            }

            public double GetArea()
            {
                AreaMassProperties prop = AreaMassProperties.Compute(polygon);
                return prop.Area;
            }

            public double GetPerimeter()
            {
                return polygon.GetLength();
            }

            /// <summary>
            /// Get last possition of blade.
            /// </summary>
            /// <returns>Point3d. Point3d(NaN,NaN,NaN) if there is no cutting data. </returns>
            public Point3d GetLastKnifePossition()
            {
                if (bladeFootPrint == null) return new Point3d(double.NaN, double.NaN, double.NaN);
                if (bladeFootPrint.Count == 0) return new Point3d(double.NaN, double.NaN, double.NaN);
                return bladeFootPrint.Last().PointAtNormalizedLength(0.5);
            }

            public JArray GetJSON()
            {
                JArray instructionsArray = new JArray();
                if (bladeFootPrint.Count == 0) return instructionsArray;
                foreach (LineCurve line in bladeFootPrint)
                {
                    //Angle
                    JObject cutData = new JObject();
                    double angle = Vector3d.VectorAngle(Vector3d.XAxis, line.Line.UnitTangent);
                    cutData.Add("Angle", angle);
                    //Point 
                    JArray pointArray = new JArray();
                    Point3d midPt = line.Line.PointAt(0.5);
                    pointArray.Add(midPt.X);
                    pointArray.Add(midPt.Y);
                    cutData.Add("Point", pointArray);
                    instructionsArray.Add(cutData);
                }
                return instructionsArray;
            }
        }

        */

        /*
        static class Setups
        {
            // All stuff in mm.
            // IMAGE
            // Later this will be taken from calibration data
            public const double CalibrationVectorX = 133.48341;
            public const double CalibrationVectorY = 127.952386;
            public const double Spacing = 0.15645;
            public const double Rotate = 0.021685;

            // GENERAL TOLERANCES
            public const double GeneralTolerance = 0.0001;
            public const double CurveFitTolerance = 0.2;
            public const double CurveDistanceTolerance = 0.05;  // Curve tO polyline distance tolerance.
            public const double IntersectionTolerance = 0.0001;
            public const double OverlapTolerance = 0.0001;
            // BLADE STUFF
            public const double BladeLength = 44.0;
            public const double BladeTol = 2.0;
            public const double BladeWidth = 3.0;
            
            // CARTESIAN
            public const double CartesianThickness = 5.0;
            public const double CartesianDepth = 3.0;
            public const double BlisterCartesianDistance = 3.0;
            public const double CartesianMaxWidth = 85.0;
            public const double CartesianMinWidth = 10.0;

            //OTHER
            public const double IsoRadius = 2000.0;
            public const double MinimumCutOutSize = 35.0;

            // SIMPLIFY PATH TOLERANCES
            public const double AngleTolerance = (0.5 * Math.PI) * 0.2;
            public const double CollapseTolerance = 1.0; // if path segment is shorter then this, it will be collapsed
            public const double SnapDistance = 1.0; // if path segment is shorter then this, it will be collapsed
        }
        */

        /*
        static class Geometry
        {
            //TODO: SnapToPoints could not check only poilt-point realtion byt also point-cyrve... to investigate
            //public static PolylineCurve SnapToPoints_v2(PolylineCurve moving, PolylineCurve stationary, double tolerance)
            //{
            //    stationary.ClosestPoint()

            //}

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

            public static void FlipIsoRays(Curve guideCrv, LineCurve crv)
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
                if (crvs.Count <= 1) return crvs;
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
                            PointContainment result = region.Contains(testPt, Plane.WorldXY, 0.000001);
                            if (result == PointContainment.Inside) inside.Add(part_crv);
                            else if (result == PointContainment.Outside) outside.Add(part_crv);
                            else if (result == PointContainment.Unset) throw new InvalidOperationException("Unset");
                            else 
                            {
                                
                                throw new InvalidOperationException(String.Format("Trim Failed- {0}", result.ToString()));
                            }
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

            public static Tuple<List<Curve>, List<Curve>> TrimWithRegion(List<Curve> crv, Curve region)
            {
                List<Curve> inside = new List<Curve>();
                List<Curve> outside = new List<Curve>();
                foreach (Curve c in crv)
                {
                    Tuple<List<Curve>, List<Curve>> result = Geometry.TrimWithRegion(c, region);
                    inside.AddRange(result.Item1);
                    outside.AddRange(result.Item2);
                }
                return Tuple.Create(inside, outside);
            }

            // BUGERSONS!!!!
            public static Tuple<List<List<Curve>>, List<List<Curve>>> TrimWithRegions(List<Curve> crv, List<Curve> regions)
            {
                List<List<Curve>> inside = new List<List<Curve>>();
                List<List<Curve>> outside = new List<List<Curve>>();
                foreach (Curve region in regions)
                {
                    Tuple<List<Curve>, List<Curve>> result = Geometry.TrimWithRegion(crv, region);
                    inside.Add(result.Item1);
                    outside.Add(result.Item2);
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

                GH_Delanuey.Connectivity del_con = GH_Delanuey.Solver.Solve_Connectivity(n2l, 0.0001, true);
                List<GH_Voronoi.Cell2> voronoi = GH_Voronoi.Solver.Solve_Connectivity(n2l, del_con, outline);

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
               
                    Circle fitCirc;
                    Circle.TryFitCircleToPoints(pts, out fitCirc);
                    Polyline poly = new Polyline(SortPtsAlongCurve(Point3d.CullDuplicates(pts, 0.0001), fitCirc.ToNurbsCurve()));
                    poly.Add(poly[0]);
                    poly.ReduceSegments(tolerance);
                    vCells.Add(new PolylineCurve(poly));
                    cells[i].voronoi = new PolylineCurve(poly);
                }
                return vCells;
            }

            public static PolylineCurve MinimumAreaRectangleBF(Curve crv)
            {
                Point3d centre = AreaMassProperties.Compute(crv).Centroid;
                double minArea = double.MaxValue;
                PolylineCurve outCurve = null;

                for (double i = 0; i < 180; i++)
                {
                    double radians = RhinoMath.ToRadians(i);
                    Curve currentCurve = crv.DuplicateCurve();
                    currentCurve.Rotate(radians, Vector3d.ZAxis, centre);
                    BoundingBox box = currentCurve.GetBoundingBox(false);
                    if (box.Area < minArea)
                    {
                        minArea = box.Area;
                        Rectangle3d rect = new Rectangle3d(Plane.WorldXY, box.Min, box.Max);
                        PolylineCurve r = rect.ToPolyline().ToPolylineCurve();
                        r.Rotate(-radians, Vector3d.ZAxis, centre);
                        outCurve = r;
                    }
                }
                return outCurve;
            }
       
            public static PolylineCurve PolylineThicken(PolylineCurve crv, double thickness) {
                
                List<Curve> Outline = new List<Curve>();
                Curve[] offser_1 = crv.Offset(Plane.WorldXY, thickness, Setups.GeneralTolerance, CurveOffsetCornerStyle.Sharp);
                if (offser_1.Length == 1) Outline.Add(offser_1[0]);
                else return null;
                Curve[] offser_2 = crv.Offset(Plane.WorldXY, -thickness, Setups.GeneralTolerance, CurveOffsetCornerStyle.Sharp);
                if (offser_2.Length == 1) Outline.Add(offser_2[0]);
                else return null ;

                if (Outline.Count != 2) return null;
                Outline.Add(new LineCurve(Outline[0].PointAtStart, Outline[1].PointAtStart));
                Outline.Add(new LineCurve(Outline[0].PointAtEnd, Outline[1].PointAtEnd));
                Curve[] result = Curve.JoinCurves(Outline);
                if (result.Length != 1) return null;
                return (PolylineCurve)result[0];
            }

            /// <summary>
            /// Set Counter-Clockwise direction of curve and set it domain to 0.0 - 1.0
            /// </summary>
            /// <param name="crv"></param>
            public static void UnifyCurve(Curve crv)
            {
                CurveOrientation orient = crv.ClosedCurveOrientation(Vector3d.ZAxis);
                if (orient == CurveOrientation.Clockwise)
                {
                    crv.Reverse();
                }
                crv.Domain = new Interval(0.0, 1.0);
            }
        }
        */
       
        
        public class Logger
        {
            private static readonly ILog log = LogManager.GetLogger("Blistructor.Logger");

            public static void Setup()
            {
                Hierarchy hierarchy = (Hierarchy)LogManager.GetRepository();
                hierarchy.Root.Level = Level.Info;

                PatternLayout patternLayout = new PatternLayout();
                patternLayout.ConversionPattern = "%5level %logger.%M - %message%newline";
                patternLayout.ActivateOptions();

                //FileAppender - Debug
                FileAppender debug_roller = new FileAppender();

                debug_roller.File = @"D:\PIXEL\Blistructor\debug_cutter.log";
                debug_roller.Layout = patternLayout;
                var levelFilter = new LevelRangeFilter();
                levelFilter.LevelMin = Level.Debug;
                debug_roller.AddFilter(levelFilter);
                //debug_roller.MaxSizeRollBackups = 5;
                //debug_roller.MaximumFileSize = "10MB";
                //debug_roller.RollingStyle = RollingFileAppender.RollingMode.Size;
                //debug_roller.StaticLogFileName = true;
                // debug_roller.LockingModel = new FileAppender.MinimalLock();
                debug_roller.ActivateOptions();
                debug_roller.AppendToFile = false;

                //FileAppender - Production
                FileAppender prod_roller = new FileAppender();
                prod_roller.File = @"D:\PIXEL\Blistructor\cutter.log";
                prod_roller.Layout = patternLayout;
                var levelFilter2 = new LevelRangeFilter();
                levelFilter2.LevelMin = Level.Info;
                prod_roller.AddFilter(levelFilter2);
                //prod_roller.MaxSizeRollBackups = 5;
                //prod_roller.MaximumFileSize = "10MB";
                //prod_roller.RollingStyle = RollingFileAppender.RollingMode.Size;
                //prod_roller.StaticLogFileName = true;
                //  prod_roller.LockingModel = new FileAppender.MinimalLock();
                prod_roller.ActivateOptions();
                prod_roller.AppendToFile = false;


                // Add to root
                // hierarchy.Root.AddAppender(debug_roller);
                hierarchy.Root.AddAppender(prod_roller);
                //hierarchy.Root.Appenders[0].L

                //MemoryAppender memory = new MemoryAppender();
                //memory.ActivateOptions();
                //hierarchy.Root.AddAppender(memory);


                hierarchy.Configured = true;
            }
            public static void ClearAllLogFile()
            {
                RollingFileAppender fileAppender = LogManager.GetRepository()
                          .GetAppenders().FirstOrDefault(appender => appender is RollingFileAppender) as RollingFileAppender;


                if (fileAppender != null && File.Exists(((RollingFileAppender)fileAppender).File))
                {
                    string path = ((RollingFileAppender)fileAppender).File;
                    log4net.Appender.FileAppender curAppender = fileAppender as log4net.Appender.FileAppender;
                    curAppender.File = path;

                    FileStream fs = null;
                    try
                    {
                        fs = new FileStream(path, FileMode.Create);
                    }
                    catch (Exception ex)
                    {
                        log.Error("Could not clear the file log", ex);
                    }
                    finally
                    {
                        if (fs != null)
                        {
                            fs.Close();
                        }

                    }
                }
            }
        }
        

        /*
        public static class Conturer
        {
            public static List<List<int[]>> getContours(string pathToImage, double tolerance)
            {
                Image<Gray, Byte>  img = new Image<Gray, byte>(pathToImage);
                UMat uimage = new UMat();
                VectorOfVectorOfPoint contours = new VectorOfVectorOfPoint();
                CvInvoke.FindContours(img, contours, null, RetrType.List, ChainApproxMethod.LinkRuns);
                List<List<int[]>> allPoints = new List<List<int[]>>();
                int count = contours.Size;
                int nContours = count;
                for (int i = 0; i < count; i++)
                {
                    using (VectorOfPoint contour = contours[i])
                    using (VectorOfPoint approxContour = new VectorOfPoint())
                    {
                        CvInvoke.ApproxPolyDP(contour, approxContour, CvInvoke.ArcLength(contour, true) * tolerance, true);
                        System.Drawing.Point[] pts = approxContour.ToArray();
                        List<int[]> contPoints = new List<int[]>();
                        for (int k = 0; k < pts.Length; k++)
                        {
                            int[] pointsCord = new int[2];
                            pointsCord[0] = pts[k].X;
                            pointsCord[1] = pts[k].Y;
                            contPoints.Add(pointsCord);
                        }
                        allPoints.Add(contPoints);
                    }
                }
                return allPoints;
            }
        }

        */
        // </Custom additional code>
        #endregion

    }
}