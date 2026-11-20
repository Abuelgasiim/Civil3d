namespace RoadwayDesignChecker.Core.Models;

public enum UnitSystem
{
    Metric,
    Imperial
}

public enum RoadwayClassification
{
    FreewayExpressway,
    Arterial,
    Collector,
    Local
}

public enum TerrainType
{
    Level,
    Rolling,
    Mountainous
}

public enum PassingSightMethod
{
    /// <summary>Completing the passing maneuver (SHC 301 Table 4-12 Method 1 / AASHTO 2018).</summary>
    Method1Complete,

    /// <summary>Aborting the passing maneuver (SHC 301 Table 4-12 Method 2 / TxDOT 2022).</summary>
    Method2Abort
}

public enum CheckCategory
{
    HorizontalAlignment,
    VerticalProfile,
    SightDistance
}

public enum CheckSeverity
{
    Pass,
    Warning,
    Fail
}

public enum HorizontalSegmentKind
{
    Line,
    Arc,
    Spiral
}

public enum VerticalEntityKind
{
    Tangent,
    SymmetricParabola,
    AsymmetricParabola,
    CircularCurve
}

public enum VerticalCurveOrientation
{
    None,
    Crest,
    Sag
}
