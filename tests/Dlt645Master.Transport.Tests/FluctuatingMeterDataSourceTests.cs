using Dlt645Master.Core.Configuration;
using Dlt645Master.Transport.Simulation;
using FluentAssertions;
using Xunit;

namespace Dlt645Master.Transport.Tests;

public class FluctuatingMeterDataSourceTests
{
    /// <summary>固定种子构造被测数据源：基准电压 220.0 V、基准正向电能 12.34 kWh、基准 A 相电流 1.234 A。</summary>
    private static FluctuatingMeterDataSource CreateDataSource(int seed = 12345)
    {
        var baseline = new FixedMeterDataSource()
            .Set(DataItemCatalog.VoltagePhaseA.DataId, 220.0m)
            .Set(DataItemCatalog.ForwardActiveEnergy.DataId, 12.34m)
            .Set(DataItemCatalog.CurrentPhaseA.DataId, 1.234m);
        return new FluctuatingMeterDataSource(baseline, seed);
    }

    [Fact]
    public void TryGetValue_Voltage_StaysWithinTenPercentBandAndPositive()
    {
        FluctuatingMeterDataSource dataSource = CreateDataSource();
        byte[] dataId = DataItemCatalog.VoltagePhaseA.DataId;

        for (int i = 0; i < 500; i++)
        {
            dataSource.TryGetValue(dataId, out decimal value).Should().BeTrue();
            value.Should().BeGreaterThan(0m);
            value.Should().BeInRange(220m * 0.9m, 220m * 1.1m, "电压波动约 ±2%，必须落在 220±10% 的宽验收带内");
        }
    }

    [Fact]
    public void TryGetValue_Energy_IsMonotonicallyNonDecreasing()
    {
        FluctuatingMeterDataSource dataSource = CreateDataSource();
        byte[] dataId = DataItemCatalog.ForwardActiveEnergy.DataId;

        dataSource.TryGetValue(dataId, out decimal previous).Should().BeTrue();
        previous.Should().BeGreaterThan(12.34m, "电能走字从基准值起步累加");

        for (int i = 0; i < 200; i++)
        {
            dataSource.TryGetValue(dataId, out decimal value);
            value.Should().BeGreaterThan(previous, "电能只增不减且每次都在走字");
            previous = value;
        }
    }

    [Fact]
    public void TryGetValue_ConsecutiveReads_ProduceDifferentValues()
    {
        FluctuatingMeterDataSource dataSource = CreateDataSource();
        byte[] dataId = DataItemCatalog.VoltagePhaseA.DataId;

        for (int i = 0; i < 100; i++)
        {
            dataSource.TryGetValue(dataId, out decimal first);
            dataSource.TryGetValue(dataId, out decimal second);
            second.Should().NotBe(first, "随机游走必须保证连续两次读取值不同（确认在动）");
        }
    }

    [Fact]
    public void TryGetValue_Current_StaysWithinFivePercentBand()
    {
        FluctuatingMeterDataSource dataSource = CreateDataSource();
        byte[] dataId = DataItemCatalog.CurrentPhaseA.DataId;

        for (int i = 0; i < 500; i++)
        {
            dataSource.TryGetValue(dataId, out decimal value);
            // 带边界 ±5%，再放宽一个最小分辨率（0.001）容纳「贴边强制挪一格」的越界。
            value.Should().BeInRange(1.234m * 0.95m - 0.001m, 1.234m * 1.05m + 0.001m);
        }
    }

    [Fact]
    public void TryGetValue_UnknownDataId_ReturnsFalse()
    {
        FluctuatingMeterDataSource dataSource = CreateDataSource();

        bool found = dataSource.TryGetValue([0xFF, 0xFF, 0xFF, 0xFF], out _);

        found.Should().BeFalse();
    }

    [Fact]
    public void TryGetValue_SameSeed_ProducesReproducibleSequence()
    {
        FluctuatingMeterDataSource first = CreateDataSource(seed: 42);
        FluctuatingMeterDataSource second = CreateDataSource(seed: 42);
        byte[] dataId = DataItemCatalog.VoltagePhaseA.DataId;

        for (int i = 0; i < 50; i++)
        {
            first.TryGetValue(dataId, out decimal a);
            second.TryGetValue(dataId, out decimal b);
            a.Should().Be(b, "相同种子的波动序列必须可复现");
        }
    }
}
