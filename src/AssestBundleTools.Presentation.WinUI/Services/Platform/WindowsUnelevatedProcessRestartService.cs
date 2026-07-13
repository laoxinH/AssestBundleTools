using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace AssestBundleTools.Presentation.WinUI.Services;

internal sealed class WindowsUnelevatedProcessRestartService : IUnelevatedProcessRestartService
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

        var existingProcessIds = CaptureMatchingProcessIds(executablePath);
        var explorerLaunch = TryRequestExplorerShellLaunch(executablePath, arguments);
        if (explorerLaunch.Requested)
        {
            return await WaitForUnelevatedProcessAsync(executablePath, existingProcessIds);
        }

        var tokenResult = TryRestartWithShellToken(executablePath, arguments);
        if (tokenResult.Succeeded)
        {
            return tokenResult;
        }

        return UnelevatedProcessRestartResult.Failure(
            $"Explorer Shell launch was unavailable: {explorerLaunch.DiagnosticDetail} " +
            $"Shell-token fallback failed: {tokenResult.DiagnosticDetail}",
            tokenResult.NativeErrorCode);
    }

    private static UnelevatedProcessRestartResult TryRestartWithShellToken(
        string executablePath,
        IReadOnlyList<string> arguments)
    {

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
                var tokenCheck = CheckShellTokenIsUnelevated(shellToken);
                if (!tokenCheck.Succeeded)
                {
                    return tokenCheck;
                }

                return StartWithShellToken(shellToken, executablePath, arguments);
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

    private static ExplorerShellLaunchResult TryRequestExplorerShellLaunch(
        string executablePath,
        IReadOnlyList<string> arguments)
    {
        object? shell = null;
        try
        {
            var shellType = Type.GetTypeFromProgID("Shell.Application", throwOnError: false);
            if (shellType is null)
            {
                return ExplorerShellLaunchResult.Unavailable(
                    "Shell.Application is not registered for the current desktop.");
            }

            shell = Activator.CreateInstance(shellType);
            if (shell is null)
            {
                return ExplorerShellLaunchResult.Unavailable(
                    "Shell.Application could not be activated.");
            }

            _ = shellType.InvokeMember(
                "ShellExecute",
                BindingFlags.InvokeMethod | BindingFlags.Public | BindingFlags.Instance,
                binder: null,
                target: shell,
                args:
                [
                    executablePath,
                    UnelevatedRestartLaunchArguments.BuildArgumentList(arguments),
                    Path.GetDirectoryName(executablePath) ?? AppContext.BaseDirectory,
                    "open",
                    1
                ]);
            return ExplorerShellLaunchResult.Accepted();
        }
        catch (Exception ex)
        {
            return ExplorerShellLaunchResult.Unavailable(
                $"Shell.Application.ShellExecute failed: {ex.GetBaseException().Message}");
        }
        finally
        {
            if (shell is not null && Marshal.IsComObject(shell))
            {
                _ = Marshal.FinalReleaseComObject(shell);
            }
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
                    return UnelevatedProcessRestartResult.Success(processId);
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

    private static UnelevatedProcessRestartResult CheckShellTokenIsUnelevated(IntPtr shellToken)
    {
        var elevationSize = Marshal.SizeOf<TokenElevation>();
        if (!GetTokenInformation(
                shellToken,
                TokenElevationInformationClass,
                out var elevation,
                elevationSize,
                out _))
        {
            return CreateNativeFailure("Reading the Explorer shell elevation state failed");
        }

        return elevation.TokenIsElevated == 0
            ? UnelevatedProcessRestartResult.Success(processId: 0)
            : UnelevatedProcessRestartResult.Failure(
                "The Explorer shell token is elevated, so it cannot be used for an unelevated restart.");
    }

    private static UnelevatedProcessRestartResult StartWithShellToken(
        IntPtr shellToken,
        string executablePath,
        IReadOnlyList<string> arguments)
    {
        if (!CreateEnvironmentBlock(out var environmentBlock, shellToken, false))
        {
            return CreateNativeFailure("Creating the standard-user environment block failed");
        }

        try
        {
            var startupInfo = new StartupInfo
            {
                Size = Marshal.SizeOf<StartupInfo>(),
                Desktop = "winsta0\\default"
            };
            var commandLine = new StringBuilder(
                UnelevatedRestartLaunchArguments.BuildCommandLine(executablePath, arguments));
            var workingDirectory = Path.GetDirectoryName(executablePath) ?? AppContext.BaseDirectory;

            if (!CreateProcessWithTokenW(
                    shellToken,
                    LogonWithProfile,
                    executablePath,
                    commandLine,
                    CreateUnicodeEnvironment,
                    environmentBlock,
                    workingDirectory,
                    ref startupInfo,
                    out var processInformation))
            {
                return CreateNativeFailure("Starting the standard-user process failed");
            }

            try
            {
                return UnelevatedProcessRestartResult.Success(
                    checked((int)processInformation.ProcessId));
            }
            finally
            {
                _ = CloseHandle(processInformation.ThreadHandle);
                _ = CloseHandle(processInformation.ProcessHandle);
            }
        }
        finally
        {
            _ = DestroyEnvironmentBlock(environmentBlock);
        }
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

    [DllImport("userenv.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateEnvironmentBlock(
        out IntPtr environment,
        IntPtr tokenHandle,
        [MarshalAs(UnmanagedType.Bool)] bool inherit);

    [DllImport("userenv.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyEnvironmentBlock(IntPtr environment);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateProcessWithTokenW(
        IntPtr tokenHandle,
        uint logonFlags,
        string applicationName,
        StringBuilder commandLine,
        uint creationFlags,
        IntPtr environment,
        string currentDirectory,
        ref StartupInfo startupInfo,
        out ProcessInformation processInformation);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);

    [StructLayout(LayoutKind.Sequential)]
    private struct TokenElevation
    {
        public int TokenIsElevated;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct StartupInfo
    {
        public int Size;
        public string? Reserved;
        public string? Desktop;
        public string? Title;
        public int X;
        public int Y;
        public int XSize;
        public int YSize;
        public int XCountChars;
        public int YCountChars;
        public int FillAttribute;
        public int Flags;
        public short ShowWindow;
        public short Reserved2Size;
        public IntPtr Reserved2;
        public IntPtr StandardInput;
        public IntPtr StandardOutput;
        public IntPtr StandardError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessInformation
    {
        public IntPtr ProcessHandle;
        public IntPtr ThreadHandle;
        public uint ProcessId;
        public uint ThreadId;
    }

    private readonly record struct ExplorerShellLaunchResult(
        bool Requested,
        string DiagnosticDetail)
    {
        public static ExplorerShellLaunchResult Accepted()
        {
            return new ExplorerShellLaunchResult(true, string.Empty);
        }

        public static ExplorerShellLaunchResult Unavailable(string diagnosticDetail)
        {
            return new ExplorerShellLaunchResult(false, diagnosticDetail);
        }
    }
}
