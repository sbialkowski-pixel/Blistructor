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

using GH_Voronoi = Grasshopper.Kernel.Geometry.Voronoi;
using GH_Delanuey = Grasshopper.Kernel.Geometry.Delaunay;

using Rhino.Geometry.Intersect;
using System.Linq;
using Combinators;
using System.IO;
using log4net;
using log4net.Appender;
using log4net.Core;
using log4net.Layout;
using log4net.Repository.Hierarchy;
using log4net.Filter;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Schema;
using Newtonsoft.Json.Serialization;

using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.CV.Util;


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

        private void RunScript(string PillsPath, string BlisterPath, Point3d calibPt, ref object Pills, ref object CuttedPolygons, ref object CuttingLines, ref object Rays, ref object LeftOvers, ref object AllCuttingPolygons, ref object Path, ref object JSON, ref object I, ref object AA, ref object anchAAUp, ref object anchMAUp, ref object anchPred)

       // private void RunScript(List<Polyline> Pills, Polyline Blister, int cellId, int iter1, int iter2, ref object A, ref object B, ref object C, ref object D, ref object E, ref object F, ref object G, ref object H, ref object I, ref object AA)
        {

            // <Custom code>
            ILog log = LogManager.GetLogger("Main");
            Logger.Setup();
            //Logger.ClearAllLogFile();

            log.Info("====NEW RUN====");

            // TODO: -DONE-: Przejechanie wszystkich blistrów i sprawdzenie jak działa -> szukanie błedów
            // TODO: -DON-E: Dodac sprawdzenie czy przed i po cieciu mam tyle samo tabletek. -> czy cały blister sie pociął. (ErrorType)
            // TODO: Adaptacyje anchory -> Aktualizacja anchorów (ich przemieszczania) wraz z procesem ciecia np. przypadek blistra 19.
            // TODO: AdvancedCutting -> blister 19.
            // TODO: -DONE-: (Część bedzie w logach) Obsługa braku możliwości technicznych pociecia (Za ciasno, za skomplikowany, nie da sie wprowadzić noża, pocięty kawałek wiekszy niż 34mm..)
            // TODO: -DONE-: (Bład wyswietlania) Brak BladeFootPrintów dla ostanich blistów oznaczonych jako anchor np jezeli dwa ostatnie trzeba rozciac. np. Blistr 31
            // TODO: Adaptacyjna kolejność ciecia - po każdej wycietej tabletce, nalezało by przesortowac cell tak aby wubierał najbliższe
            // TODO: BlisterFootPrint -> mądzej, bo teraz nie są brana pod uwage w ogóle Rays i ich możliwości tylko na pałe jest robiona 
            // TODO: Weryfikacja PolygonSelectora (patrz blister 6, dziwnie wybrał...)
            // TODO: -DONE???-: Generowanie JSONA, Obsługa wyjątków, lista errorów. struktura pliku
            // TODO: Generowani punktów kartezjana, sprawdzanie rozstawu, właczenie tego do JSON'a
            // TODO: Kalibracja punktu 0,0
            // TODO: Właczenie Conturera do Blistructora
            // TODO: "ładne" logowanie produkcyjne jak i debugowe.
            // TODO: Posprzątanie w klasach.

            /*States:
             * CTR_SUCCESS -> Cutting successful.
             * CTR_TO_TIGHT -> Pills are to tight. Cutting aborted.
             * CTR_ONE_PILL -> One pill on blister only. Nothing to do.
             * CTR_FAILED -> Cutting Failed. Cannot Found cutting paths for all pills. Blister is to complicated or it is uncuttable.
             * CTR_ANCHOR_LOCATION_ERR: Blister side to small to pick by both graspers or No place for graspers.
             */

            try
            {
                MultiBlister structor = new MultiBlister();

                JSON = structor.CutBlister(PillsPath, BlisterPath);
                // G = structor.CutBlister(Pills, Blister);
                if (structor.Queue.Count != 0)
                {
                    //E = structor.Queue[0].GetConnectionLines;
                    // F = structor.Queue[0].GetProxyLines;
                    //  G = structor.Queue[0].GetSamplePoints;
                    // GetObstacles will generate obstacels. not intended....
                    // H = structor.Queue[0].GetObstacles;
                    // I = structor.Queue[0].irVoronoi;
                    // F = structor.Queue[0].Outline;
                }
                
                Pills = structor.GetPillsGH;
                CuttedPolygons = structor.GetCuttedPolygons;
                CuttingLines = structor.GetCuttingLinesGH;
                Rays = structor.GetRaysGH;
                LeftOvers = structor.GetLeftOversGH;
                AllCuttingPolygons = structor.GetAllCuttingPolygonsGH;
                Path = structor.GetPathGH;
              
               //I = structor.mainOutline;
               //AA = structor.pillsss;


                anchMAUp = structor.anchor.maUpperLimitLine;
                anchAAUp = structor.anchor.aaUpperLimitLine;
                anchPred = structor.anchor.GrasperPossibleLocation;

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

        public class MultiBlister
        {
            private static readonly ILog log = LogManager.GetLogger("Main.Blistructor");
            public PolylineCurve mainOutline;
            public List<PolylineCurve> pillsss;
            /*
          private PolylineCurve mainOutline;
          private PolylineCurve aaBBox;
          private PolylineCurve maBBox;
          private LineCurve aaGuideLine;
          private LineCurve maGuideLine;
                */
            private int loopTolerance = 5;
            public List<Blister> Queue;
            public List<Blister> Cutted;
            public Point3d knifeLastPoint = new Point3d();
            public Anchor anchor;
            public List<Curve> worldObstacles;

            public MultiBlister()
            {
                Queue = new List<Blister>();
                Cutted = new List<Blister>();
            }

            #region PROPERTIES
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
                    for (int i = 0; i < Cutted.Count; i++)
                    {
                        GH_Path path = new GH_Path(i);
                        out_data.AddRange(Cutted[i].GetLeftOvers(), path);
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

                // TODO: Here should be return code to json and end of process
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
                    status = PerformCut();
                }
                catch
                {
                    status = CuttingState.CTR_OTHER_ERR;
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
                return cuttingResult;
            }

            private CuttingState PerformCut()
            {
                log.Info(String.Format("=== Start Cutting ==="));
                int initialPillCount = Queue[0].Cells.Count;
                if (Queue[0].ToTight) return CuttingState.CTR_TO_TIGHT;
                if (Queue[0].LeftCellsCount == 1) return CuttingState.CTR_ONE_PILL;

                int n = 0; // control
                // Main Loop
                while (Queue.Count > 0)
                {
                    // Extra control to not loop forever...
                    if (n > initialPillCount + loopTolerance) break;
                    log.Debug(String.Format(String.Format("==MainLoop: {0} ==", n)));
                    log.Info(String.Format(String.Format("Blisters Count: Queue: {0}, Cutted {1}", Queue.Count, Cutted.Count)));
                    // InnerLoop - Queue Blisters
                    for (int i = 0; i < Queue.Count; i++)
                    {
                        //DEbug break stuff

                        log.Debug(String.Format(String.Format("InnerLoop: {0}", i)));
                        if (Queue == null) continue;
                        Blister blister = Queue[i];
                        log.Info(String.Format("{0} Cells on Blister: {1} to cut.", blister.Cells.Count, i));
                        if (blister.IsDone)
                        {
                            log.Info("Blister is already cutted or is to tight for cutting.");
                            continue;
                        }
                        Tuple<Blister, Blister, List<Blister>> result = blister.CutNext(worldObstacles) ;
                        log.Debug(String.Format("Cutting Result: Cutout: {0} - Current Blister {1} - New Blisters {2}.", result.Item1, result.Item2, result.Item3.Count));
                        // If anything was cutted, add to list
                        if (result.Item1 != null)
                        {
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
                        }
                        else
                        {
                            log.Info("Updating blister");
                            blister = result.Item2;

                            // Sort rest of the cells by distance to the knife las move... knife possition taken form CutData
                            //Cutted.Last()
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

            //public JObject GetJSON()
            //{
            //    JObject data = new JObject();

            //    // Status
            //    JObject status = new JObject();
            //    status.Add("Code", null);
            //    status.Add("Description", null);
            //    data.Add("Status", status);
            //    data.Add("PillsDetected", initialPillCount);
            //    data.Add("PillsCutted", Cutted.Count);

            //    // Cutting Instructions
            //    JArray allCuttingInstruction = new JArray();
            //    foreach (Blister blister in Cutted)
            //    {
            //        allCuttingInstruction.Add(blister.Cells[0].GetJSON());
            //    }
            //    data.Add("CuttingData", allCuttingInstruction);
            //    return data;
            //}

            private JObject PrepareEmptyJSON()
            {
                JObject data = new JObject();
                data.Add("Status", null);
                data.Add("PillsDetected", null);
                data.Add("PillsCutted", null);
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
                }
            }

        }

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
        public class Anchor
        {

            public MultiBlister mBlister;
            public PolylineCurve mainOutline;
            public PolylineCurve aaBBox;
            public PolylineCurve maBBox;
            public LineCurve aaGuideLine;
            public LineCurve aaUpperLimitLine;
            public LineCurve maGuideLine;
            public LineCurve maUpperLimitLine;

            public List<LineCurve> GrasperPossibleLocation;


            public Anchor(MultiBlister mBlister)
            {
                this.mBlister = mBlister;
                GrasperPossibleLocation = new List<LineCurve>();
                //Build initial blister shape data
                mainOutline = mBlister.Queue[0].Outline;
             
                // minPoint = new Point3d(0, double.MaxValue, 0);
                BoundingBox blisterBB = mainOutline.GetBoundingBox(false);
                Rectangle3d rect = new Rectangle3d(Plane.WorldXY, blisterBB.Min, blisterBB.Max);
                maBBox = Geometry.MinimumAreaRectangleBF(mainOutline);
                Geometry.UnifyCurve(maBBox);
                aaBBox = rect.ToPolyline().ToPolylineCurve();
                Geometry.UnifyCurve(aaBBox);
                // Find lowest mid point on Blister MA Bounding Box
                List<Line> maSegments = new List<Line>(maBBox.ToPolyline().GetSegments());
                // List<Tuple<Line, Point3d>> maData = maSegments.OrderBy(line => line.PointAt(0.5).Y).Select(line => Tuple.Create(line, line.PointAt(0.5))).ToList();
                maGuideLine = new LineCurve(maSegments.OrderBy(line => line.PointAt(0.5).Y).ToList()[0]);
                // Normalize
          
                Plane perpFrame;
                maGuideLine.PerpendicularFrameAt(0.5, out perpFrame);
                maUpperLimitLine = new LineCurve(maGuideLine);
                maUpperLimitLine.Translate(perpFrame.XAxis * Setups.CartesianDepth);
                 
                // Find lowest mid point on Blister AA Bounding Box
                List<Line> aaSegments = new List<Line>(aaBBox.ToPolyline().GetSegments());
                aaGuideLine = new LineCurve(aaSegments.OrderBy(line => line.PointAt(0.5).Y).ToList()[0]);
                aaUpperLimitLine = new LineCurve(aaGuideLine);
                aaUpperLimitLine.Translate(Vector3d.YAxis * Setups.CartesianDepth);

                LineCurve fullPredLine = new LineCurve(aaGuideLine);
                fullPredLine.Translate(Vector3d.YAxis * Setups.CartesianDepth/2);
                //GrasperPossibleLocation.Add(fullPredLine);

                // Create controlPoint list
                double[] paramT = aaGuideLine.DivideByCount(50, true);
                //List<Point3d> controlPoints = new List<Point3d>(paramT.Length);
                List<double> limitedParamT = new List<double>(paramT.Length);
                foreach (double t in paramT)
                {
                    double parT;
                    if (mainOutline.ClosestPoint(aaGuideLine.PointAt(t), out parT, Setups.CartesianDepth / 2)) limitedParamT.Add(parT);
                }
                List<Point3d> extremePointsOnBlister = new List<Point3d>(){
                    mainOutline.PointAt(limitedParamT.First()),
                    mainOutline.PointAt(limitedParamT.Last())
                };
                List<double> fullPredLineParamT = new List<double>(paramT.Length);
                foreach (Point3d pt in extremePointsOnBlister)
                {
                    double parT;
                    if (fullPredLine.ClosestPoint(pt, out parT)) fullPredLineParamT.Add(parT);
                }

                // Shrink curve on both sides by half of Grasper width.
                fullPredLine = (LineCurve)fullPredLine.Trim(fullPredLineParamT[0], fullPredLineParamT[1]);
                Line tempLine = fullPredLine.Line;
                tempLine.Extend(-Setups.CartesianThickness / 2, -Setups.CartesianThickness / 2);
                // Move temporaly prefLine to the upper position
                fullPredLine = new LineCurve(tempLine);
                fullPredLine.Translate(Vector3d.YAxis * Setups.CartesianDepth / 2);
                Tuple<List<Curve>, List<Curve>> trimResult =  Geometry.TrimWithRegions(fullPredLine, mBlister.Queue[0].GetPills);
                if (trimResult.Item2.Count == 1) fullPredLine = (LineCurve)trimResult.Item2.First();
                fullPredLine.Translate(Vector3d.YAxis * -Setups.CartesianDepth / 2);

                GrasperPossibleLocation.Add(fullPredLine);

                // DOKONCZYC TUUUUUUUU
                // Te wszystkie wymairy, to zamiast odnosic do aaBoxa to przeba do zafixowanego 0,0 



            }

         //   public 


        }

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
                this.cells = cells.OrderBy(cell => cell.CoordinateIndicator).Reverse().ToList();
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

            public List<Curve> GetPills
            {
                get
                {
                    List<Curve> pills = new List<Curve>();
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

            public Tuple<Blister, Blister, List<Blister>> CutNext(List<Curve> worldObstacles)
            {

                //Check if 
                Cell found_cell = null;
                List<Blister> newBlisters = new List<Blister>(); ;
                int counter = 0;
                log.Debug(String.Format("There is still {0} cells on blister", cells.Count));
                //bool[] anchorSwitcher = new bool[2] { true, false };
                bool[] anchorSwitcher = new bool[2] { true, false };
                foreach (bool ommitAnchor in anchorSwitcher)
                {
                    counter = 0;
                    foreach (Cell currentCell in cells)
                    {
                        if (currentCell.TryCut(ommitAnchor, worldObstacles) == false)
                        {
                            counter++;
                            continue;
                        }
                        else
                        {
                            found_cell = currentCell;
                            log.Info(String.Format("Cut Path found for cell {0} after checking {1} cells", found_cell.id, counter));
                            break;
                        }
                    }
                    if (found_cell == null)
                    {
                        log.Warn("No cutting data generated for whole blister. Try to fined cutting data in anchored ...");
                        //return Tuple.Create<Blister, Blister, List<Blister>>(null, this, newBlisters);
                    }
                    else
                    {
                        break;
                    }
                }

                if (found_cell == null)
                {
                    log.Warn("No cutting data generated for whole blister.");

                    return Tuple.Create<Blister, Blister, List<Blister>>(null, this, newBlisters);
                }
                // If on blister was only one cell, after cutting is status change to Alone, so just return it, without any leftover blisters. 
                if (found_cell.State == CellState.Alone)
                {
                    log.Info(String.Format("Cell {0}. That was last cell on blister.", found_cell.id));
                    return Tuple.Create<Blister, Blister, List<Blister>>(this, null, newBlisters);
                }

                log.Info(String.Format("Cell {0}. That was NOT last cell on blister.", found_cell.id));
                log.Info("Updating current blister outline. Creating cutout blister to store.");

                // If more cells are on blister, replace outline of current blister by first curve from the list...
                // Update current blister outline
                outline = found_cell.bestCuttingData.BlisterLeftovers[0];
                // Create new blister with cutted pill
                Blister cutted = new Blister(found_cell, found_cell.bestCuttingData.Polygon);
                cells.RemoveAt(counter);
                // Deal with more then one leftover
                // remove all cells which are not belong to this blister anymore...
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
                // if (LeftCellsCount == 1) cells[0].State = CellState.Alone;
                log.Debug(String.Format("After removal {0} - Removed Cells {1}", cells.Count, removerdCells.Count));
                // Loop by Leftovers (ommit first, it is current blister) and create new blisters.

                log.Debug(String.Format("Loop by Leftovers  [{0}] (ommit first, it is current blister) and create new blisters.", found_cell.bestCuttingData.BlisterLeftovers.Count - 1));
                //int cellCount = 0;
                for (int j = 1; j < found_cell.bestCuttingData.BlisterLeftovers.Count; j++)
                {
                    PolylineCurve blisterLeftover = found_cell.bestCuttingData.BlisterLeftovers[j];
                    Blister newBli = new Blister(removerdCells, blisterLeftover);
                    // Verify if all cells were used.
                    //cellCount += newBli.Cells.Count;
                    newBlisters.Add(newBli);
                }
                //if (cellCount != removerdCells.Count) throw new InvalidOperationException("Number of cells not equal Befor and After new Blisters creation.");

                return Tuple.Create<Blister, Blister, List<Blister>>(cutted, this, newBlisters);
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
                //if (cells[0].PossibleAnchor) return new List<LineCurve>();
                if (cells[0].bestCuttingData == null) return new List<PolylineCurve>();

                // cells[0].bestCuttingData.GenerateBladeFootPrint();
                return cells[0].bestCuttingData.Path;
            }

            public List<LineCurve> GetCuttingLines()
            {
                // !!!============If cell is anchor it probably doesn't have cutting stuff... To validate===========!!!!
                //if (cells[0].PossibleAnchor) return new List<LineCurve>();
                if (cells[0].bestCuttingData == null) return new List<LineCurve>();

                // cells[0].bestCuttingData.GenerateBladeFootPrint();
                return cells[0].bestCuttingData.bladeFootPrint;
            }
            public List<LineCurve> GetIsoRays()
            {
                // !!!============If cell is anchor it probably doesn't have cutting stuff... To validate===========!!!!
                //if (cells[0].PossibleAnchor) return new List<LineCurve>();
                if (cells[0].bestCuttingData == null) return new List<LineCurve>();
                // cells[0].bestCuttingData.GenerateBladeFootPrint();
                return cells[0].bestCuttingData.IsoRays;
            }
            public List<PolylineCurve> GetLeftOvers()
            {
                // !!!============If cell is anchor it probably doesn't have cutting stuff... To validate===========!!!!
                // if (cells[0].PossibleAnchor) return new List<PolylineCurve>();
                // cells[0].bestCuttingData.GenerateBladeFootPrint();
                if (cells[0].bestCuttingData == null) return new List<PolylineCurve>();
                return cells[0].bestCuttingData.BlisterLeftovers;
            }
            public List<PolylineCurve> GetAllPossiblePolygons()
            {
                // !!!============If cell is anchor it probably doesn't have cutting stuff... To validate===========!!!!
                // if (cells[0].PossibleAnchor) return new List<PolylineCurve>();
                // cells[0].bestCuttingData.GenerateBladeFootPrint();
                if (cells[0].bestCuttingData == null) return new List<PolylineCurve>();
                return cells[0].bestCuttingData.BlisterLeftovers;
            }


            #endregion
        }

        public enum CellState { Queue = 0, Cutted = 1, Alone = 2 };

        public class Cell
        {
            private static readonly ILog log = LogManager.GetLogger("Blistructor.Cell");

            public int id;

            // Parent Blister
            private Blister blister;

            // States
            private CellState state = CellState.Queue;
            public bool PossibleAnchor = false;
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
                    PolylineCurve outPline;
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
                    return PillCenter.X + PillCenter.Y * 10000;
                }
            }

            public NurbsCurve OrientationCircle { get; private set; }

            public CellState State { get { return state; } }

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
                PolygonBuilder_v2(GenerateIsoCurvesStage3());
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
            public CutData PolygonSelector()
            {
                // Order by number of cuts to be performed.
                cuttingData = cuttingData.OrderBy(x => x.EstimatedCuttingCount).ToList();
                // Limit only to lower number of cuts
                List<CutData> selected = cuttingData.Where(x => x.EstimatedCuttingCount == cuttingData[0].EstimatedCuttingCount).ToList();
                //Then sort by perimeter
                selected = selected.OrderBy(x => x.GetPerimeter()).ToList();
                //Pick best one.
                bestCuttingData = selected[0];
                bestCuttingData.GenerateBladeFootPrint();
                return bestCuttingData;
                // bestCuttingData = cuttingData[0];
            }

            public void PolygonSelector2()
            {
                /*
                List<PolylineCurve> output = new List<PolylineCurve>();
                 List<CutData> selected = cuttingData.Where(x => x.Count == cuttingData[0].Count).ToList();
                foreach(CutData cData in selected){
                  output.Add(cData.Path);
                 }
                 Here some more filtering, if more polygons hase same number of cutting segments...
                 Dummy get first...
                 if (selected.Count > 0){
                   bestCuttingData = selected[0];
                 }
                 return output;
                */
            }

            public bool TryCut(bool ommitAnchor, List<Curve> worldObstacles)
            {

                log.Info(String.Format("Trying to cut cell id: {0} with status: {1}", id, state));
                // If cell is cutted, dont try to cut it again... It supose to be in cutted blisters list...
                if (state == CellState.Cutted) return false;


                // If cell is not surrounded by other cell, update data
                log.Debug(String.Format("Check if cell is alone on blister: No. adjacent cells: {0}", adjacentCells.Count));
                if (adjacentCells.Count == 0)
                {
                    state = CellState.Alone;
                    log.Debug("This is last cell on blister.");
                    return true;
                }
                // If cell is marekd as possible anchor, also dont try to cut
                if (ommitAnchor == true && PossibleAnchor == true)
                {
                    log.Info("Marked as anchored. Omitting");
                    return false;
                }

                // If still here, try to cut 
                log.Debug("Perform cutting data generation");
                if (GenerateSimpleCuttingData_v2(worldObstacles))
                {
                    state = CellState.Cutted;
                    RemoveConnectionData();
                    PolygonSelector();
                    return true;
                }
                else return false;

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
                        pLineToCheck.DeleteShortSegments(Setups.CollapseTolerance);
                        // Look if end of cutting line is close to existing point on blister. If tolerance is smaller snap to this point
                        curveToCheck = pLineToCheck.ToPolylineCurve();
                        curveToCheck = Geometry.SnapToPoints(curveToCheck, blister.Outline, Setups.SnapDistance);
                        // NOTE: straighten parts of curve????
                        localCutData.Add(VerifyPath(curveToCheck));
                        log.Debug("STAGE 2: Pass.");
                    }

                    //List<CutData> localCutData = new List<CutData>(2)
                    //{
                    //    // Verify if path is cuttable
                    //    VerifyPath(curveToCheck),
                    //    // Now check if each cutter seperatly can divide bliter and generates cutting data. 
                    //    // Internally it is looking only for combination of 2 cutters and above. One cutter was handled in stage 1.

                    //    VerifyPath(pLineCombination)
                    //};

                    foreach (CutData cutData in localCutData)
                    {
                        if (cutData == null) continue;
                        cutData.TrimmedIsoRays = currentTimmedIsoRays;
                        cutData.IsoRays = currentFullIsoRays;
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
                        log.Warn("More then one pill in cutout region. CutData creation failed.");
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
                data.Add("Anchor", null);
                // Add Cutting Instruction
                if (bestCuttingData != null) data.Add("CutInstruction", bestCuttingData.GetJSON());
                else data.Add("CutInstruction", new JArray());
                return data;
            }
        }

        public class CutData
        {
            private static readonly ILog log = LogManager.GetLogger("Blistructor.CutData");
            private List<PolylineCurve> path;
            private PolylineCurve polygon;
            public List<PolylineCurve> BlisterLeftovers;
            public List<LineCurve> bladeFootPrint;
            private List<Curve> obstacles;


            public CutData()
            {
                TrimmedIsoRays = new List<LineCurve>();
                IsoRays = new List<LineCurve>();
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

            public List<LineCurve> TrimmedIsoRays { set; get; }

            public List<LineCurve> IsoRays { set; get; }

            public bool Cuttable
            {
                get
                {
                    if (IsoRays.Count > 0) return true;
                    else return false;
                }
            }

            public void GenerateBladeFootPrint()
            {
                log.Info("===GENERATE CUTTING FOOTPRINT===");
                if (polygon == null || path == null) return;
                //  log.Info("Data are ok.");
                // Loop by all paths
                List<Line> segments = new List<Line>();
                foreach (PolylineCurve pline in Path)
                {
                    segments.AddRange(pline.ToPolyline().GetSegments().ToList());
                }


                // foreach (PolylineCurve pline in Path)
                // { 
                //Line[] segments = pline.ToPolyline().GetSegments();
                //log.Info(String.Format("{0} segements to check", segments.Length));
                // If polyline is not proper (less then 2 contruction points), skip this.
                if (segments == null) return;
                //if (segments == null) continue;
                for (int i = 0; i < segments.Count; i++)
                {
                    Line seg = segments[i];
                    // First Segment. End point is on the blister Edge
                    if (i < segments.Count - 1 || segments.Count == 1)
                    {
                        // log.Info(String.Format("Segement id: {0} - Type A", i));
                        int parts = GetCuttingPartsCount(seg);
                        Point3d cutStartPt = seg.To + (seg.UnitTangent * Setups.BladeTol);
                        for (int j = 0; j < parts; j++)
                        {
                            // log.Info(String.Format("Parts generted: {0}", parts));
                            Point3d cutEndPt = cutStartPt + (seg.UnitTangent * -Setups.BladeLength);
                            Line cutPrint = new Line(cutStartPt, cutEndPt);
                            bladeFootPrint.Add(new LineCurve(cutPrint));
                            cutStartPt = cutEndPt + (seg.UnitTangent * Setups.BladeTol);
                        }
                    }

                    // Last segment.
                    else if (i == segments.Count - 1)
                    {
                        // log.Info(String.Format("Segement id: {0} - Type B", i));
                        int parts = GetCuttingPartsCount(seg);
                        Point3d cutStartPt = seg.From - (seg.UnitTangent * Setups.BladeTol);
                        for (int j = 0; j < parts; j++)
                        {
                            // log.Info(String.Format("Parts generted: {0}", parts));
                            Point3d cutEndPt = cutStartPt + (seg.UnitTangent * Setups.BladeLength);
                            Line cutPrint = new Line(cutStartPt, cutEndPt);
                            bladeFootPrint.Add(new LineCurve(cutPrint));
                            cutStartPt = cutEndPt - (seg.UnitTangent * Setups.BladeTol);
                        }
                    }
                }
                //  }
                log.Info(String.Format("Generated {0} Blade Footpronts.", bladeFootPrint.Count));
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

        static class Setups
        {
            // All stuff in mm.
            // IMAGE
            // Later this will be taken from calibration data
            public const double CalibrationVectorX = 133.48341;
            public const double CalibrationVectorY = 127.952386;
            public const double Spacing = 0.15645; 

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
            public const double IsoRadius = 1000.0;
            public const double MinimumCutOutSize = 35.0;

            // SIMPLIFY PATH TOLERANCES
            public const double AngleTolerance = (0.5 * Math.PI) * 0.2;
            public const double CollapseTolerance = 1.0; // if path segment is shorter then this, it will be collapsed
            public const double SnapDistance = 1.0; // if path segment is shorter then this, it will be collapsed
        }

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

                GH_Delanuey.Connectivity del_con = GH_Delanuey.Solver.Solve_Connectivity(n2l, 0.001, true);
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

                    Polyline poly = new Polyline(SortPtsAlongCurve(Point3d.CullDuplicates(pts, 0.001), cells[i].pill));
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

        // </Custom additional code>
        #endregion

    }
}