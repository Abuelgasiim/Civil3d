namespace RoadwayDesignChecker.Core.Checks;

/// <summary>
/// Pure geometric roadway engineering formulas verified against SHC 301 Chapter 4 &amp; AASHTO 2018 (Metric).
/// </summary>
public static class RoadwayFormulas
{
    /// <summary>
    /// SHC 301 Eq. 4-6: R_min = V^2 / [127 * (e_dec + f_R)]
    /// </summary>
    public static double ComputeMinimumRadiusM(double speedKph, double emaxPercent, double sideFrictionFactor)
    {
        var denom = 127.0 * ((emaxPercent / 100.0) + sideFrictionFactor);
        return denom > 0 ? (speedKph * speedKph) / denom : 0;
    }

    /// <summary>
    /// SHC 301 Eq. 4-1: SSD = 0.278 * V * t + V^2 / [254 * (a/9.81 + G_dec)]
    /// GradePercent is positive for upgrades and negative for downgrades.
    /// </summary>
    public static double ComputeStoppingSightDistanceM(
        double speedKph,
        double gradePercent = 0.0,
        double reactionTimeSec = 2.5,
        double decelerationMps2 = 3.4)
    {
        var reactionDist = 0.278 * speedKph * reactionTimeSec;
        var frictionTerm = (decelerationMps2 / 9.81) + (gradePercent / 100.0);
        if (frictionTerm <= 0.05)
            frictionTerm = 0.05; // Prevent division blowup on extreme negative slopes

        var brakingDist = (speedKph * speedKph) / (254.0 * frictionTerm);
        return reactionDist + brakingDist;
    }

    /// <summary>
    /// SHC 301 Eq. 4-2: m = R * [1 - cos(S / (2 * R))]
    /// Required clear lateral distance (middle ordinate) on the inside of a horizontal curve.
    /// </summary>
    public static double ComputeRequiredMiddleOrdinateM(double radiusM, double sightDistanceM)
    {
        if (radiusM <= 0 || sightDistanceM <= 0)
            return 0;

        var halfAngleRad = Math.Min(sightDistanceM / (2.0 * radiusM), Math.PI / 2.0);
        return radiusM * (1.0 - Math.Cos(halfAngleRad));
    }

    /// <summary>
    /// SHC 301 Eq. 4-3: S_a = 2 * R * arccos(1 - m / R)
    /// Available horizontal sight distance given curve radius and clear lateral offset m.
    /// </summary>
    public static double ComputeAvailableHorizontalSightDistanceM(double radiusM, double lateralClearanceM)
    {
        if (radiusM <= 0 || lateralClearanceM <= 0)
            return 0;

        var ratio = Math.Clamp(1.0 - (lateralClearanceM / radiusM), -1.0, 1.0);
        return 2.0 * radiusM * Math.Acos(ratio);
    }

    /// <summary>
    /// SHC 301 Eq. 4-7 / 4-8: Crest K_min = S^2 / [200 * (sqrt(h1) + sqrt(h2))^2]
    /// Note: For h1 = 1.08 m, h2 = 0.60 m (SSD), denominator is 658. For h2 = 1.08 m (PSD), denominator is 864.
    /// </summary>
    public static double ComputeCrestMinK(double sightDistanceM, double eyeHeightM = 1.08, double objectHeightM = 0.60)
    {
        var denom = 200.0 * Math.Pow(Math.Sqrt(eyeHeightM) + Math.Sqrt(objectHeightM), 2);
        return denom > 0 ? (sightDistanceM * sightDistanceM) / denom : 0;
    }

    /// <summary>
    /// SHC 301 Eq. 4-9: Sag K_min = S^2 / (200 * h3 + 3.5 * S)
    /// </summary>
    public static double ComputeSagMinK(double sightDistanceM, double headlightHeightM = 0.60)
    {
        var denom = (200.0 * headlightHeightM) + (3.5 * sightDistanceM);
        return denom > 0 ? (sightDistanceM * sightDistanceM) / denom : 0;
    }

