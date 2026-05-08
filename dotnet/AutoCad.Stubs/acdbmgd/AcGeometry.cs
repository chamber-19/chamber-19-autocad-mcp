// Stub implementation of Autodesk.AutoCAD.Geometry for CI builds.
// All members are compile-time only and may throw/not be functional at runtime.

namespace Autodesk.AutoCAD.Geometry;

public readonly struct Point3d
{
    public Point3d(double x, double y, double z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public double X { get; }
    public double Y { get; }
    public double Z { get; }
}
