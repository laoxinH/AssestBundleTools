namespace AssestBundleTools.Presentation.WinUI.Services;

/// <summary>
/// 负责播放与当前 UI 交互相关的轻量反馈。
/// </summary>
/// <remarks>
/// 当前阶段先承接系统提示音，后续也可以继续扩展到更细粒度的
/// 成功、警告、完成提示等统一交互反馈，而不把具体实现散落到控件内部。
/// </remarks>
public interface IAppInteractionFeedbackService
{
    void Play(AppInteractionFeedbackKind feedbackKind);
}
