using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace EfficiencyModeScanner;

internal static class EcoreAffinityCommand
{
    internal static Task RunAsync(IReadOnlyList<string> targetNames, ulong affinityMaskGroup0, IReadOnlyList<string> processExclusions)
    {
        if (targetNames.Count == 0)
        {
            Console.WriteLine("appsettings.json has no process names in \"processes\".");
            return Task.CompletedTask;
        }

        HashSet<string> excludedNames = new(
            processExclusions.Where(static s => !string.IsNullOrWhiteSpace(s)).Select(static s => s.Trim()),
            StringComparer.OrdinalIgnoreCase);

        nuint mask = (nuint)affinityMaskGroup0;
        Console.WriteLine($"E-core affinity mask (processor group 0) = 0x{affinityMaskGroup0:X}");
        Console.WriteLine("Resolving each name from JSON with Process.GetProcessesByName, then setting affinity on current PIDs.");
        if (excludedNames.Count > 0)
            Console.WriteLine($"Exclusions (skipped): {string.Join(", ", excludedNames.OrderBy(static s => s, StringComparer.OrdinalIgnoreCase))}");
        Console.WriteLine();

        bool isElevated = new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
        bool seDebugEnabled = PrivilegeHelper.TryEnableSeDebugPrivilege();

        if (!isElevated)
            Console.WriteLine("Note: not running elevated; OpenProcess often returns error 5 (access denied) for many SYSTEM / service processes.");
        else if (!seDebugEnabled)
            Console.WriteLine("Note: running elevated but SeDebugPrivilege could not be enabled; some protected processes may still fail.");
        Console.WriteLine();

        uint openAccess = ProcessConstants.ProcessSetInformation | ProcessConstants.ProcessQueryLimitedInformation;

        foreach (string processName in targetNames)
        {
            if (excludedNames.Contains(processName))
            {
                Console.WriteLine($"Skipped (exclusion list): {processName}");
                continue;
            }

            Process[] running;
            try
            {
                running = Process.GetProcessesByName(processName);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"'{processName}': failed to enumerate processes ({ex.Message}).");
                continue;
            }

            if (running.Length == 0)
            {
                Console.WriteLine($"Not running, skipped: {processName}");
                continue;
            }

            foreach (Process proc in running)
            {
                using (proc)
                {
                    int pid = proc.Id;
                    nint handle = NativeMethods.OpenProcess(openAccess, false, (uint)pid);
                    if (handle == 0)
                    {
                        int openErr = Marshal.GetLastWin32Error();
                        string errText = openErr == 0 ? "no error code (process may have exited)" : openErr.ToString();
                        string hint = openErr == 5
                            ? " (typical for Windows services / SYSTEM; run this program as Administrator)"
                            : openErr == 87
                                ? " (possible multi processor group or unsupported process state)"
                                : string.Empty;
                        Console.Error.WriteLine($"'{processName}' PID {pid}: OpenProcess failed (error {errText}){hint}");
                        continue;
                    }

                    try
                    {
                        if (!NativeMethods.SetProcessAffinityMask(handle, mask))
                        {
                            int err = Marshal.GetLastWin32Error();
                            Console.Error.WriteLine($"'{processName}' PID {pid}: SetProcessAffinityMask failed (error {err}). Possible multi-group, insufficient privilege, or policy.");
                            continue;
                        }

                        Console.WriteLine($"Set affinity: {processName} (PID={pid})");
                    }
                    finally
                    {
                        _ = NativeMethods.CloseHandle(handle);
                    }
                }
            }
        }

        return Task.CompletedTask;
    }
}
