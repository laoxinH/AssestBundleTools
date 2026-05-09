using System.Globalization;
using System.Text;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.Services;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using Microsoft.UI.Xaml;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

public sealed partial class InspectorSpineAnimationPreviewSurfaceView
{
    private const double DefaultFrameSequenceExportFramesPerSecond = 60d;

    private CancellationTokenSource? _frameSequenceExportCancellation;
    private bool _isFrameSequenceExportActive;

    private async void OnExportFramesButtonClick(object sender, RoutedEventArgs e)
    {
        await ExportFrameSequenceAsync();
    }

    private async Task ExportFrameSequenceAsync()
    {
        if (_isFrameSequenceExportActive)
        {
            return;
        }

        if (!TryCreateExportPlanRequest(out var request))
        {
            ShowExportWarning(ResourceKeys.InspectorSpineAnimationPreviewExportUnavailableMessage);
            return;
        }

        string? selectedDirectoryPath;
        try
        {
            selectedDirectoryPath = await App.CurrentApp.FilePickerService.PickSpineFrameSequenceExportDirectoryAsync();
        }
        catch
        {
            ShowExportError();
            return;
        }

        if (string.IsNullOrWhiteSpace(selectedDirectoryPath))
        {
            return;
        }

        _frameSequenceExportCancellation?.Dispose();
        _frameSequenceExportCancellation = new CancellationTokenSource();
        SetFrameSequenceExportActive(true);
        try
        {
            var plan = await _runtimePlaybackService!.PlanFrameSequenceExportAsync(
                request,
                _frameSequenceExportCancellation.Token);
            if (!plan.IsHandled ||
                plan.Layout is null ||
                plan.Frames.Count == 0)
            {
                ShowExportWarning(ResourceKeys.InspectorSpineAnimationPreviewExportUnavailableMessage);
                return;
            }

            var outputDirectoryPath = CreateExportOutputDirectoryPath(selectedDirectoryPath, request);
            LogExportMessage(
                ResourceKeys.InspectorSpineAnimationPreviewExportStartedFormat,
                plan.Frames.Count,
                outputDirectoryPath);
            var exportedCount = await new SpineAnimationFrameSequencePngExporter(SpineCanvas).ExportAsync(
                plan,
                ViewModel!.TexturePageContents,
                outputDirectoryPath,
                _frameSequenceExportCancellation.Token);
            LogExportMessage(
                ResourceKeys.InspectorSpineAnimationPreviewExportSucceededFormat,
                exportedCount,
                outputDirectoryPath);
            ShowExportSuccess(exportedCount, outputDirectoryPath);
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
            ShowExportError();
        }
        finally
        {
            _frameSequenceExportCancellation?.Dispose();
            _frameSequenceExportCancellation = null;
            SetFrameSequenceExportActive(false);
        }
    }

    private void CancelFrameSequenceExport()
    {
        _frameSequenceExportCancellation?.Cancel();
    }

    private bool TryCreateExportPlanRequest(
        out SpineAnimationRuntimeFrameSequenceExportPlanRequestDto request)
    {
        request = null!;
        if (_runtimePlaybackService is null ||
            ViewModel is not { CanExportFrameSequence: true, Preview.PlaybackSource: { } source } viewModel)
        {
            return false;
        }

        request = new SpineAnimationRuntimeFrameSequenceExportPlanRequestDto(
            source,
            viewModel.SelectedAnimationName,
            viewModel.SelectedSkinName,
            DefaultFrameSequenceExportFramesPerSecond,
            FileNamePrefix: CreateExportNamePrefix(viewModel));
        return true;
    }

    private void SetFrameSequenceExportActive(bool isActive)
    {
        _isFrameSequenceExportActive = isActive;
        ExportFramesButton.IsEnabled = !isActive && ViewModel?.CanExportFrameSequence == true;
    }

    private static string CreateExportOutputDirectoryPath(
        string selectedDirectoryPath,
        SpineAnimationRuntimeFrameSequenceExportPlanRequestDto request)
    {
        var timestamp = DateTimeOffset.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        var directoryName = string.Create(
            CultureInfo.InvariantCulture,
            $"{SanitizeFileNameStem(request.FileNamePrefix ?? request.AnimationName ?? "spine")}_{timestamp}");
        return Path.Combine(selectedDirectoryPath, directoryName);
    }

    private static string CreateExportNamePrefix(InspectorPreviewSpineAnimationViewModel viewModel)
    {
        var builder = new StringBuilder();
        AppendNamePart(builder, viewModel.SelectedSkinName);
        AppendNamePart(builder, viewModel.SelectedAnimationName);
        return builder.Length == 0 ? "spine" : builder.ToString();
    }

    private static void AppendNamePart(StringBuilder builder, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (builder.Length > 0)
        {
            builder.Append('_');
        }

        builder.Append(value.Trim());
    }

    private static string SanitizeFileNameStem(string value)
    {
        var invalidCharacters = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(value.Length);
        foreach (var character in value.Trim())
        {
            builder.Append(IsInvalidFileNameCharacter(character, invalidCharacters) ? '_' : character);
        }

        var sanitized = builder.ToString().Trim(' ', '.', '_');
        return sanitized.Length == 0 ? "spine" : sanitized;
    }

    private static bool IsInvalidFileNameCharacter(char character, IReadOnlyCollection<char> invalidCharacters) =>
        char.IsControl(character) ||
        char.IsWhiteSpace(character) ||
        invalidCharacters.Contains(character);

    private void ShowExportSuccess(int frameCount, string outputDirectoryPath)
    {
        App.CurrentApp.AppNotificationService.Show(
            UiText.Get(ResourceKeys.AppNotificationSuccessTitle),
            FormatExportMessage(
                ResourceKeys.InspectorSpineAnimationPreviewExportSucceededFormat,
                frameCount,
                outputDirectoryPath),
            AppNotificationSeverity.Success);
    }

    private static void ShowExportWarning(string messageResourceKey)
    {
        App.CurrentApp.AppNotificationService.Show(
            UiText.Get(ResourceKeys.AppNotificationWarningTitle),
            UiText.Get(messageResourceKey),
            AppNotificationSeverity.Warning);
    }

    private static void ShowExportError()
    {
        App.CurrentApp.AppNotificationService.Show(
            UiText.Get(ResourceKeys.AppNotificationErrorTitle),
            UiText.Get(ResourceKeys.InspectorSpineAnimationPreviewExportFailedMessage),
            AppNotificationSeverity.Error);
        App.CurrentApp.AppLogService.AppendError(
            UiText.Get(ResourceKeys.InspectorSpineAnimationPreviewExportFailedMessage));
    }

    private static void LogExportMessage(string resourceKey, int frameCount, string outputDirectoryPath)
    {
        App.CurrentApp.AppLogService.AppendInfo(FormatExportMessage(
            resourceKey,
            frameCount,
            outputDirectoryPath));
    }

    private static string FormatExportMessage(string resourceKey, int frameCount, string outputDirectoryPath) =>
        string.Format(
            CultureInfo.CurrentCulture,
            UiText.Get(resourceKey),
            frameCount,
            outputDirectoryPath);
}
