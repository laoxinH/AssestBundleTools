using System.ComponentModel;
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

    public UnelevatedProcessRestartResult TryRestart(IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        var executablePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
        {
            return UnelevatedProcessRestartResult.Failure(
                $"Current executable path is unavailable: {executablePath ?? "<null>"}.");
        }

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
}
