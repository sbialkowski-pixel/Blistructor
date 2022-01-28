#if PIXEL
using Pixel.Rhino.Geometry;
#else
using Rhino.Geometry;
#endif

namespace Blistructor
{
    public struct JawPoint
    {
        public Point3d Location { get; set; }
        public JawSite Orientation { get; set; }
        public JawState State { get; set; }

        public JawPoint(Point3d location, JawSite orientation, JawState state = JawState.Active)
        {
            Location = location;
            Orientation = orientation;
            State = state;
        }

    }
}
