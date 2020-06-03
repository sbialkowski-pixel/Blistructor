using Diagrams.Delaunay;
using System;
using System.Collections.Generic;

namespace Diagrams.Voronoi
{
    /// <exclude />
    public sealed class Solver
    {
        /// <summary>This class cannot be constructed.</summary>
        private Solver()
        {
        }

        /// <summary>
        /// Solve the voronoi diagram using a Sorted Brute force approach.
        /// Works best with a collection of nodes which are spread along the x-direction.
        /// This function will renumber and sort the nodes.
        /// </summary>
        /// <param name="nodes">Nodes to solve for. This list will be renumbered and sorted.</param>
        /// <param name="outline">Initial boundary for every cell.</param>
        /// <returns>The voronoi cells. Order of cells is identical to the order of nodes.</returns>
        public static List<Cell2> Solve_BruteForce(Node2List nodes, IEnumerable<Node2> outline)
        {
            nodes = new Node2List(nodes);
            List<Node2> node2List;
            if (outline is List<Node2>)
            {
                node2List = (List<Node2>)outline;
            }
            else
            {
                node2List = new List<Node2>();
                node2List.AddRange(outline);
            }
            nodes.RenumberNodes();
            nodes.Sort(Node2List.NodeListSort.X);
            List<Cell2> cell2List = new List<Cell2>(nodes.Count);
            int num1 = nodes.Count - 1;
            for (int index = 0; index <= num1; ++index)
                cell2List.Add((Cell2)null);
            int num2 = nodes.Count - 1;
            for (int index1 = 0; index1 <= num2; ++index1)
            {
                if (nodes[index1] != null)
                {
                    Cell2 cell2 = new Cell2(nodes[index1], (IEnumerable<Node2>)node2List);
                    double num3 = cell2.Radius();
                    for (int index2 = index1 - 1; index2 >= 0; index2 += -1)
                    {
                        if (nodes[index2] != null)
                        {
                            if (nodes[index2].x >= cell2.M.x - num3)
                            {
                                if (cell2.Slice(nodes[index2]))
                                {
                                    if (cell2.C.Count != 0)
                                        num3 = cell2.Radius();
                                    else
                                        break;
                                }
                            }
                            else
                                break;
                        }
                    }
                    if (cell2.C.Count != 0)
                    {
                        int num4 = index1 + 1;
                        int num5 = nodes.Count - 1;
                        for (int index2 = num4; index2 <= num5; ++index2)
                        {
                            if (nodes[index2] != null)
                            {
                                if (nodes[index2].x <= cell2.M.x + num3)
                                {
                                    if (cell2.Slice(nodes[index2]))
                                    {
                                        if (cell2.C.Count != 0)
                                            num3 = cell2.Radius();
                                        else
                                            break;
                                    }
                                }
                                else
                                    break;
                            }
                        }
                        cell2List[nodes[index1].tag] = cell2;
                    }
                }
            }
            return cell2List;
        }

        /// <summary>Solve the voronoi diagram using a minimal connectivity map.</summary>
        /// <param name="nodes">Nodes to solve for.</param>
        /// <param name="diagram">Connectivity diagram. Can be obtained from a Delaunay mesh.</param>
        /// <param name="outline">Initial boundary for every cell.</param>
        public static List<Cell2> Solve_Connectivity(
          Node2List nodes,
          Connectivity diagram,
          IEnumerable<Node2> outline)
        {
            if (nodes == null)
                throw new ArgumentNullException(nameof(nodes));
            if (diagram == null)
                throw new ArgumentNullException(nameof(diagram));
            if (outline == null)
                throw new ArgumentNullException("boundary");
            List<Node2> node2List;
            if (outline is List<Node2>)
            {
                node2List = (List<Node2>)outline;
            }
            else
            {
                node2List = new List<Node2>();
                node2List.AddRange(outline);
            }
            nodes = new Node2List(nodes);
            nodes.RenumberNodes();
            List<Cell2> cell2List = new List<Cell2>(nodes.Count);
            int num1 = nodes.Count - 1;
            for (int index = 0; index <= num1; ++index)
                cell2List.Add((Cell2)null);
            int num2 = nodes.Count - 1;
            for (int node_index = 0; node_index <= num2; ++node_index)
            {
                if (nodes[node_index] != null)
                {
                    Cell2 cell2 = new Cell2(nodes[node_index], (IEnumerable<Node2>)node2List);
                    List<int> connections = diagram.GetConnections(node_index);
                    if (connections != null)
                    {
                        int num3 = connections.Count - 1;
                        for (int index1 = 0; index1 <= num3; ++index1)
                        {
                            int index2 = connections[index1];
                            if (index2 != node_index)
                                cell2.Slice(nodes[index2]);
                        }
                        cell2List[nodes[node_index].tag] = cell2;
                    }
                }
            }
            return cell2List;
        }
    }
}
