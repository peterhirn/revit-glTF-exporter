using System.Globalization;

namespace Revit_glTF_Exporter;

internal readonly record struct Meter(double Value)
{
    public static Meter operator +(Meter lhs, Meter rhs)
        => new(lhs.Value + rhs.Value);

    public string ToFixed(int decimals = 3) =>
        Value.ToString($"F{decimals}", CultureInfo.InvariantCulture);

    public bool AlmostEqual(double value, double delta = double.Epsilon) =>
        Math.Abs(Value - value) < delta;
};

internal readonly record struct Vector3(Meter X, Meter Y, Meter Z);

internal readonly record struct Degrees(double Value)
{
    public string ToFixed(int decimals = 4) =>
        Value.ToString($"F{decimals}", CultureInfo.InvariantCulture);
};
