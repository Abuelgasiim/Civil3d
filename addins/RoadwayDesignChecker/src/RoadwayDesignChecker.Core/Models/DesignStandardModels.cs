namespace RoadwayDesignChecker.Core.Models;

public sealed class DesignStandardProfile
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string ReferenceCode { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public UnitSystem Units { get; set; } = UnitSystem.Metric;
    public DesignConstants Constants { get; set; } = new();
    public List<SpeedCriteriaRow> SpeedTable { get; set; } = new();
}

public sealed class DesignConstants
{
    /// <summary>Driver perception/reaction time in seconds (SHC 301 Eq. 4-1 &amp; AASHTO: 2.5 s).</summary>
    public double ReactionTimeSeconds { get; set; } = 2.5;

    /// <summary>Design deceleration rate in m/s^2 (SHC 301 Eq. 4-1 &amp; AASHTO: 3.4 m/s^2).</summary>
    public double DecelerationRateMps2 { get; set; } = 3.4;

    /// <summary>Driver eye height above roadway surface in meters (1.08 m).</summary>
    public double DriverEyeHeightM { get; set; } = 1.08;

    /// <summary>Stopping sight distance object height in meters (0.60 m).</summary>
    public double SsdObjectHeightM { get; set; } = 0.60;

    /// <summary>Passing sight distance opposing vehicle height in meters (1.08 m).</summary>
    public double PsdObjectHeightM { get; set; } = 1.08;

    /// <summary>Headlight height above pavement surface in meters (0.60 m).</summary>
    public double HeadlightHeightM { get; set; } = 0.60;

    /// <summary>Headlight upward beam divergence angle in degrees (1.0 deg).</summary>
    public double HeadlightBeamAngleDeg { get; set; } = 1.0;

    /// <summary>Absolute minimum longitudinal drainage grade in percent (SHC 301 Sec. 4.6.4.2: 0.5%).</summary>
    public double MinDrainageGradePercent { get; set; } = 0.5;

    /// <summary>Desirable minimum longitudinal drainage grade in percent (SHC 301 Sec. 4.6.4.2: 0.7%).</summary>
    public double DesirableDrainageGradePercent { get; set; } = 0.7;

    /// <summary>Maximum allowed compound slope sqrt(G^2 + e^2) in percent (SHC 301 Eq. 4-10: 12.0%).</summary>
    public double MaxCompoundSlopePercent { get; set; } = 12.0;

    /// <summary>Minimum tangent length between same-direction (broken-back) curves on Freeways/Expressways (SHC 301 Sec. 7.5.3.1: 400 m).</summary>
    public double MinBrokenBackTangentFreewayM { get; set; } = 400.0;

    /// <summary>Minimum preferred tangent length between same-direction curves on general roadways in meters.</summary>
    public double MinBrokenBackTangentGeneralM { get; set; } = 150.0;

    /// <summary>Maximum recommended tangent length on Freeways/Expressways (SHC 301 Sec. 7.5.3.1: 2000 m).</summary>
    public double MaxTangentFreewayM { get; set; } = 2000.0;

    /// <summary>Maximum recommended tangent length on Rural/General Highways (SHC 301 Sec. 5.5.3.1: 1500 m).</summary>
    public double MaxTangentGeneralM { get; set; } = 1500.0;

    /// <summary>Minimum superelevation runoff length on rural highways in meters (SHC 301 Sec. 5.5.6.4: 30 m).</summary>
    public double MinSuperelevationRunoffM { get; set; } = 30.0;

    /// <summary>Recommended superelevation runoff length in meters (SHC 301 Sec. 5.5.6.4: 50 m).</summary>
    public double RecommendedSuperelevationRunoffM { get; set; } = 50.0;

    /// <summary>Normal crown cross-slope minimum in percent (SHC 301 Sec. 4.6.6.1: 2.0%).</summary>
    public double MinNormalCrownPercent { get; set; } = 2.0;

    /// <summary>Factor multiplying design speed V (km/h) for minimum vertical curve length (0.6 * V).</summary>
    public double MinVerticalCurveSpeedFactor { get; set; } = 0.6;
}

public sealed class SpeedCriteriaRow
{
    public double DesignSpeedKph { get; set; }
    public double SideFrictionFactor { get; set; }
    public double MinCircularArcLengthM { get; set; }

    /// <summary>Minimum horizontal curve radius keyed by e_max percent (e.g., "4", "6", "8", "10", "12").</summary>
    public Dictionary<string, double> MinRadiusByEmax { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public double MinSpiralParameterA { get; set; }
    public double MinSpiralLengthM { get; set; }
    public double SpiralUpperRadiusLimitM { get; set; }
    public double RecommendedSpiralLengthM { get; set; }
    public double RecommendedSpiralUpperRadiusM { get; set; }

    public double MaxDesirableGradePercent { get; set; }
    public double MaxAllowableGradePercent { get; set; }

    public double MinCrestKSsd { get; set; }
    public double? MinCrestKPsdMethod1 { get; set; }
    public double? MinCrestKPsdMethod2 { get; set; }
    public double MinSagK { get; set; }
    public double MinVerticalCurveLengthM { get; set; }

    public double StoppingSightDistanceLevelM { get; set; }
    public double? PassingSightDistanceMethod1M { get; set; }
    public double? PassingSightDistanceMethod2M { get; set; }

    /// <summary>Maximum superelevation runoff slope r (%) from SHC 301 Table 4-20.</summary>
    public double MaxRunoffSlopePercent { get; set; }

    /// <summary>Minimum curve radius for adverse crown (-2.0%) from SHC 301 Table 4-19.</summary>
    public double? MinRadiusAdverseCrown2PercentM { get; set; }
}

/// <summary>
/// Partial user override file that modifies only specified fields on top of a base standard (SHC 301, AASHTO, or Civil 3D XML).
/// </summary>
public sealed class DesignRuleOverrideSet
{
    public string Name { get; set; } = "Custom Project Overrides";
    public string? BaseStandardId { get; set; }
    public double? MinDrainageGradePercent { get; set; }
    public double? DesirableDrainageGradePercent { get; set; }
    public double? MaxCompoundSlopePercent { get; set; }
    public double? MinBrokenBackTangentFreewayM { get; set; }
    public double? MinBrokenBackTangentGeneralM { get; set; }
    public double? MaxTangentFreewayM { get; set; }
    public double? MaxTangentGeneralM { get; set; }
    public List<SpeedCriteriaOverrideRow> SpeedOverrides { get; set; } = new();
}

public sealed class SpeedCriteriaOverrideRow
{
    public double DesignSpeedKph { get; set; }
    public double? SideFrictionFactor { get; set; }
    public double? MinCircularArcLengthM { get; set; }
    public Dictionary<string, double>? MinRadiusByEmax { get; set; }
    public double? MinSpiralLengthM { get; set; }
    public double? SpiralUpperRadiusLimitM { get; set; }
    public double? MaxDesirableGradePercent { get; set; }
    public double? MaxAllowableGradePercent { get; set; }
    public double? MinCrestKSsd { get; set; }
    public double? MinSagK { get; set; }
    public double? MinVerticalCurveLengthM { get; set; }
    public double? StoppingSightDistanceLevelM { get; set; }
    public double? PassingSightDistanceMethod1M { get; set; }
    public double? PassingSightDistanceMethod2M { get; set; }
    public double? MaxRunoffSlopePercent { get; set; }
}
