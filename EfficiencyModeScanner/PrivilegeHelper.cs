using System.Runtime.InteropServices;

namespace EfficiencyModeScanner;

/// <summary>Tries to enable SeDebugPrivilege for the current process (usually requires elevation).</summary>
internal static class PrivilegeHelper
{
    private const uint TokenAdjustPrivileges = 0x0020;
    private const uint TokenQuery = 0x0008;
    private const uint SePrivilegeEnabled = 0x00000002u;

    internal static bool TryEnableSeDebugPrivilege()
    {
        nint token = nint.Zero;
        try
        {
            if (!NativeMethods.OpenProcessToken(NativeMethods.GetCurrentProcess(), TokenAdjustPrivileges | TokenQuery, out token) || token == 0)
                return false;

            if (!NativeMethods.LookupPrivilegeValue(null, "SeDebugPrivilege", out NativeMethods.Luid luid))
                return false;

            var tp = new NativeMethods.TokenPrivileges
            {
                PrivilegeCount = 1,
                Luid = luid,
                Attributes = SePrivilegeEnabled,
            };

            bool adjusted = NativeMethods.AdjustTokenPrivileges(token, false, ref tp, (uint)Marshal.SizeOf<NativeMethods.TokenPrivileges>(), nint.Zero, nint.Zero);
            int err = Marshal.GetLastWin32Error();
            return adjusted && err == 0;
        }
        finally
        {
            if (token != 0)
                _ = NativeMethods.CloseHandle(token);
        }
    }
}
