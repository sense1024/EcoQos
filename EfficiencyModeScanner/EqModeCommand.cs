using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace EfficiencyModeScanner;

internal static class EqModeCommand
{
    internal static Task RunAsync(IReadOnlyList<string> targetNames, IReadOnlyList<string> processExclusions)
    {
        uint openAccess = ProcessConstants.ProcessSetInformation | ProcessConstants.ProcessQueryLimitedInformation;
        var throttling = new ProcessPowerThrottlingState
        {
            Version = ProcessConstants.ProcessPowerThrottlingCurrentVersion,
            ControlMask = ProcessConstants.ProcessPowerThrottlingExecutionSpeed,
            StateMask = ProcessConstants.ProcessPowerThrottlingExecutionSpeed,
        };

        return ProcessModifier.RunAsync(
            targetNames,
            processExclusions,
            openAccess,
            handle =>
            {
                if (!NativeMethods.SetPriorityClass(handle, ProcessConstants.IdlePriorityClass)) return false;
                if (!NativeMethods.SetProcessInformation(handle, ProcessInformationClass.ProcessPowerThrottling, ref throttling, (uint)Marshal.SizeOf<ProcessPowerThrottlingState>())) return false;
                return true;
            },
            "Efficiency Mode");
    }
}

