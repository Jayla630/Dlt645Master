using Dlt645Master.Core.Configuration;
using Dlt645Master.Core.Protocol;

namespace Dlt645Master.Transport.Simulation;

/// <summary>DL/T645-2007 仿真从站：解析读请求帧，按数据源查值并构造应答帧。</summary>
public sealed class Dlt645MeterSlave : IMeterSlave
{
    private const byte StartByte = 0x68;
    private const byte EndByte = 0x16;
    private const byte ControlCodeReadRequest = 0x11;
    private const byte ControlCodeAbnormalResponse = 0xD1;
    private const int AddressLength = 6;
    private const int DataIdLength = 4;

    // DL/T645-2007 的异常应答错误信息字是按位表示多种错误的位图，本刀未完整建模其含义；
    // 这里固定使用占位值，只用于让「未知数据标识」分支产生一个可被验证的 0xD1 应答。
    private const byte ErrorInfoUnknownDataId = 0x02;

    private static readonly byte[] BroadcastAddressWire = [0xAA, 0xAA, 0xAA, 0xAA, 0xAA, 0xAA];

    private readonly byte[] _wireAddress;
    private readonly IMeterDataSource _dataSource;

    /// <param name="address">本从站地址，6 字节，显示序（高位在前），与 IMeterProtocol 的地址约定一致。</param>
    /// <param name="dataSource">按数据标识提供当前值的仿真数据源。</param>
    public Dlt645MeterSlave(byte[] address, IMeterDataSource dataSource)
    {
        if (address.Length != AddressLength)
        {
            throw new ArgumentException($"地址必须是 {AddressLength} 字节。", nameof(address));
        }

        _wireAddress = Dlt645FrameCodec.ReverseBytes(address);
        _dataSource = dataSource;
    }

    public byte[]? HandleRequest(byte[] requestFrame)
    {
        if (!TryParseRequest(requestFrame, out byte[] wireAddress, out byte controlCode, out byte[] data))
        {
            return null;
        }

        bool isBroadcast = wireAddress.SequenceEqual(BroadcastAddressWire);
        bool isOwnAddress = wireAddress.SequenceEqual(_wireAddress);
        if (!isBroadcast && !isOwnAddress)
        {
            return null;
        }

        if (controlCode != ControlCodeReadRequest)
        {
            return null;
        }

        byte[] unmasked = Dlt645FrameCodec.Sub33H(data);
        if (unmasked.Length < DataIdLength)
        {
            return null;
        }

        byte[] displayDataId = Dlt645FrameCodec.ReverseBytes(unmasked[..DataIdLength]);

        if (_dataSource.TryGetValue(displayDataId, out decimal value) && DataItemCatalog.Find(displayDataId) is { } definition)
        {
            byte[] displayAddress = Dlt645FrameCodec.ReverseBytes(_wireAddress);
            return Dlt645FrameCodec.BuildReadResponse(displayAddress, displayDataId, value, definition);
        }

        return BuildAbnormalResponse();
    }

    private static bool TryParseRequest(byte[] frame, out byte[] wireAddress, out byte controlCode, out byte[] data)
    {
        wireAddress = [];
        controlCode = 0;
        data = [];

        const int minimumFrameLength = 1 + AddressLength + 1 + 1 + 1 + 1 + 1; // 68 地址(6) 68 C L CS 16，L=0 时的最短长度
        if (frame.Length < minimumFrameLength || frame[0] != StartByte || frame[1 + AddressLength] != StartByte)
        {
            return false;
        }

        byte length = frame[1 + AddressLength + 2];
        int dataStart = 1 + AddressLength + 3;
        if (frame.Length != dataStart + length + 2 || frame[^1] != EndByte)
        {
            return false;
        }

        byte[] bytesForChecksum = frame[..(dataStart + length)];
        byte expectedChecksum = Dlt645FrameCodec.ComputeChecksum(bytesForChecksum);
        if (frame[dataStart + length] != expectedChecksum)
        {
            return false;
        }

        wireAddress = frame[1..(1 + AddressLength)];
        controlCode = frame[1 + AddressLength + 1];
        data = frame[dataStart..(dataStart + length)];
        return true;
    }

    private byte[] BuildAbnormalResponse()
    {
        byte[] data = Dlt645FrameCodec.Add33H([ErrorInfoUnknownDataId]);

        byte[] body = new byte[1 + AddressLength + 1 + 1 + 1 + data.Length];
        int i = 0;
        body[i++] = StartByte;
        Array.Copy(_wireAddress, 0, body, i, AddressLength);
        i += AddressLength;
        body[i++] = StartByte;
        body[i++] = ControlCodeAbnormalResponse;
        body[i++] = (byte)data.Length;
        Array.Copy(data, 0, body, i, data.Length);

        byte checksum = Dlt645FrameCodec.ComputeChecksum(body);

        byte[] frame = new byte[body.Length + 2];
        Array.Copy(body, frame, body.Length);
        frame[^2] = checksum;
        frame[^1] = EndByte;
        return frame;
    }
}
