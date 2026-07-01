using Dlt645Master.Core.Protocol;
using FluentAssertions;
using Xunit;

namespace Dlt645Master.Core.Tests;

public class Dlt645FrameCodecTests
{
    [Fact]
    public void ComputeChecksum_ForRequestFrameBody_Returns0xAE()
    {
        byte[] bytesBeforeChecksum =
        [
            0x68, 0xAA, 0xAA, 0xAA, 0xAA, 0xAA, 0xAA, 0x68, 0x11, 0x04, 0x33, 0x33, 0x34, 0x33
        ];

        byte checksum = Dlt645FrameCodec.ComputeChecksum(bytesBeforeChecksum);

        checksum.Should().Be(0xAE);
    }

    [Fact]
    public void ComputeChecksum_ForResponseFrameBody_Returns0xE7()
    {
        byte[] bytesBeforeChecksum =
        [
            0x68, 0x01, 0x72, 0x00, 0x72, 0x00, 0x00, 0x68, 0x91, 0x08,
            0x33, 0x33, 0x34, 0x33, 0x33, 0x33, 0x33, 0x33
        ];

        byte checksum = Dlt645FrameCodec.ComputeChecksum(bytesBeforeChecksum);

        checksum.Should().Be(0xE7);
    }

    [Fact]
    public void Add33H_AddsOffsetToEachByte()
    {
        byte[] rawDataId = [0x00, 0x00, 0x01, 0x00];

        byte[] transmitted = Dlt645FrameCodec.Add33H(rawDataId);

        transmitted.Should().Equal(0x33, 0x33, 0x34, 0x33);
    }

    [Fact]
    public void Sub33H_SubtractsOffsetFromEachByte()
    {
        byte[] transmitted = [0x33, 0x33, 0x34, 0x33];

        byte[] rawDataId = Dlt645FrameCodec.Sub33H(transmitted);

        rawDataId.Should().Equal(0x00, 0x00, 0x01, 0x00);
    }

    [Fact]
    public void ReverseBytes_ReversesByteOrder()
    {
        byte[] wireOrder = [0x01, 0x02, 0x03, 0x04];

        byte[] displayOrder = Dlt645FrameCodec.ReverseBytes(wireOrder);

        displayOrder.Should().Equal(0x04, 0x03, 0x02, 0x01);
    }

    [Fact]
    public void BcdToDecimal_ConvertsDisplayOrderBcdBytesToDecimalValue()
    {
        byte[] displayOrderBytes = [0x00, 0x00, 0x01, 0x86];

        decimal value = Dlt645FrameCodec.BcdToDecimal(displayOrderBytes, decimalPlaces: 2);

        value.Should().Be(1.86m);
    }
}
