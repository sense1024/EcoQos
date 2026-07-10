using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace EfficiencyModeScanner;

internal static class RealtimePriorityCommand
{
    internal static Task RunAsync(IReadOnlyList<string> targetNames, IReadOnlyList<string> processExclusions)
    {
        uint openAccess = ProcessConstants.ProcessSetInformation | ProcessConstants.ProcessQueryLimitedInformation;
        return ProcessModifier.RunAsync(
            targetNames,
            processExclusions,
            openAccess,
            handle => NativeMethods.SetPriorityClass(handle, ProcessConstants.RealtimePriorityClass),
            "Realtime priority");
    }
}
