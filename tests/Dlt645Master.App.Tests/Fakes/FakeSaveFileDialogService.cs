using Dlt645Master.App.Services;

namespace Dlt645Master.App.Tests.Fakes;

/// <summary>
/// <see cref="ISaveFileDialogService"/> 的假实现：返回预设路径（默认 null 模拟用户取消），
/// 并记录调用参数。用于隔离视图模型导出命令与真实文件对话框。
/// </summary>
public sealed class FakeSaveFileDialogService : ISaveFileDialogService
{
    /// <summary>PromptSavePath 的返回值；保持 null 即模拟用户点了取消。</summary>
    public string? PathToReturn { get; set; }

    public int PromptCount { get; private set; }

    public string? LastFilter { get; private set; }

    public string? LastDefaultFileName { get; private set; }

    public string? PromptSavePath(string filter, string defaultFileName)
    {
        PromptCount++;
        LastFilter = filter;
        LastDefaultFileName = defaultFileName;
        return PathToReturn;
    }
}
