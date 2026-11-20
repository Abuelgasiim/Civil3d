using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.Civil;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using RoadwayDesignChecker.Core.Models;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace RoadwayDesignChecker.Civil3D.Adapters;

public sealed record AlignmentListItem(ObjectId AlignmentId, string Name, double Length, List<ProfileListItem> Profiles);
public sealed record ProfileListItem(ObjectId ProfileId, string Name);

/// <summary>
/// Reads Civil 3D Alignments, DesignSpeeds, Sub-Entities, Superelevation critical stations,
/// and child Profiles defensively inside Read Transactions and maps them to pure Core snapshots.
/// </summary>
public sealed class CivilDrawingAdapter
{
    public IReadOnlyList<AlignmentListItem> GetAlignmentsInActiveDrawing()
    {
        var doc = AcadApp.DocumentManager.MdiActiveDocument;
        var civilDoc = CivilApplication.ActiveDocument;
        if (doc == null || civilDoc == null)
            return Array.Empty<AlignmentListItem>();

        var result = new List<AlignmentListItem>();
        using var tr = doc.Database.TransactionManager.StartTransaction();

        var alignmentIds = civilDoc.GetAlignmentIds();
        foreach (ObjectId alignId in alignmentIds)
        {
            if (alignId.IsNull || alignId.IsErased)
                continue;

            if (tr.GetObject(alignId, OpenMode.ForRead) is not Alignment alignment)
                continue;

            var profiles = new List<ProfileListItem>();
            try
            {
                foreach (ObjectId profId in alignment.GetProfileIds())
                {
                    if (profId.IsNull || profId.IsErased)
                        continue;

                    if (tr.GetObject(profId, OpenMode.ForRead) is Profile profile)
                        profiles.Add(new ProfileListItem(profId, profile.Name));
                }
            }
            catch
            {
                // Keep alignment selectable even if a referenced profile cannot be opened
            }

            result.Add(new AlignmentListItem(
                alignId,
                alignment.Name,
                alignment.Length,
                profiles.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ToList()));
        }

        tr.Commit();
        return result.OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public ObjectId? PickAlignmentInDrawing()
    {
        var doc = AcadApp.DocumentManager.MdiActiveDocument;
        if (doc == null)
            return null;

        var editor = doc.Editor;
        var opts = new PromptEntityOptions("\nSelect a Civil 3D Alignment to audit: ");
        opts.SetRejectMessage("\nSelected object is not a Civil 3D Alignment.");
        opts.AddAllowedClass(typeof(Alignment), exactMatch: false);

        var res = editor.GetEntity(opts);
        return res.Status == PromptStatus.OK ? res.ObjectId : null;
    }

    public (AlignmentSnapshot Alignment, ProfileSnapshot? Profile) ExtractSnapshots(
        ObjectId alignmentId,
        ObjectId? profileId)
    {
        var doc = AcadApp.DocumentManager.MdiActiveDocument
            ?? throw new InvalidOperationException("No active Civil 3D drawing is open.");

        using var tr = doc.Database.TransactionManager.StartTransaction();

        if (tr.GetObject(alignmentId, OpenMode.ForRead) is not Alignment alignment)
            throw new InvalidOperationException("Selected alignment could not be opened.");

        var alignmentSnap = BuildAlignmentSnapshot(alignment);
        ProfileSnapshot? profileSnap = null;

        if (profileId.HasValue && !profileId.Value.IsNull && !profileId.Value.IsErased)
        {
            if (tr.GetObject(profileId.Value, OpenMode.ForRead) is Profile profile)
                profileSnap = BuildProfileSnapshot(profile, alignment.Name);
        }

        tr.Commit();
        return (alignmentSnap, profileSnap);
    }

    private static AlignmentSnapshot BuildAlignmentSnapshot(Alignment alignment)
    {
        var snap = new AlignmentSnapshot
        {
            ObjectHandle = alignment.Handle.ToString(),
            Name = alignment.Name,
            StartStation = alignment.StartingStation,
            EndStation = alignment.EndingStation,
            Length = alignment.Length,
            DrawingUnits = UnitSystem.Metric
        };

        try
        {
            if (alignment.UseDesignCriteriaFile && !string.IsNullOrWhiteSpace(alignment.CriteriaFileName))
                snap.CriteriaFilePath = alignment.CriteriaFileName;
        }
        catch
        {
            // CriteriaFileName can throw on some reference/offset alignments
        }

        // 1. Read Design Speeds
        try
        {
            if (alignment.DesignSpeeds != null)
            {
                foreach (DesignSpeed ds in alignment.DesignSpeeds)
                {
                    snap.DesignSpeeds.Add(new DesignSpeedEntry
                    {
                        StartStation = ds.Station,
                        SpeedKph = ds.Value,
                        Comment = ds.Comment
                    });
                }
            }
        }
        catch
        {
            // Fall back to user-configured design speed in AuditOptions
        }

        // 2. Read Horizontal Sub-Entities (Lines, Arcs, Spirals)
        int arcCounter = 0, lineCounter = 0, spiralCounter = 0, segIndex = 0;
        try
        {
            foreach (AlignmentEntity entity in alignment.Entities)
            {
                for (var i = 0; i < entity.SubEntityCount; i++)
                {
                    AlignmentSubEntity sub;
                    try
                    {
                        sub = entity[i];
                    }
                    catch
                    {
                        continue;
                    }

                    var item = new HorizontalSegmentSnapshot
                    {
                        Index = ++segIndex,
                        StartStation = sub.StartStation,
                        EndStation = sub.EndStation,
                        Length = sub.Length
                    };

                    if (sub is AlignmentSubEntityArc arc)
                    {
                        arcCounter++;
                        item.Kind = HorizontalSegmentKind.Arc;
                        item.Label = $"Curve C{arcCounter}";
                        item.Radius = SafeRead(() => arc.Radius);
                        item.IsClockwise = SafeRead(() => arc.Clockwise);
                    }
                    else if (sub is AlignmentSubEntitySpiral spiral)
                    {
                        spiralCounter++;
                        item.Kind = HorizontalSegmentKind.Spiral;
                        item.Label = $"Spiral S{spiralCounter}";
                        item.SpiralParameterA = SafeRead(() => spiral.A);

                        var rIn = SafeRead(() => spiral.RadiusIn);
                        var rOut = SafeRead(() => spiral.RadiusOut);
                        if (rIn.HasValue && rIn.Value > 0 && rIn.Value < 1e9) item.Radius = rIn;
                        else if (rOut.HasValue && rOut.Value > 0 && rOut.Value < 1e9) item.Radius = rOut;
                    }
                    else if (sub is AlignmentSubEntityLine)
                    {
                        lineCounter++;
                        item.Kind = HorizontalSegmentKind.Line;
                        item.Label = $"Tangent T{lineCounter}";
                    }
                    else
                    {
                        continue;
                    }

                    // Resolve midpoint Easting/Northing for navigation fallback
                    try
                    {
                        double e = 0, n = 0;
                        alignment.PointLocation((item.StartStation + item.EndStation) / 2.0, 0.0, ref e, ref n);
                        item.MidEasting = e;
                        item.MidNorthing = n;
                    }
                    catch
                    {
                        // Optional navigation cache
                    }

                    snap.Segments.Add(item);
                }
            }
        }
        catch
        {
            // Defensive guard for corrupted or data-shortcut alignments
        }

        // 3. Read Superelevation Critical Stations defensively
        try
        {
            if (alignment.SuperelevationCriticalStations != null)
            {
                var laneSegments = new[]
                {
                    SuperelevationCrossSegmentType.LeftOutLaneCrossSlope,
                    SuperelevationCrossSegmentType.LeftInLaneCrossSlope,
                    SuperelevationCrossSegmentType.RightOutLaneCrossSlope,
                    SuperelevationCrossSegmentType.RightInLaneCrossSlope
                };

                foreach (SuperelevationCriticalStation cs in alignment.SuperelevationCriticalStations)
                {
                    double maxAbsPercent = 0.0;
                    foreach (var segType in laneSegments)
                    {
                        try
                        {
                            var rawSlope = cs.GetSlope(segType);
                            var pct = Math.Abs(NormalizeSlopeToPercent(rawSlope));
                            if (pct > maxAbsPercent)
                                maxAbsPercent = pct;
                        }
                        catch
                        {
                            // InvalidOperationException is normal when a segment type is unused at that station
                        }
                    }

                    snap.SuperelevationStations.Add(new SuperelevationStationSnapshot
                    {
                        Station = cs.Station,
                        StationTypeName = cs.StationType.ToString(),
                        CurveName = SafeReadRef(() => cs.SuperelevationCurveName),
                        MaxAbsLaneSlopePercent = maxAbsPercent
                    });
                }
            }
        }
        catch
        {
            // Alignment has no computed superelevation
        }

        return snap;
    }

    private static ProfileSnapshot BuildProfileSnapshot(Profile profile, string alignmentName)
    {
        var snap = new ProfileSnapshot
        {
            ObjectHandle = profile.Handle.ToString(),
            Name = profile.Name,
            AlignmentName = alignmentName,
            StartStation = profile.StartingStation,
            EndStation = profile.EndingStation
        };

        int gradeCounter = 0, curveCounter = 0, idx = 0;
        try
        {
            foreach (ProfileEntity ent in profile.Entities)
            {
                var item = new VerticalEntitySnapshot
                {
                    Index = ++idx,
                    StartStation = ent.StartStation,
                    EndStation = ent.EndStation,
                    Length = ent.Length,
                    StartElevation = ent.StartElevation,
                    EndElevation = ent.EndElevation
                };

                if (ent is ProfileTangent tangent)
                {
                    gradeCounter++;
                    item.Kind = VerticalEntityKind.Tangent;
                    item.Label = $"Grade G{gradeCounter}";
                    item.GradePercent = NormalizeSlopeToPercent(tangent.Grade);
                }
                else if (ent is ProfileParabolaSymmetric sym)
                {
                    curveCounter++;
                    item.Kind = VerticalEntityKind.SymmetricParabola;
                    item.Label = $"Vertical Curve VC{curveCounter}";
                    item.Orientation = sym.CurveType == VerticalCurveType.Crest
                        ? VerticalCurveOrientation.Crest
                        : VerticalCurveOrientation.Sag;
                    item.GradeInPercent = NormalizeSlopeToPercent(sym.GradeIn);
                    item.GradeOutPercent = NormalizeSlopeToPercent(sym.GradeOut);
                    item.AlgebraicGradeDiffPercent = Math.Abs(NormalizeSlopeToPercent(sym.GradeChange));
                    item.KValue = sym.K;
                    item.PviStation = SafeRead(() => sym.PVIStation);
                    item.PviElevation = SafeRead(() => sym.PVIElevation);
                }
                else if (ent is ProfileParabolaAsymmetric asym)
                {
                    curveCounter++;
                    item.Kind = VerticalEntityKind.AsymmetricParabola;
                    item.Label = $"Asym Curve VC{curveCounter}";
                    item.GradeInPercent = NormalizeSlopeToPercent(asym.GradeIn);
                    item.GradeOutPercent = NormalizeSlopeToPercent(asym.GradeOut);
                    item.AlgebraicGradeDiffPercent = Math.Abs(NormalizeSlopeToPercent(asym.GradeChange));
                    item.Orientation = item.GradeInPercent > item.GradeOutPercent
                        ? VerticalCurveOrientation.Crest
                        : VerticalCurveOrientation.Sag;
                    item.KValue = asym.K;
                    item.PviStation = SafeRead(() => asym.PVIStation);
                    item.PviElevation = SafeRead(() => asym.PVIElevation);
                }
                else if (ent is ProfileCircular circ)
                {
                    curveCounter++;
                    item.Kind = VerticalEntityKind.CircularCurve;
                    item.Label = $"Circular VC{curveCounter}";
                    item.KValue = SafeRead(() => circ.K);
                }
                else
                {
                    continue;
                }

                snap.Entities.Add(item);
            }
        }
        catch
        {
            // Surface profiles (sampled EG) may not expose editable layout entities
        }

        return snap;
    }

    private static double NormalizeSlopeToPercent(double rawValue)
    {
        // Civil 3D .NET API stores slopes/grades as decimal rise/run (e.g., 0.04 = 4%).
        return Math.Abs(rawValue) <= 0.50 ? rawValue * 100.0 : rawValue;
    }

    private static T? SafeRead<T>(Func<T> getter) where T : struct
    {
        try { return getter(); }
        catch { return null; }
    }

    private static T? SafeReadRef<T>(Func<T> getter) where T : class
    {
        try { return getter(); }
        catch { return null; }
    }
}
