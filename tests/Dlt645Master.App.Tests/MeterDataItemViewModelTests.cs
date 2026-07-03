using Dlt645Master.App.ViewModels;
using Dlt645Master.Core.Configuration;
using Dlt645Master.Core.Models;
using FluentAssertions;
using Xunit;

namespace Dlt645Master.App.Tests;

public class MeterDataItemViewModelTests
{
    private static readonly DateTimeOffset Timestamp = new(2026, 7, 2, 12, 0, 0, TimeSpan.FromHours(8));

    private static MeterReadResult Success(byte[] dataId, decimal value) => new()
    {
        IsSuccess = true,
        DataId = dataId,
        Value = value,
        Unit = DataItemCatalog.Find(dataId)?.Unit,
        ItemName = DataItemCatalog.Find(dataId)?.Name,
    };

    private static MeterReadResult Failure(byte[] dataId) => new()
    {
        IsSuccess = false,
        DataId = dataId,
        ErrorMessage = "仿真失败",
    };

    // ---- 涨跌趋势 ----

    [Fact]
    public void Trend_FirstUpdate_IsFlat()
    {
        var item = new MeterDataItemViewModel(DataItemCatalog.VoltagePhaseA.DataId, "A 相电压");

        item.Update(Success(DataItemCatalog.VoltagePhaseA.DataId, 220.0m), Timestamp);

        item.Trend.Should().Be(ValueTrend.Flat, "首次读取没有可比较的上一次值");
    }

    [Theory]
    [InlineData(221.0, ValueTrend.Up)]
    [InlineData(219.0, ValueTrend.Down)]
    [InlineData(220.0, ValueTrend.Flat)]
    public void Trend_SecondUpdate_ComparesAgainstPreviousValue(double next, ValueTrend expected)
    {
        var item = new MeterDataItemViewModel(DataItemCatalog.VoltagePhaseA.DataId, "A 相电压");
        item.Update(Success(DataItemCatalog.VoltagePhaseA.DataId, 220.0m), Timestamp);

        item.Update(Success(DataItemCatalog.VoltagePhaseA.DataId, (decimal)next), Timestamp);

        item.Trend.Should().Be(expected);
    }

    [Fact]
    public void Trend_EnergyCategory_StaysFlatEvenWhenIncreasing()
    {
        var item = new MeterDataItemViewModel(DataItemCatalog.ForwardActiveEnergy.DataId, "正向有功总电能");
        item.Update(Success(DataItemCatalog.ForwardActiveEnergy.DataId, 12.34m), Timestamp);

        item.Update(Success(DataItemCatalog.ForwardActiveEnergy.DataId, 12.36m), Timestamp);

        item.Trend.Should().Be(ValueTrend.Flat, "电能恒升，箭头是视觉噪音，刻意不显示");
    }

    [Fact]
    public void Trend_AfterFailure_ResetsToFlatOnNextSuccess()
    {
        byte[] dataId = DataItemCatalog.VoltagePhaseA.DataId;
        var item = new MeterDataItemViewModel(dataId, "A 相电压");
        item.Update(Success(dataId, 220.0m), Timestamp);
        item.Update(Failure(dataId), Timestamp);

        item.Update(Success(dataId, 221.0m), Timestamp);

        item.Trend.Should().Be(ValueTrend.Flat, "失败后没有可信的上一次成功值，不做涨跌比较");
    }

    // ---- 分组（与 DataItemCategory 同源）----

    [Theory]
    [InlineData(new byte[] { 0x00, 0x01, 0x00, 0x00 }, "电能", 0)]
    [InlineData(new byte[] { 0x02, 0x01, 0x01, 0x00 }, "电压", 1)]
    [InlineData(new byte[] { 0x02, 0x02, 0x02, 0x00 }, "电流", 2)]
    [InlineData(new byte[] { 0x02, 0x03, 0x00, 0x00 }, "功率与频率", 3)]
    [InlineData(new byte[] { 0x02, 0x06, 0x00, 0x00 }, "功率与频率", 3)]
    [InlineData(new byte[] { 0x02, 0x80, 0x00, 0x07 }, "功率与频率", 3)]
    [InlineData(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF }, "其他", 4)]
    public void Group_MapsCategoryToFourGroups(byte[] dataId, string groupName, int groupOrder)
    {
        var item = new MeterDataItemViewModel(dataId, "条目");

        item.GroupName.Should().Be(groupName);
        item.GroupOrder.Should().Be(groupOrder);
    }

    [Fact]
    public void SortOrder_FollowsCatalogOrder_UnknownGoesLast()
    {
        var energy = new MeterDataItemViewModel(DataItemCatalog.ForwardActiveEnergy.DataId, "电能");
        var frequency = new MeterDataItemViewModel(DataItemCatalog.GridFrequency.DataId, "频率");
        var unknown = new MeterDataItemViewModel([0xFF, 0xFF, 0xFF, 0xFF], "未知");

        energy.SortOrder.Should().BeLessThan(frequency.SortOrder);
        unknown.SortOrder.Should().Be(int.MaxValue);
    }

    // ---- 色带令牌 ----

    [Theory]
    [InlineData(new byte[] { 0x02, 0x01, 0x01, 0x00 }, "PhaseA")]
    [InlineData(new byte[] { 0x02, 0x01, 0x02, 0x00 }, "PhaseB")]
    [InlineData(new byte[] { 0x02, 0x01, 0x03, 0x00 }, "PhaseC")]
    [InlineData(new byte[] { 0x00, 0x02, 0x00, 0x00 }, "Energy")]
    [InlineData(new byte[] { 0x02, 0x02, 0x01, 0x00 }, "Current")]
    [InlineData(new byte[] { 0x02, 0x03, 0x00, 0x00 }, "Power")]
    [InlineData(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF }, "Neutral")]
    public void AccentToken_MapsPhaseColorsAndGroupColors(byte[] dataId, string expected)
    {
        var item = new MeterDataItemViewModel(dataId, "条目");

        item.AccentToken.Should().Be(expected);
    }

    [Fact]
    public void IsVoltage_TrueOnlyForVoltageItems()
    {
        new MeterDataItemViewModel(DataItemCatalog.VoltagePhaseB.DataId, "B 相电压").IsVoltage.Should().BeTrue();
        new MeterDataItemViewModel(DataItemCatalog.CurrentPhaseA.DataId, "A 相电流").IsVoltage.Should().BeFalse();
    }
}
