using System.Globalization;
using Dlt645Master.Core.Models;

namespace Dlt645Master.Core.Protocol;

/// <summary>
/// 低层、可独立测试的 DL/T645-2007 字节级变换。
/// 只包含纯函数：组帧 / 校验 / 查表等逻辑均不放在这里。
/// </summary>
public static class Dlt645FrameCodec
{
    private const int DataOffset = 0x33;

    /// <summary>
    /// CS = 从第一个 0x68 到（不含）CS 自身之间所有字节的算术和，对 256 取模。
    /// 调用方需自行确保切片范围正是这一段。
    /// </summary>
    public static byte ComputeChecksum(ReadOnlySpan<byte> bytesFromFirst68ToBeforeChecksum)
    {
        int sum = 0;
        foreach (byte b in bytesFromFirst68ToBeforeChecksum)
        {
            sum += b;
        }

        return unchecked((byte)sum);
    }

    /// <summary>
    /// 线上编码规则：每个 DATA 字节以（原始值 + 0x33）的形式传输，
    /// 从而让 0x68/0x16 等控制字节在线上永远不会与数据内容冲突。
    /// </summary>
    public static byte[] Add33H(ReadOnlySpan<byte> rawData)
    {
        byte[] result = new byte[rawData.Length];
        for (int i = 0; i < rawData.Length; i++)
        {
            result[i] = unchecked((byte)(rawData[i] + DataOffset));
        }

        return result;
    }

    /// <summary><see cref="Add33H"/> 的逆操作：把收到的每个 DATA 字节减去 0x33。</summary>
    public static byte[] Sub33H(ReadOnlySpan<byte> transmittedData)
    {
        byte[] result = new byte[transmittedData.Length];
        for (int i = 0; i < transmittedData.Length; i++)
        {
            result[i] = unchecked((byte)(transmittedData[i] - DataOffset));
        }

        return result;
    }

    /// <summary>
    /// DL/T645 传输多字节字段（地址、DI、数据值）时一律低位在前。
    /// 本方法用于在线序与显示/高位在前序之间互相转换（该操作互为逆操作）。
    /// </summary>
    public static byte[] ReverseBytes(ReadOnlySpan<byte> bytes)
    {
        byte[] result = bytes.ToArray();
        Array.Reverse(result);
        return result;
    }

    /// <summary>
    /// 把 BCD 字节（已是显示/高位在前序，高半字节为更高位数字）解码为 decimal 数值，
    /// 从右侧第 <paramref name="decimalPlaces"/> 位插入小数点。
    /// 例：字节 00 00 01 86，decimalPlaces=2 -> 1.86。
    /// </summary>
    public static decimal BcdToDecimal(ReadOnlySpan<byte> bytesDisplayOrder, int decimalPlaces)
    {
        Span<char> digits = stackalloc char[bytesDisplayOrder.Length * 2];
        for (int i = 0; i < bytesDisplayOrder.Length; i++)
        {
            byte b = bytesDisplayOrder[i];
            int high = b >> 4;
            int low = b & 0x0F;
            if (high > 9 || low > 9)
            {
                throw new FormatException($"第 {i} 个字节是非法 BCD 字节 0x{b:X2}。");
            }

            digits[i * 2] = (char)('0' + high);
            digits[i * 2 + 1] = (char)('0' + low);
        }

        int decimalPointIndex = digits.Length - decimalPlaces;
        string intPart = decimalPointIndex > 0 ? new string(digits[..decimalPointIndex]) : "0";
        string fracPart = decimalPlaces > 0 ? new string(digits[Math.Max(decimalPointIndex, 0)..]) : string.Empty;
        string numeric = decimalPlaces > 0 ? $"{intPart}.{fracPart}" : intPart;

        return decimal.Parse(numeric, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// <see cref="BcdToDecimal"/> 的逆操作：把数值编码为 BCD 字节（显示/高位在前序），
    /// 固定输出 <paramref name="byteLength"/> 字节，不足位数在左侧补 0。
    /// </summary>
    public static byte[] DecimalToBcd(decimal value, int byteLength, int decimalPlaces)
    {
        decimal scaled = value;
        for (int i = 0; i < decimalPlaces; i++)
        {
            scaled *= 10m;
        }

        long intValue = (long)Math.Round(scaled, MidpointRounding.AwayFromZero);
        int totalDigits = byteLength * 2;
        string digits = intValue.ToString(CultureInfo.InvariantCulture).PadLeft(totalDigits, '0');
        if (digits.Length > totalDigits)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, $"数值超出 {byteLength} 字节 BCD 可表示的范围。");
        }

        byte[] result = new byte[byteLength];
        for (int i = 0; i < byteLength; i++)
        {
            int high = digits[i * 2] - '0';
            int low = digits[i * 2 + 1] - '0';
            result[i] = (byte)((high << 4) | low);
        }

        return result;
    }

    /// <summary>
    /// 构造一条正常应答帧（控制码 0x91）。与 <see cref="Dlt645Protocol.TryParseResponse"/> 的解码逻辑对称。
    /// </summary>
    /// <param name="address">从站地址，6 字节，显示序（高位在前）。</param>
    /// <param name="dataId">数据标识，4 字节，显示序（高位在前）。</param>
    /// <param name="value">要回填的数值。</param>
    /// <param name="definition">该数据标识对应的目录定义，决定 BCD 字节数与小数位数。</param>
    public static byte[] BuildReadResponse(byte[] address, byte[] dataId, decimal value, DataItemDefinition definition)
    {
        const byte StartByte = 0x68;
        const byte EndByte = 0x16;
        const byte ControlCodeNormalResponse = 0x91;

        if (address.Length != 6)
        {
            throw new ArgumentException("地址必须是 6 字节。", nameof(address));
        }

        if (dataId.Length != 4)
        {
            throw new ArgumentException("数据标识必须是 4 字节。", nameof(dataId));
        }

        byte[] wireAddress = ReverseBytes(address);
        byte[] wireDataId = ReverseBytes(dataId);
        byte[] wireValue = ReverseBytes(DecimalToBcd(value, definition.ByteLength, definition.DecimalPlaces));

        byte[] rawData = new byte[wireDataId.Length + wireValue.Length];
        Array.Copy(wireDataId, rawData, wireDataId.Length);
        Array.Copy(wireValue, 0, rawData, wireDataId.Length, wireValue.Length);

        byte[] data = Add33H(rawData);

        byte[] body = new byte[1 + wireAddress.Length + 1 + 1 + 1 + data.Length];
        int i = 0;
        body[i++] = StartByte;
        Array.Copy(wireAddress, 0, body, i, wireAddress.Length);
        i += wireAddress.Length;
        body[i++] = StartByte;
        body[i++] = ControlCodeNormalResponse;
        body[i++] = (byte)data.Length;
        Array.Copy(data, 0, body, i, data.Length);

        byte checksum = ComputeChecksum(body);

        byte[] frame = new byte[body.Length + 2];
        Array.Copy(body, frame, body.Length);
        frame[^2] = checksum;
        frame[^1] = EndByte;
        return frame;
    }
}
