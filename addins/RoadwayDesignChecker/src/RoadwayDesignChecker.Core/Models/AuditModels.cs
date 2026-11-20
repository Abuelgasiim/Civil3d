namespace RoadwayDesignChecker.Core.Models;

public sealed class AuditOptions
{
    public RoadwayClassification RoadwayClass { get; set; } = RoadwayClassification.FreewayExpressway;
    public TerrainType Terrain { get; set; } = TerrainType.Level;

    /// <summary>Selected maximum superelevation rate in percent (4, 6, 8, 10, or 12).</summary>
    public double MaxSuperelevationPercent { get; set; } = 6.0;

    /// <summary>Fallback design speed in km/h when the Alignment has no DesignSpeeds assigned.</summary>
    public double FallbackDesignSpeedKph { get; set; } = 100.0;

    /// <summary>Available lateral sight clearance M (meters) from inside lane centerline to obstruction.</summary>
    public double AvailableLateralClearanceM { get; set; } = 5.0;

    /// <summary>Rotated roadway width w (meters) used for SHC 301 Eq. 4-11 superelevation runoff check (default 7.3 m = 2 lanes).</summary>
    public double RotatedPavementWidthM { get; set; } = 7.3;

    /// <summary>Enable two-lane undivided Passing Sight Distance (PSD) checks.</summary>
    public bool EvaluatePassingSightDistance { get; set; } = false;

    public PassingSightMethod PassingMethod { get; set; } = PassingSightMethod.Method1Complete;

    public bool CheckHorizontal { get; set; } = true;
    public bool CheckVertical { get; set; } = true;
    public bool CheckSightDistance { get; set; } = true;
}

/// <summary>
/// Human-readable audit finding designed for plain-English presentation in the Civil 3D PaletteSet
/// while retaining exact stationing, technical rules, and engineer exception notes.
/// </summary>
public sealed class DesignCheckResult
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public CheckSeverity Severity { get; set; }
    public CheckCategory Category { get; set; }

    /// <summary>Friendly status badge label: "Needs Fix", "Advisory", or "Passed".</summary>
    public string StatusLabel => Severity switch
    {
        CheckSeverity.Fail => "Needs Fix",
        CheckSeverity.Warning => "Advisory",
        _ => "Passed"
    };

    /// <summary>Friendly category label shown on the card.</summary>
    public string CategoryLabel { get; set; } = string.Empty;

    /// <summary>Plain-English one-sentence headline describing what passed or needs fixing.</summary>
    public string Headline { get; set; } = string.Empty;

    /// <summary>Entity label &amp; station span, e.g. "Curve C2 · Station 0+420.00 to 0+590.00".</summary>
    public string LocationText { get; set; } = string.Empty;

    public string AlignmentName { get; set; } = string.Empty;
    public string? ProfileName { get; set; }
    public string EntityLabel { get; set; } = string.Empty;
    public string CheckCode { get; set; } = string.Empty;

    public double StartStation { get; set; }
    public double EndStation { get; set; }
    public double FocusStation => (StartStation + EndStation) / 2.0;
    public double DesignSpeedKph { get; set; }

    /// <summary>Human-readable measured value from the Civil 3D drawing, e.g. "360.0 m radius".</summary>
    public string YourDrawingText { get; set; } = string.Empty;

    /// <summary>Human-readable required standard value, e.g. "At least 440.0 m".</summary>
    public string StandardRequiresText { get; set; } = string.Empty;

    /// <summary>Human-readable difference/margin, e.g. "80.0 m too tight" or "+240.0 m safety margin".</summary>
    public string DifferenceText { get; set; } = string.Empty;

    /// <summary>Plain-English explanation of why this design criterion matters for road safety.</summary>
    public string WhyItMatters { get; set; } = string.Empty;

    /// <summary>Clear actionable instruction on how to resolve the issue inside Civil 3D.</summary>
    public string HowToFix { get; set; } = string.Empty;

    /// <summary>Exact SHC 301 / AASHTO citation and formula for technical audit traceability.</summary>
    public string TechnicalReference { get; set; } = string.Empty;

    /// <summary>Visual progress bar percentage (0–100) comparing actual design against standard target.</summary>
    public double CompliancePercent { get; set; } = 100.0;

    /// <summary>Optional engineer waiver or review comment saved with the report.</summary>
    public string EngineerNote { get; set; } = string.Empty;

    public double? ActualNumeric { get; set; }
    public double? RequiredNumeric { get; set; }
}

public sealed class AuditReport
{
    public DateTimeOffset GeneratedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string AlignmentName { get; set; } = string.Empty;
    public string? ProfileName { get; set; }
    public string StandardDisplayName { get; set; } = string.Empty;
    public AuditOptions Options { get; set; } = new();
    public List<DesignCheckResult> Results { get; set; } = new();

    public int TotalCount => Results.Count;
    public int FailCount => Results.Count(r => r.Severity == CheckSeverity.Fail);
    public int WarningCount => Results.Count(r => r.Severity == CheckSeverity.Warning);
    public int PassCount => Results.Count(r => r.Severity == CheckSeverity.Pass);

    public string PlainEnglishSummary
    {
        get
        {
            if (TotalCount == 0)
                return "No roadway elements were found to audit on the selected alignment/profile.";
            if (FailCount == 0 && WarningCount == 0)
                return $"All {PassCount} roadway checks passed against {StandardDisplayName}.";
            if (FailCount == 0)
                return $"No critical failures. {WarningCount} advisory item(s) to review and {PassCount} passed.";
            return $"{FailCount} location(s) need fixing before approval ({WarningCount} advisory, {PassCount} passed).";
        }
    }
}
