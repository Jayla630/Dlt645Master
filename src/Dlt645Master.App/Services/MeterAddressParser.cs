using System.Globalization;

namespace Dlt645Master.App.Services;

/// <summary>
/// 电表地址文本解析：把 12 位十六进制字符串（显示序，高位在前）解析为 6 字节地址。
/// 允许夹带空白字符（如 <c>00 00 72 00 72 01</c>）。非法输入返回 false，由调用方拒绝并提示——
/// 绝不吞掉错误默默回退到默认地址。
/// </summary>
public static class MeterAddressParser
{
    private const int AddressByteLength = 6;

    public static bool TryParse(string? text, out byte[] address)
    {
        address = [];
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        string compact = new string(text.Where(c => !char.IsWhiteSpace(c)).ToArray());
        if (compact.Length != AddressByteLength * 2)
        {
            return false;
        }

        byte[] result = new byte[AddressByteLength];
        for (int i = 0; i < AddressByteLength; i++)
        {
            if (!byte.TryParse(compact.AsSpan(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out result[i]))
            {
                return false;
            }
        }

        address = result;
        return true;
    }
}
