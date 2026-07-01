using Dlt645Master.Core.Configuration;
using Dlt645Master.Core.Models;
using Dlt645Master.Core.Protocol;
using Dlt645Master.Transport.Simulation;
using FluentAssertions;
using Xunit;

namespace Dlt645Master.Transport.Tests;

public class LoopbackTransportRoundTripTests
{
    private readonly Dlt645Protocol _protocol = new();

    // 从站地址（显示序，高位在前），任意选取，不影响判定逻辑。
    private static readonly byte[] SlaveDisplayAddress = Dlt645FrameCodec.ReverseBytes([0x01, 0x72, 0x00, 0x72, 0x00, 0x00]);

    [Fact]
    public void RoundTrip_ForwardActiveEnergyViaBroadcast_ParsesToSuccessfulResult()
    {
        var dataSource = new FixedMeterDataSource().Set(DataItemCatalog.ForwardActiveEnergy.DataId, 0.00m);
        var slave = new Dlt645MeterSlave(SlaveDisplayAddress, dataSource);
        using var transport = new LoopbackTransport(slave);

        byte[] request = _protocol.BuildReadRequest(Dlt645Protocol.BroadcastAddress, DataItemCatalog.ForwardActiveEnergy.DataId);
        transport.Send(request);
        byte[]? responseFrame = transport.ReceiveFrame(TimeSpan.FromMilliseconds(200));

        responseFrame.Should().NotBeNull();
        MeterReadResult result = _protocol.TryParseResponse(responseFrame!);

        result.IsSuccess.Should().BeTrue();
        result.ControlCode.Should().Be(0x91);
        result.Value.Should().Be(0.00m);
        result.ItemName.Should().Be(DataItemCatalog.ForwardActiveEnergy.Name);
        result.Unit.Should().Be(DataItemCatalog.ForwardActiveEnergy.Unit);
    }

    [Fact]
    public void RoundTrip_UnknownDataId_ParsesToAbnormalResponseResult()
    {
        var slave = new Dlt645MeterSlave(SlaveDisplayAddress, new FixedMeterDataSource());
        using var transport = new LoopbackTransport(slave);
        byte[] unknownDataId = [0xFF, 0xFF, 0xFF, 0xFF];

        byte[] request = _protocol.BuildReadRequest(Dlt645Protocol.BroadcastAddress, unknownDataId);
        transport.Send(request);
        byte[]? responseFrame = transport.ReceiveFrame(TimeSpan.FromMilliseconds(200));

        responseFrame.Should().NotBeNull();
        MeterReadResult result = _protocol.TryParseResponse(responseFrame!);

        result.IsSuccess.Should().BeFalse();
        result.ControlCode.Should().Be(0xD1);
    }

    [Fact]
    public void RoundTrip_AddressMismatch_ReceiveFrameTimesOutAndReturnsNull()
    {
        var slave = new Dlt645MeterSlave(SlaveDisplayAddress, new FixedMeterDataSource());
        using var transport = new LoopbackTransport(slave);
        byte[] otherDisplayAddress = Dlt645FrameCodec.ReverseBytes([0x02, 0x72, 0x00, 0x72, 0x00, 0x00]);

        byte[] request = _protocol.BuildReadRequest(otherDisplayAddress, DataItemCatalog.ForwardActiveEnergy.DataId);
        transport.Send(request);
        byte[]? responseFrame = transport.ReceiveFrame(TimeSpan.FromMilliseconds(50));

        responseFrame.Should().BeNull();
    }
}
