using System.Globalization;
using RoadwayDesignChecker.Core.Checks;
using RoadwayDesignChecker.Core.Models;

namespace RoadwayDesignChecker.Core.Criteria;

/// <summary>
/// Resolves active design speed at any alignment station and looks up
/// the governing geometric limits from the active DesignStandardProfile.
/// </summary>
public static class CriteriaResolver
{
    public static double ResolveDesignSpeedKph(
        AlignmentSnapshot alignment,
        double station,
        double fallbackSpeedKph)
    {
        if (alignment.DesignSpeeds.Count == 0)
            return fallbackSpeedKph;

        var ordered = alignment.DesignSpeeds
            .OrderBy(d => d.StartStation)
            .ToList();

        DesignSpeedEntry? active = null;
        foreach (var entry in ordered)
        {
            if (entry.StartStation <= station + 1e-6)
                active = entry;
            else
                break;
        }

        var speed = (active ?? ordered[0]).SpeedKph;
        return speed > 0 ? speed : fallbackSpeedKph;
    }

    public static SpeedCriteriaRow ResolveRow(DesignStandardProfile standard, double designSpeedKph)
    {
        if (standard.SpeedTable.Count == 0)
            throw new InvalidOperationException($"Standard '{standard.DisplayName}' has no speed table rows.");

        var ordered = standard.SpeedTable.OrderBy(r => r.DesignSpeedKph).ToList();

        // Exact match first
        var exact = ordered.FirstOrDefault(r => Math.Abs(r.DesignSpeedKph - designSpeedKph) < 0.01);
        if (exact != null)
            return exact;

        // Conservative engineering rule: pick the next higher design speed row if between table speeds
        var nextHigher = ordered.FirstOrDefault(r => r.DesignSpeedKph >= designSpeedKph);
        return nextHigher ?? ordered[^1];
    }

    public static double ResolveMinRadiusM(
        SpeedCriteriaRow row,
        double emaxPercent,
        double designSpeedKph)
    {
        var key = ((int)Math.Round(emaxPercent)).ToString(CultureInfo.InvariantCulture);
        if (row.MinRadiusByEmax.TryGetValue(key, out var tabularRadius) && tabularRadius > 0)
            return tabularRadius;

        return RoadwayFormulas.ComputeMinimumRadiusM(designSpeedKph, emaxPercent, row.SideFrictionFactor);
    }

    public static (double DesirableMaxGrade, double AllowableMaxGrade) ResolveMaxGrades(
        SpeedCriteriaRow row,
        RoadwayClassification roadClass,
        TerrainType terrain)
    {
        // Base SHC 301 Table 4-17 / 5-5 / 7-4 values are directly in the speed row.
        // Terrain adjustments per SHC 301 Sec. 4.6.4.1 (low-volume mountainous rural may add up to +2% on allowable grade).
        var desirable = row.MaxDesirableGradePercent;
        var allowable = row.MaxAllowableGradePercent;

        if (terrain == TerrainType.Mountainous &&
            (roadClass == RoadwayClassification.Collector || roadClass == RoadwayClassification.Local))
        {
            allowable += 2.0;
        }
        else if (terrain == TerrainType.Rolling && roadClass == RoadwayClassification.Local)
        {
            allowable += 1.0;
        }

        return (desirable, allowable);
    }
}
