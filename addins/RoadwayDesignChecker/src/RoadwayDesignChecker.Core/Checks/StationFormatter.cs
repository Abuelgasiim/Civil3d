using System.Globalization;
using RoadwayDesignChecker.Core.Models;

namespace RoadwayDesignChecker.Core.Checks;

public static class StationFormatter
{
    public static string Format(double station, UnitSystem units = UnitSystem.Metric)
    {
        var sign = station < 0 ? "-" : string.Empty;
        var abs = Math.Abs(station);
        var interval = units == UnitSystem.Metric ? 1000.0 : 100.0;
        var major = (long)Math.Floor(abs / interval);
        var minor = abs - (major * interval);

        return units == UnitSystem.Metric
            ? $"{sign}{major}+{minor.ToString("000.00", CultureInfo.InvariantCulture)}"
            : $"{sign}{major}+{minor.ToString("00.00", CultureInfo.InvariantCulture)}";
    }

    public static string FormatRange(double startStation, double endStation, UnitSystem units = UnitSystem.Metric)
    {
        return $"Station {Format(startStation, units)} to {Format(endStation, units)}";
    }
}
