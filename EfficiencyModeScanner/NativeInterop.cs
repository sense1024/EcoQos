using System.Runtime.InteropServices;

namespace EfficiencyModeScanner;

internal static class ProcessConstants
{
    internal const uint ProcessSetInformation = 0x0200;
    internal const uint ProcessSetLimitedInformation = 0x0800;
    internal const uint ProcessQueryLimitedInformation = 0x1000;
    internal const uint IdlePriorityClass = 0x0000_0040;
    internal const uint RealtimePriorityClass = 0x00000100;
    internal const uint ProcessPowerThrottlingExecutionSpeed = 0x1;
    internal const uint ProcessPowerThrottlingCurrentVersion = 1;
}

internal enum ProcessInformationClass
{
    ProcessPowerThrottling = 4,
}

[StructLayout(LayoutKind.Sequential)]
internal struct ProcessPowerThrottlingState
{
    public uint Version;
    public uint ControlMask;
    public uint StateMask;
}

internal static class NativeMethods
{
    internal const int ErrorInsufficientBuffer = 122;

    /// <summary>ERROR_NOT_ALL_ASSIGNED — common from AdjustTokenPrivileges when the token lacks SeDebugPrivilege.</summary>
    internal const int ErrorNotAllAssigned = 1308;

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern nint GetCurrentProcess();

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool OpenProcessToken(nint processHandle, uint desiredAccess, out nint tokenHandle);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool LookupPrivilegeValue(string? lpSystemName, string lpName, out Luid lpLuid);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool AdjustTokenPrivileges(nint tokenHandle, [MarshalAs(UnmanagedType.Bool)] bool disableAllPrivileges, ref TokenPrivileges newState, uint bufferLength, nint previousState, nint returnLength);

    [StructLayout(LayoutKind.Sequential)]
    internal struct Luid
    {
        internal uint LowPart;
        internal int HighPart;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct TokenPrivileges
    {
        internal int PrivilegeCount;
        internal Luid Luid;
        internal uint Attributes;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern nint OpenProcess(uint dwDesiredAccess, [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, uint dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool CloseHandle(nint hObject);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern uint GetPriorityClass(nint hProcess);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetPriorityClass(nint hProcess, uint dwPriorityClass);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetProcessInformation(
        nint hProcess,
        ProcessInformationClass processInformationClass,
        ref ProcessPowerThrottlingState processInformation,
        uint processInformationSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetProcessInformation(
        nint hProcess,
        ProcessInformationClass processInformationClass,
        ref ProcessPowerThrottlingState processInformation,
        uint processInformationSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetLogicalProcessorInformationEx(int relationshipType, nint buffer, ref uint returnedLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetProcessAffinityMask(nint hProcess, nuint dwProcessAffinityMask);
}
