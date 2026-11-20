using RoadwayDesignChecker.Core.Criteria;
using RoadwayDesignChecker.Core.Models;

namespace RoadwayDesignChecker.Core.Checks;

/// <summary>
/// Checks horizontal alignment geometry against SHC 301 &amp; AASHTO standards:
/// minimum curve radius, minimum circular arc length, spiral transition requirement &amp; length,
/// broken-back / long tangents, and superelevation rate &amp; runoff length.
/// </summary>
public sealed class HorizontalAlignmentChecker : IRoadwayCheck
{
    public CheckCategory Category => CheckCategory.HorizontalAlignment;

    public IEnumerable<DesignCheckResult> Evaluate(
        AlignmentSnapshot alignment,
        ProfileSnapshot? profile,
        DesignStandardProfile standard,
        AuditOptions options)
    {
        var results = new List<DesignCheckResult>();
        var segments = alignment.Segments.OrderBy(s => s.StartStation).ToList();

        for (var i = 0; i < segments.Count; i++)
        {
            var seg = segments[i];
            var midSta = (seg.StartStation + seg.EndStation) / 2.0;
            var speed = CriteriaResolver.ResolveDesignSpeedKph(alignment, midSta, options.FallbackDesignSpeedKph);
            var row = CriteriaResolver.ResolveRow(standard, speed);

            switch (seg.Kind)
            {
                case HorizontalSegmentKind.Arc:
                    EvaluateArcRadius(alignment, seg, speed, row, standard, options, results);
                    EvaluateArcLength(alignment, seg, speed, row, standard, results);
                    EvaluateSpiralPresenceForArc(alignment, segments, i, speed, row, standard, results);
                    break;

                case HorizontalSegmentKind.Spiral:
                    EvaluateSpiralLength(alignment, seg, speed, row, standard, results);
                    break;

                case HorizontalSegmentKind.Line:
                    EvaluateTangentLength(alignment, segments, i, speed, standard, options, results);
                    break;
            }
        }

        EvaluateSuperelevation(alignment, standard, options, results);
        return results;
    }

    private static void EvaluateArcRadius(
        AlignmentSnapshot alignment,
        HorizontalSegmentSnapshot arc,
        double speed,
        SpeedCriteriaRow row,
        DesignStandardProfile standard,
        AuditOptions options,
        List<DesignCheckResult> results)
    {
        if (!arc.Radius.HasValue || arc.Radius.Value <= 0)
            return;

        var actualR = arc.Radius.Value;
        var minR = CriteriaResolver.ResolveMinRadiusM(row, options.MaxSuperelevationPercent, speed);
        var diff = actualR - minR;
        var passes = actualR + 1e-6 >= minR;
        var loc = $"{arc.Label} · {StationFormatter.FormatRange(arc.StartStation, arc.EndStation, alignment.DrawingUnits)}";

        results.Add(new DesignCheckResult
        {
            Severity = passes ? CheckSeverity.Pass : CheckSeverity.Fail,
            Category = CheckCategory.HorizontalAlignment,
            CategoryLabel = "Horizontal Curve",
            CheckCode = "H-RADIUS",
            AlignmentName = alignment.Name,
            EntityLabel = arc.Label,
            StartStation = arc.StartStation,
            EndStation = arc.EndStation,
            DesignSpeedKph = speed,
            LocationText = loc,
            ActualNumeric = actualR,
            RequiredNumeric = minR,
            CompliancePercent = RoadwayFormulas.ComputeCompliancePercent(actualR, minR, higherIsBetter: true),
            Headline = passes
                ? $"{arc.Label} radius is gentle and safe for {speed:0} km/h"
                : $"{arc.Label} is too sharp for a {speed:0} km/h roadway",
            YourDrawingText = $"{actualR:0.0} m radius",
            StandardRequiresText = $"At least {minR:0.0} m (e_max {options.MaxSuperelevationPercent:0.#}%)",
            DifferenceText = passes
                ? $"+{diff:0.0} m safety margin"
                : $"{Math.Abs(diff):0.0} m too tight",
            WhyItMatters = passes
                ? $"At {speed:0} km/h with {options.MaxSuperelevationPercent:0.#}% maximum cross-slope, a {actualR:0.0} m radius comfortably keeps lateral tire forces within safe limits."
                : $"At {speed:0} km/h, a {actualR:0.0} m radius creates excessive outward side-force on vehicles even at {options.MaxSuperelevationPercent:0.#}% maximum superelevation, increasing skid risk.",
            HowToFix = passes
                ? "No action needed — curve radius meets standard."
                : $"Increase {arc.Label} radius in Civil 3D Alignment Layout Tools from {actualR:0.0} m to at least {minR:0.0} m (or raise e_max / lower the segment design speed).",
            TechnicalReference = $"{standard.ReferenceCode} Table 4-14 / Eq. 4-6 · R_min = V² / [127 × (e + f)], f_R = {row.SideFrictionFactor:0.00}"
        });
    }

