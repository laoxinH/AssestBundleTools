using System.Runtime.InteropServices;

namespace AssestBundleTools.Presentation.WinUI.Services;

internal sealed partial class WindowsUnelevatedProcessRestartService
{
    private const int TokenSessionIdInformationClass = 12;
    private const int TokenLinkedTokenInformationClass = 19;
    private const int TokenIntegrityLevelInformationClass = 25;
    private const int SecurityImpersonation = 2;
    private const int TokenPrimary = 1;
    private const uint SecurityMandatoryMediumRid = 0x00002000;
    private const uint SecurityMandatoryHighRid = 0x00003000;

    private static UnelevatedProcessRestartResult TryRestartWithLinkedToken(
        string executablePath,
        IReadOnlyList<string> arguments)
    {
        if (!OpenProcessToken(GetCurrentProcess(), TokenQuery, out var currentToken))
        {
            return CreateNativeFailure("Opening the current process token failed");
        }

        try
        {
            if (!GetLinkedTokenInformation(
                    currentToken,
                    TokenLinkedTokenInformationClass,
                    out var linkedToken,
                    Marshal.SizeOf<TokenLinkedToken>(),
                    out _))
            {
                return CreateNativeFailure("Reading the UAC linked token failed");
            }

            try
            {
                if (!DuplicateTokenEx(
                        linkedToken.Handle,
                        TokenAssignPrimary | TokenDuplicate | TokenQuery,
                        IntPtr.Zero,
                        SecurityImpersonation,
                        TokenPrimary,
                        out var primaryToken))
                {
                    return CreateNativeFailure("Duplicating the UAC linked token failed");
                }

                try
                {
                    var tokenCheck = ValidateLinkedToken(primaryToken);
                    return tokenCheck.Succeeded
                        ? StartWithToken(
                            primaryToken,
                            executablePath,
                            arguments,
                            "LinkedToken")
                        : tokenCheck;
                }
                finally
                {
                    _ = CloseHandle(primaryToken);
                }
            }
            finally
            {
                _ = CloseHandle(linkedToken.Handle);
            }
        }
        finally
        {
            _ = CloseHandle(currentToken);
        }
    }

    private static UnelevatedProcessRestartResult ValidateLinkedToken(IntPtr token)
    {
        var elevationCheck = CheckTokenIsUnelevated(
            token,
            "UAC linked token",
            out _);
        if (!elevationCheck.Succeeded)
        {
            return elevationCheck;
        }

        if (!TryReadTokenInt32(
                token,
                TokenSessionIdInformationClass,
                out var sessionId))
        {
            return CreateNativeFailure("Reading the UAC linked token session failed");
        }

        using var currentProcess = System.Diagnostics.Process.GetCurrentProcess();
        if (sessionId != currentProcess.SessionId)
        {
            return UnelevatedProcessRestartResult.Failure(
                $"The UAC linked token belongs to session {sessionId}, not the current interactive session.");
        }

        if (!TryReadTokenIntegrityRid(token, out var integrityRid))
        {
            return CreateNativeFailure("Reading the UAC linked token integrity level failed");
        }

        return integrityRid is >= SecurityMandatoryMediumRid and < SecurityMandatoryHighRid
            ? UnelevatedProcessRestartResult.Success(processId: 0)
            : UnelevatedProcessRestartResult.Failure(
                $"The UAC linked token integrity RID 0x{integrityRid:X} is not a standard desktop integrity level.");
    }

    private static bool TryReadTokenInt32(
        IntPtr token,
        int informationClass,
        out int value)
    {
        return GetTokenInt32Information(
            token,
            informationClass,
            out value,
            sizeof(int),
            out _);
    }

    private static bool TryReadTokenIntegrityRid(IntPtr token, out uint integrityRid)
    {
        integrityRid = 0;
        _ = GetTokenBufferInformation(
            token,
            TokenIntegrityLevelInformationClass,
            IntPtr.Zero,
            0,
            out var requiredLength);
        if (requiredLength <= 0)
        {
            return false;
        }

        var buffer = Marshal.AllocHGlobal(requiredLength);
        try
        {
            if (!GetTokenBufferInformation(
                    token,
                    TokenIntegrityLevelInformationClass,
                    buffer,
                    requiredLength,
                    out _))
            {
                return false;
            }

            var sid = Marshal.ReadIntPtr(buffer);
            if (sid == IntPtr.Zero)
            {
                return false;
            }

            var subAuthorityCountPointer = GetSidSubAuthorityCount(sid);
            if (subAuthorityCountPointer == IntPtr.Zero)
            {
                return false;
            }

            var subAuthorityCount = Marshal.ReadByte(subAuthorityCountPointer);
            if (subAuthorityCount == 0)
            {
                return false;
            }

            var integrityRidPointer = GetSidSubAuthority(sid, checked((uint)(subAuthorityCount - 1)));
            if (integrityRidPointer == IntPtr.Zero)
            {
                return false;
            }

            integrityRid = unchecked((uint)Marshal.ReadInt32(integrityRidPointer));
            return true;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();

    [DllImport("advapi32.dll", EntryPoint = "GetTokenInformation", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetLinkedTokenInformation(
        IntPtr tokenHandle,
        int tokenInformationClass,
        out TokenLinkedToken tokenInformation,
        int tokenInformationLength,
        out int returnLength);

    [DllImport("advapi32.dll", EntryPoint = "GetTokenInformation", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetTokenInt32Information(
        IntPtr tokenHandle,
        int tokenInformationClass,
        out int tokenInformation,
        int tokenInformationLength,
        out int returnLength);

    [DllImport("advapi32.dll", EntryPoint = "GetTokenInformation", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetTokenBufferInformation(
        IntPtr tokenHandle,
        int tokenInformationClass,
        IntPtr tokenInformation,
        int tokenInformationLength,
        out int returnLength);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DuplicateTokenEx(
        IntPtr existingToken,
        uint desiredAccess,
        IntPtr tokenAttributes,
        int impersonationLevel,
        int tokenType,
        out IntPtr newToken);

    [DllImport("advapi32.dll")]
    private static extern IntPtr GetSidSubAuthorityCount(IntPtr sid);

    [DllImport("advapi32.dll")]
    private static extern IntPtr GetSidSubAuthority(IntPtr sid, uint subAuthorityIndex);

    [StructLayout(LayoutKind.Sequential)]
    private struct TokenLinkedToken
    {
        public IntPtr Handle;
    }
}
