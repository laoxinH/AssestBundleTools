using AssestBundleTools.Application.Tests.Testing;
using AssestBundleTools.Application.Workspace.Services;

namespace AssestBundleTools.Application.Tests.Workspace.Services;

internal static class WorkspaceImportExecutionOptionsResolverTests
{
    public static Task ResolveBundleWorkerCount_ShouldFollowDocumentedThresholds()
    {
        TestAssert.Equal(1, WorkspaceImportExecutionOptionsResolver.ResolveBundleWorkerCount(0), "Non-positive processor counts should clamp to one worker.");
        TestAssert.Equal(1, WorkspaceImportExecutionOptionsResolver.ResolveBundleWorkerCount(1), "Single-core environments should keep imports serial.");
        TestAssert.Equal(2, WorkspaceImportExecutionOptionsResolver.ResolveBundleWorkerCount(2), "Two logical cores should use two workers.");
        TestAssert.Equal(2, WorkspaceImportExecutionOptionsResolver.ResolveBundleWorkerCount(4), "Four logical cores should still cap at two workers.");
        TestAssert.Equal(3, WorkspaceImportExecutionOptionsResolver.ResolveBundleWorkerCount(5), "Five logical cores should upgrade to three workers.");
        TestAssert.Equal(3, WorkspaceImportExecutionOptionsResolver.ResolveBundleWorkerCount(6), "Six logical cores should keep three workers.");
        TestAssert.Equal(4, WorkspaceImportExecutionOptionsResolver.ResolveBundleWorkerCount(7), "Seven logical cores should reach the documented cap.");
        TestAssert.Equal(4, WorkspaceImportExecutionOptionsResolver.ResolveBundleWorkerCount(16), "High-core environments should stay capped at four workers.");
        return Task.CompletedTask;
    }
}
