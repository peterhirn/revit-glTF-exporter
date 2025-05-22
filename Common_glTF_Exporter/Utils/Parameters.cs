using System.Globalization;
using Autodesk.Revit.DB;

namespace Revit_glTF_Exporter;

internal record Parameter(
    string Name,
    string Value);

internal enum ParameterType
{
    Symbol,
    Instance
}

static class Parameters
{
    static Autodesk.Revit.DB.Parameter? IfcGuid(FamilyInstance instance) =>
        instance.get_Parameter(BuiltInParameter.IFC_GUID);

    static string ParameterValue(Autodesk.Revit.DB.Parameter parameter)
    {
        switch (parameter.StorageType)
        {
            case StorageType.Double:
                var unit = parameter.GetUnitTypeId();
                var value = parameter.AsDouble();

                if (unit == UnitTypeId.SquareMeters || unit == UnitTypeId.SquareCentimeters || unit == UnitTypeId.SquareMillimeters)
                {
                    return Conversion.SquareFeetToMeter(value).ToFixed();
                }
                else if (unit == UnitTypeId.CubicMeters || unit == UnitTypeId.CubicCentimeters || unit == UnitTypeId.CubicMillimeters)
                {
                    return Conversion.CubicFeetToMeter(value).ToFixed();
                }
                else
                {
                    return Conversion.FeetToMeter(value).ToFixed();
                }
            case StorageType.Integer:
                if (parameter.Definition.GetDataType() == SpecTypeId.Boolean.YesNo)
                    return parameter.AsInteger() == 1 ? "True" : "False";
                return parameter.AsValueString();
            default:
                return parameter.AsValueString();
        }
    }

    static Parameter? CreateParameter(Autodesk.Revit.DB.Parameter parameter,
        ParameterType type, string name, string fallback = "")
    {
        var value = ParameterValue(parameter);

        return new(
          Name: name,
          Value: string.IsNullOrWhiteSpace(value) ? fallback : value);
    }

    static Parameter? GetParameter(FamilyInstance instance,
        ParameterType type, string name, string exportName, string fallback = "")
    {
        using var parameter =
            type == ParameterType.Instance
                ? instance.LookupParameter(name)
                : instance.Symbol.LookupParameter(name);

        if (parameter is null) return null;
        return CreateParameter(parameter, type, exportName, fallback);
    }

    static Parameter? IfcGuidParameter(FamilyInstance instance, string exportName)
    {
        var parameter = IfcGuid(instance);
        if (parameter is null) return null;
        return CreateParameter(parameter, ParameterType.Instance, exportName);
    }

    static IEnumerable<Parameter> ObjectParameters(FamilyInstance instance)
    {
        Parameter? SymbolParameter(string name, string exportName) =>
            GetParameter(instance, ParameterType.Symbol, name, exportName);

        Parameter? InstanceParameter(string name, string exportName, string fallback = "") =>
            GetParameter(instance, ParameterType.Instance, name, exportName, fallback);

        var ifcGuid = IfcGuidParameter(instance, "IfcGUID");

        return new[]{
            SymbolParameter("01 Manufacturer", "Manufacturer"),
            SymbolParameter("03 Product Line", "ProductLine"),
            SymbolParameter("04 Device / Instrument", "Device"),
            SymbolParameter("06 Stencil ID", "StencilID"),
            SymbolParameter("07 Stencil Version", "StencilVersion"),
            SymbolParameter("08 Material Number", "MaterialNumber"),
            SymbolParameter("10 Lifecycle Status", "LifecycleStatus"),
            SymbolParameter("01 Length (X)", "Length"),
            SymbolParameter("02 Width (Y)", "Width"),
            SymbolParameter("03 Height (Z)", "Height"),

            // V1
            SymbolParameter("11 0-Offset (X)", "LengthOffset"),
            SymbolParameter("12 0-Offset (Y)", "WidthOffset"),
            SymbolParameter("13 0-Offset (Z)", "HeightOffset"),

            // V2
            SymbolParameter("12 0-Offset (X)", "LengthOffset"),
            SymbolParameter("13 0-Offset (Y)", "WidthOffset"),
            SymbolParameter("14 0-Offset (Z)", "HeightOffset"),

            InstanceParameter("01 System Variant", "SystemVariant"),
            InstanceParameter("01 System Variant Number", "SystemVariantNumber"),
            InstanceParameter("02 Serial Number", "SerialNumber"),
            InstanceParameter("03 RUDI", "RUDI"),

            // 05 Specific Device Identifier = eg. TT#5
            InstanceParameter("05 Specific Device Identifier", "DeviceID", ifcGuid?.Value ?? ""),
            ifcGuid,

            InstanceParameter("06 Specific System Identifier", "SystemID"),

            InstanceParameter("08 LIS IP", "LISIP"),
            InstanceParameter("09 LIS Port", "LISPort"),
            InstanceParameter("01 Bottom Belt Height", "BottomBeltHeight"),

            InstanceParameter("01 Top Belt Height", "TopBeltHeight"),
            InstanceParameter("01 Belt Height", "TopBeltHeight"),

            InstanceParameter("03 Direction Upward", "DirectionUpward"),
            InstanceParameter("03 Direction Forward", "DirectionForward")
        }
        .Where(p => p is not null)
        .Cast<Parameter>();
    }

    internal static Dictionary<string, string> ObjectParametersDict(FamilyInstance instance)
    {
        return ObjectParameters(instance).ToDictionary(p => p.Name, p => p.Value);
    }
}