using System.Text.Json;
using System.Text.Json.Serialization;
using RoadwayDesignChecker.Core.Models;

namespace RoadwayDesignChecker.Core.Criteria;

/// <summary>
/// Loads built-in SHC 301 &amp; AASHTO 2018 JSON standards, merges user override files,
/// and saves custom design values back to portable JSON.
/// </summary>
public sealed class StandardRepository
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _standardsDirectory;

    public StandardRepository(string? standardsDirectory = null)
    {
        _standardsDirectory = standardsDirectory
            ?? Path.Combine(AppContext.BaseDirectory, "Standards");
    }

    public string StandardsDirectory => _standardsDirectory;

    public IReadOnlyList<DesignStandardProfile> LoadBuiltInStandards()
    {
        var profiles = new List<DesignStandardProfile>();
        var files = new[] { "shc-301-metric.json", "aashto-2018-metric.json" };

        foreach (var file in files)
        {
            var path = Path.Combine(_standardsDirectory, file);
            if (File.Exists(path))
            {
                var profile = LoadProfileFromFile(path);
                if (profile != null)
                    profiles.Add(profile);
            }
        }

        return profiles;
    }

    public DesignStandardProfile? LoadProfileFromFile(string filePath)
    {
        var json = File.ReadAllText(filePath);
        return JsonSerializer.Deserialize<DesignStandardProfile>(json, JsonOptions);
    }

    public DesignRuleOverrideSet? LoadOverrideFile(string filePath)
    {
        var json = File.ReadAllText(filePath);
        return JsonSerializer.Deserialize<DesignRuleOverrideSet>(json, JsonOptions);
    }

    public void SaveOverrideFile(string filePath, DesignRuleOverrideSet overrides)
    {
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(overrides, JsonOptions);
        File.WriteAllText(filePath, json);
    }

    public static DesignStandardProfile ApplyOverrides(
        DesignStandardProfile baseProfile,
        DesignRuleOverrideSet? overrides)
    {
        var merged = CloneProfile(baseProfile);
        if (overrides == null)
            return merged;

        if (!string.IsNullOrWhiteSpace(overrides.Name))
            merged.DisplayName = $"{baseProfile.DisplayName} + {overrides.Name}";

        if (overrides.MinDrainageGradePercent.HasValue)
            merged.Constants.MinDrainageGradePercent = overrides.MinDrainageGradePercent.Value;
        if (overrides.DesirableDrainageGradePercent.HasValue)
            merged.Constants.DesirableDrainageGradePercent = overrides.DesirableDrainageGradePercent.Value;
        if (overrides.MaxCompoundSlopePercent.HasValue)
            merged.Constants.MaxCompoundSlopePercent = overrides.MaxCompoundSlopePercent.Value;
        if (overrides.MinBrokenBackTangentFreewayM.HasValue)
            merged.Constants.MinBrokenBackTangentFreewayM = overrides.MinBrokenBackTangentFreewayM.Value;
        if (overrides.MinBrokenBackTangentGeneralM.HasValue)
            merged.Constants.MinBrokenBackTangentGeneralM = overrides.MinBrokenBackTangentGeneralM.Value;
        if (overrides.MaxTangentFreewayM.HasValue)
            merged.Constants.MaxTangentFreewayM = overrides.MaxTangentFreewayM.Value;
        if (overrides.MaxTangentGeneralM.HasValue)
            merged.Constants.MaxTangentGeneralM = overrides.MaxTangentGeneralM.Value;

        foreach (var ov in overrides.SpeedOverrides)
        {
            var target = merged.SpeedTable.FirstOrDefault(r => Math.Abs(r.DesignSpeedKph - ov.DesignSpeedKph) < 0.01);
            if (target == null)
            {
                target = new SpeedCriteriaRow { DesignSpeedKph = ov.DesignSpeedKph };
                merged.SpeedTable.Add(target);
            }

            if (ov.SideFrictionFactor.HasValue) target.SideFrictionFactor = ov.SideFrictionFactor.Value;
            if (ov.MinCircularArcLengthM.HasValue) target.MinCircularArcLengthM = ov.MinCircularArcLengthM.Value;
            if (ov.MinSpiralLengthM.HasValue) target.MinSpiralLengthM = ov.MinSpiralLengthM.Value;
            if (ov.SpiralUpperRadiusLimitM.HasValue) target.SpiralUpperRadiusLimitM = ov.SpiralUpperRadiusLimitM.Value;
            if (ov.MaxDesirableGradePercent.HasValue) target.MaxDesirableGradePercent = ov.MaxDesirableGradePercent.Value;
            if (ov.MaxAllowableGradePercent.HasValue) target.MaxAllowableGradePercent = ov.MaxAllowableGradePercent.Value;
            if (ov.MinCrestKSsd.HasValue) target.MinCrestKSsd = ov.MinCrestKSsd.Value;
            if (ov.MinSagK.HasValue) target.MinSagK = ov.MinSagK.Value;
            if (ov.MinVerticalCurveLengthM.HasValue) target.MinVerticalCurveLengthM = ov.MinVerticalCurveLengthM.Value;
            if (ov.StoppingSightDistanceLevelM.HasValue) target.StoppingSightDistanceLevelM = ov.StoppingSightDistanceLevelM.Value;
            if (ov.PassingSightDistanceMethod1M.HasValue) target.PassingSightDistanceMethod1M = ov.PassingSightDistanceMethod1M.Value;
            if (ov.PassingSightDistanceMethod2M.HasValue) target.PassingSightDistanceMethod2M = ov.PassingSightDistanceMethod2M.Value;
            if (ov.MaxRunoffSlopePercent.HasValue) target.MaxRunoffSlopePercent = ov.MaxRunoffSlopePercent.Value;

            if (ov.MinRadiusByEmax != null)
            {
                foreach (var kvp in ov.MinRadiusByEmax)
                    target.MinRadiusByEmax[kvp.Key] = kvp.Value;
            }
        }

        merged.SpeedTable = merged.SpeedTable.OrderBy(r => r.DesignSpeedKph).ToList();
        return merged;
    }

    public static DesignStandardProfile CloneProfile(DesignStandardProfile source)
    {
        var json = JsonSerializer.Serialize(source, JsonOptions);
        return JsonSerializer.Deserialize<DesignStandardProfile>(json, JsonOptions)
               ?? throw new InvalidOperationException("Failed to clone DesignStandardProfile.");
    }
}
