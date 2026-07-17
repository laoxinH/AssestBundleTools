using System.Runtime.InteropServices;
using System.Text;

namespace AssestBundleTools.Presentation.WinUI.Services;

internal sealed partial class WindowsUnelevatedProcessRestartService
{
    private static UnelevatedProcessRestartResult StartWithToken(
        IntPtr token,
        string executablePath,
        IReadOnlyList<string> arguments,
        string tokenSource)
    {
        if (!CreateEnvironmentBlock(out var environmentBlock, token, false))
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
                    token,
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
                if (!TryReadProcessHandleElevation(
                        processInformation.ProcessHandle,
                        out var isElevated) ||
                    isElevated)
                {
                    _ = TerminateProcess(processInformation.ProcessHandle, 1);
                    return UnelevatedProcessRestartResult.Failure(
                        isElevated
                            ? $"The process created from {tokenSource} was still elevated."
                            : $"The process created from {tokenSource} could not be verified as unelevated.");
                }

                return UnelevatedProcessRestartResult.Success(
                    checked((int)processInformation.ProcessId),
                    $"TokenSource={tokenSource}.");
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

    private static bool TryReadProcessHandleElevation(
        IntPtr processHandle,
        out bool isElevated)
    {
        isElevated = false;
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
    private static extern bool TerminateProcess(IntPtr processHandle, uint exitCode);

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
