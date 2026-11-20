using RoadwayDesignChecker.Core.Criteria;
using RoadwayDesignChecker.Core.Models;

namespace RoadwayDesignChecker.Core.Checks;

/// <summary>
/// Checks vertical profile geometry against SHC 301 &amp; AASHTO standards:
/// minimum drainage grade, maximum desirable &amp; allowable grade, crest/sag K-values,
/// and minimum vertical curve length.
/// </summary>
public sealed class VerticalProfileChecker : IRoadwayCheck
{
    public CheckCategory Category => CheckCategory.VerticalProfile;

    public IEnumerable<DesignCheckResult> Evaluate(
        AlignmentSnapshot alignment,
        ProfileSnapshot? profile,
        DesignStandardProfile standard,
        AuditOptions options)
    {
        var results = new List<DesignCheckResult>();
        if (profile == null || profile.Entities.Count == 0)
            return results;

        var entities = profile.Entities.OrderBy(e => e.StartStation).ToList();

        foreach (var ent in entities)
        {
            var midSta = (ent.StartStation + ent.EndStation) / 2.0;
            var speed = CriteriaResolver.ResolveDesignSpeedKph(alignment, midSta, options.FallbackDesignSpeedKph);
            var row = CriteriaResolver.ResolveRow(standard, speed);

            if (ent.Kind == VerticalEntityKind.Tangent)
            {
                EvaluateTangentGrade(alignment, profile, ent, speed, row, standard, options, results);
            }
            else
            {
                EvaluateVerticalCurveK(alignment, profile, ent, speed, row, standard, results);
                EvaluateVerticalCurveLength(alignment, profile, ent, speed, row, standard, results);
            }
        }

        return results;
    }

    private static void EvaluateTangentGrade(
        AlignmentSnapshot alignment,
        ProfileSnapshot profile,
        VerticalEntitySnapshot tangent,
        double speed,
        SpeedCriteriaRow row,
        DesignStandardProfile standard,
        AuditOptions options,
        List<DesignCheckResult> results)
    {
        if (!tangent.GradePercent.HasValue)
            return;

        var signedGrade = tangent.GradePercent.Value;
        var absGrade = Math.Abs(signedGrade);
        var slopeWord = signedGrade >= 0 ? "uphill" : "downhill";
        var loc = $"{tangent.Label} · {StationFormatter.FormatRange(tangent.StartStation, tangent.EndStation, alignment.DrawingUnits)}";

        // 1. Maximum Grade Check (Desirable vs Allowable per SHC 301 Table 4-17 / 5-5 / 7-4)
        var (maxDesirable, maxAllowable) = CriteriaResolver.ResolveMaxGrades(row, options.RoadwayClass, options.Terrain);

        var maxSeverity = absGrade > maxAllowable + 1e-6
            ? CheckSeverity.Fail
            : absGrade > maxDesirable + 1e-6
                ? CheckSeverity.Warning
                : CheckSeverity.Pass;

        results.Add(new DesignCheckResult
        {
            Severity = maxSeverity,
            Category = CheckCategory.VerticalProfile,
            CategoryLabel = "Road Steepness (Grade)",
            CheckCode = "V-GRADE-MAX",
            AlignmentName = alignment.Name,
            ProfileName = profile.Name,
            EntityLabel = tangent.Label,
            StartStation = tangent.StartStation,
            EndStation = tangent.EndStation,
            DesignSpeedKph = speed,
            LocationText = loc,
            ActualNumeric = absGrade,
            RequiredNumeric = maxAllowable,
            CompliancePercent = RoadwayFormulas.ComputeCompliancePercent(absGrade, maxAllowable, higherIsBetter: false),
            Headline = maxSeverity switch
            {
                CheckSeverity.Fail => $"{tangent.Label} {slopeWord} slope ({absGrade:0.00}%) exceeds maximum allowed grade",
                CheckSeverity.Warning => $"{tangent.Label} {slopeWord} slope ({absGrade:0.00}%) is steeper than desirable ({maxDesirable:0.0}%)",
                _ => $"{tangent.Label} {slopeWord} slope ({absGrade:0.00}%) is smooth and comfortable"
            },
            YourDrawingText = $"{signedGrade:+0.00;-0.00;0.00}% grade",
            StandardRequiresText = $"Max {maxAllowable:0.0}% allowed (Desirable ≤ {maxDesirable:0.0}%)",
            DifferenceText = maxSeverity switch
            {
                CheckSeverity.Fail => $"+{absGrade - maxAllowable:0.00}% over limit",
                CheckSeverity.Warning => $"+{absGrade - maxDesirable:0.00}% above desirable",
                _ => $"{maxAllowable - absGrade:0.00}% below max"
            },
            WhyItMatters = "Steep grades slow heavy trucks uphill (reducing capacity and safety) and lengthen braking distances downhill.",
            HowToFix = maxSeverity == CheckSeverity.Pass
                ? "No action needed — longitudinal grade meets desirable criteria."
                : $"Adjust the PVI elevations at the ends of {tangent.Label} in Civil 3D Profile Layout Tools to bring the slope within {maxAllowable:0.0}% (ideally ≤ {maxDesirable:0.0}%).",
            TechnicalReference = $"{standard.ReferenceCode} Table 4-17 / 5-5 / 7-4 · Max Desirable {maxDesirable:0.0}%, Max Allowable {maxAllowable:0.0}%"
        });

        // 2. Minimum Drainage Grade Check (SHC 301 Sec. 4.6.4.2: 0.5% min, 0.7% desirable)
        var minDrain = standard.Constants.MinDrainageGradePercent;
        var desDrain = standard.Constants.DesirableDrainageGradePercent;
        if (absGrade < desDrain - 1e-6)
        {
            var isBelowAbsMin = absGrade < minDrain - 1e-6;
            results.Add(new DesignCheckResult
            {
                Severity = isBelowAbsMin ? CheckSeverity.Warning : CheckSeverity.Pass,
                Category = CheckCategory.VerticalProfile,
                CategoryLabel = "Road Drainage Slope",
                CheckCode = "V-GRADE-MIN",
                AlignmentName = alignment.Name,
                ProfileName = profile.Name,
                EntityLabel = tangent.Label,
                StartStation = tangent.StartStation,
                EndStation = tangent.EndStation,
                DesignSpeedKph = speed,
                LocationText = loc,
                ActualNumeric = absGrade,
                RequiredNumeric = minDrain,
                CompliancePercent = RoadwayFormulas.ComputeCompliancePercent(Math.Max(absGrade, 0.05), minDrain, higherIsBetter: true),
                Headline = isBelowAbsMin
                    ? $"{tangent.Label} is very flat ({absGrade:0.00}%) — check rainwater drainage"
                    : $"{tangent.Label} meets minimum paved drainage grade ({absGrade:0.00}%)",
                YourDrawingText = $"{absGrade:0.00}% slope",
                StandardRequiresText = $"Min {minDrain:0.0}% (Preferred ≥ {desDrain:0.0}%)",
                DifferenceText = isBelowAbsMin
                    ? $"{minDrain - absGrade:0.00}% below min drainage"
                    : "Adequate for paved/curbed section",
                WhyItMatters = "Very flat longitudinal grades (< 0.5%) can allow rainwater to pond along curbs, gutters, and superelevation transition zones, creating hydroplaning hazards.",
                HowToFix = isBelowAbsMin
                    ? $"Tilt {tangent.Label} to at least {minDrain:0.0}% (or {desDrain:0.0}% on bridges/tunnels), or confirm sufficient lateral cross-slope drainage."
                    : "Verify cross-slope is ≥ 2.0% if this section passes through a superelevation transition.",
                TechnicalReference = $"{standard.ReferenceCode} Sec. 4.6.4.2 · Minimum Drainage Grade (0.5% min / 0.7% desirable)"
            });
        }
    }

