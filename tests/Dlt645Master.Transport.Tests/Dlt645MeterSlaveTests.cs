using Dlt645Master.Core.Configuration;
using Dlt645Master.Core.Protocol;
using Dlt645Master.Transport.Simulation;
using FluentAssertions;
using Xunit;

namespace Dlt645Master.Transport.Tests;

public class Dlt645MeterSlaveTests
{
    private readonly Dlt645Protocol _protocol = new();

    // 从站地址（显示序，高位在前）。ReverseBytes 后即为应答向量中的地址段 01 72 00 72 00 00（线序）。
    private static readonly byte[] SlaveDisplayAddress = Dlt645FrameCodec.ReverseBytes([0x01, 0x72, 0x00, 0x72, 0x00, 0x00]);

    [Fact]
    public void HandleRequest_ForwardActiveEnergyGoldenVector_MatchesKnownGoodResponse()
    {
        var dataSource = new FixedMeterDataSource().Set(DataItemCatalog.ForwardActiveEnergy.DataId, 0.00m);
        var slave = new Dlt645MeterSlave(SlaveDisplayAddress, dataSource);
        byte[] request = [0x68, 0xAA, 0xAA, 0xAA, 0xAA, 0xAA, 0xAA, 0x68, 0x11, 0x04, 0x33, 0x33, 0x34, 0x33, 0xAE, 0x16];

        byte[]? response = slave.HandleRequest(request);

        response.Should().Equal(
            0x68, 0x01, 0x72, 0x00, 0x72, 0x00, 0x00, 0x68, 0x91, 0x08,
            0x33, 0x33, 0x34, 0x33, 0x33, 0x33, 0x33, 0x33, 0xE7, 0x16);
    }

    [Fact]
    public void HandleRequest_AddressNeitherBroadcastNorOwn_ReturnsNull()
    {
        var slave = new Dlt645MeterSlave(SlaveDisplayAddress, new FixedMeterDataSource());
        byte[] otherDisplayAddress = Dlt645FrameCodec.ReverseBytes([0x02, 0x72, 0x00, 0x72, 0x00, 0x00]);
        byte[] request = _protocol.BuildReadRequest(otherDisplayAddress, DataItemCatalog.ForwardActiveEnergy.DataId);

        byte[]? response = slave.HandleRequest(request);

        response.Should().BeNull();
    }

    [Fact]
    public void HandleRequest_UnknownDataId_ReturnsAbnormalResponseWithControlCode0xD1()
    {
        var slave = new Dlt645MeterSlave(SlaveDisplayAddress, new FixedMeterDataSource());
        byte[] unknownDataId = [0xFF, 0xFF, 0xFF, 0xFF];
        byte[] request = _protocol.BuildReadRequest(Dlt645Protocol.BroadcastAddress, unknownDataId);

        byte[]? response = slave.HandleRequest(request);

        // 帧布局：0x68, 地址(6, 线序), 0x68, C, L, DATA..., CS, 0x16
        response.Should().NotBeNull();
        response![0].Should().Be(0x68);
        response[1..7].Should().Equal(0x01, 0x72, 0x00, 0x72, 0x00, 0x00);
        response[7].Should().Be(0x68);
        response[8].Should().Be(0xD1);
        response[^1].Should().Be(0x16);
    }

    [Fact]
    public void HandleRequest_CorruptedChecksum_ReturnsNull()
    {
        var slave = new Dlt645MeterSlave(SlaveDisplayAddress, new FixedMeterDataSource());
        byte[] request = _protocol.BuildReadRequest(Dlt645Protocol.BroadcastAddress, DataItemCatalog.ForwardActiveEnergy.DataId);
        request[^2] ^= 0xFF; // 破坏校验和字节（位于结束符 0x16 之前）

        byte[]? response = slave.HandleRequest(request);

        response.Should().BeNull();
    }
}
