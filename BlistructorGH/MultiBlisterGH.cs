﻿using System.Collections.Generic;
using System.Linq;
using RhGeo = Rhino.Geometry;
using PixGeo = Pixel.Geometry;

using Grasshopper;
using Grasshopper.Kernel.Data;
using Blistructor;

namespace BlistructorGH
{
    public class MultiBlisterGH : MultiBlister
    {
        public MultiBlisterGH(int Limit1, int Limit2) : base(Limit1, Limit2)
        {

        }

        #region GH - PROPERTIES

        public DataTree<RhGeo.Curve> GetCuttedPillDataGH
        {
            get
            {
                DataTree<RhGeo.Curve> out_data = new DataTree<RhGeo.Curve>();
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
                    else out_data.AddRange(new List<RhGeo.Curve>(), new GH_Path(i, 1, 3));
                    // Cell Data
                    out_data.AddRange(cuttedBlister.GetPills(false), new GH_Path(i, 2, 0));
                    out_data.AddRange(cuttedCell.connectionLines, new GH_Path(i, 2, 1));
                    out_data.AddRange(cuttedCell.proxLines, new GH_Path(i, 2, 2));
                    if (cuttedCell.cuttingData != null) out_data.AddRange(cuttedCell.obstacles, new GH_Path(i, 2, 3));
                    else out_data.AddRange(new List<RhGeo.Curve>(), new GH_Path(i, 2, 3));
                    out_data.Add(cuttedCell.voronoi, new GH_Path(i, 2, 4));
                }
                return out_data;
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
                        anchors.Add(Queue[j].Cells[i].Anchor.state.ToString(), new GH_Path(j, i));
                    }
                }
                return anchors;
            }
        }

        public DataTree<RhGeo.PolylineCurve> GetPillsGH
        {
            get
            {
                DataTree<RhGeo.PolylineCurve> out_data = new DataTree<RhGeo.PolylineCurve>();
                //List<List<Curve>> out_data = new List<List<Curve>>();
                for (int i = 0; i < Cutted.Count; i++)
                {
                    GH_Path path = new GH_Path(i);
                    out_data.Add(Cutted[i].Cells[0].pill, path);
                }
                return out_data;
            }
        }

        public DataTree<RhGeo.PolylineCurve> GetAllCuttingPolygonsGH
        {
            get
            {
                DataTree<RhGeo.PolylineCurve> out_data = new DataTree<RhGeo.PolylineCurve>();
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

        public DataTree<RhGeo.Curve> GetCuttingLinesGH
        {
            get
            {
                DataTree<RhGeo.Curve> out_data = new DataTree<RhGeo.Curve>();
                //List<List<Curve>> out_data = new List<List<Curve>>();
                for (int i = 0; i < Cutted.Count; i++)
                {
                    GH_Path path = new GH_Path(i);
                    out_data.AddRange(Cutted[i].GetCuttingLines(), path);
                }
                return out_data;
            }
        }

        public DataTree<RhGeo.PolylineCurve> GetPathGH
        {
            get
            {
                DataTree<RhGeo.PolylineCurve> out_data = new DataTree<RhGeo.PolylineCurve>();
                //List<List<Curve>> out_data = new List<List<Curve>>();
                for (int i = 0; i < Cutted.Count; i++)
                {
                    GH_Path path = new GH_Path(i);
                    out_data.AddRange(Cutted[i].GetCuttingPath(), path);
                }
                return out_data;
            }
        }

        public DataTree<RhGeo.LineCurve> GetRaysGH
        {
            get
            {
                DataTree<RhGeo.LineCurve> out_data = new DataTree<RhGeo.LineCurve>();
                //List<List<Curve>> out_data = new List<List<Curve>>();
                for (int i = 0; i < Cutted.Count; i++)
                {
                    GH_Path path = new GH_Path(i);
                    out_data.AddRange(Cutted[i].GetIsoRays(), path);
                }
                return out_data;
            }
        }

        public DataTree<RhGeo.PolylineCurve> GetLeftOversGH
        {
            get
            {
                DataTree<RhGeo.PolylineCurve> out_data = new DataTree<RhGeo.PolylineCurve>();
                //List<List<Curve>> out_data = new List<List<Curve>>();
                for (int i = 0; i < Queue.Count; i++)
                {
                    GH_Path path = new GH_Path(i);
                    out_data.Add(Queue[i].Outline, path);
                }
                return out_data;
            }
        }

        public DataTree<RhGeo.Curve> GetObstaclesGH
        {
            get
            {
                DataTree<RhGeo.Curve> out_data = new DataTree<RhGeo.Curve>();
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

        public DataTree<RhGeo.Curve> GetUnfinishedCutDataGH
        {
            get
            {
                DataTree<RhGeo.Curve> out_data = new DataTree<RhGeo.Curve>();
                //List<List<Curve>> out_data = new List<List<Curve>>();
                for (int i = 0; i < Queue[0].Cells.Count; i++)
                {
                    Cell cell = Queue[0].Cells[i];
                    if (cell.cuttingData == null) continue;
                    if (cell.cuttingData.Count == 0) continue;
                    for (int j = 0; j < cell.cuttingData.Count; j++)
                    {
                        out_data.AddRange(cell.cuttingData[j].IsoSegments, new GH_Path(i, j, 0));
                        out_data.AddRange(cell.cuttingData[j].Segments, new GH_Path(i, j, 1));
                        out_data.AddRange(cell.cuttingData[j].obstacles, new GH_Path(i, j, 2));
                        out_data.AddRange(cell.cuttingData[j].Path, new GH_Path(i, j, 3));
                    }
                }
                return out_data;
            }
        }

        public DataTree<RhGeo.Curve> GetQueuePillDataGH
        {
            get
            {
                DataTree<RhGeo.Curve> out_data = new DataTree<RhGeo.Curve>();
                if (Queue.Count == 0) return out_data;
                //List<List<Curve>> out_data = new List<List<Curve>>();
                for (int j = 0; j < Queue.Count; j++)
                {
                    if (Queue[j].Cells.Count == 0) continue;
                    for (int i = 0; i < Queue[j].Cells.Count; i++)
                    {
                        Cell cell = Queue[j].Cells[i];
                        //if (cell.cuttingData == null) continue;
                        out_data.Add(cell.pill, new GH_Path(j, i, 0));
                        out_data.Add(cell.pillOffset, new GH_Path(j, i, 0));

                        out_data.AddRange(cell.proxLines, new GH_Path(j, i, 1));
                        out_data.AddRange(cell.connectionLines, new GH_Path(j, i, 2));

                        out_data.Add(cell.voronoi, new GH_Path(j, i, 3));
                    }
                }
                return out_data;
            }
        }

        #endregion
    }
}