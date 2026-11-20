using RoadwayDesignChecker.Core.Criteria;
using RoadwayDesignChecker.Core.Models;

namespace RoadwayDesignChecker.Core.Checks;

/// <summary>
/// Checks driver sight distance across horizontal curves and vertical curves:
/// - Horizontal Stopping Sight Distance (lateral clearance middle ordinate M per SHC 301 Eq. 4-2 / 4-3)
/// - Vertical Stopping Sight Distance with longitudinal grade adjustment (SHC 301 Eq. 4-1)
/// - Optional Passing Sight Distance (SHC 301 Table 4-12 Method 1 / Method 2)
/// </summary>
public sealed class SightDistanceChecker : IRoadwayCheck
{
    public CheckCategory Category => CheckCategory.SightDistance;

    public IEnumerable<DesignCheckResult> Evaluate(
        AlignmentSnapshot alignment,
        ProfileSnapshot? profile,
        DesignStandardProfile standard,
        AuditOptions options)
    {
        var results = new List<DesignCheckResult>();

        EvaluateHorizontalSightDistance(alignment, profile, standard, options, results);

        if (profile != null)
            EvaluateVerticalSightDistance(alignment, profile, standard, options, results);

        return results;
    }

    private static void EvaluateHorizontalSightDistance(
        AlignmentSnapshot alignment,
        ProfileSnapshot? profile,
        DesignStandardProfile standard,
        AuditOptions options,
        List<DesignCheckResult> results)
    {
        foreach (var arc in alignment.Segments.Where(s => s.Kind == HorizontalSegmentKind.Arc && s.Radius > 0))
        {
            var radius = arc.Radius!.Value;
            var midSta = (arc.StartStation + arc.EndStation) / 2.0;
            var speed = CriteriaResolver.ResolveDesignSpeedKph(alignment, midSta, options.FallbackDesignSpeedKph);
            var row = CriteriaResolver.ResolveRow(standard, speed);

            // Account for worst downgrade along the curve if a profile is provided
            var governingGrade = ResolveWorstDowngradePercent(profile, arc.StartStation, arc.EndStation);
            var calcSsd = RoadwayFormulas.ComputeStoppingSightDistanceM(
                speed,
                governingGrade,
                standard.Constants.ReactionTimeSeconds,
                standard.Constants.DecelerationRateMps2);
            var reqSsd = Math.Max(row.StoppingSightDistanceLevelM, calcSsd);

            var reqM = RoadwayFormulas.ComputeRequiredMiddleOrdinateM(radius, reqSsd);
            var availM = options.AvailableLateralClearanceM;
            var availSightDist = RoadwayFormulas.ComputeAvailableHorizontalSightDistanceM(radius, availM);

            var passes = availM + 1e-6 >= reqM;
            var loc = $"{arc.Label} · {StationFormatter.FormatRange(arc.StartStation, arc.EndStation, alignment.DrawingUnits)}";

            results.Add(new DesignCheckResult
            {
                Severity = passes ? CheckSeverity.Pass : CheckSeverity.Fail,
                Category = CheckCategory.SightDistance,
                CategoryLabel = "Horizontal Sight Clearance",
                CheckCode = "SD-HORIZ-SSD",
                AlignmentName = alignment.Name,
                ProfileName = profile?.Name,
                EntityLabel = arc.Label,
                StartStation = arc.StartStation,
                EndStation = arc.EndStation,
                DesignSpeedKph = speed,
                LocationText = loc,
                ActualNumeric = availM,
                RequiredNumeric = reqM,
                CompliancePercent = RoadwayFormulas.ComputeCompliancePercent(availM, reqM, higherIsBetter: true),
                Headline = passes
                    ? $"Inside clearance on {arc.Label} gives clear stopping sight view"
                    : $"Inside barrier or cut slope on {arc.Label} blocks driver sightline",
                YourDrawingText = $"{availM:0.00} m clear (gives {availSightDist:0} m view)",
                StandardRequiresText = $"At least {reqM:0.00} m clear (for {reqSsd:0} m SSD)",
                DifferenceText = passes
                    ? $"+{availM - reqM:0.00} m extra clearance"
                    : $"Need {reqM - availM:0.00} m more inside clearance",
                WhyItMatters = $"On horizontal curves, median barriers, bridge piers, or cut slopes on the inside of the turn can block a driver's view around the bend before they have {reqSsd:0} m to stop.",
                HowToFix = passes
                    ? "No action needed — assumed lateral clearance satisfies stopping sight distance."
                    : $"Widen the inside shoulder/clear zone on {arc.Label} to at least {reqM:0.00} m from the inside lane centerline, or increase the curve radius.",
                TechnicalReference = $"{standard.ReferenceCode} Eq. 4-2 & 4-3 · m = R × [1 - cos(S / (2R))], grade = {governingGrade:+0.0;-0.0;0.0}%"
            });
        }
    }

