using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using RoadwayDesignChecker.Core.Models;

namespace RoadwayDesignChecker.Core.Criteria;

/// <summary>
/// Parses Autodesk Civil 3D Design Criteria XML files (e.g.
/// "Autodesk Civil 3D Metric (2011)/(2018) Roadway Design Standards.xml")
/// and merges parsed tables onto a base DesignStandardProfile.
/// </summary>
public static partial class CivilCriteriaXmlParser
{
    public static DesignStandardProfile ParseFile(string xmlFilePath, DesignStandardProfile fallbackBase)
    {
        if (!File.Exists(xmlFilePath))
            throw new FileNotFoundException("Civil 3D Design Criteria XML file was not found.", xmlFilePath);

        var doc = XDocument.Load(xmlFilePath);
        return ParseDocument(doc, Path.GetFileNameWithoutExtension(xmlFilePath), fallbackBase);
    }

    public static DesignStandardProfile ParseDocument(
        XDocument doc,
        string sourceName,
        DesignStandardProfile fallbackBase)
    {
        // Clone base profile so fields not present in Civil 3D XML (such as grades or broken-back rules) remain populated.
        var profile = StandardRepository.CloneProfile(fallbackBase);
        profile.Id = $"xml:{sourceName}";
        profile.DisplayName = $"Civil 3D Criteria XML ({sourceName})";
        profile.ReferenceCode = sourceName;

        var rowsBySpeed = profile.SpeedTable.ToDictionary(r => (int)Math.Round(r.DesignSpeedKph));

        // 1. Parse <MinimumRadiusTable name="... eMax 6%"> / <MinimumRadius speed="100" radius="437" />
        foreach (var table in doc.Descendants().Where(e => e.Name.LocalName.Equals("MinimumRadiusTable", StringComparison.OrdinalIgnoreCase)))
        {
            var tableName = table.Attribute("name")?.Value ?? string.Empty;
            var emaxKey = ExtractEmaxKey(tableName);
            if (string.IsNullOrEmpty(emaxKey))
                continue;

            foreach (var entry in table.Elements().Where(e => e.Name.LocalName.Equals("MinimumRadius", StringComparison.OrdinalIgnoreCase)))
            {
                if (!TryReadDouble(entry, out var speed, "speed", "designSpeed") ||
                    !TryReadDouble(entry, out var radius, "radius", "value", "minRadius"))
                    continue;

                var row = GetOrCreateRow(rowsBySpeed, speed);
                row.MinRadiusByEmax[emaxKey] = radius;
            }
        }

        // 2. Parse Crest SSD / PSD and Sag HSD MinimumK tables
        ParseKTable(doc, "StoppingSightDistance", rowsBySpeed, (row, k) => row.MinCrestKSsd = k);
        ParseKTable(doc, "PassingSightDistance", rowsBySpeed, (row, k) => row.MinCrestKPsdMethod1 = k);
        ParseKTable(doc, "HeadlightSightDistance", rowsBySpeed, (row, k) => row.MinSagK = k);

        profile.SpeedTable = rowsBySpeed.Values.OrderBy(r => r.DesignSpeedKph).ToList();
        return profile;
    }

    private static void ParseKTable(
        XDocument doc,
        string containerElementName,
        Dictionary<int, SpeedCriteriaRow> rowsBySpeed,
        Action<SpeedCriteriaRow, double> assign)
    {
        foreach (var container in doc.Descendants().Where(e => e.Name.LocalName.Equals(containerElementName, StringComparison.OrdinalIgnoreCase)))
        {
            foreach (var entry in container.Descendants().Where(e =>
                         e.Name.LocalName.Contains("MinimumK", StringComparison.OrdinalIgnoreCase) ||
                         e.Name.LocalName.Equals("KValue", StringComparison.OrdinalIgnoreCase) ||
                         e.Name.LocalName.Equals("Entry", StringComparison.OrdinalIgnoreCase)))
            {
                if (!TryReadDouble(entry, out var speed, "speed", "designSpeed") ||
                    !TryReadDouble(entry, out var kVal, "kValue", "k", "value", "minK"))
                    continue;

                var row = GetOrCreateRow(rowsBySpeed, speed);
                assign(row, kVal);
            }
        }
    }

    private static SpeedCriteriaRow GetOrCreateRow(Dictionary<int, SpeedCriteriaRow> map, double speed)
    {
        var key = (int)Math.Round(speed);
        if (!map.TryGetValue(key, out var row))
        {
            row = new SpeedCriteriaRow
            {
                DesignSpeedKph = speed,
                MinVerticalCurveLengthM = Math.Round(0.6 * speed, 1)
            };
            map[key] = row;
        }

        return row;
    }

    private static string? ExtractEmaxKey(string tableName)
    {
        var match = EmaxRegex().Match(tableName);
        if (!match.Success)
            return null;

        if (double.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var pct))
            return ((int)Math.Round(pct)).ToString(CultureInfo.InvariantCulture);

        return null;
    }

    private static bool TryReadDouble(XElement el, out double value, params string[] attrNames)
    {
        foreach (var name in attrNames)
        {
            var attr = el.Attributes().FirstOrDefault(a => a.Name.LocalName.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (attr != null && double.TryParse(attr.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                return true;
        }

        value = 0;
        return false;
    }

    [GeneratedRegex(@"(\d+(?:\.\d+)?)\s*%", RegexOptions.Compiled)]
    private static partial Regex EmaxRegex();
}
