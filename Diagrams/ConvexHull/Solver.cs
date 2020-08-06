using System;
using System.Collections.Generic;
using Rhino.Geometry;
//using Pixel.Geometry;  


namespace Diagrams.ConvexHull
{
	public class Solver
	{
		/// <summary>
		/// You cannot construct this class, use the shader functions directly.
		/// </summary>
		private Solver()
		{
		}

		/// <summary>
		/// Compute the convex hull of list of nodes.
		/// </summary>
		/// <param name="nodes">Nodes to wrap. May not contain null references.</param>
		/// <param name="hull">Index list describing the convex hull (closing segment not included)</param>
		public static bool Compute(Node2List nodes, List<int> hull)
		{
			if (nodes == null)
			{
				throw new ArgumentNullException("nodes");
			}
			if (hull == null)
			{
				throw new ArgumentNullException("hull");
			}
			List<bool> list = new List<bool>();
			hull.Clear();
			list.Clear();
			hull.Capacity = nodes.Count;
			list.Capacity = nodes.Count;
			if (nodes.Count == 0)
			{
				return false;
			}
			if (nodes.Count == 1)
			{
				return false;
			}
			if (nodes.Count == 2)
			{
				hull.Add(0);
				hull.Add(1);
				return true;
			}
			int num = nodes.Count - 1;
			for (int i = 0; i <= num; i++)
			{
				list.Add(item: false);
			}
			int num2 = -1;
			int num3 = -1;
			int num4 = nodes.Count - 1;
			for (int j = 0; j <= num4; j++)
			{
				if (nodes[j] != null)
				{
					num2 = j;
					num3 = j;
					break;
				}
			}
			if (num2 < 0)
			{
				return false;
			}
			int num5 = nodes.Count - 1;
			for (int k = 1; k <= num5; k++)
			{
				if (nodes[k] != null)
				{
					if (nodes[k].x < nodes[num2].x)
					{
						num2 = k;
					}
					else if (nodes[k].x == nodes[num2].x && nodes[k].y < nodes[num2].y)
					{
						num2 = k;
					}
				}
			}
			num3 = num2;
			do
			{
				int num6 = -1;
				int num7 = nodes.Count - 1;
				for (int l = 0; l <= num7; l++)
				{
					if (nodes[l] == null || list[l] || l == num3)
					{
						continue;
					}
					if (num6 == -1)
					{
						num6 = l;
						continue;
					}
					double num8 = CrossProduct(nodes[l], nodes[num3], nodes[num6]);
					if (num8 == 0.0)
					{
						if (DotProduct(nodes[num3], nodes[l], nodes[l]) > DotProduct(nodes[num3], nodes[num6], nodes[num6]))
						{
							num6 = l;
						}
					}
					else if (num8 < 0.0)
					{
						num6 = l;
					}
				}
				num3 = num6;
				list[num3] = true;
				hull.Add(num3);
			}
			while (num3 != num2);
			return true;
		}

		private static double CrossProduct(Node2 A, Node2 B, Node2 C)
		{
			return (B.x - A.x) * (C.y - A.y) - (C.x - A.x) * (B.y - A.y);
		}

		private static double DotProduct(Node2 A, Node2 B, Node2 C)
		{
			return (B.x - A.x) * (C.x - A.x) + (B.y - A.y) * (C.y - A.y);
		}

		public static Polyline ComputeHull(Node2List pts)
		{
			List<int> list = new List<int>();
			if (!Compute(pts, list))
			{
				return null;
			}
			Polyline polyline = new Polyline(list.Count);
			int num = list.Count - 1;
			for (int i = 0; i <= num; i++)
			{
				polyline.Add(pts[list[i]].x, pts[list[i]].y, 0.0);
			}
			polyline.Add(polyline[0]);
			return polyline;
		}
	}

}