    private static void EvaluateVerticalSightDistance(
        AlignmentSnapshot alignment,
        ProfileSnapshot profile,
        DesignStandardProfile standard,
        AuditOptions options,
        List<DesignCheckResult> results)
    {
        foreach (var curve in profile.Entities.Where(e => e.Kind != VerticalEntityKind.Tangent))
        {
            var midSta = (curve.StartStation + curve.EndStation) / 2.0;
            var speed = CriteriaResolver.ResolveDesignSpeedKph(alignment, midSta, options.FallbackDesignSpeedKph);
            var row = CriteriaResolver.ResolveRow(standard, speed);

            var algebraicA = curve.AlgebraicGradeDiffPercent
                ?? (curve.GradeInPercent.HasValue && curve.GradeOutPercent.HasValue
                    ? Math.Abs(curve.GradeOutPercent.Value - curve.GradeInPercent.Value)
                    : 0.0);

            if (algebraicA <= 1e-6 || curve.Length <= 0)
                continue;

            var isCrest = curve.Orientation == VerticalCurveOrientation.Crest ||
                          (curve.Orientation == VerticalCurveOrientation.None &&
                           curve.GradeInPercent.HasValue && curve.GradeOutPercent.HasValue &&
                           curve.GradeInPercent.Value > curve.GradeOutPercent.Value);

            var worstGrade = Math.Min(curve.GradeInPercent ?? 0.0, curve.GradeOutPercent ?? 0.0);
            var reqSsd = Math.Max(
                row.StoppingSightDistanceLevelM,
                RoadwayFormulas.ComputeStoppingSightDistanceM(
                    speed,
                    Math.Min(0.0, worstGrade),
                    standard.Constants.ReactionTimeSeconds,
                    standard.Constants.DecelerationRateMps2));

            var availSsd = isCrest
                ? RoadwayFormulas.ComputeAvailableCrestSightDistanceM(
                    curve.Length,
                    algebraicA,
                    standard.Constants.DriverEyeHeightM,
                    standard.Constants.SsdObjectHeightM)
                : RoadwayFormulas.ComputeAvailableSagSightDistanceM(
                    curve.Length,
                    algebraicA,
                    standard.Constants.HeadlightHeightM);

            var passesSsd = availSsd + 1e-6 >= reqSsd;
            var loc = $"{curve.Label} · {StationFormatter.FormatRange(curve.StartStation, curve.EndStation, alignment.DrawingUnits)}";

            results.Add(new DesignCheckResult
            {
                Severity = passesSsd ? CheckSeverity.Pass : CheckSeverity.Fail,
                Category = CheckCategory.SightDistance,
                CategoryLabel = isCrest ? "Crest Stopping Sight Distance" : "Sag Headlight Sight Distance",
                CheckCode = isCrest ? "SD-VERT-CREST-SSD" : "SD-VERT-SAG-SSD",
                AlignmentName = alignment.Name,
                ProfileName = profile.Name,
                EntityLabel = curve.Label,
                StartStation = curve.StartStation,
                EndStation = curve.EndStation,
                DesignSpeedKph = speed,
                LocationText = loc,
                ActualNumeric = availSsd,
                RequiredNumeric = reqSsd,
                CompliancePercent = RoadwayFormulas.ComputeCompliancePercent(availSsd, reqSsd, higherIsBetter: true),
                Headline = passesSsd
                    ? $"{curve.Label} gives drivers clear stopping visibility ({availSsd:0} m)"
                    : $"{curve.Label} only provides {availSsd:0} m sight distance (need {reqSsd:0} m)",
                YourDrawingText = $"{availSsd:0.0} m available view",
                StandardRequiresText = $"At least {reqSsd:0.0} m SSD",
                DifferenceText = passesSsd
                    ? $"+{availSsd - reqSsd:0.0} m extra view"
                    : $"{reqSsd - availSsd:0.0} m short of safe stopping distance",
                WhyItMatters = $"At {speed:0} km/h (with governing slope {Math.Min(0.0, worstGrade):0.0}%), a driver needs {reqSsd:0} m to react (2.5 s) and brake safely to a full stop.",
                HowToFix = passesSsd
                    ? "No action needed — vertical stopping sight distance is met."
                    : $"Lengthen vertical curve {curve.Label} or reduce grade difference |A| ({algebraicA:0.0}%) until available sight distance reaches {reqSsd:0} m.",
                TechnicalReference = $"{standard.ReferenceCode} Eq. 4-1 & Sec. 4.6.4.3 · t = {standard.Constants.ReactionTimeSeconds:0.0} s, a = {standard.Constants.DecelerationRateMps2:0.0} m/s²"
            });

            // Optional Passing Sight Distance (PSD) check on crest vertical curves for 2-lane undivided highways
            if (isCrest && options.EvaluatePassingSightDistance)
            {
                var reqPsd = options.PassingMethod == PassingSightMethod.Method1Complete
                    ? row.PassingSightDistanceMethod1M
                    : row.PassingSightDistanceMethod2M;

                if (reqPsd.HasValue && reqPsd.Value > 0)
                {
                    var availPsd = RoadwayFormulas.ComputeAvailableCrestSightDistanceM(
                        curve.Length,
                        algebraicA,
                        standard.Constants.DriverEyeHeightM,
                        standard.Constants.PsdObjectHeightM);

                    var passesPsd = availPsd + 1e-6 >= reqPsd.Value;
                    var methodLabel = options.PassingMethod == PassingSightMethod.Method1Complete
                        ? "Method 1 (Complete Pass)"
                        : "Method 2 (Abort Pass)";

                    results.Add(new DesignCheckResult
                    {
                        Severity = passesPsd ? CheckSeverity.Pass : CheckSeverity.Warning,
                        Category = CheckCategory.SightDistance,
                        CategoryLabel = "Passing Sight Distance (2-Lane)",
                        CheckCode = "SD-VERT-PSD",
                        AlignmentName = alignment.Name,
                        ProfileName = profile.Name,
                        EntityLabel = curve.Label,
                        StartStation = curve.StartStation,
                        EndStation = curve.EndStation,
                        DesignSpeedKph = speed,
                        LocationText = loc,
                        ActualNumeric = availPsd,
                        RequiredNumeric = reqPsd.Value,
                        CompliancePercent = RoadwayFormulas.ComputeCompliancePercent(availPsd, reqPsd.Value, higherIsBetter: true),
                        Headline = passesPsd
                            ? $"{curve.Label} allows safe overtaking ({availPsd:0} m view)"
                            : $"{curve.Label} should be marked as a No-Passing Zone",
                        YourDrawingText = $"{availPsd:0.0} m passing view",
                        StandardRequiresText = $"At least {reqPsd.Value:0.0} m ({methodLabel})",
                        DifferenceText = passesPsd
                            ? $"+{availPsd - reqPsd.Value:0.0} m margin"
                            : $"{reqPsd.Value - availPsd:0.0} m below passing distance",
                        WhyItMatters = "On two-lane single-carriageway roads, drivers overtaking a slower vehicle must see an oncoming vehicle far enough ahead to complete or abort the maneuver safely.",
                        HowToFix = passesPsd
                            ? "No action needed — overtaking sight distance is available."
                            : $"Stripe {curve.Label} as a No-Passing Zone (double solid centerline), or flatten the crest curve significantly if passing is required here.",
                        TechnicalReference = $"{standard.ReferenceCode} Table 4-12 ({methodLabel}) · h₁ = {standard.Constants.DriverEyeHeightM:0.00} m, h₂ = {standard.Constants.PsdObjectHeightM:0.00} m"
                    });
                }
            }
        }
    }

    private static double ResolveWorstDowngradePercent(ProfileSnapshot? profile, double startSta, double endSta)
    {
        if (profile == null || profile.Entities.Count == 0)
            return 0.0;

        double worst = 0.0;
        foreach (var ent in profile.Entities)
        {
            if (ent.EndStation < startSta || ent.StartStation > endSta)
                continue;

            if (ent.GradePercent.HasValue && ent.GradePercent.Value < worst)
                worst = ent.GradePercent.Value;
            if (ent.GradeInPercent.HasValue && ent.GradeInPercent.Value < worst)
                worst = ent.GradeInPercent.Value;
            if (ent.GradeOutPercent.HasValue && ent.GradeOutPercent.Value < worst)
                worst = ent.GradeOutPercent.Value;
        }

        return worst;
    }
}
