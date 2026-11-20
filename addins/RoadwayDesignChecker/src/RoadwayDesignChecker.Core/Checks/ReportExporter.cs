using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using RoadwayDesignChecker.Core.Criteria;
using RoadwayDesignChecker.Core.Models;

namespace RoadwayDesignChecker.Core.Checks;

/// <summary>
/// Exports human-readable QA/QC review sheets (HTML), spreadsheets (CSV), and structured data (JSON).
/// </summary>
public static class ReportExporter
{
    public static string ToCsv(AuditReport report, bool includePassed = true)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Status,Category,Headline,Entity,Location,DesignSpeedKph,YourDrawing,StandardRequires,Difference,WhyItMatters,HowToFix,TechnicalReference,EngineerNote");

        foreach (var r in report.Results)
        {
            if (!includePassed && r.Severity == CheckSeverity.Pass)
                continue;

            sb.AppendLine(string.Join(",",
                CsvEscape(r.StatusLabel),
                CsvEscape(r.CategoryLabel),
                CsvEscape(r.Headline),
                CsvEscape(r.EntityLabel),
                CsvEscape(r.LocationText),
                r.DesignSpeedKph.ToString("0.#", CultureInfo.InvariantCulture),
                CsvEscape(r.YourDrawingText),
                CsvEscape(r.StandardRequiresText),
                CsvEscape(r.DifferenceText),
                CsvEscape(r.WhyItMatters),
                CsvEscape(r.HowToFix),
                CsvEscape(r.TechnicalReference),
                CsvEscape(r.EngineerNote)));
        }

