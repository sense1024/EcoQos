using System.Diagnostics;
using System.Runtime.InteropServices;

namespace EfficiencyModeScanner;

internal static class EfficiencyScanCommand
{
    internal static Task<string[]> RunAsync()
    {
        Console.WriteLine("Scanning for processes in efficiency mode (IDLE + CPU throttling / EcoQoS)…");
        Console.WriteLine();

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var process in Process.GetProcesses())
        {
            try
            {
                nint handle = NativeMethods.OpenProcess(ProcessConstants.ProcessQueryLimitedInformation, false, (uint)process.Id);
                if (handle == 0)
                    continue;

                try
                {
                    uint priorityClass = NativeMethods.GetPriorityClass(handle);
                    if (priorityClass == 0 || priorityClass != ProcessConstants.IdlePriorityClass)
                        continue;

                    var throttling = new ProcessPowerThrottlingState
                    {
                        Version = ProcessConstants.ProcessPowerThrottlingCurrentVersion,
                    };

                    if (!NativeMethods.GetProcessInformation(
                            handle,
                            ProcessInformationClass.ProcessPowerThrottling,
                            ref throttling,
                            (uint)Marshal.SizeOf<ProcessPowerThrottlingState>()))
                        continue;

                    bool executionSpeedThrottled = (throttling.StateMask & ProcessConstants.ProcessPowerThrottlingExecutionSpeed) != 0;
                    if (!executionSpeedThrottled)
                        continue;

                    names.Add(process.ProcessName);
                }
                finally
                {
                    _ = NativeMethods.CloseHandle(handle);
                }
            }
            catch
            {
                // No access or process exited; skip
            }
            finally
            {
                process.Dispose();
            }
        }

        string[] processList = names.OrderBy(static s => s, StringComparer.OrdinalIgnoreCase).ToArray();
        Console.WriteLine($"{processList.Length} unique process name(s).");

        return Task.FromResult(processList);
    }
}
