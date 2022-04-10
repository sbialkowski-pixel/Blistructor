using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;

#if PIXEL
using Pixel.Rhino.FileIO;
using Pixel.Rhino.DocObjects;
using Pixel.Rhino.Geometry;
using Pixel.Rhino.Geometry.Intersect;
#else
using Rhino.Geometry;
using Rhino.Geometry.Intersect;
#endif

namespace Blistructor
{
    public class Graph
    {
        protected Blister Blister { get; set; }
        protected Diagrams.Node2List PillCenterNodes { get; set; }
        protected List<Diagrams.Node2> BlisterOutlineNodes { get; set; }
        protected Diagrams.Delaunay.Connectivity Connectivity { get; set; }

        public OrderedDictionary PillsGraph { get; private set; }

        public Graph(Blister blister, double diagramTolerance = 1e-6)
        {
            // Create proper data based on Blister
            Blister = blister;
            PillCenterNodes = PillsCentersToNodeList();
            BlisterOutlineNodes = BlisterToNode2();
            // Generate all standard Diagrams
            Connectivity = Diagrams.Delaunay.Solver.Solve_Connectivity(PillCenterNodes, diagramTolerance, true);
            // Fill public properties by PixGeo data
            PillsGraph = GetPillsGraph();
        }

        #region OrderedDict Getters

        public List<Pill> GetAdjacentPills(int pillID)
        {
            return (List<Pill>)PillsGraph[(object)pillID];
        }

        #endregion

        #region DIAGRAM-GEO CONVERTERS

        protected Diagrams.Node2List PillsCentersToNodeList()
        {
            Diagrams.Node2List n2l = new Diagrams.Node2List();
            foreach (Pill pill in Blister.Pills)
            {
                n2l.Append(new Diagrams.Node2(pill.Center.X, pill.Center.Y));
            }
            return n2l;
        }

        protected Diagrams.Node2List PillsOutlineToNodeList(int resolution)
        {
            Diagrams.Node2List n2l = new Diagrams.Node2List();

            foreach (Pill pill in Blister.Pills)
            {
                Point3d[] pts;
                pill.Outline.DivideByCount(resolution, false, out pts);
                foreach (Point3d pt in pts)
                {
                    n2l.Append(new Diagrams.Node2(pt.X, pt.Y));
                }
            }
            return n2l;
        }

        protected List<Diagrams.Node2> BlisterToNode2()
        {
            List<Diagrams.Node2> outline = new List<Diagrams.Node2>();
            foreach (Point3d pt in Blister.Outline.ToPolyline())
            {
                outline.Add(new Diagrams.Node2(pt.X, pt.Y));
            }
            return outline;
        }

        /// <summary>
        /// Adjacent Pills organized in OrderedDictionaies. 
        /// Dictionary keeps order of pills in bliser. As Key: Current pill.Id,
        /// As Values: List of Pill reerences.
        /// </summary>
        /// <returns>OrderedDictionary with adjacent pills.</returns>
        protected OrderedDictionary GetPillsGraph()
        {
            OrderedDictionary output = new OrderedDictionary(Connectivity.Count);
            for (int i = 0; i < Connectivity.Count; i++)
            {
                List<int> ids = Connectivity.GetConnections(i);
                List<Pill> pills = ids.Select(id => Blister.Pills[id]).ToList();
                output.Add(Blister.Pills[i].Id, pills);
            }
            return output;
        }

        #endregion

        public virtual OrderedDictionary Voronoi { get; protected set; }
        public virtual OrderedDictionary IrVoronoi { get; protected set; }

        public virtual PolylineCurve GetVoronoi(int pillID) { return null; }
        public virtual PolylineCurve GetIrVoronoi(int pillID) { return null; }
    }


    public class VoronoiGraph : Graph
    {
        private List<Diagrams.Voronoi.Cell2> VoronoiCells { get; set; }
        public override OrderedDictionary IrVoronoi { get; protected set; }
        public override OrderedDictionary Voronoi { get; protected set; }
        public VoronoiGraph(Blister blister, int irregularVoronoiSamples = 50, double diagramTolerance = 1e-6) : base(blister, diagramTolerance)
        {
            VoronoiCells = Diagrams.Voronoi.Solver.Solve_Connectivity(PillCenterNodes, Connectivity, BlisterOutlineNodes);
            Voronoi = VoronoiCellsToPolylineCurves(VoronoiCells);
            // Generate irregular Voronoi Diagram
            IrVoronoi = IrregularVoronoi(irregularVoronoiSamples);
        }

