using Microsoft.Win32;

namespace Dlt645Master.App.Services;

/// <summary><see cref="ISaveFileDialogService"/> 的 WPF 实现：包装 <see cref="SaveFileDialog"/>。</summary>
public sealed class SaveFileDialogService : ISaveFileDialogService
{
    public string? PromptSavePath(string filter, string defaultFileName)
    {
        var dialog = new SaveFileDialog
        {
            Filter = filter,
            FileName = defaultFileName,
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}
