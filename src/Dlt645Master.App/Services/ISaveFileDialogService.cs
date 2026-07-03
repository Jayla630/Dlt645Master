namespace Dlt645Master.App.Services;

/// <summary>「另存为」对话框抽象：把视图模型的导出命令与真实 WPF 对话框隔离，测试用同步假件替代。</summary>
public interface ISaveFileDialogService
{
    /// <summary>弹出另存为对话框；返回用户选定的完整路径，取消则返回 null。</summary>
    /// <param name="filter">文件类型过滤器（SaveFileDialog.Filter 语法）。</param>
    /// <param name="defaultFileName">默认文件名（含扩展名）。</param>
    string? PromptSavePath(string filter, string defaultFileName);
}