    private static void EvaluateArcLength(
        AlignmentSnapshot alignment,
        HorizontalSegmentSnapshot arc,
        double speed,
        SpeedCriteriaRow row,
        DesignStandardProfile standard,
        List<DesignCheckResult> results)
    {
        if (row.MinCircularArcLengthM <= 0)
            return;

        var actualLen = arc.Length;
        var minLen = row.MinCircularArcLengthM;
        var diff = actualLen - minLen;
        var passes = actualLen + 1e-6 >= minLen;
        var loc = $"{arc.Label} · {StationFormatter.FormatRange(arc.StartStation, arc.EndStation, alignment.DrawingUnits)}";

        results.Add(new DesignCheckResult
        {
            Severity = passes ? CheckSeverity.Pass : CheckSeverity.Warning,
            Category = CheckCategory.HorizontalAlignment,
            CategoryLabel = "Horizontal Curve Length",
            CheckCode = "H-ARCLEN",
            AlignmentName = alignment.Name,
            EntityLabel = arc.Label,
            StartStation = arc.StartStation,
            EndStation = arc.EndStation,
            DesignSpeedKph = speed,
            LocationText = loc,
            ActualNumeric = actualLen,
            RequiredNumeric = minLen,
            CompliancePercent = RoadwayFormulas.ComputeCompliancePercent(actualLen, minLen, higherIsBetter: true),
            Headline = passes
                ? $"{arc.Label} arc length is clearly visible to drivers"
                : $"{arc.Label} circular curve is shorter than recommended",
            YourDrawingText = $"{actualLen:0.0} m arc length",
            StandardRequiresText = $"At least {minLen:0.0} m",
            DifferenceText = passes ? $"+{diff:0.0} m margin" : $"{Math.Abs(diff):0.0} m too short",
            WhyItMatters = "Very short circular curves look like sudden kinks in the road and make it harder for drivers to perceive the curve as a smooth turning element.",
            HowToFix = passes
                ? "No action needed — circular arc length is adequate."
                : $"Lengthen {arc.Label} to at least {minLen:0.0} m by increasing the curve radius slightly.",
            TechnicalReference = $"{standard.ReferenceCode} Table 4-14 / Table 5-2 · Minimum Circular Arc Length"
        });
    }

