using Autodesk.Revit.DB;
using Revit_glTF_Exporter;

namespace Revit_glTF_Exporter;

static class Conversion
{
    internal static Meter FeetToMeter(double value) =>
        new(value * 0.3048);

    internal static Meter SquareFeetToMeter(double value) =>
        new(value * 0.09290304);

    internal static Meter CubicFeetToMeter(double value) =>
        new(value * 0.028316846592);

    internal static Vector3 FeetToMeter(XYZ point) =>
        new(FeetToMeter(point.X), FeetToMeter(point.Y), FeetToMeter(point.Z));

    internal static Degrees RadiansToDegrees(double radians) =>
        new(180 / Math.PI * radians);

    internal static Degrees? RadiansToDegrees(double? radians) =>
        (radians is null) ? null : RadiansToDegrees(radians.Value);
}
