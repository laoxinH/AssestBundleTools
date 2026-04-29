using AssestBundleTools.Domain.Common.Errors;

namespace AssestBundleTools.Domain.Tests.Common.Errors;

public sealed class AppErrorTests
{
    [Fact]
    public void Constructor_ShouldThrow_WhenCodeIsBlank()
    {
        var exception = Assert.Throws<ArgumentException>(() => new AppError(
            code: " ",
            category: ErrorCategory.Validation,
            severity: ErrorSeverity.Warning,
            messageToken: "Error.Token"));

        Assert.Equal("code", exception.ParamName);
    }

    [Fact]
    public void Constructor_ShouldUseEmptyParameters_WhenNotProvided()
    {
        var error = new AppError(
            code: "Workspace.Catalog.Invalid",
            category: ErrorCategory.Validation,
            severity: ErrorSeverity.Warning,
            messageToken: "Error.Workspace.Catalog.Invalid");

        Assert.Empty(error.Parameters);
        Assert.True(error.IsUserActionable);
        Assert.False(error.IsRetryable);
    }
}
