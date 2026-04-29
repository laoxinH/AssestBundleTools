namespace AssestBundleTools.Presentation.WinUI.Services;

/// <summary>
/// 表示 UI 偏好设置的低层持久化抽象。
/// </summary>
/// <remarks>
/// 这是 Presentation 自己持有的壳层偏好，不属于 Application 用例端口；
/// 因此具体的 JSON / 文件系统实现由组合根适配进来，而不是让普通服务直接引用 Infrastructure。
/// </remarks>
internal interface IAppPreferencesPersistenceStore
{
    AppPreferencesPersistenceSnapshot Load();

    void Save(AppPreferencesPersistenceSnapshot snapshot);
}
