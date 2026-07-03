using System.Globalization;
using Dlt645Master.App.Converters;
using FluentAssertions;
using Xunit;

namespace Dlt645Master.App.Tests;

public class ConvertersTests
{
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    // ---- VoltageToRatioConverter：对 250V 上限的占比 ----

    [Theory]
    [InlineData(220.0, 0.88)]
    [InlineData(250.0, 1.0)]
    [InlineData(300.0, 1.0)]
    public void VoltageToRatio_ClampsIntoUnitInterval(double voltage, double expected)
    {
        var converter = new VoltageToRatioConverter();

        object ratio = converter.Convert((decimal)voltage, typeof(double), null, Culture);

        ((double)ratio).Should().BeApproximately(expected, 1e-9);
    }

    [Fact]
    public void VoltageToRatio_NullValue_ReturnsZero()
    {
        var converter = new VoltageToRatioConverter();

        converter.Convert(null, typeof(double), null, Culture).Should().Be(0d);
    }

    // ---- VoltageLevelConverter：着色档位 ----

    [Theory]
    [InlineData(220.0, "Normal")]
    [InlineData(239.9, "Normal")]
    [InlineData(240.0, "High")]
    [InlineData(250.0, "High")]
    [InlineData(250.1, "Over")]
    public void VoltageLevel_MapsThresholds(double voltage, string expected)
    {
        var converter = new VoltageLevelConverter();

        converter.Convert((decimal)voltage, typeof(string), null, Culture).Should().Be(expected);
    }
}
