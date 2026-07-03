using Dlt645Master.Core.Configuration;
using Dlt645Master.Core.Models;
using FluentAssertions;
using Xunit;

namespace Dlt645Master.Core.Tests;

public class DataItemCatalogTests
{
    [Fact]
    public void Find_KnownDataId_ReturnsMatchingDefinition()
    {
        byte[] dataId = [0x00, 0x01, 0x00, 0x00];

        var found = DataItemCatalog.Find(dataId);

        found.Should().NotBeNull();
        found!.Name.Should().Be("正向有功总电能");
        found.ByteLength.Should().Be(4);
        found.DecimalPlaces.Should().Be(2);
        found.Unit.Should().Be("kWh");
        found.Category.Should().Be(DataItemCategory.Energy);
    }

    [Fact]
    public void All_Definitions_CarryExpectedCategories()
    {
        // 类别是仿真波动标定与界面卡片分组的单一来源，映射错一处两边全错，逐项锁死。
        DataItemCatalog.ForwardActiveEnergy.Category.Should().Be(DataItemCategory.Energy);
        DataItemCatalog.ReverseActiveEnergy.Category.Should().Be(DataItemCategory.Energy);
        DataItemCatalog.VoltagePhaseA.Category.Should().Be(DataItemCategory.Voltage);
        DataItemCatalog.VoltagePhaseB.Category.Should().Be(DataItemCategory.Voltage);
        DataItemCatalog.VoltagePhaseC.Category.Should().Be(DataItemCategory.Voltage);
        DataItemCatalog.CurrentPhaseA.Category.Should().Be(DataItemCategory.Current);
        DataItemCatalog.CurrentPhaseB.Category.Should().Be(DataItemCategory.Current);
        DataItemCatalog.CurrentPhaseC.Category.Should().Be(DataItemCategory.Current);
        DataItemCatalog.TotalActivePower.Category.Should().Be(DataItemCategory.ActivePower);
        DataItemCatalog.TotalPowerFactor.Category.Should().Be(DataItemCategory.PowerFactor);
        DataItemCatalog.GridFrequency.Category.Should().Be(DataItemCategory.Frequency);
    }

    [Fact]
    public void Find_UnknownDataId_ReturnsNull()
    {
        byte[] dataId = [0xFF, 0xFF, 0xFF, 0xFF];

        var found = DataItemCatalog.Find(dataId);

        found.Should().BeNull();
    }
}
