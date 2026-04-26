using Microsoft.UI.Xaml;
using System.Runtime.InteropServices;

namespace AssestBundleTools.Presentation.WinUI.Services;

/// <summary>
/// 默认的应用交互反馈服务实现。
/// </summary>
public sealed class AppInteractionFeedbackService : IAppInteractionFeedbackService
{
    private const uint MessageBeepIconHand = 0x00000010;

    public void Play(AppInteractionFeedbackKind feedbackKind)
    {
        try
        {
            switch (feedbackKind)
            {
                case AppInteractionFeedbackKind.ValidationError:
                    PlayValidationErrorFeedback();
                    break;
            }
        }
        catch
        {
            // 提示音只是增强反馈，不应影响主流程。
        }
    }

    /// <summary>
    /// 优先播放系统级错误提示音，确保在对话框校验场景里也能稳定听见。
    /// 优先尝试 Win32 `MessageBeep`，这样即使系统声音方案里没有绑定错误音效，
    /// 也仍然能得到系统回退蜂鸣；如果失败，再回退到 WinUI 元素音。
    /// </summary>
    private static void PlayValidationErrorFeedback()
    {
        try
        {
            if (MessageBeep(MessageBeepIconHand))
            {
                return;
            }
        }
        catch
        {
            // 回退到托管系统音。
        }

        ElementSoundPlayer.State = ElementSoundPlayerState.On;
        ElementSoundPlayer.Play(ElementSoundKind.Invoke);
    }

    [DllImport("user32.dll", SetLastError = false)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool MessageBeep(uint type);
}