    private static void EvaluateSpiralPresenceForArc(
        AlignmentSnapshot alignment,
        List<HorizontalSegmentSnapshot> segments,
        int arcIndex,
        double speed,
        SpeedCriteriaRow row,
        DesignStandardProfile standard,
        List<DesignCheckResult> results)
    {
        var arc = segments[arcIndex];
        if (!arc.Radius.HasValue || row.SpiralUpperRadiusLimitM <= 0)
            return;

        // Only required when curve radius is at or below the SHC 301 Table 4-15 upper radius limit
        if (arc.Radius.Value > row.SpiralUpperRadiusLimitM)
            return;

        var hasSpiralIn = arcIndex > 0 && segments[arcIndex - 1].Kind == HorizontalSegmentKind.Spiral;
        var hasSpiralOut = arcIndex + 1 < segments.Count && segments[arcIndex + 1].Kind == HorizontalSegmentKind.Spiral;
        if (hasSpiralIn && hasSpiralOut)
            return;

        var missingDesc = (!hasSpiralIn && !hasSpiralOut)
            ? "both entry and exit spiral transitions"
            : !hasSpiralIn ? "an entry spiral transition" : "an exit spiral transition";

        results.Add(new DesignCheckResult
        {
            Severity = CheckSeverity.Warning,
            Category = CheckCategory.HorizontalAlignment,
            CategoryLabel = "Spiral Transition",
            CheckCode = "H-SPIRAL-REQ",
            AlignmentName = alignment.Name,
            EntityLabel = arc.Label,
            StartStation = arc.StartStation,
            EndStation = arc.EndStation,
            DesignSpeedKph = speed,
            LocationText = $"{arc.Label} · {StationFormatter.FormatRange(arc.StartStation, arc.EndStation, alignment.DrawingUnits)}",
            ActualNumeric = arc.Radius.Value,
            RequiredNumeric = row.SpiralUpperRadiusLimitM,
            CompliancePercent = 70.0,
            Headline = $"{arc.Label} is missing {missingDesc}",
            YourDrawingText = $"No spiral (R = {arc.Radius.Value:0.0} m)",
            StandardRequiresText = $"Spiral needed when R ≤ {row.SpiralUpperRadiusLimitM:0.0} m",
            DifferenceText = $"Min L_s = {row.MinSpiralLengthM:0.0} m",
            WhyItMatters = $"At {speed:0} km/h, curves with radius {row.SpiralUpperRadiusLimitM:0.0} m or tighter should use spiral transitions so drivers can turn the steering wheel and attain superelevation gradually.",
            HowToFix = $"Replace the simple circular curve {arc.Label} with a Free Spiral-Curve-Spiral (SCS) using spiral length L_s ≥ {row.MinSpiralLengthM:0.0} m (recommended {row.RecommendedSpiralLengthM:0.0} m).",
            TechnicalReference = $"{standard.ReferenceCode} Table 4-15 & Table 4-16 · Upper Radius Limit for Spiral Application"
        });
    }

    private static void EvaluateSpiralLength(
        AlignmentSnapshot alignment,
        HorizontalSegmentSnapshot spiral,
        double speed,
        SpeedCriteriaRow row,
        DesignStandardProfile standard,
        List<DesignCheckResult> results)
    {
        if (row.MinSpiralLengthM <= 0)
            return;

        var actualLs = spiral.Length;
        var minLs = row.MinSpiralLengthM;
        var recLs = row.RecommendedSpiralLengthM > 0 ? row.RecommendedSpiralLengthM : minLs;
        var diff = actualLs - minLs;

        var severity = actualLs + 1e-6 < minLs
            ? CheckSeverity.Fail
            : actualLs + 1e-6 < recLs ? CheckSeverity.Warning : CheckSeverity.Pass;

        results.Add(new DesignCheckResult
        {
            Severity = severity,
            Category = CheckCategory.HorizontalAlignment,
            CategoryLabel = "Spiral Transition",
            CheckCode = "H-SPIRAL-LEN",
            AlignmentName = alignment.Name,
            EntityLabel = spiral.Label,
            StartStation = spiral.StartStation,
            EndStation = spiral.EndStation,
            DesignSpeedKph = speed,
            LocationText = $"{spiral.Label} · {StationFormatter.FormatRange(spiral.StartStation, spiral.EndStation, alignment.DrawingUnits)}",
            ActualNumeric = actualLs,
            RequiredNumeric = minLs,
            CompliancePercent = RoadwayFormulas.ComputeCompliancePercent(actualLs, minLs, higherIsBetter: true),
            Headline = severity switch
            {
                CheckSeverity.Fail => $"{spiral.Label} transition spiral is too short",
                CheckSeverity.Warning => $"{spiral.Label} meets minimum spiral length but is below recommended comfort length",
                _ => $"{spiral.Label} spiral transition length is smooth and comfortable"
            },
            YourDrawingText = $"{actualLs:0.0} m spiral",
            StandardRequiresText = $"Min {minLs:0.0} m (Rec {recLs:0.0} m)",
            DifferenceText = diff >= 0 ? $"+{diff:0.0} m above min" : $"{Math.Abs(diff):0.0} m too short",
            WhyItMatters = "A transition spiral lets drivers rotate the steering wheel smoothly and lets the pavement rotate into superelevation without an abrupt lateral jolt.",
            HowToFix = severity == CheckSeverity.Pass
                ? "No action needed — spiral length meets recommended standard."
                : $"Extend {spiral.Label} length to at least {minLs:0.0} m (ideally {recLs:0.0} m for smoother driving comfort).",
            TechnicalReference = $"{standard.ReferenceCode} Table 4-15 (Min L_s = {minLs:0.0} m, A_s = {row.MinSpiralParameterA:0.0} m) & Table 4-16 (Rec L_s = {recLs:0.0} m)"
        });
    }

