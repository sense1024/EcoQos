using System.Reflection;

namespace EfficiencyModeScanner;

internal static class Usage
{
    internal static string GetVersionString()
    {
        Assembly? asm = Assembly.GetEntryAssembly();
        string? informational = asm?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informational))
            return informational.Trim();

        Version? v = asm?.GetName().Version;
        return v?.ToString() ?? "0.0.0";
    }

    internal static void Print(TextWriter writer)
    {
        string version = GetVersionString();
        writer.WriteLine(
            $"""
            EfficiencyModeScanner version {version}

            Arguments
              (no arguments)
                Show this help and exit.

              -v
                Scan all processes and list those in Task Manager-style efficiency mode
                (IDLE priority class + PROCESS_POWER_THROTTLING_EXECUTION_SPEED / EcoQoS).
                Writes unique process names to appsettings.json under "processes"
                and updates "generatedAt" timestamp.

                Requires appsettings.json next to the executable. processExclusions
                are not used to filter the scan.

              -ecore
                Reads process names from appsettings.json -> "processes" (no .exe suffix),
                resolves current PIDs at runtime with Process.GetProcessesByName, then sets
                each PID's processor affinity to E-cores only (from CPU topology
                EfficiencyClass).

              -eq
                Reads process names from appsettings.json -> "processes" (no .exe suffix),
                resolves current PIDs at runtime with Process.GetProcessesByName, then
                applies Efficiency Mode to each PID by setting:
                1) IDLE priority class, and
                2) ProcessPowerThrottling execution speed flag (EcoQoS).

                On startup, appsettings.json (if present) supplies processExclusions: if a
                process name matches any entry (case-insensitive), all instances of that
                name are skipped for -ecore and -eq changes.

                If appsettings.json is missing, exclusions are empty; E-core mask must still
                be resolved successfully.

                For Windows services, SYSTEM, or protected processes, OpenProcess often returns
                error 5 without elevation; run as Administrator (the app tries to enable
                SeDebugPrivilege to widen access for debuggable processes).

            You may combine flags: for example -v then -ecore or -eq refreshes appsettings first,
            then applies runtime changes.
            Examples:
              EfficiencyModeScanner.exe
              EfficiencyModeScanner.exe -v
              EfficiencyModeScanner.exe -ecore
              EfficiencyModeScanner.exe -eq
              EfficiencyModeScanner.exe -v -ecore
              EfficiencyModeScanner.exe -v -eq

            Paths
              Default is the executable directory (e.g. bin\Debug\net8.0-windows\ under a
              Debug build). appsettings.json is read from this directory.
            """);
    }
}
