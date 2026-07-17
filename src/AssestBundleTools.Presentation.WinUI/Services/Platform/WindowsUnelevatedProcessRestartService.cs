using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace AssestBundleTools.Presentation.WinUI.Services;

internal sealed partial class WindowsUnelevatedProcessRestartService : IUnelevatedProcessRestartService
{
    private const uint ProcessQueryLimitedInformation = 0x1000;
    private const uint TokenAssignPrimary = 0x0001;
    private const uint TokenDuplicate = 0x0002;
    private const uint TokenQuery = 0x0008;
    private const uint LogonWithProfile = 0x00000001;
    private const uint CreateUnicodeEnvironment = 0x00000400;
    private const int TokenElevationInformationClass = 20;
    private const int ExplorerProcessWaitAttemptCount = 100;
    private static readonly TimeSpan ExplorerProcessWaitInterval = TimeSpan.FromMilliseconds(100);

    public async Task<UnelevatedProcessRestartResult> TryRestartAsync(IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        var executablePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
        {
            return UnelevatedProcessRestartResult.Failure(
                $"Current executable path is unavailable: {executablePath ?? "<null>"}.");
        }

        var shellTokenResult = TryRestartWithShellToken(
            executablePath,
            arguments,
            out var shellTokenWasElevated);
        if (shellTokenResult.Succeeded)
        {
            return shellTokenResult;
        }

        var linkedTokenResult = TryRestartWithLinkedToken(executablePath, arguments);
        if (linkedTokenResult.Succeeded)
        {
            return linkedTokenResult;
        }

        var explorerLaunch = ExplorerShellLaunchResult.Unavailable(
            "Explorer automation was skipped because its Shell token is elevated.");
        UnelevatedProcessRestartResult? explorerProcessResult = null;
        if (!shellTokenWasElevated)
        {
            var existingProcessIds = CaptureMatchingProcessIds(executablePath);
            explorerLaunch = TryRequestExplorerShellLaunch(executablePath, arguments);
            if (explorerLaunch.Requested)
            {
                explorerProcessResult = await WaitForUnelevatedProcessAsync(
                    executablePath,
                    existingProcessIds);
                if (explorerProcessResult.Value.Succeeded)
                {
                    return explorerProcessResult.Value;
                }
            }
        }

        return UnelevatedProcessRestartResult.Failure(
            $"Shell-token launch failed: {shellTokenResult.DiagnosticDetail} " +
            $"Linked-token fallback failed: {linkedTokenResult.DiagnosticDetail} " +
            (explorerProcessResult is { } processResult
                ? $"Explorer automation launch failed: {processResult.DiagnosticDetail}"
                : $"Explorer automation was unavailable: {explorerLaunch.DiagnosticDetail}"),
            linkedTokenResult.NativeErrorCode ?? shellTokenResult.NativeErrorCode);
    }

    private static UnelevatedProcessRestartResult TryRestartWithShellToken(
        string executablePath,
        IReadOnlyList<string> arguments,
        out bool shellTokenWasElevated)
    {
        shellTokenWasElevated = false;

        var shellWindow = GetShellWindow();
        if (shellWindow == IntPtr.Zero)
        {
            return UnelevatedProcessRestartResult.Failure(
                "The current desktop does not expose an Explorer shell window.");
        }

        _ = GetWindowThreadProcessId(shellWindow, out var shellProcessId);
        if (shellProcessId == 0)
        {
            return CreateNativeFailure("Resolving the Explorer shell process failed");
        }

        var shellProcess = OpenProcess(ProcessQueryLimitedInformation, false, shellProcessId);
        if (shellProcess == IntPtr.Zero)
        {
            return CreateNativeFailure("Opening the Explorer shell process failed");
        }

        try
        {
            if (!OpenProcessToken(
                    shellProcess,
                    TokenAssignPrimary | TokenDuplicate | TokenQuery,
                    out var shellToken))
            {
                return CreateNativeFailure("Opening the Explorer shell token failed");
            }

            try
            {
                var tokenCheck = CheckTokenIsUnelevated(
                    shellToken,
                    "Explorer shell token",
                    out shellTokenWasElevated);
                if (!tokenCheck.Succeeded)
                {
                    return tokenCheck;
                }

                return StartWithToken(
                    shellToken,
                    executablePath,
                    arguments,
                    "ShellToken");
            }
            finally
            {
                _ = CloseHandle(shellToken);
            }
        }
        finally
        {
            _ = CloseHandle(shellProcess);
        }
    }