    /// <summary>
    /// Computes available Stopping Sight Distance on a crest vertical curve of length L and algebraic grade change A (%).
    /// Handles both S &lt;= L and S &gt; L regimes cleanly.
    /// </summary>
    public static double ComputeAvailableCrestSightDistanceM(
        double curveLengthM,
        double algebraicGradeDiffPercent,
        double eyeHeightM = 1.08,
        double objectHeightM = 0.60)
    {
        var a = Math.Abs(algebraicGradeDiffPercent);
        if (curveLengthM <= 0 || a <= 1e-6)
            return double.PositiveInfinity;

        var c = 200.0 * Math.Pow(Math.Sqrt(eyeHeightM) + Math.Sqrt(objectHeightM), 2);
        var sInside = Math.Sqrt((curveLengthM * c) / a);
        if (sInside <= curveLengthM)
            return sInside;

        // S > L regime: L = 2*S - c / A  =>  S = (L + c / A) / 2
        return 0.5 * (curveLengthM + (c / a));
    }

    /// <summary>
    /// Computes available Headlight Sight Distance on a sag vertical curve of length L and algebraic grade change A (%).
    /// </summary>
    public static double ComputeAvailableSagSightDistanceM(
        double curveLengthM,
        double algebraicGradeDiffPercent,
        double headlightHeightM = 0.60)
    {
        var a = Math.Abs(algebraicGradeDiffPercent);
        if (curveLengthM <= 0 || a <= 1e-6)
            return double.PositiveInfinity;

        // S <= L regime: A * S^2 - 3.5 * L * S - 200 * h3 * L = 0
        var bTerm = -3.5 * curveLengthM;
        var cTerm = -200.0 * headlightHeightM * curveLengthM;
        var disc = (bTerm * bTerm) - (4.0 * a * cTerm);
        var sInside = (-bTerm + Math.Sqrt(Math.Max(0, disc))) / (2.0 * a);
        if (sInside <= curveLengthM)
            return sInside;

        // S > L regime: L = 2*S - (200*h3 + 3.5*S)/A  =>  S*(2 - 3.5/A) = L + 200*h3/A
        var denom = 2.0 - (3.5 / a);
        if (denom <= 0.05)
            return Math.Max(sInside, curveLengthM * 2.0);

        return (curveLengthM + ((200.0 * headlightHeightM) / a)) / denom;
    }

    /// <summary>
    /// SHC 301 Eq. 4-10: Compound slope p = sqrt(G^2 + e^2) in percent.
    /// </summary>
    public static double ComputeCompoundSlopePercent(double gradePercent, double superelevationPercent)
    {
        return Math.Sqrt((gradePercent * gradePercent) + (superelevationPercent * superelevationPercent));
    }

    /// <summary>
    /// SHC 301 Eq. 4-11 / Eq. 5-1: L = 50 * e_dec * w / r_percent
    /// Example from SHC 301 p.107: V=80 (r=0.50%), w=7.3 m, e=10% (0.10) => L = 73.0 m.
    /// </summary>
    public static double ComputeMinSuperelevationRunoffLengthM(
        double superelevationPercent,
        double rotatedWidthM,
        double maxRunoffSlopePercent)
    {
        if (superelevationPercent <= 0 || rotatedWidthM <= 0 || maxRunoffSlopePercent <= 0)
            return 0;

        var eDecimal = superelevationPercent / 100.0;
        return (50.0 * eDecimal * rotatedWidthM) / maxRunoffSlopePercent;
    }

    /// <summary>
    /// Computes vertical curve rate of curvature K = L / |A|.
    /// </summary>
    public static double ComputeVerticalK(double curveLengthM, double algebraicGradeDiffPercent)
    {
        var a = Math.Abs(algebraicGradeDiffPercent);
        return a > 1e-6 ? curveLengthM / a : double.PositiveInfinity;
    }

    /// <summary>
    /// Clamps a ratio (0..100) for clean UI progress meter display.
    /// </summary>
    public static double ComputeCompliancePercent(double actual, double required, bool higherIsBetter)
    {
        if (required <= 0 || actual <= 0)
            return 100.0;

        var ratio = higherIsBetter ? (actual / required) : (required / actual);
        return Math.Clamp(Math.Round(ratio * 100.0, 1), 5.0, 100.0);
    }
}
