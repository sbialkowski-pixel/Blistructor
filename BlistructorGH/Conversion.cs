#if PIXEL
using System.Linq;
using RhGeo = Rhino.Geometry;
using PixGeo = Pixel.Rhino.Geometry;


namespace BlistructorGH
{

    public static class Convert
    {
        #region ToRH
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
        #endregion

        #region ToPix

        public static PixGeo.Point3d ToPix(RhGeo.Point3d pt)
        {
            return new PixGeo.Point3d(pt.X, pt.Y, pt.Z);
        }

        public static PixGeo.PolylineCurve ToPix(RhGeo.Curve crv)
        {
            RhGeo.Polyline pline;
            crv.TryGetPolyline(out pline);
            return new PixGeo.PolylineCurve(pline.Select(x => Convert.ToPix(x)).ToList());
        }

        public static PixGeo.Line ToPix(RhGeo.Line ln)
        {
            return new PixGeo.Line(Convert.ToPix(ln.From), Convert.ToPix(ln.To));
        }
        public static PixGeo.LineCurve ToPix(RhGeo.LineCurve crv)
        {
            return new PixGeo.LineCurve(Convert.ToPix(crv.Line));
        }

        public static PixGeo.Polyline ToPix(RhGeo.Polyline pline)
        {
            PixGeo.Polyline output = new PixGeo.Polyline();
            foreach (RhGeo.Point3d pt in pline)
            {
                output.Add(Convert.ToPix(pt));
            }
            return output;
        }
        public static PixGeo.PolylineCurve ToPix(RhGeo.PolylineCurve pline)
        {
            return new PixGeo.PolylineCurve(pline.ToPolyline().Select(x => Convert.ToPix(x)).ToList());
        }
        #endregion
    }
}
#endif