        return sb.ToString();
    }

    public static string ToJson(AuditReport report)
    {
        return JsonSerializer.Serialize(report, StandardRepository.JsonOptions);
    }

    public static string ToHumanReadableHtml(AuditReport report, bool includePassed = true)
    {
        var items = includePassed
            ? report.Results
            : report.Results.Where(r => r.Severity != CheckSeverity.Pass).ToList();

        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html><html lang=\"en\"><head><meta charset=\"utf-8\"/>");
        sb.AppendLine($"<title>Roadway Design Review - {Html(report.AlignmentName)}</title>");
        sb.AppendLine("<style>");
        sb.AppendLine("body{font-family:'Segoe UI',Arial,sans-serif;background:#f8fafc;color:#0f172a;margin:0;padding:24px;line-height:1.5}");
        sb.AppendLine(".wrap{max-width:1040px;margin:0 auto}");
        sb.AppendLine(".header{background:#ffffff;border:1px solid #e2e8f0;border-radius:12px;padding:20px;margin-bottom:20px}");
        sb.AppendLine(".kpis{display:flex;gap:12px;margin-top:14px;flex-wrap:wrap}");
        sb.AppendLine(".kpi{padding:10px 16px;border-radius:8px;border:1px solid #e2e8f0;background:#f8fafc;font-weight:700}");
        sb.AppendLine(".card{background:#ffffff;border:1px solid #e2e8f0;border-left:5px solid #22c55e;border-radius:10px;padding:16px;margin-bottom:14px}");
        sb.AppendLine(".card.fail{border-left-color:#ef4444}.card.warn{border-left-color:#f59e0b}");
        sb.AppendLine(".badge{display:inline-block;padding:2px 10px;border-radius:999px;font-size:12px;font-weight:700}");
        sb.AppendLine(".badge.fail{background:#fee2e2;color:#b91c1c}.badge.warn{background:#fef3c7;color:#b45309}.badge.pass{background:#dcfce7;color:#15803d}");
        sb.AppendLine(".grid{display:grid;grid-template-columns:repeat(3,1fr);gap:12px;background:#f8fafc;border:1px solid #e2e8f0;border-radius:8px;padding:10px 12px;margin:10px 0}");
        sb.AppendLine(".lbl{font-size:11px;color:#64748b;text-transform:uppercase;font-weight:600}.val{font-size:14px;font-weight:700}");
        sb.AppendLine(".box{background:#f8fafc;border-radius:8px;padding:10px 12px;margin-top:8px;font-size:13px}");
        sb.AppendLine(".ref{font-size:11px;color:#64748b;margin-top:8px;font-family:Consolas,monospace}");
        sb.AppendLine("</style></head><body><div class=\"wrap\">");

        sb.AppendLine("<div class=\"header\">");
        sb.AppendLine($"<h1 style=\"margin:0 0 6px 0;font-size:22px\">Roadway Design Review Sheet — {Html(report.AlignmentName)}</h1>");
        sb.AppendLine($"<div style=\"color:#475569;font-size:13px\">Standard: <strong>{Html(report.StandardDisplayName)}</strong> · Profile: <strong>{Html(report.ProfileName ?? "(Horizontal Only)")}</strong> · Generated {report.GeneratedAtUtc:yyyy-MM-dd HH:mm} UTC</div>");
        sb.AppendLine($"<p style=\"margin:12px 0 0 0;font-weight:600\">{Html(report.PlainEnglishSummary)}</p>");
        sb.AppendLine("<div class=\"kpis\">");
        sb.AppendLine($"<div class=\"kpi\" style=\"color:#b91c1c\">Needs Fix: {report.FailCount}</div>");
        sb.AppendLine($"<div class=\"kpi\" style=\"color:#b45309\">Advisory: {report.WarningCount}</div>");
        sb.AppendLine($"<div class=\"kpi\" style=\"color:#15803d\">Passed: {report.PassCount}</div>");
        sb.AppendLine($"<div class=\"kpi\">Total Checks: {report.TotalCount}</div>");
        sb.AppendLine("</div></div>");

        foreach (var r in items)
        {
            var cls = r.Severity == CheckSeverity.Fail ? "fail" : r.Severity == CheckSeverity.Warning ? "warn" : "pass";
            sb.AppendLine($"<div class=\"card {cls}\">");
            sb.AppendLine($"<div><span class=\"badge {cls}\">{Html(r.StatusLabel)}</span> <strong style=\"margin-left:8px;color:#475569;font-size:13px\">{Html(r.CategoryLabel)}</strong></div>");
            sb.AppendLine($"<h2 style=\"margin:8px 0 2px 0;font-size:16px\">{Html(r.Headline)}</h2>");
            sb.AppendLine($"<div style=\"font-size:12px;color:#64748b\">{Html(r.LocationText)} · Design Speed: {r.DesignSpeedKph:0} km/h</div>");
            sb.AppendLine("<div class=\"grid\">");
            sb.AppendLine($"<div><div class=\"lbl\">Your Drawing Has</div><div class=\"val\">{Html(r.YourDrawingText)}</div></div>");
            sb.AppendLine($"<div><div class=\"lbl\">Standard Requires</div><div class=\"val\">{Html(r.StandardRequiresText)}</div></div>");
            sb.AppendLine($"<div><div class=\"lbl\">Result</div><div class=\"val\">{Html(r.DifferenceText)}</div></div>");
            sb.AppendLine("</div>");
            sb.AppendLine($"<div class=\"box\"><strong>Why This Matters:</strong> {Html(r.WhyItMatters)}</div>");
            sb.AppendLine($"<div class=\"box\"><strong>How to Fix It:</strong> {Html(r.HowToFix)}</div>");
            if (!string.IsNullOrWhiteSpace(r.EngineerNote))
                sb.AppendLine($"<div class=\"box\" style=\"background:#eff6ff\"><strong>Engineer Note:</strong> {Html(r.EngineerNote)}</div>");
            sb.AppendLine($"<div class=\"ref\">{Html(r.TechnicalReference)}</div>");
            sb.AppendLine("</div>");
        }

        sb.AppendLine("</div></body></html>");
        return sb.ToString();
    }

    private static string CsvEscape(string? value)
    {
        var text = value ?? string.Empty;
        if (text.Contains(',') || text.Contains('"') || text.Contains('\n') || text.Contains('\r'))
            return $"\"{text.Replace("\"", "\"\"")}\"";
        return text;
    }

    private static string Html(string? text) => WebUtility.HtmlEncode(text ?? string.Empty);
}
