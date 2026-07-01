using Dlt645Master.Core.Configuration;
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
    }

    [Fact]
    public void Find_UnknownDataId_ReturnsNull()
    {
        byte[] dataId = [0xFF, 0xFF, 0xFF, 0xFF];

        var found = DataItemCatalog.Find(dataId);

        found.Should().BeNull();
    }
}
