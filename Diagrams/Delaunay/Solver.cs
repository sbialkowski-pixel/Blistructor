//using Pixel.Geometry;
using Rhino.Geometry;
using System;
using System.Collections.Generic;

namespace Diagrams.Delaunay
{
    /// <exclude />
    public sealed class Solver
    {
        private Node2List m_nodes;
        private List<Face> m_faces;
        private int[] m_box_corners;

        /// <summary>This class cannot be constructed.</summary>
        private Solver()
        {
            this.m_nodes = new Node2List();
            this.m_faces = new List<Face>();
            this.m_box_corners = new int[4] { -1, -1, -1, -1 };
        }

        private bool Triangulate()
        {
            FaceExList dynamics = new FaceExList(this.m_nodes.Count);
            this.m_faces.Clear();
            this.m_faces.Capacity = this.m_nodes.Count * 2;
            bool flag;
            if (this.m_nodes == null)
                flag = false;
            else if (this.m_nodes.Count < 3)
            {
                flag = false;
            }
            else
            {
                this.CreateSuperBox(dynamics);
                List<FaceEx> F = new List<FaceEx>();
                int num1 = this.get_SuperBoxIndex(1) + 1;
                int num2 = this.get_SuperBoxIndex(2) - 1;
                for (int C = num1; C <= num2; ++C)
                {
                    Node2 node = this.m_nodes[C];
                    dynamics.MigrateStaticFaces(this.m_faces, node.x);
                    F.Clear();
                    if (dynamics.CullFaces(node.x, node.y, F) != 0)
                    {
                        EdgeList edgeList = new EdgeList(F);
                        edgeList.TrimHighValenceEdges();
                        int num3 = edgeList.Count - 1;
                        for (int index = 0; index <= num3; ++index)
                            dynamics.AddFace(edgeList[index].A, edgeList[index].B, C, this.m_nodes);
                    }
                }
                dynamics.MigrateRemainingFaces(this.m_faces);
                this.DestroySuperBox();
                this.SolveStaticOrientation();
                flag = true;
            }
            return flag;
        }

        /// <summary>Add 4 nodes and 2 triangles to the solution.</summary>
        private void CreateSuperBox(FaceExList dynamics)
        {
            int count = this.m_nodes.Count;
            double x0 = default(double); 
            double x1 = default(double); 
            double y0 = default(double);
            double y1 = default(double);
            this.m_nodes.BoundingBox(10.0, true, ref x0, ref x1, ref y0, ref y1);
            Node2 node1 = new Node2(x0, y0, -100);
            Node2 node2 = new Node2(x0, y1, -101);
            Node2 node3 = new Node2(x1, y0, -102);
            Node2 node4 = new Node2(x1, y1, -103);
            this.m_nodes.Append(node1);
            this.m_nodes.Append(node2);
            this.m_nodes.Append(node3);
            this.m_nodes.Append(node4);
            this.m_nodes.Sort(Node2List.NodeListSort.X);
            this.set_SuperBoxIndex(0, this.m_nodes.InternalList.IndexOf(node1));
            this.set_SuperBoxIndex(1, this.get_SuperBoxIndex(0) + 1);
            this.set_SuperBoxIndex(2, this.m_nodes.Count - 2);
            this.set_SuperBoxIndex(3, this.m_nodes.Count - 1);
            dynamics.AddFace(this.get_SuperBoxIndex(0), this.get_SuperBoxIndex(2), this.get_SuperBoxIndex(3), this.m_nodes);
            dynamics.AddFace(this.get_SuperBoxIndex(0), this.get_SuperBoxIndex(3), this.get_SuperBoxIndex(1), this.m_nodes);
        }

        /// <summary>
        /// Remove the super box indices from the nodelist and also
        /// remove all triangles that touch the super box.
        /// </summary>
        private void DestroySuperBox()
        {
            int num = 0;
            int count = m_faces.Count;
            int num2 = count - 1;
            for (int i = 0; i <= num2; i++)
            {
                if (!IsSuperBoxIndex(m_faces[i]))
                {
                    m_faces[num] = m_faces[i];
                    num++;
                }
            }
            if (num < count)
            {
                m_faces.RemoveRange(num, count - num);
            }
            int num3 = m_faces.Count - 1;
            for (int j = 0; j <= num3; j++)
            {
                m_faces[j].A -= 2;
                m_faces[j].B -= 2;
                m_faces[j].C -= 2;
            }
            m_nodes.RemoveAt(this.get_SuperBoxIndex(3));
            m_nodes.RemoveAt(this.get_SuperBoxIndex(2));
            m_nodes.RemoveAt(this.get_SuperBoxIndex(1));
            m_nodes.RemoveAt(this.get_SuperBoxIndex(0));
            m_faces.TrimExcess();
        }

        private int get_SuperBoxIndex(int c)
        {
            return this.m_box_corners[c];
        }

        private void set_SuperBoxIndex(int c, int Value)
        {
            this.m_box_corners[c] = Value;
        }

        private bool IsSuperBoxIndex(int index)
        {
            int c = 0;
            bool flag;
            while (index != this.get_SuperBoxIndex(c))
            {
                ++c;
                if (c > 3)
                {
                    flag = false;
                    goto label_5;
                }
            }
            flag = true;
        label_5:
            return flag;
        }

