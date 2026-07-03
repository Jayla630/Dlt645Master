using System.Globalization;
using Dlt645Master.App.Services;
using Dlt645Master.App.ViewModels;
using Dlt645Master.Core.Services;
using FluentAssertions;

namespace Dlt645Master.App.Tests;

/// <summary>
/// 导出格式化纯函数的契约测试（CSV 与报文文本）。
/// 文件对话框与磁盘写入刻意不测——格式化逻辑已隔离为纯函数，命令体只是「取快照 → 问路径 → 写文件」的薄壳。
/// </summary>
public class ExportFormattersTests
{
    private static ReadingRecord Record(
        decimal? value = 220.5m,
        string itemName = "A 相电压",
        string unit = "V",
        bool isSuccess = true,
        string errorMessage = "")
        => new(
            new DateTimeOffset(2026, 7, 3, 8, 47, 12, 345, TimeSpan.FromHours(8)),
            itemName,
            "02 01 01 00",
            value,
            unit,
            isSuccess,
            errorMessage);

    // ---- CSV：表头 + 行格式 ----
    [Fact]
    public void FormatCsv_StartsWithHeaderRow()
    {
        string csv = ReadingCsvFormatter.Format([Record()]);

        csv.Should().StartWith("时间戳,数据项,DI,数值,单位,状态,备注\r\n");
    }

    [Fact]
    public void FormatCsv_SuccessRecord_ProducesExpectedColumns()
    {
        string csv = ReadingCsvFormatter.Format([Record()]);

        csv.Should().Contain("2026-07-03 08:47:12.345,A 相电压,02 01 01 00,220.5,V,成功,");
    }

    [Fact]
    public void FormatCsv_FailureRecord_HasFailureStateAndErrorRemark()
    {
        string csv = ReadingCsvFormatter.Format(
            [Record(value: null, itemName: "", unit: "", isSuccess: false, errorMessage: "等待应答超时")]);

        csv.Should().Contain("2026-07-03 08:47:12.345,,02 01 01 00,,,失败,等待应答超时");
    }

    // ---- CSV：数值不变文化序列化（小数点必须是点号，与操作系统区域设置无关）----
    [Fact]
    public void FormatCsv_DecimalValue_UsesInvariantCulture()
    {
        CultureInfo original = CultureInfo.CurrentCulture;
        try
        {
            // 德语区域用逗号做小数分隔符，若格式化未固定不变文化，此处会产出 "1234,5678" 破坏 CSV 列。
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");

            string csv = ReadingCsvFormatter.Format([Record(value: 1234.5678m)]);

            csv.Should().Contain(",1234.5678,");
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    // ---- CSV：字段转义（逗号/引号/换行）----
    [Theory]
    [InlineData("普通字段", "普通字段")]
    [InlineData("含,逗号", "\"含,逗号\"")]
    [InlineData("含\"引号", "\"含\"\"引号\"")]
    [InlineData("含\n换行", "\"含\n换行\"")]
    public void EscapeField_QuotesAndDoublesOnlyWhenNeeded(string input, string expected)
    {
        ReadingCsvFormatter.EscapeField(input).Should().Be(expected);
    }

    [Fact]
    public void FormatCsv_FieldWithComma_IsEscapedSoColumnCountStaysStable()
    {
        string csv = ReadingCsvFormatter.Format(
            [Record(value: null, isSuccess: false, errorMessage: "错误信息字: 0x01, 其他")]);

        string dataLine = csv.Split("\r\n")[1];
        dataLine.Should().EndWith("\"错误信息字: 0x01, 其他\"");
    }

    // ---- 报文日志：两行一条 + 条目间空行，与界面显示一致 ----
    [Fact]
    public void FormatFrameLog_TwoLinesPerEntrySeparatedByBlankLine()
    {
        var first = new FrameLogEntry(
            FrameDirection.Tx,
            [0x68, 0x01, 0x16],
            new DateTimeOffset(2026, 7, 3, 8, 47, 12, 345, TimeSpan.FromHours(8)));
        var second = new FrameLogEntry(
            FrameDirection.Rx,
            [0x68, 0x91, 0x16],
            new DateTimeOffset(2026, 7, 3, 8, 47, 12, 400, TimeSpan.FromHours(8)));

        string text = FrameLogFormatter.Format([first, second]);

        text.Should().Be(
            "08:47:12.345 发送\r\n" +
            "68 01 16\r\n" +
            "\r\n" +
            "08:47:12.400 接收\r\n" +
            "68 91 16\r\n");
    }
}
