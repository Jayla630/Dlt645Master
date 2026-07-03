using Dlt645Master.Core.Configuration;
using Dlt645Master.Transport.Simulation;
using FluentAssertions;
using Xunit;

namespace Dlt645Master.Transport.Tests;

public class FluctuatingMeterDataSourceTests
{
    /// <summary>
    /// 固定种子构造被测数据源：基准电压 220.0 V、正向电能 12.34 kWh、A 相电流 1.234 A、
    /// 有功功率 0.8123 kW、功率因数 0.96、电网频率 50.00 Hz（与标定表基准一致）。
    /// </summary>
    private static FluctuatingMeterDataSource CreateDataSource(int seed = 12345)
    {
        var baseline = new FixedMeterDataSource()
            .Set(DataItemCatalog.VoltagePhaseA.DataId, 220.0m)
            .Set(DataItemCatalog.ForwardActiveEnergy.DataId, 12.34m)
            .Set(DataItemCatalog.CurrentPhaseA.DataId, 1.234m)
            .Set(DataItemCatalog.TotalActivePower.DataId, 0.8123m)
            .Set(DataItemCatalog.TotalPowerFactor.DataId, 0.96m)
            .Set(DataItemCatalog.GridFrequency.DataId, 50.00m);
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

    // ---- slice-06 标定表值域钳制（固定种子长序列断言，1000 次覆盖十分钟走查量级）----

    [Fact]
    public void TryGetValue_Frequency_StaysWithinCalibratedRange()
    {
        FluctuatingMeterDataSource dataSource = CreateDataSource();
        byte[] dataId = DataItemCatalog.GridFrequency.DataId;

        for (int i = 0; i < 1000; i++)
        {
            dataSource.TryGetValue(dataId, out decimal value).Should().BeTrue();
            value.Should().BeInRange(49.90m, 50.10m, "电网频率标定钳制值域为 [49.90, 50.10] Hz");
        }
    }

    [Fact]
    public void TryGetValue_PowerFactor_StaysWithinCalibratedRange()
    {
        FluctuatingMeterDataSource dataSource = CreateDataSource();
        byte[] dataId = DataItemCatalog.TotalPowerFactor.DataId;

        for (int i = 0; i < 1000; i++)
        {
            dataSource.TryGetValue(dataId, out decimal value).Should().BeTrue();
            value.Should().BeInRange(0.93m, 0.99m, "功率因数标定钳制值域为 [0.93, 0.99]");
        }
    }

    [Fact]
    public void TryGetValue_Voltage_StaysWithinCalibratedRangeAndPositive()
    {
        FluctuatingMeterDataSource dataSource = CreateDataSource();
        byte[] dataId = DataItemCatalog.VoltagePhaseA.DataId;

        for (int i = 0; i < 1000; i++)
        {
            dataSource.TryGetValue(dataId, out decimal value).Should().BeTrue();
            value.Should().BeGreaterThan(0m);
            value.Should().BeInRange(209m, 235m, "电压标定钳制值域为 [209, 235] V（220±7% 内）");
        }
    }

    [Fact]
    public void TryGetValue_ActivePower_StaysPositive()
    {
        FluctuatingMeterDataSource dataSource = CreateDataSource();
        byte[] dataId = DataItemCatalog.TotalActivePower.DataId;

        for (int i = 0; i < 1000; i++)
        {
            dataSource.TryGetValue(dataId, out decimal value).Should().BeTrue();
            value.Should().BeGreaterThan(0m, "有功功率恒大于 0");
        }
    }

    [Fact]
    public void TryGetValue_Energy_IsMonotonicOverLongSequence()
    {
        FluctuatingMeterDataSource dataSource = CreateDataSource();
        byte[] dataId = DataItemCatalog.ForwardActiveEnergy.DataId;

        dataSource.TryGetValue(dataId, out decimal previous);
        for (int i = 0; i < 1000; i++)
        {
            dataSource.TryGetValue(dataId, out decimal value);
            value.Should().BeGreaterThanOrEqualTo(previous, "电能单调不减");
            previous = value;
        }
    }

    [Fact]
    public void TryGetValue_NarrowRangeItems_KeepChangingBetweenReads()
    {
        // 确认频率/功率因数没有被钳制成贴边常数：相邻读取存在变化。
        FluctuatingMeterDataSource dataSource = CreateDataSource();

        foreach (byte[] dataId in new[] { DataItemCatalog.GridFrequency.DataId, DataItemCatalog.TotalPowerFactor.DataId })
        {
            for (int i = 0; i < 100; i++)
            {
                dataSource.TryGetValue(dataId, out decimal first);
                dataSource.TryGetValue(dataId, out decimal second);
                second.Should().NotBe(first, "随机游走必须保证连续两次读取值不同（确认没有被钳成常数）");
            }
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
