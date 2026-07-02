using Dlt645Master.Core.Transport;

namespace Dlt645Master.App.Tests.Fakes;

/// <summary>可控 <see cref="ITransport"/> 替身：IsOpen 由 Open/Close 维护，可模拟 Open 抛异常。</summary>
public sealed class FakeTransport : ITransport
{
    public bool IsOpen { get; private set; }

    public int OpenCount { get; private set; }

    public int CloseCount { get; private set; }

    /// <summary>为 true 时 <see cref="Open"/> 抛异常，用于验证连接命令的异常兜底。</summary>
    public bool ThrowOnOpen { get; set; }

    public void Open()
    {
        OpenCount++;
        if (ThrowOnOpen)
        {
            throw new InvalidOperationException("模拟打开传输失败");
        }

        IsOpen = true;
    }

    public void Close()
    {
        CloseCount++;
        IsOpen = false;
    }

    public void Send(byte[] frame)
    {
    }

    public byte[]? ReceiveFrame(TimeSpan timeout) => null;

    public void Dispose() => IsOpen = false;
}
