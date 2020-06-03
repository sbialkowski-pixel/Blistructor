using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RhGeo = Rhino.Geometry;
using PixGeo = Pixel.Geometry;
using Emgu.CV.OCR;

namespace BlistructorGH
{
    public static class Convert
    {

        public static RhGeo.Point3d ToPix(PixGeo.Point3d pt) 
        {
            return new RhGeo.Point3d(pt.X, pt.Y, pt.Z);
        }

        public static RhGeo.PolylineCurve ToPix(PixGeo.Curve crv)
        {
            PixGeo.Polyline pline;
            crv.TryGetPolyline(out pline);
            return new RhGeo.PolylineCurve(pline.Select(x => Convert.ToPix(x)).ToList());
        }

        public static RhGeo.Line ToPix(PixGeo.Line ln)
        {
            return new RhGeo.Line(Convert.ToPix(ln.From), Convert.ToPix(ln.To));
        }
        public static RhGeo.LineCurve ToPix(PixGeo.LineCurve crv)
        {
            return new RhGeo.LineCurve(Convert.ToPix(crv.Line));
        }

        public static RhGeo.Polyline ToPix(PixGeo.Polyline pline)
        {
            RhGeo.Polyline output = new RhGeo.Polyline();
            foreach(PixGeo.Point3d pt in pline)
            {
                output.Add(Convert.ToPix(pt));
            }
            return output;
        }
        public static RhGeo.PolylineCurve ToPix(PixGeo.PolylineCurve pline)
        {
            return new RhGeo.PolylineCurve(pline.ToPolyline().Select(x => Convert.ToPix(x)).ToList());
        }

    }


}                                                                   