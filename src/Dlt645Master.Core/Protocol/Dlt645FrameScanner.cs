namespace Dlt645Master.Core.Protocol;

/// <summary>
/// 增量组帧器：喂入陆续到达的字节，按 DL/T645 帧边界切出完整帧。
/// 自动跳过前导唤醒字节 FE 与帧头前的垃圾字节，遇畸形帧丢一字节重同步。
/// </summary>
public sealed class Dlt645FrameScanner
{
    private const byte StartByte = 0x68;
    private const byte EndByte = 0x16;
    private const int AddressLength = 6;

    // 0x68 + 地址(6) + 0x68 + C(1) + L(1) = 10 字节，凑够这些才能读出 L 并算出整帧长度。
    private const int HeaderLength = 1 + AddressLength + 1 + 1 + 1;
    private const int SecondStartByteIndex = 1 + AddressLength;
    private const int LengthByteIndex = 1 + AddressLength + 2;

    private readonly List<byte> _buffer = [];

    /// <summary>追加收到的字节。</summary>
    public void Append(ReadOnlySpan<byte> data) => _buffer.AddRange(data);

    /// <summary>尝试取出一个完整帧；缓冲区里凑不齐则返回 false。</summary>
    public bool TryReadFrame(out byte[] frame)
    {
        while (true)
        {
            // 丢弃第一个 0x68 之前的所有字节（含 FE 唤醒前导与任何垃圾字节）。
            int startIndex = _buffer.IndexOf(StartByte);
            if (startIndex < 0)
            {
                _buffer.Clear();
                frame = [];
                return false;
            }

            if (startIndex > 0)
            {
                _buffer.RemoveRange(0, startIndex);
            }

            if (_buffer.Count < HeaderLength)
            {
                frame = [];
                return false;
            }

            if (_buffer[SecondStartByteIndex] != StartByte)
            {
                // 畸形帧：当前 0x68 不是真正的帧头，丢一字节后从下一个 0x68 重新同步。
                _buffer.RemoveAt(0);
                continue;
            }

            byte length = _buffer[LengthByteIndex];
            int totalFrameLength = HeaderLength + length + 1 + 1; // + CS(1) + 结束符(1)，即 12 + L

            if (_buffer.Count < totalFrameLength)
            {
                frame = [];
                return false;
            }

            if (_buffer[totalFrameLength - 1] != EndByte)
            {
                // 结束符对不上，同样视为畸形帧，丢一字节重同步。
                _buffer.RemoveAt(0);
                continue;
            }

            frame = _buffer.GetRange(0, totalFrameLength).ToArray();
            _buffer.RemoveRange(0, totalFrameLength);
            return true;
        }
    }

    /// <summary>清空内部缓冲。</summary>
    public void Reset() => _buffer.Clear();
}
