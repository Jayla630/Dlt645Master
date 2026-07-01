using Dlt645Master.Transport.Serial;
using FluentAssertions;
using Xunit;

namespace Dlt645Master.Transport.Tests;

// SerialPortTransport 依赖真实硬件，无法在 CI 环境完整单测；
// 这里仅验证未打开时的状态与参数默认值，不触碰真实串口。
public class SerialPortTransportTests
{
    [Fact]
    public void Constructor_BeforeOpen_IsOpenIsFalse()
    {
        using var transport = new SerialPortTransport(new SerialPortSettings());

        transport.IsOpen.Should().BeFalse();
    }

    [Fact]
    public void SerialPortSettings_Defaults_MatchDlt645CommonValues()
    {
        var settings = new SerialPortSettings();

        settings.PortName.Should().Be("COM1");
        settings.BaudRate.Should().Be(2400);
        settings.DataBits.Should().Be(8);
        settings.StopBits.Should().Be(System.IO.Ports.StopBits.One);
        settings.Parity.Should().Be(System.IO.Ports.Parity.Even);
        settings.ReadTimeout.Should().Be(TimeSpan.FromMilliseconds(500));
        settings.WakeupByteCount.Should().Be(0);
    }
}
