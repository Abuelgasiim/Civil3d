using System.IO;
using System.Reflection;
using Autodesk.AutoCAD.DatabaseServices;
using RoadwayDesignChecker.Civil3D.Adapters;
using RoadwayDesignChecker.Core.Checks;
using RoadwayDesignChecker.Core.Criteria;
using RoadwayDesignChecker.Core.Models;

namespace RoadwayDesignChecker.Civil3D.Services;

/// <summary>
/// High-level coordinator used by the WPF UI to list alignments, manage active standards/overrides,
/// execute roadway audits, zoom to findings, and export review sheets.
/// </summary>
public sealed class AuditWorkflowService
{
    private readonly CivilDrawingAdapter _adapter = new();
    private readonly DrawingNavigator _navigator = new();
    private readonly RoadwayAuditEngine _engine = new();
    private readonly StandardRepository _repository;

    public AuditWorkflowService()
    {
        var dllDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? AppContext.BaseDirectory;
        var standardsDir = Path.Combine(dllDir, "Standards");
        _repository = new StandardRepository(standardsDir);
    }

    public IReadOnlyList<DesignStandardProfile> LoadBuiltInStandards()
    {
        return _repository.LoadBuiltInStandards();
    }

    public DesignStandardProfile LoadCivil3DCriteriaXml(string xmlPath, DesignStandardProfile fallbackBase)
    {
        return CivilCriteriaXmlParser.ParseFile(xmlPath, fallbackBase);
    }

    public DesignRuleOverrideSet? LoadOverrideJson(string jsonPath)
    {
        return _repository.LoadOverrideFile(jsonPath);
    }

    public void SaveOverrideJson(string jsonPath, DesignRuleOverrideSet overrides)
    {
        _repository.SaveOverrideFile(jsonPath, overrides);
    }

    public IReadOnlyList<AlignmentListItem> GetAlignments() => _adapter.GetAlignmentsInActiveDrawing();

    public ObjectId? PickAlignment() => _adapter.PickAlignmentInDrawing();

    public AuditReport RunAudit(
        ObjectId alignmentId,
        ObjectId? profileId,
        DesignStandardProfile baseStandard,
        DesignRuleOverrideSet? overrides,
        AuditOptions options)
    {
        var (alignmentSnap, profileSnap) = _adapter.ExtractSnapshots(alignmentId, profileId);
        var effectiveStandard = StandardRepository.ApplyOverrides(baseStandard, overrides);
        return _engine.RunAudit(alignmentSnap, profileSnap, effectiveStandard, options);
    }

    public bool ZoomToFinding(ObjectId alignmentId, DesignCheckResult finding)
    {
        var span = Math.Max(60.0, Math.Abs(finding.EndStation - finding.StartStation));
        return _navigator.ZoomToStation(alignmentId, finding.FocusStation, span);
    }

    public void ExportReport(string filePath, AuditReport report, string format, bool includePassed)
    {
        var content = format.ToLowerInvariant() switch
        {
            "csv" => ReportExporter.ToCsv(report, includePassed),
            "json" => ReportExporter.ToJson(report),
            _ => ReportExporter.ToHumanReadableHtml(report, includePassed)
        };

        File.WriteAllText(filePath, content);
    }
}
