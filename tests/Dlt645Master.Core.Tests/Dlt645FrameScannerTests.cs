using Dlt645Master.Core.Protocol;
using FluentAssertions;
using Xunit;

namespace Dlt645Master.Core.Tests;

public class Dlt645FrameScannerTests
{
    private static readonly byte[] SingleFrame =
    [
        0x68, 0xAA, 0xAA, 0xAA, 0xAA, 0xAA, 0xAA, 0x68, 0x11, 0x04, 0x33, 0x33, 0x34, 0x33, 0xAE, 0x16,
    ];

    [Fact]
    public void TryReadFrame_SingleFrameFedAtOnce_ReturnsFrameByteForByte()
    {
        var scanner = new Dlt645FrameScanner();

        scanner.Append(SingleFrame);
        bool result = scanner.TryReadFrame(out byte[] frame);

        result.Should().BeTrue();
        frame.Should().Equal(SingleFrame);
    }

    [Fact]
    public void TryReadFrame_FedInTwoChunks_OnlySucceedsAfterSecondChunk()
    {
        var scanner = new Dlt645FrameScanner();
        scanner.Append(SingleFrame.AsSpan(0, 8));

        bool firstAttempt = scanner.TryReadFrame(out _);

        firstAttempt.Should().BeFalse();

        scanner.Append(SingleFrame.AsSpan(8));
        bool secondAttempt = scanner.TryReadFrame(out byte[] frame);

        secondAttempt.Should().BeTrue();
        frame.Should().Equal(SingleFrame);
    }

    [Fact]
    public void TryReadFrame_WithLeadingWakeupPreamble_StripsPreambleAndReturnsFrame()
    {
        var scanner = new Dlt645FrameScanner();
        byte[] withPreamble = [0xFE, 0xFE, 0xFE, 0xFE, .. SingleFrame];

        scanner.Append(withPreamble);
        bool result = scanner.TryReadFrame(out byte[] frame);

        result.Should().BeTrue();
        frame.Should().Equal(SingleFrame);
    }

    [Fact]
    public void TryReadFrame_WithLeadingGarbageBytes_ResynchronizesAndReturnsFrame()
    {
        var scanner = new Dlt645FrameScanner();
        byte[] withGarbage = [0x00, 0x01, 0x02, .. SingleFrame];

        scanner.Append(withGarbage);
        bool result = scanner.TryReadFrame(out byte[] frame);

        result.Should().BeTrue();
        frame.Should().Equal(SingleFrame);
    }

    [Fact]
    public void TryReadFrame_MalformedFrameMissingSecondStartByte_ResynchronizesAndReadsNextValidFrame()
    {
        var scanner = new Dlt645FrameScanner();
        // 伪帧：0x68 开头，但第 8 字节（index 7）不是第二个 0x68 —— 应丢一字节重同步，
        // 紧随其后的合法帧仍应被正确取出。
        byte[] malformedThenValid = [0x68, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x00, .. SingleFrame];

        scanner.Append(malformedThenValid);
        bool result = scanner.TryReadFrame(out byte[] frame);

        result.Should().BeTrue();
        frame.Should().Equal(SingleFrame);
    }

    [Fact]
    public void TryReadFrame_BackToBackTwoFrames_ReturnsBothOnConsecutiveCalls()
    {
        var scanner = new Dlt645FrameScanner();
        scanner.Append([.. SingleFrame, .. SingleFrame]);

        bool first = scanner.TryReadFrame(out byte[] firstFrame);
        bool second = scanner.TryReadFrame(out byte[] secondFrame);
        bool third = scanner.TryReadFrame(out _);

        first.Should().BeTrue();
        firstFrame.Should().Equal(SingleFrame);
        second.Should().BeTrue();
        secondFrame.Should().Equal(SingleFrame);
        third.Should().BeFalse();
    }
}