        #region OrderedDict Getters

        public override PolylineCurve GetVoronoi(int pillID)
        {
            return (PolylineCurve)Voronoi[(object)pillID];
        }

        public override PolylineCurve GetIrVoronoi(int pillID)
        {
            return (PolylineCurve)IrVoronoi[(object)pillID];
        }
        #endregion

        private OrderedDictionary VoronoiCellsToPolylineCurves(List<Diagrams.Voronoi.Cell2> voronoiDiagram)
        {

            OrderedDictionary output = new OrderedDictionary(voronoiDiagram.Count);
            foreach ((Diagrams.Voronoi.Cell2 voroCell, Pill pill) in voronoiDiagram.Zip(Blister.Pills, (voronoi, pill) => (voronoi, pill)))
            {
                output.Add(pill.Id, voroCell.ToPolyline().ToPolylineCurve());
            }
            return output;
        }

        #region DIAGRAMS METHODS
        /// <summary>
        /// Create Delauney Graphs based on Pills center points. 
        /// </summary>
        /// <param name="pills">List of pills to create graph</param>
        /// <param name="tolerance">Some tolerance. Value near 0.0 gives best results.</param>
        /// <returns>List of List of ints. First list is same order and length as pills. Consider pill index as i. Secound list contains id of pills interconected with this pill - j. So i pill has connection with j pills.</returns>
        //private Diagrams.Delaunay.Connectivity DelaunayGraph(double tolerance = 0.0001)
        //{
        //    return Diagrams.Delaunay.Solver.Solve_Connectivity(Connectivity, tolerance, true);
        //}

        /// <summary>
        /// Generate Irregular Voronoi diagram. This method is desinged for complex blister layout, where standard Voronoi failes. 
        /// This diagrams are more Distance Maps  between pills, then Voronoi diagrams. 
        /// </summary>
        /// <param name="connectivity">Delauney connectivity diagram</param>
        /// <param name="resolution">Number od VoronoiCell per pill, default = 50</param>
        /// <returns>List of irregular Voronoi cells</returns>
        private OrderedDictionary IrregularVoronoi(int resolution = 50)
        {
            Diagrams.Node2List n2l = PillsOutlineToNodeList(resolution);

            //Here, new connectivity must be computed.
            Diagrams.Delaunay.Connectivity del_con = Diagrams.Delaunay.Solver.Solve_Connectivity(n2l, 1e-6, true);
            List<Diagrams.Voronoi.Cell2> voronoi = Diagrams.Voronoi.Solver.Solve_Connectivity(n2l, del_con, BlisterOutlineNodes);

            OrderedDictionary output = new OrderedDictionary(Connectivity.Count);
            ConcurrentDictionary<int, Tuple<int, PolylineCurve>> test = new ConcurrentDictionary<int, Tuple<int, PolylineCurve>>(Environment.ProcessorCount * 2, Connectivity.Count);
            Parallel.ForEach(Blister.Pills, (pill, state, index) =>
            {
                int i = (int)index;
                List<Point3d> pts = new List<Point3d>();
                for (int j = 0; j < resolution - 1; j++)
                {
                    int glob_index = (i * (resolution - 1)) + j;
                    // vor.Add(voronoi[glob_index].ToPolyline());
                    if (voronoi[glob_index].C.Count == 0) continue;
                    Point3d[] vert = voronoi[glob_index].ToPolyline().ToArray();
                    foreach (Point3d pt in vert)
                    {
                        PointContainment result = Blister.Pills[i].Outline.Contains(pt, Plane.WorldXY, Setups.GeneralTolerance);
                        if (result == PointContainment.Outside)
                        {
                            pts.Add(pt);
                        }
                    }
                }
                Circle fitCirc = Geometry.FitCircle(pts);
                Polyline poly = new Polyline(Geometry.SortPtsAlongCurve(Point3d.CullDuplicates(pts, 0.0001), fitCirc.ToNurbsCurve()));
                poly.Add(poly[0]);
                poly.ReduceSegments(0.00001);
                //vCells.Add(new PolylineCurve(poly));
                test[i] = new Tuple<int, PolylineCurve>(Blister.Pills[i].Id, new PolylineCurve(poly));
            });

            List<bool> e = test.Select(data => { output.Insert(data.Key, data.Value.Item1, data.Value.Item2); return true; }).ToList();
            return output;
        }

        #endregion
    }
}
