//using Rhino.Geometry;
using Pixel.Geometry;

namespace Blistructor
{
    public class AnchorPoint
    {

        public Point3d location;
        public AnchorSite orientation;
        public AnchorState state;

        public AnchorPoint()
        {
            location = new Point3d(-1, -1, -1);
            orientation = AnchorSite.Unset;
            state = AnchorState.Inactive;
        }

        public AnchorPoint(Point3d pt, AnchorSite site)
        {
            location = pt;
            orientation = site;
            state = AnchorState.Active;
        }
    }
}
