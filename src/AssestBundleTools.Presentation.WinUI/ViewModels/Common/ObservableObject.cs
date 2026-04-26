using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.Common;

/// <summary>
/// 为 WinUI 页面和控件提供最基础的属性变更通知能力。
/// </summary>
/// <remarks>
/// 当前项目还没有引入额外的 MVVM 工具包，因此先在 Presentation 层
/// 提供一个足够轻量的基类，后续如果切换到统一框架，也只需要在这一层替换。
/// </remarks>
public abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool SetProperty<T>(
        ref T storage,
        T value,
        [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(storage, value))
        {
            return false;
        }

        storage = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