        private bool IsSuperBoxIndex(Face face)
        {
            int c = 0;
            bool flag;
            while (face.A != this.get_SuperBoxIndex(c))
            {
                if (face.B == this.get_SuperBoxIndex(c))
                {
                    flag = true;
                    goto label_9;
                }
                else if (face.C == this.get_SuperBoxIndex(c))
                {
                    flag = true;
                    goto label_9;
                }
                else
                {
                    ++c;
                    if (c > 3)
                    {
                        flag = false;
                        goto label_9;
                    }
                }
            }
            flag = true;
        label_9:
            return flag;
        }

        /// <summary>Make sure all triangles are oriented counter-clockwise</summary>
        private void SolveStaticOrientation()
        {
            int num = this.m_faces.Count - 1;
            for (int index = 0; index <= num; ++index)
            {
                int a = this.m_faces[index].A;
                int b = this.m_faces[index].B;
                int c = this.m_faces[index].C;
                Node2 node1 = this.m_nodes[a];
                Node2 node2 = this.m_nodes[b];
                Node2 node3 = this.m_nodes[c];
                if (Line2.Side(node1.x, node1.y, node2.x, node2.y, node3.x, node3.y) == Side2.Right)
                {
                    this.m_faces[index].B = c;
                    this.m_faces[index].C = b;
                }
            }
        }

        /// <summary>Remap the face corner indices back onto the original node order.</summary>
        private void RemapFaceIndices()
        {
            int num = this.m_faces.Count - 1;
            for (int index = 0; index <= num; ++index)
            {
                this.m_faces[index].A = this.m_nodes[this.m_faces[index].A].tag;
                this.m_faces[index].B = this.m_nodes[this.m_faces[index].B].tag;
                this.m_faces[index].C = this.m_nodes[this.m_faces[index].C].tag;
            }
        }

        /// <summary>Core Delaunay solver.</summary>
        /// <param name="nodes">Nodes to triangulate</param>
        /// <param name="jitter_amount">Amount of random noise. Make sure there is at least some noise
        /// if your input nodes are structured.</param>
        /// <returns>A list of triangular faces that connect indices in the [nodes] parameter.</returns>
        public static List<Face> Solve_Faces(Node2List nodes, double jitter_amount)
        {
            if (nodes == null)
                throw new ArgumentNullException(nameof(nodes));
            if (nodes.Count < 3)
                throw new InvalidOperationException("Insufficient nodes for a triangulation");
            Solver solver = new Solver();
            solver.m_nodes = new Node2List(nodes);
            solver.m_nodes.RenumberNodes();
            if (jitter_amount != 0.0)
                solver.m_nodes.JitterNodes(jitter_amount);
            List<Face> faceList;
            if (!solver.Triangulate())
            {
                faceList = (List<Face>)null;
            }
            else
            {
                solver.RemapFaceIndices();
                faceList = solver.m_faces;
            }
            return faceList;
        }

        /*
        /// <summary>Delaunay mesher.</summary>
        /// <param name="nodes">Nodes to triangulate</param>
        /// <param name="jitter_amount">Amount of random noise. Make sure there is at least some noise
        /// if your input nodes are structured.</param>
        /// <param name="faces">Face list output</param>
        /// <returns>Mesh instance</returns>
        public static Mesh Solve_Mesh(Node2List nodes, double jitter_amount, ref List<Face> faces)
        {
            faces = Solver.Solve_Faces(nodes, jitter_amount);
            Mesh mesh1;
            if (faces == null)
                mesh1 = (Mesh)null;
            else if (faces.Count == 0)
            {
                mesh1 = (Mesh)null;
            }
            else
            {
                Mesh mesh2 = new Mesh();
                int num1 = nodes.Count - 1;
                for (int index = 0; index <= num1; ++index)
                    mesh2.Vertices.Add(nodes[index].x, nodes[index].y, 0.0);
                int num2 = faces.Count - 1;
                for (int index = 0; index <= num2; ++index)
                    mesh2.Faces.AddFace(faces[index].A, faces[index].B, faces[index].C);
                mesh2.Normals.ComputeNormals();
                mesh1 = mesh2;
            }
            return mesh1;
        }
        */

        /// <summary>Connectivity solver. Returns a topological edge diagram of delaunay faces.</summary>
        /// <param name="nodes">Nodes to connect</param>
        /// <param name="jitter_amount">Amount of random motion.</param>
        /// <param name="include_convex_hull_edges">If true, the edges of the convex hull are included in the connectivity diagram.</param>
        /// <returns>Connectivity diagram for [nodes]</returns>
        public static Connectivity Solve_Connectivity(
          Node2List nodes,
          double jitter_amount,
          bool include_convex_hull_edges)
        {
            if (nodes == null)
                throw new ArgumentNullException(nameof(nodes));
            if (nodes.Count < 2)
                throw new InvalidOperationException("Insufficient nodes for a Connectivity diagram");
            List<Face> faces = (List<Face>)null;
            if (nodes.Count > 2)
                faces = Solver.Solve_Faces(nodes, jitter_amount);
            if (faces == null)
                faces = new List<Face>();
            Connectivity connectivity = new Connectivity();
            connectivity.SolveConnectivity(nodes, faces, include_convex_hull_edges);
            return connectivity;
        }
    }
}
