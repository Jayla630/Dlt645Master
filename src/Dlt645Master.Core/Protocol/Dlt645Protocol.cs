using Dlt645Master.Core.Configuration;
using Dlt645Master.Core.Models;

namespace Dlt645Master.Core.Protocol;

/// <summary>
/// DL/T645-2007「读数据」请求/应答编解码器。只做纯粹的 byte[] &lt;-&gt; 模型逻辑，
/// 不涉及串口或 UI。底层字节级变换见 Dlt645FrameCodec。
/// </summary>
public sealed class Dlt645Protocol : IMeterProtocol
{
    private const byte StartByte = 0x68;
    private const byte EndByte = 0x16;
    private const byte WakeupPreambleByte = 0xFE;

    private const byte ControlCodeReadRequest = 0x11;
    private const byte ControlCodeNormalResponse = 0x91;
    private const byte ControlCodeAbnormalResponse = 0xD1;

    private const int AddressLength = 6;
    private const int DataIdLength = 4;

    /// <summary>所有电表都会响应该地址；用于总线上只挂一台设备时的单表读取。</summary>
    public static readonly byte[] BroadcastAddress = [0xAA, 0xAA, 0xAA, 0xAA, 0xAA, 0xAA];

    public byte[] BuildReadRequest(byte[] address, byte[] dataId)
    {
        if (address.Length != AddressLength)
        {
            throw new ArgumentException($"地址必须是 {AddressLength} 字节。", nameof(address));
        }

        if (dataId.Length != DataIdLength)
        {
            throw new ArgumentException($"数据标识必须是 {DataIdLength} 字节。", nameof(dataId));
        }

        // 地址与数据标识按惯例以高位在前（显示序）书写/录入，但 DL/T645 在线上传输时
        // 所有多字节字段一律低位在前——组帧前需先反转字节序。
        byte[] wireAddress = Dlt645FrameCodec.ReverseBytes(address);
        byte[] wireDataId = Dlt645FrameCodec.ReverseBytes(dataId);

        // 每个 DATA 字节 +0x33 是 DL/T645 的线上编码规则：即使数据内容全为 0，
        // 也能保证 DATA 不会与帧定界符 0x68/0x16 冲突。
        byte[] data = Dlt645FrameCodec.Add33H(wireDataId);

        byte[] body = new byte[1 + AddressLength + 1 + 1 + 1 + data.Length];
        int i = 0;
        body[i++] = StartByte;
        Array.Copy(wireAddress, 0, body, i, AddressLength);
        i += AddressLength;
        body[i++] = StartByte;
        body[i++] = ControlCodeReadRequest;
        body[i++] = (byte)data.Length;
        Array.Copy(data, 0, body, i, data.Length);

        // CS = 从第一个 0x68 到最后一个 DATA 字节的所有字节算术和，对 256 取模
        // （即正好是 `body`）——不包含 CS 自身与末尾的 0x16。
        byte checksum = Dlt645FrameCodec.ComputeChecksum(body);

        byte[] frame = new byte[body.Length + 2];
        Array.Copy(body, frame, body.Length);
        frame[^2] = checksum;
        frame[^1] = EndByte;
        return frame;
    }