    private static void EvaluateVerticalCurveK(
        AlignmentSnapshot alignment,
        ProfileSnapshot profile,
        VerticalEntitySnapshot curve,
        double speed,
        SpeedCriteriaRow row,
        DesignStandardProfile standard,
        List<DesignCheckResult> results)
    {
        var orientation = ResolveOrientation(curve);
        if (orientation == VerticalCurveOrientation.None)
            return;

        var algebraicA = curve.AlgebraicGradeDiffPercent
            ?? (curve.GradeInPercent.HasValue && curve.GradeOutPercent.HasValue
                ? Math.Abs(curve.GradeOutPercent.Value - curve.GradeInPercent.Value)
                : 0.0);

        var actualK = curve.KValue ?? RoadwayFormulas.ComputeVerticalK(curve.Length, algebraicA);
        if (double.IsInfinity(actualK) || double.IsNaN(actualK))
            return;

        var isCrest = orientation == VerticalCurveOrientation.Crest;
        var minK = isCrest ? row.MinCrestKSsd : row.MinSagK;
        if (minK <= 0)
            return;

        var passes = actualK + 1e-6 >= minK;
        var diff = actualK - minK;
        var reqLength = algebraicA > 1e-6 ? minK * algebraicA : row.MinVerticalCurveLengthM;
        var curveKindWords = isCrest ? "Hill crest" : "Valley dip";
        var loc = $"{curve.Label} · {StationFormatter.FormatRange(curve.StartStation, curve.EndStation, alignment.DrawingUnits)}";

        results.Add(new DesignCheckResult
        {
            Severity = passes ? CheckSeverity.Pass : CheckSeverity.Fail,
            Category = CheckCategory.VerticalProfile,
            CategoryLabel = isCrest ? "Hill Crest Curve (K-Value)" : "Valley Sag Curve (K-Value)",
            CheckCode = isCrest ? "V-K-CREST" : "V-K-SAG",
            AlignmentName = alignment.Name,
            ProfileName = profile.Name,
            EntityLabel = curve.Label,
            StartStation = curve.StartStation,
            EndStation = curve.EndStation,
            DesignSpeedKph = speed,
            LocationText = loc,
            ActualNumeric = actualK,
            RequiredNumeric = minK,
            CompliancePercent = RoadwayFormulas.ComputeCompliancePercent(actualK, minK, higherIsBetter: true),
            Headline = passes
                ? $"{curveKindWords} {curve.Label} is gentle enough for safe visibility at {speed:0} km/h"
                : isCrest
                    ? $"Hill crest {curve.Label} is too abrupt and blocks forward stopping view"
                    : $"Valley curve {curve.Label} is too abrupt for night headlight visibility",
            YourDrawingText = $"K = {actualK:0.0} (Length {curve.Length:0.0} m)",
            StandardRequiresText = $"At least K = {minK:0.0} (Length ≥ {reqLength:0.0} m)",
            DifferenceText = passes
                ? $"+{diff:0.0} K safety margin"
                : $"{Math.Abs(diff):0.0} K below minimum",
            WhyItMatters = isCrest
                ? $"Drivers going over a sharp hill crest at {speed:0} km/h cannot see an obstacle on the far side early enough to brake safely."
                : $"In a sharp valley dip at night, vehicle headlights shine into the upward slope ahead instead of illuminating the full {row.StoppingSightDistanceLevelM:0} m stopping distance.",
            HowToFix = passes
                ? "No action needed — vertical curve K-value satisfies stopping sight distance."
                : $"Increase vertical curve {curve.Label} length from {curve.Length:0.0} m to at least {reqLength:0.0} m (K ≥ {minK:0.0}), or flatten the incoming/outgoing tangent grades.",
            TechnicalReference = $"{standard.ReferenceCode} Table 4-18 / Eq. {(isCrest ? "4-7" : "4-9")} · K = L / |A|, |A| = {algebraicA:0.00}%"
        });
    }

