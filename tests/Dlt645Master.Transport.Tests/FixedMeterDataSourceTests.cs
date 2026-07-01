using Dlt645Master.Transport.Simulation;
using FluentAssertions;
using Xunit;

namespace Dlt645Master.Transport.Tests;

public class FixedMeterDataSourceTests
{
    [Fact]
    public void TryGetValue_ForwardActiveEnergyDefault_ReturnsZero()
    {
        var dataSource = new FixedMeterDataSource();

        bool found = dataSource.TryGetValue([0x00, 0x01, 0x00, 0x00], out decimal value);

        found.Should().BeTrue();
        value.Should().Be(0.00m);
    }

    [Fact]
    public void TryGetValue_UnknownDataId_ReturnsFalse()
    {
        var dataSource = new FixedMeterDataSource();

        bool found = dataSource.TryGetValue([0xFF, 0xFF, 0xFF, 0xFF], out _);

        found.Should().BeFalse();
    }

    [Fact]
    public void Set_ConfiguresValueForSubsequentTryGetValue()
    {
        var dataSource = new FixedMeterDataSource();
        byte[] dataId = [0x02, 0x01, 0x01, 0x00];

        FixedMeterDataSource result = dataSource.Set(dataId, 220.5m);
        bool found = dataSource.TryGetValue(dataId, out decimal value);

        result.Should().BeSameAs(dataSource);
        found.Should().BeTrue();
        value.Should().Be(220.5m);
    }

    [Fact]
    public void Set_CalledTwiceForSameDataId_OverwritesPreviousValue()
    {
        var dataSource = new FixedMeterDataSource();
        byte[] dataId = [0x00, 0x01, 0x00, 0x00];

        dataSource.Set(dataId, 1.23m).Set(dataId, 4.56m);
        dataSource.TryGetValue(dataId, out decimal value);

        value.Should().Be(4.56m);
    }
}