    public MeterReadResult TryParseResponse(byte[] frame)
    {
        if (frame is null || frame.Length == 0)
        {
            return MeterReadResult.Failure("空报文");
        }

        // 真正的帧之前可能存在可选的唤醒前导码（重复的 0xFE），需先跳过。
        int start = 0;
        while (start < frame.Length && frame[start] == WakeupPreambleByte)
        {
            start++;
        }

        const int minimumFrameLength = 1 + AddressLength + 1 + 1 + 1 + 1 + 1; // 68 地址(6) 68 C L CS 16，L=0 时的长度
        if (frame.Length - start < minimumFrameLength)
        {
            return MeterReadResult.Failure("报文长度不足");
        }

        if (frame[start] != StartByte)
        {
            return MeterReadResult.Failure($"起始符错误，期望 0x68，实际 0x{frame[start]:X2}");
        }

        byte[] wireAddress = frame[(start + 1)..(start + 1 + AddressLength)];

        int secondStart = start + 1 + AddressLength;
        if (frame[secondStart] != StartByte)
        {
            return MeterReadResult.Failure($"第二个起始符错误，期望 0x68，实际 0x{frame[secondStart]:X2}");
        }

        byte controlCode = frame[secondStart + 1];
        byte length = frame[secondStart + 2];
        int dataStart = secondStart + 3;

        if (frame.Length < dataStart + length + 2)
        {
            return MeterReadResult.Failure("数据域长度声明超出报文实际长度");
        }

        byte[] data = frame[dataStart..(dataStart + length)];
        byte checksumByte = frame[dataStart + length];
        byte endByte = frame[dataStart + length + 1];

        if (endByte != EndByte)
        {
            return MeterReadResult.Failure($"结束符错误，期望 0x16，实际 0x{endByte:X2}");
        }

        // 在信任其他字段之前，先在 [第一个 0x68 .. 最后一个 DATA 字节] 范围内重新计算 CS 并校验——
        // 校验和不匹配意味着整帧（含地址）都可能已损坏，因此失败帧的任何字段都不会出现在结果中。
        byte[] bytesForChecksum = frame[start..(dataStart + length)];
        byte expectedChecksum = Dlt645FrameCodec.ComputeChecksum(bytesForChecksum);
        if (checksumByte != expectedChecksum)
        {
            return MeterReadResult.Failure($"校验和不匹配，期望 0x{expectedChecksum:X2}，实际 0x{checksumByte:X2}");
        }

        byte[] displayAddress = Dlt645FrameCodec.ReverseBytes(wireAddress);

        if (controlCode == ControlCodeAbnormalResponse)
        {
            string errorInfo = data.Length > 0 ? $"0x{data[0]:X2}" : "(empty)";
            return new MeterReadResult
            {
                IsSuccess = false,
                ControlCode = controlCode,
                Address = displayAddress,
                ErrorMessage = $"从站异常应答，错误信息字={errorInfo}",
            };
        }

        try
        {
            return ParseNormalResponseData(controlCode, displayAddress, data);
        }
        catch (FormatException ex)
        {
            return MeterReadResult.Failure($"数据字段解析失败：{ex.Message}");
        }
    }

    private static MeterReadResult ParseNormalResponseData(byte controlCode, byte[] displayAddress, byte[] data)
    {
        byte[] unmasked = Dlt645FrameCodec.Sub33H(data);
        if (unmasked.Length < DataIdLength)
        {
            return MeterReadResult.Failure("数据域不足以包含数据标识");
        }

        byte[] wireDataId = unmasked[..DataIdLength];
        byte[] displayDataId = Dlt645FrameCodec.ReverseBytes(wireDataId);

        DataItemDefinition? definition = DataItemCatalog.Find(displayDataId);
        if (definition is null)
        {
            return new MeterReadResult
            {
                IsSuccess = false,
                ControlCode = controlCode,
                Address = displayAddress,
                DataId = displayDataId,
                ErrorMessage = "未知数据标识，未在 DataItemCatalog 中登记",
            };
        }

        byte[] valueBytes = unmasked[DataIdLength..];
        if (valueBytes.Length != definition.ByteLength)
        {
            return new MeterReadResult
            {
                IsSuccess = false,
                ControlCode = controlCode,
                Address = displayAddress,
                DataId = displayDataId,
                ItemName = definition.Name,
                ErrorMessage = $"数据字节数与 {definition.Name} 定义不符：期望 {definition.ByteLength}，实际 {valueBytes.Length}",
            };
        }

        byte[] displayValueBytes = Dlt645FrameCodec.ReverseBytes(valueBytes);
        decimal value = Dlt645FrameCodec.BcdToDecimal(displayValueBytes, definition.DecimalPlaces);

        return new MeterReadResult
        {
            IsSuccess = true,
            ControlCode = controlCode,
            Address = displayAddress,
            DataId = displayDataId,
            ItemName = definition.Name,
            Value = value,
            Unit = definition.Unit,
        };
    }
}