    private static void EvaluateTangentLength(
        AlignmentSnapshot alignment,
        List<HorizontalSegmentSnapshot> segments,
        int lineIndex,
        double speed,
        DesignStandardProfile standard,
        AuditOptions options,
        List<DesignCheckResult> results)
    {
        var line = segments[lineIndex];
        var isFreeway = options.RoadwayClass == RoadwayClassification.FreewayExpressway;
        var loc = $"{line.Label} · {StationFormatter.FormatRange(line.StartStation, line.EndStation, alignment.DrawingUnits)}";

        // 1. Check maximum tangent length (driver fatigue / glare, SHC 301 Sec. 5.5.3.1 & 7.5.3.1)
        var maxTangent = isFreeway ? standard.Constants.MaxTangentFreewayM : standard.Constants.MaxTangentGeneralM;
        if (maxTangent > 0 && line.Length > maxTangent)
        {
            results.Add(new DesignCheckResult
            {
                Severity = CheckSeverity.Warning,
                Category = CheckCategory.HorizontalAlignment,
                CategoryLabel = "Straight Tangent",
                CheckCode = "H-TANGENT-MAX",
                AlignmentName = alignment.Name,
                EntityLabel = line.Label,
                StartStation = line.StartStation,
                EndStation = line.EndStation,
                DesignSpeedKph = speed,
                LocationText = loc,
                ActualNumeric = line.Length,
                RequiredNumeric = maxTangent,
                CompliancePercent = RoadwayFormulas.ComputeCompliancePercent(line.Length, maxTangent, higherIsBetter: false),
                Headline = $"{line.Label} is a very long straight section ({line.Length:0} m)",
                YourDrawingText = $"{line.Length:0.0} m straight",
                StandardRequiresText = $"Recommended ≤ {maxTangent:0.0} m",
                DifferenceText = $"+{line.Length - maxTangent:0.0} m over target",
                WhyItMatters = "Very long straightaways encourage excessive speeding, worsen oncoming headlight glare at night, and cause driver monotony and fatigue.",
                HowToFix = $"If terrain and right-of-way permit, introduce a gentle large-radius curve to break up {line.Label} into segments under {maxTangent:0} m.",
                TechnicalReference = $"{standard.ReferenceCode} Sec. {(isFreeway ? "7.5.3.1 (≤ 2,000 m)" : "5.5.3.1 (≤ 1,500 m)")}"
            });
        }

        // 2. Check broken-back (same-direction) short tangent between two curves
        var prevArc = FindNearestArc(segments, lineIndex, direction: -1);
        var nextArc = FindNearestArc(segments, lineIndex, direction: +1);
        if (prevArc?.IsClockwise != null && nextArc?.IsClockwise != null && prevArc.IsClockwise == nextArc.IsClockwise)
        {
            var minBrokenBack = isFreeway
                ? standard.Constants.MinBrokenBackTangentFreewayM
                : standard.Constants.MinBrokenBackTangentGeneralM;

            var passes = line.Length + 1e-6 >= minBrokenBack;
            results.Add(new DesignCheckResult
            {
                Severity = passes ? CheckSeverity.Pass : CheckSeverity.Warning,
                Category = CheckCategory.HorizontalAlignment,
                CategoryLabel = "Straight Tangent",
                CheckCode = "H-BROKENBACK",
                AlignmentName = alignment.Name,
                EntityLabel = line.Label,
                StartStation = line.StartStation,
                EndStation = line.EndStation,
                DesignSpeedKph = speed,
                LocationText = loc,
                ActualNumeric = line.Length,
                RequiredNumeric = minBrokenBack,
                CompliancePercent = RoadwayFormulas.ComputeCompliancePercent(line.Length, minBrokenBack, higherIsBetter: true),
                Headline = passes
                    ? $"{line.Label} provides enough straight separation between same-direction curves"
                    : $"Short straight gap ({line.Label}) between two same-direction curves",
                YourDrawingText = $"{line.Length:0.0} m straight",
                StandardRequiresText = $"At least {minBrokenBack:0.0} m between same-direction curves",
                DifferenceText = passes
                    ? $"+{line.Length - minBrokenBack:0.0} m margin"
                    : $"{minBrokenBack - line.Length:0.0} m shorter than target",
                WhyItMatters = "Two curves turning the same way separated by a short straight ('broken-back' curves) look awkward and force drivers through a double superelevation reversal.",
                HowToFix = passes
                    ? "No action needed — tangent separation between same-direction curves meets standard."
                    : $"Either combine {prevArc.Label} and {nextArc.Label} into one compound curve, or extend {line.Label} to at least {minBrokenBack:0.0} m.",
                TechnicalReference = $"{standard.ReferenceCode} Sec. {(isFreeway ? "7.5.3.1 (Min 400 m)" : "5.5.3.2 (Avoid Broken-Back Tangents)")}"
            });
        }
    }

