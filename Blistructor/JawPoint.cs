#if PIXEL
using Pixel.Rhino.Geometry;
#else
using Rhino.Geometry;
#endif

namespace Blistructor
{
    public class JawPoint

    {
        public Point3d location;
        public JawSite orientation;
        public JawState state;

        public JawPoint()
        {
            location = new Point3d(-1, -1, -1);
            orientation = JawSite.Unset;
            state = JawState.Inactive;
        }

        public JawPoint(Point3d pt, JawSite site)
        {
            location = pt;
            orientation = site;
            state = JawState.Active;
        }
    }
}
