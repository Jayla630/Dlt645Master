namespace Dlt645Master.App.Services;

/// <summary>
/// 一条读数记录（不可变）：读数记录缓冲与 CSV 导出的行单元。
/// 失败读数（超时/校验和错）同样成记录——名称/DI 可能为空串，<see cref="ErrorMessage"/> 带失败原因。
/// </summary>
/// <param name="Timestamp">读取完成时刻。</param>
/// <param name="ItemName">数据项中文名；无法确定时为空串。</param>
/// <param name="DataIdText">DI 的显示序十六进制串（如 <c>02 01 01 00</c>）；无 DI 时为空串。</param>
/// <param name="Value">数值；失败时为 null。</param>
/// <param name="Unit">单位；无单位或未知时为空串。</param>
/// <param name="IsSuccess">该次读取是否成功。</param>
/// <param name="ErrorMessage">失败原因；成功时为空串。</param>
public sealed record ReadingRecord(
    DateTimeOffset Timestamp,
    string ItemName,
    string DataIdText,
    decimal? Value,
    string Unit,
    bool IsSuccess,
    string ErrorMessage);
