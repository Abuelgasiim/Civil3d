namespace RoadwayDesignChecker.Core.Models;

/// <summary>
/// Civil 3D-agnostic snapshot of an Alignment extracted inside a Read Transaction.
/// </summary>
public sealed class AlignmentSnapshot
{
    public string ObjectHandle { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public double StartStation { get; set; }
    public double EndStation { get; set; }
    public double Length { get; set; }
    public UnitSystem DrawingUnits { get; set; } = UnitSystem.Metric;
    public string? CriteriaFilePath { get; set; }
    public List<DesignSpeedEntry> DesignSpeeds { get; set; } = new();
    public List<HorizontalSegmentSnapshot> Segments { get; set; } = new();
    public List<SuperelevationStationSnapshot> SuperelevationStations { get; set; } = new();
}

public sealed class DesignSpeedEntry
{
    public double StartStation { get; set; }
    public double SpeedKph { get; set; }
    public string? Comment { get; set; }
}

public sealed class HorizontalSegmentSnapshot
{
    public int Index { get; set; }
    public string Label { get; set; } = string.Empty;
    public HorizontalSegmentKind Kind { get; set; }
    public double StartStation { get; set; }
    public double EndStation { get; set; }
    public double Length { get; set; }

    /// <summary>Radius in meters (for Arc) or non-infinite endpoint radius (for Spiral).</summary>
    public double? Radius { get; set; }

    /// <summary>True if curve turns clockwise (right-hand turn), false if counter-clockwise (left-hand turn).</summary>
    public bool? IsClockwise { get; set; }

    /// <summary>Spiral flatness parameter A (for clothoid spirals).</summary>
    public double? SpiralParameterA { get; set; }

    /// <summary>Midpoint X (Easting) in drawing coordinates for Zoom-to-Station fallback.</summary>
    public double? MidEasting { get; set; }

    /// <summary>Midpoint Y (Northing) in drawing coordinates for Zoom-to-Station fallback.</summary>
    public double? MidNorthing { get; set; }
}

public sealed class SuperelevationStationSnapshot
{
    public double Station { get; set; }
    public string StationTypeName { get; set; } = string.Empty;
    public string? CurveName { get; set; }

    /// <summary>Maximum absolute lane cross slope in percent (e.g., 6.0 for 6%).</summary>
    public double MaxAbsLaneSlopePercent { get; set; }
}

/// <summary>
/// Civil 3D-agnostic snapshot of a Profile associated with an Alignment.
/// </summary>
public sealed class ProfileSnapshot
{
    public string ObjectHandle { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string AlignmentName { get; set; } = string.Empty;
    public double StartStation { get; set; }
    public double EndStation { get; set; }
    public List<VerticalEntitySnapshot> Entities { get; set; } = new();
}

public sealed class VerticalEntitySnapshot
{
    public int Index { get; set; }
    public string Label { get; set; } = string.Empty;
    public VerticalEntityKind Kind { get; set; }
    public VerticalCurveOrientation Orientation { get; set; }
    public double StartStation { get; set; }
    public double EndStation { get; set; }
    public double Length { get; set; }
    public double StartElevation { get; set; }
    public double EndElevation { get; set; }

    /// <summary>Tangent longitudinal grade in percent (e.g., -4.2 for -4.2%).</summary>
    public double? GradePercent { get; set; }

    /// <summary>Incoming tangent grade in percent for vertical curves.</summary>
    public double? GradeInPercent { get; set; }

    /// <summary>Outgoing tangent grade in percent for vertical curves.</summary>
    public double? GradeOutPercent { get; set; }

    /// <summary>Algebraic grade difference |g2 - g1| in percent.</summary>
    public double? AlgebraicGradeDiffPercent { get; set; }

    /// <summary>Vertical curve rate of curvature K = L / |A| (m/%).</summary>
    public double? KValue { get; set; }

    public double? PviStation { get; set; }
    public double? PviElevation { get; set; }
}
