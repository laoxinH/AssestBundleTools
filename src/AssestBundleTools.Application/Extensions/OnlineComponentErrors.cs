using AssestBundleTools.Domain.Common.Errors;

namespace AssestBundleTools.Application.Extensions;

public static class OnlineComponentErrors
{
    public static AppError NotRegistered(string componentId) =>
        new(
            code: "Extensions.OnlineComponent.NotRegistered",
            category: ErrorCategory.NotFound,
            severity: ErrorSeverity.Error,
            messageToken: "Error.Extensions.OnlineComponent.NotRegistered",
            parameters: new Dictionary<string, object?> { ["ComponentId"] = componentId });

    public static AppError InstallFailed(string componentId, string? detail = null) =>
        new(
            code: "Extensions.OnlineComponent.InstallFailed",
            category: ErrorCategory.ExternalDependency,
            severity: ErrorSeverity.Error,
            messageToken: "Error.Extensions.OnlineComponent.InstallFailed",
            parameters: new Dictionary<string, object?>
            {
                ["ComponentId"] = componentId,
                ["Detail"] = detail
            },
            isRetryable: true);
}