    private static void EvaluateSuperelevation(
        AlignmentSnapshot alignment,
        DesignStandardProfile standard,
        AuditOptions options,
        List<DesignCheckResult> results)
    {
        if (alignment.SuperelevationStations.Count == 0)
            return;

        // Group by curve name (or single group if unnamed)
        var groups = alignment.SuperelevationStations
            .GroupBy(s => string.IsNullOrWhiteSpace(s.CurveName) ? "Superelevation Region" : s.CurveName!)
            .ToList();

        foreach (var group in groups)
        {
            var ordered = group.OrderBy(s => s.Station).ToList();
            var startSta = ordered[0].Station;
            var endSta = ordered[^1].Station;
            var midSta = (startSta + endSta) / 2.0;
            var speed = CriteriaResolver.ResolveDesignSpeedKph(alignment, midSta, options.FallbackDesignSpeedKph);
            var row = CriteriaResolver.ResolveRow(standard, speed);

            var peakSlope = ordered.Max(s => s.MaxAbsLaneSlopePercent);
            var exceedsMax = peakSlope > options.MaxSuperelevationPercent + 0.05;
            var loc = $"{group.Key} · {StationFormatter.FormatRange(startSta, endSta, alignment.DrawingUnits)}";

            results.Add(new DesignCheckResult
            {
                Severity = exceedsMax ? CheckSeverity.Fail : CheckSeverity.Pass,
                Category = CheckCategory.HorizontalAlignment,
                CategoryLabel = "Superelevation (Road Tilt)",
                CheckCode = "H-SUPER-MAX",
                AlignmentName = alignment.Name,
                EntityLabel = group.Key,
                StartStation = startSta,
                EndStation = endSta,
                DesignSpeedKph = speed,
                LocationText = loc,
                ActualNumeric = peakSlope,
                RequiredNumeric = options.MaxSuperelevationPercent,
                CompliancePercent = RoadwayFormulas.ComputeCompliancePercent(peakSlope, options.MaxSuperelevationPercent, higherIsBetter: false),
                Headline = exceedsMax
                    ? $"{group.Key} cross-slope ({peakSlope:0.0}%) exceeds maximum allowed tilt"
                    : $"{group.Key} superelevation tilt ({peakSlope:0.0}%) is within allowed limit",
                YourDrawingText = $"{peakSlope:0.00}% max cross-slope",
                StandardRequiresText = $"At most {options.MaxSuperelevationPercent:0.0}%",
                DifferenceText = exceedsMax
                    ? $"+{peakSlope - options.MaxSuperelevationPercent:0.00}% too steep"
                    : $"{options.MaxSuperelevationPercent - peakSlope:0.00}% below max",
                WhyItMatters = "Excessive cross-slope can cause slow-moving heavy trucks to slide inward when traffic slows down or pavement is wet.",
                HowToFix = exceedsMax
                    ? $"Open Civil 3D Superelevation Tabular Editor for {group.Key} and cap full superelevation at {options.MaxSuperelevationPercent:0.0}% (increase curve radius if required)."
                    : "No action needed — superelevation rate is within project e_max.",
                TechnicalReference = $"{standard.ReferenceCode} Sec. 4.6.6.1 · Maximum Superelevation Rate (e_max)"
            });

            // Check runoff transition length (LevelCrown -> BeginFullSuper) using SHC 301 Eq. 4-11
            var levelCrown = ordered.FirstOrDefault(s => s.StationTypeName.Contains("LevelCrown", StringComparison.OrdinalIgnoreCase));
            var beginFull = ordered.FirstOrDefault(s => s.StationTypeName.Contains("BeginFullSuper", StringComparison.OrdinalIgnoreCase));
            if (levelCrown != null && beginFull != null && beginFull.Station > levelCrown.Station && row.MaxRunoffSlopePercent > 0)
            {
                var actualRunoff = beginFull.Station - levelCrown.Station;
                var formulaRunoff = RoadwayFormulas.ComputeMinSuperelevationRunoffLengthM(
                    peakSlope,
                    options.RotatedPavementWidthM,
                    row.MaxRunoffSlopePercent);
                var reqRunoff = Math.Max(formulaRunoff, standard.Constants.MinSuperelevationRunoffM);
                var passes = actualRunoff + 1e-6 >= reqRunoff;

                results.Add(new DesignCheckResult
                {
                    Severity = passes ? CheckSeverity.Pass : CheckSeverity.Fail,
                    Category = CheckCategory.HorizontalAlignment,
                    CategoryLabel = "Superelevation Runoff",
                    CheckCode = "H-SUPER-RUNOFF",
                    AlignmentName = alignment.Name,
                    EntityLabel = group.Key,
                    StartStation = levelCrown.Station,
                    EndStation = beginFull.Station,
                    DesignSpeedKph = speed,
                    LocationText = $"{group.Key} Runoff · {StationFormatter.FormatRange(levelCrown.Station, beginFull.Station, alignment.DrawingUnits)}",
                    ActualNumeric = actualRunoff,
                    RequiredNumeric = reqRunoff,
                    CompliancePercent = RoadwayFormulas.ComputeCompliancePercent(actualRunoff, reqRunoff, higherIsBetter: true),
                    Headline = passes
                        ? $"{group.Key} superelevation transition length is smooth"
                        : $"{group.Key} rotates into superelevation too abruptly",
                    YourDrawingText = $"{actualRunoff:0.0} m runoff",
                    StandardRequiresText = $"At least {reqRunoff:0.0} m (r ≤ {row.MaxRunoffSlopePercent:0.00}%)",
                    DifferenceText = passes
                        ? $"+{actualRunoff - reqRunoff:0.0} m margin"
                        : $"{reqRunoff - actualRunoff:0.0} m too short",
                    WhyItMatters = "If pavement cross-slope rotates too quickly, the road edge looks warped and drivers feel an abrupt roll motion entering the curve.",
                    HowToFix = passes
                        ? "No action needed — superelevation runoff meets SHC 301 Eq. 4-11."
                        : $"Increase the superelevation runoff distance in Civil 3D to at least {reqRunoff:0.0} m (based on w = {options.RotatedPavementWidthM:0.0} m and r = {row.MaxRunoffSlopePercent:0.00}%).",
                    TechnicalReference = $"{standard.ReferenceCode} Eq. 4-11 & Table 4-20 · L = 50 × e × w / r"
                });
            }
        }
    }

    private static HorizontalSegmentSnapshot? FindNearestArc(List<HorizontalSegmentSnapshot> segments, int index, int direction)
    {
        for (var i = index + direction; i >= 0 && i < segments.Count; i += direction)
        {
            if (segments[i].Kind == HorizontalSegmentKind.Arc)
                return segments[i];
            if (segments[i].Kind == HorizontalSegmentKind.Line)
                break;
        }

        return null;
    }
}
