using Dlt645Master.Core.Protocol;
using FluentAssertions;
using Xunit;

namespace Dlt645Master.Core.Tests;

public class MeterProtocolRegistryTests
{
    [Fact]
    public void Resolve_Dlt645ProtocolName_ReturnsDlt645Protocol()
    {
        IMeterProtocol protocol = MeterProtocolRegistry.Resolve("DLT645-2007");

        protocol.Should().BeOfType<Dlt645Protocol>();
    }

    [Fact]
    public void Resolve_UnknownProtocolName_Throws()
    {
        Action act = () => MeterProtocolRegistry.Resolve("UNKNOWN-PROTOCOL");

        act.Should().Throw<KeyNotFoundException>();
    }
}
