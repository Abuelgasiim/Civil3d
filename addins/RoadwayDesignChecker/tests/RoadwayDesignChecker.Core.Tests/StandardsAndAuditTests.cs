using System.Xml.Linq;
using RoadwayDesignChecker.Core.Checks;
using RoadwayDesignChecker.Core.Criteria;
using RoadwayDesignChecker.Core.Models;
using Xunit;

namespace RoadwayDesignChecker.Core.Tests;

public sealed class StandardsAndAuditTests
{
    [Fact]
    public void StandardRepository_LoadsOfficialShc301AndAashto2018()
    {
        var repo = new StandardRepository();
        var standards = repo.LoadBuiltInStandards();

        var shc = standards.Single(s => s.Id == "shc-301-metric");
        var aashto = standards.Single(s => s.Id == "aashto-2018-metric");

        // Verify SHC 301 140 km/h row from Tables 4-14, 4-15, 4-17, 4-18, 4-20
        var row140 = CriteriaResolver.ResolveRow(shc, 140);
        Assert.Equal(1105, row140.MinRadiusByEmax["6"]);
        Assert.Equal(965, row140.MinRadiusByEmax["8"]);
        Assert.Equal(78, row140.MinSpiralLengthM);
        Assert.Equal(3.0, row140.MaxAllowableGradePercent);
        Assert.Equal(161, row140.MinCrestKSsd);
        Assert.Equal(84, row140.MinSagK);
        Assert.Equal(0.34, row140.MaxRunoffSlopePercent);

        Assert.Contains(aashto.SpeedTable, r => r.DesignSpeedKph == 100);
    }

    [Fact]
    public void CivilCriteriaXmlParser_OverridesMinimumRadiusAndKValues()
    {
        var repo = new StandardRepository();
        var baseStd = repo.LoadBuiltInStandards().First();

        var xml = XDocument.Parse("""
            <RoadwayDesignStandards>
              <Alignments>
                <MinimumRadiusTable name="Custom Metric eMax 6%">
                  <MinimumRadius speed="100" radius="460" />
                </MinimumRadiusTable>
              </Alignments>
              <Profiles>
                <StoppingSightDistance>
                  <MinimumK speed="100" kValue="58" />
                </StoppingSightDistance>
              </Profiles>
            </RoadwayDesignStandards>
            """);

        var parsed = CivilCriteriaXmlParser.ParseDocument(xml, "TestXml", baseStd);
        var row100 = CriteriaResolver.ResolveRow(parsed, 100);

        Assert.Equal(460, row100.MinRadiusByEmax["6"]);
        Assert.Equal(58, row100.MinCrestKSsd);
    }

    [Fact]
    public void RoadwayAuditEngine_FlagsSharpCurveAndShortCrestCurveInPlainEnglish()
    {
        var repo = new StandardRepository();
        var shc = repo.LoadBuiltInStandards().Single(s => s.Id == "shc-301-metric");

        var alignment = new AlignmentSnapshot
        {
            Name = "HWY-40_Mainline",
            StartStation = 0,
            EndStation = 1000,
            Length = 1000,
            DesignSpeeds = { new DesignSpeedEntry { StartStation = 0, SpeedKph = 100 } },
            Segments =
            {
                new HorizontalSegmentSnapshot { Index = 1, Label = "Curve C1", Kind = HorizontalSegmentKind.Arc, StartStation = 100, EndStation = 300, Length = 200, Radius = 680, IsClockwise = true },
                new HorizontalSegmentSnapshot { Index = 2, Label = "Tangent T2", Kind = HorizontalSegmentKind.Line, StartStation = 300, EndStation = 425, Length = 125 },
                new HorizontalSegmentSnapshot { Index = 3, Label = "Curve C2", Kind = HorizontalSegmentKind.Arc, StartStation = 425, EndStation = 600, Length = 175, Radius = 360, IsClockwise = true }
            }
        };

        var profile = new ProfileSnapshot
        {
            Name = "FG_Mainline",
            AlignmentName = alignment.Name,
            StartStation = 0,
            EndStation = 1000,
            Entities =
            {
                new VerticalEntitySnapshot
                {
                    Index = 1,
                    Label = "Vertical Curve VC1",
                    Kind = VerticalEntityKind.SymmetricParabola,
                    Orientation = VerticalCurveOrientation.Crest,
                    StartStation = 240,
                    EndStation = 440,
                    Length = 200,
                    GradeInPercent = 4.2,
                    GradeOutPercent = -4.2,
                    AlgebraicGradeDiffPercent = 8.4,
                    KValue = 23.8
                },
                new VerticalEntitySnapshot
                {
                    Index = 2,
                    Label = "Grade G2",
                    Kind = VerticalEntityKind.Tangent,
                    StartStation = 440,
                    EndStation = 700,
                    Length = 260,
                    GradePercent = -6.0
                }
            }
        };

        var engine = new RoadwayAuditEngine();
        var report = engine.RunAudit(alignment, profile, shc, new AuditOptions
        {
            RoadwayClass = RoadwayClassification.FreewayExpressway,
            MaxSuperelevationPercent = 6.0,
            AvailableLateralClearanceM = 4.5
        });

        Assert.Contains(report.Results, r => r.CheckCode == "H-RADIUS" && r.EntityLabel == "Curve C2" && r.Severity == CheckSeverity.Fail);
        Assert.Contains(report.Results, r => r.CheckCode == "H-RADIUS" && r.EntityLabel == "Curve C1" && r.Severity == CheckSeverity.Pass);
        Assert.Contains(report.Results, r => r.CheckCode == "H-BROKENBACK" && r.EntityLabel == "Tangent T2" && r.Severity == CheckSeverity.Warning);
        Assert.Contains(report.Results, r => r.CheckCode == "V-K-CREST" && r.EntityLabel == "Vertical Curve VC1" && r.Severity == CheckSeverity.Fail);
        Assert.Contains(report.Results, r => r.CheckCode == "V-GRADE-MAX" && r.EntityLabel == "Grade G2" && r.Severity == CheckSeverity.Fail);

        // Verify plain-English HTML export produces valid content
        var html = ReportExporter.ToHumanReadableHtml(report);
        Assert.Contains("Needs Fix", html);
        Assert.Contains("Why This Matters", html);
        Assert.Contains("How to Fix It", html);
    }
}
