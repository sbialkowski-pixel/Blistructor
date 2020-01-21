using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Rhino.Geometry;
using Rhino.Geometry.Intersect;

namespace Blistructor
{
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

        public void GetInstructions()
        {

            if (Polygon != null && Path != null && trimmedIsoRays.Count > 0)
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

        public int GetCuttingPartsCount(Line line)
        {
            return (int)Math.Ceiling(line.Length / (Setups.BladeLength - (2 * Setups.BladeTol)));
        }

        public double GetArea()
        {
            AreaMassProperties prop = AreaMassProperties.Compute(Polygon);
            return prop.Area;
        }
    }
}
