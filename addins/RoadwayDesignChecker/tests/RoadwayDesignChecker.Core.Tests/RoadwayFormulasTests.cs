using RoadwayDesignChecker.Core.Checks;
using RoadwayDesignChecker.Core.Models;
using Xunit;

namespace RoadwayDesignChecker.Core.Tests;

public sealed class RoadwayFormulasTests
{
    [Fact]
    public void Shc301_StoppingSightDistance_Level_MatchesExpectedValues()
    {
        // SHC 301 Eq. 4-1: V=100 km/h, t=2.5s, a=3.4 m/s^2, G=0% => 183.09 m (design 185 m)
        var ssd100 = RoadwayFormulas.ComputeStoppingSightDistanceM(100, 0.0, 2.5, 3.4);
        Assert.InRange(ssd100, 182.5, 185.0);

        // Downgrade increases stopping distance
        var ssdDown = RoadwayFormulas.ComputeStoppingSightDistanceM(100, -4.0, 2.5, 3.4);
        Assert.True(ssdDown > ssd100);
    }

    [Fact]
    public void Shc301_SuperelevationRunoff_MatchesWorkedExampleInSection4662()
    {
        // SHC 301 p.107 worked example: V=80 km/h (r=0.50%), w=7.3 m, e=10% => L = 73.0 m
        var runoff = RoadwayFormulas.ComputeMinSuperelevationRunoffLengthM(
            superelevationPercent: 10.0,
            rotatedWidthM: 7.3,
            maxRunoffSlopePercent: 0.50);

        Assert.Equal(73.0, runoff, precision: 2);
    }

    [Fact]
    public void HorizontalSightClearance_RoundTripsWithAvailableSightDistance()
    {
        var reqM = RoadwayFormulas.ComputeRequiredMiddleOrdinateM(radiusM: 440.0, sightDistanceM: 185.0);
        var backS = RoadwayFormulas.ComputeAvailableHorizontalSightDistanceM(radiusM: 440.0, lateralClearanceM: reqM);

        Assert.Equal(185.0, backS, precision: 2);
    }

    [Fact]
    public void StationFormatter_FormatsMetricAndImperialCorrectly()
    {
        Assert.Equal("0+480.00", StationFormatter.Format(480.0, UnitSystem.Metric));
        Assert.Equal("1+250.50", StationFormatter.Format(1250.5, UnitSystem.Metric));
        Assert.Equal("4+80.00", StationFormatter.Format(480.0, UnitSystem.Imperial));
    }
}
