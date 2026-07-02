namespace Dlt645Master.App.Configuration;

/// <summary>App 层的默认取值集中处。</summary>
public static class AppDefaults
{
    /// <summary>
    /// 仿真从站默认地址（显示序，高位在前，12 位十六进制）。
    /// 必须与 Transport 测试中的从站地址一致——<c>ReverseBytes([01 72 00 72 00 00]) == [00 00 72 00 72 01]</c>，
    /// 否则仿真模式下一条也读不到。同时作为视图模型 <c>MeterAddressText</c> 的默认值。
    /// </summary>
    public const string SimulatedMeterAddress = "000072007201";
}