    private static void EvaluateVerticalCurveLength(
        AlignmentSnapshot alignment,
        ProfileSnapshot profile,
        VerticalEntitySnapshot curve,
        double speed,
        SpeedCriteriaRow row,
        DesignStandardProfile standard,
        List<DesignCheckResult> results)
    {
        var minLen = row.MinVerticalCurveLengthM > 0
            ? row.MinVerticalCurveLengthM
            : standard.Constants.MinVerticalCurveSpeedFactor * speed;

        var actualLen = curve.Length;
        var passes = actualLen + 1e-6 >= minLen;
        var diff = actualLen - minLen;
        var loc = $"{curve.Label} · {StationFormatter.FormatRange(curve.StartStation, curve.EndStation, alignment.DrawingUnits)}";

        results.Add(new DesignCheckResult
        {
            Severity = passes ? CheckSeverity.Pass : CheckSeverity.Fail,
            Category = CheckCategory.VerticalProfile,
            CategoryLabel = "Vertical Curve Length",
            CheckCode = "V-LEN-MIN",
            AlignmentName = alignment.Name,
            ProfileName = profile.Name,
            EntityLabel = curve.Label,
            StartStation = curve.StartStation,
            EndStation = curve.EndStation,
            DesignSpeedKph = speed,
            LocationText = loc,
            ActualNumeric = actualLen,
            RequiredNumeric = minLen,
            CompliancePercent = RoadwayFormulas.ComputeCompliancePercent(actualLen, minLen, higherIsBetter: true),
            Headline = passes
                ? $"{curve.Label} vertical curve length meets minimum smooth-ride requirement"
                : $"{curve.Label} vertical curve is too short for {speed:0} km/h",
            YourDrawingText = $"{actualLen:0.0} m length",
            StandardRequiresText = $"At least {minLen:0.0} m (0.6 × V)",
            DifferenceText = passes ? $"+{diff:0.0} m margin" : $"{Math.Abs(diff):0.0} m too short",
            WhyItMatters = "Even when grade changes are small, very short vertical curves create noticeable vertical bumps and poor visual appearance at highway speeds.",
            HowToFix = passes
                ? "No action needed — vertical curve length meets minimum standard."
                : $"Extend {curve.Label} length in Civil 3D Profile Grid View to at least {minLen:0.0} m.",
            TechnicalReference = $"{standard.ReferenceCode} Sec. 4.6.4.3 & Table 4-18 · L_min = 0.6 × V"
        });
    }

    private static VerticalCurveOrientation ResolveOrientation(VerticalEntitySnapshot curve)
    {
        if (curve.Orientation != VerticalCurveOrientation.None)
            return curve.Orientation;

        if (curve.GradeInPercent.HasValue && curve.GradeOutPercent.HasValue)
            return curve.GradeInPercent.Value > curve.GradeOutPercent.Value
                ? VerticalCurveOrientation.Crest
                : VerticalCurveOrientation.Sag;

        return VerticalCurveOrientation.None;
    }
}
