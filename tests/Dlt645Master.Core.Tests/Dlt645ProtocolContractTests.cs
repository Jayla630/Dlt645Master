using Dlt645Master.Core.Configuration;
using Dlt645Master.Core.Models;
using Dlt645Master.Core.Protocol;
using FluentAssertions;
using Xunit;

namespace Dlt645Master.Core.Tests;

public class Dlt645ProtocolContractTests
{
    private readonly Dlt645Protocol _protocol = new();

    [Fact]
    public void BuildReadRequest_BroadcastForwardActiveEnergy_MatchesKnownGoodFrame()
    {
        byte[] frame = _protocol.BuildReadRequest(Dlt645Protocol.BroadcastAddress, DataItemCatalog.ForwardActiveEnergy.DataId);

        frame.Should().Equal(0x68, 0xAA, 0xAA, 0xAA, 0xAA, 0xAA, 0xAA, 0x68, 0x11, 0x04, 0x33, 0x33, 0x34, 0x33, 0xAE, 0x16);
    }

    [Fact]
    public void TryParseResponse_ForwardActiveEnergyResponse_DecodesNameValueUnitAndControlCode()
    {
        byte[] frame =
        [
            0x68, 0x01, 0x72, 0x00, 0x72, 0x00, 0x00, 0x68, 0x91, 0x08,
            0x33, 0x33, 0x34, 0x33, 0x33, 0x33, 0x33, 0x33, 0xE7, 0x16,
        ];

        MeterReadResult result = _protocol.TryParseResponse(frame);

        result.IsSuccess.Should().BeTrue();
        result.ItemName.Should().Be("正向有功总电能");
        result.Value.Should().Be(0.00m);
        result.Unit.Should().Be("kWh");
        result.ControlCode.Should().Be(0x91);
    }

    [Fact]
    public void TryParseResponse_CorruptedChecksum_IsRejected()
    {
        byte[] frame =
        [
            0x68, 0x01, 0x72, 0x00, 0x72, 0x00, 0x00, 0x68, 0x91, 0x08,
            0x33, 0x33, 0x34, 0x33, 0x33, 0x33, 0x33, 0x33, 0xE8, 0x16, // CS corrupted: E7 -> E8
        ];

        MeterReadResult result = _protocol.TryParseResponse(frame);

        result.IsSuccess.Should().BeFalse();
    }
}
