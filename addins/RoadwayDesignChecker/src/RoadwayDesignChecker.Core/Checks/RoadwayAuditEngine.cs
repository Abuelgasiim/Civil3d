using RoadwayDesignChecker.Core.Models;

namespace RoadwayDesignChecker.Core.Checks;

/// <summary>
/// Orchestrates all enabled roadway checks and returns a prioritized, human-readable AuditReport
/// (Needs Fix items first, then Advisory, then Passed, sorted by station).
/// </summary>
public sealed class RoadwayAuditEngine
{
    private readonly IReadOnlyList<IRoadwayCheck> _checks;

    public RoadwayAuditEngine(IEnumerable<IRoadwayCheck>? checks = null)
    {
        _checks = checks?.ToList() ?? new List<IRoadwayCheck>
        {
            new HorizontalAlignmentChecker(),
            new VerticalProfileChecker(),
            new SightDistanceChecker()
        };
    }

    public AuditReport RunAudit(
        AlignmentSnapshot alignment,
        ProfileSnapshot? profile,
        DesignStandardProfile standard,
        AuditOptions? options = null)
    {
        options ??= new AuditOptions();
        var findings = new List<DesignCheckResult>();

        foreach (var check in _checks)
        {
            if (check.Category == CheckCategory.HorizontalAlignment && !options.CheckHorizontal) continue;
            if (check.Category == CheckCategory.VerticalProfile && !options.CheckVertical) continue;
            if (check.Category == CheckCategory.SightDistance && !options.CheckSightDistance) continue;

            findings.AddRange(check.Evaluate(alignment, profile, standard, options));
        }

        // Prioritize Needs Fix -> Advisory -> Passed, then by station ascending
        var ordered = findings
            .OrderByDescending(f => (int)f.Severity)
            .ThenBy(f => f.StartStation)
            .ThenBy(f => f.CheckCode, StringComparer.Ordinal)
            .ToList();

        return new AuditReport
        {
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            AlignmentName = alignment.Name,
            ProfileName = profile?.Name,
            StandardDisplayName = standard.DisplayName,
            Options = options,
            Results = ordered
        };
    }
}
