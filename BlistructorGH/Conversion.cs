using System.Linq;
using RhGeo = Rhino.Geometry;
using PixGeo = Pixel.Geometry;


namespace BlistructorGH
{
    public static class Convert
    {

        public static RhGeo.Point3d ToRh(PixGeo.Point3d pt) 
        {
            return new RhGeo.Point3d(pt.X, pt.Y, pt.Z);
        }

        public static RhGeo.PolylineCurve ToRh(PixGeo.Curve crv)
        {
            PixGeo.Polyline pline;
            crv.TryGetPolyline(out pline);
            return new RhGeo.PolylineCurve(pline.Select(x => Convert.ToRh(x)).ToList());
        }

        public static RhGeo.Line ToRh(PixGeo.Line ln)
        {
            return new RhGeo.Line(Convert.ToRh(ln.From), Convert.ToRh(ln.To));
        }
        public static RhGeo.LineCurve ToRh(PixGeo.LineCurve crv)
        {
            return new RhGeo.LineCurve(Convert.ToRh(crv.Line));
        }

        public static RhGeo.Polyline ToRh(PixGeo.Polyline pline)
        {
            RhGeo.Polyline output = new RhGeo.Polyline();
            foreach(PixGeo.Point3d pt in pline)
            {
                output.Add(Convert.ToRh(pt));
            }
            return output;
        }
        public static RhGeo.PolylineCurve ToRh(PixGeo.PolylineCurve pline)
        {
            return new RhGeo.PolylineCurve(pline.ToPolyline().Select(x => Convert.ToRh(x)).ToList());
        }

    }


}                                                                   