    private static async Task<UnelevatedProcessRestartResult> WaitForUnelevatedProcessAsync(
        string executablePath,
        IReadOnlySet<int> existingProcessIds)
    {
        var elevatedCandidateProcessIds = new HashSet<int>();
        for (var attempt = 0; attempt < ExplorerProcessWaitAttemptCount; attempt++)
        {
            foreach (var processId in CaptureMatchingProcessIds(executablePath))
            {
                if (existingProcessIds.Contains(processId))
                {
                    continue;
                }

                if (!TryReadProcessElevation(processId, out var isElevated))
                {
                    continue;
                }

                if (!isElevated)
                {
                    return UnelevatedProcessRestartResult.Success(
                        processId,
                        "TokenSource=ExplorerAutomation.");
                }

                elevatedCandidateProcessIds.Add(processId);
            }

            await Task.Delay(ExplorerProcessWaitInterval);
        }

        var candidateDetail = elevatedCandidateProcessIds.Count == 0
            ? "No matching child process was observed."
            : $"Only elevated matching processes were observed: {string.Join(",", elevatedCandidateProcessIds)}.";
        return UnelevatedProcessRestartResult.Failure(
            $"Explorer Shell accepted the restart request, but no unelevated process was confirmed within " +
            $"{ExplorerProcessWaitAttemptCount * ExplorerProcessWaitInterval.TotalMilliseconds:0} ms. " +
            candidateDetail);
    }

    private static HashSet<int> CaptureMatchingProcessIds(string executablePath)
    {
        var processName = Path.GetFileNameWithoutExtension(executablePath);
        var processIds = new HashSet<int>();
        foreach (var process in Process.GetProcessesByName(processName))
        {
            using (process)
            {
                if (TryReadProcessPath(process.Id, out var processPath) &&
                    string.Equals(processPath, executablePath, StringComparison.OrdinalIgnoreCase))
                {
                    processIds.Add(process.Id);
                }
            }
        }

        return processIds;
    }

    private static bool TryReadProcessPath(int processId, out string processPath)
    {
        processPath = string.Empty;
        var processHandle = OpenProcess(ProcessQueryLimitedInformation, false, checked((uint)processId));
        if (processHandle == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            var capacity = 32768u;
            var pathBuilder = new StringBuilder(checked((int)capacity));
            if (!QueryFullProcessImageNameW(processHandle, 0, pathBuilder, ref capacity))
            {
                return false;
            }

            processPath = pathBuilder.ToString();
            return !string.IsNullOrWhiteSpace(processPath);
        }
        finally
        {
            _ = CloseHandle(processHandle);
        }
    }

    private static bool TryReadProcessElevation(int processId, out bool isElevated)
    {
        isElevated = false;
        var processHandle = OpenProcess(ProcessQueryLimitedInformation, false, checked((uint)processId));
        if (processHandle == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            if (!OpenProcessToken(processHandle, TokenQuery, out var tokenHandle))
            {
                return false;
            }

            try
            {
                var elevationSize = Marshal.SizeOf<TokenElevation>();
                if (!GetTokenInformation(
                        tokenHandle,
                        TokenElevationInformationClass,
                        out var elevation,
                        elevationSize,
                        out _))
                {
                    return false;
                }

                isElevated = elevation.TokenIsElevated != 0;
                return true;
            }
            finally
            {
                _ = CloseHandle(tokenHandle);
            }
        }
        finally
        {
            _ = CloseHandle(processHandle);
        }
    }

    private static UnelevatedProcessRestartResult CheckTokenIsUnelevated(
        IntPtr token,
        string tokenDescription,
        out bool isElevated)
    {
        isElevated = false;
        var elevationSize = Marshal.SizeOf<TokenElevation>();
        if (!GetTokenInformation(
                token,
                TokenElevationInformationClass,
                out var elevation,
                elevationSize,
                out _))
        {
            return CreateNativeFailure($"Reading the {tokenDescription} elevation state failed");
        }

        isElevated = elevation.TokenIsElevated != 0;
        return !isElevated
            ? UnelevatedProcessRestartResult.Success(processId: 0)
            : UnelevatedProcessRestartResult.Failure(
                $"The {tokenDescription} is elevated, so it cannot start a standard-user process.");
    }

    private static UnelevatedProcessRestartResult CreateNativeFailure(string operation)
    {
        var errorCode = Marshal.GetLastWin32Error();
        var errorMessage = new Win32Exception(errorCode).Message;
        return UnelevatedProcessRestartResult.Failure(
            $"{operation}. Win32Error={errorCode}: {errorMessage}",
            errorCode);
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetShellWindow();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr windowHandle, out uint processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint desiredAccess, bool inheritHandle, uint processId);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryFullProcessImageNameW(
        IntPtr processHandle,
        uint flags,
        StringBuilder executablePath,
        ref uint executablePathSize);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenProcessToken(
        IntPtr processHandle,
        uint desiredAccess,
        out IntPtr tokenHandle);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetTokenInformation(
        IntPtr tokenHandle,
        int tokenInformationClass,
        out TokenElevation tokenInformation,
        int tokenInformationLength,
        out int returnLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);

    [StructLayout(LayoutKind.Sequential)]
    private struct TokenElevation
    {
        public int TokenIsElevated;
    }

}
