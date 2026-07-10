# EfficiencyModeScanner

A **.NET 8** Windows console tool that:

1. **Scans** running processes for **Task Manager–style efficiency mode** (idle priority + EcoQoS / CPU power throttling) and writes results into `appsettings.json`.
2. Optionally **sets processor affinity** for listed processes to **E-cores only** (hybrid Intel CPUs), using live PID resolution by **process name**.
3. Optionally **applies Efficiency Mode** to listed processes by name (IDLE priority class + EcoQoS power throttling).
4. Optionally **sets Realtime priority** for listed processes by name (REALTIME priority class).

The solution file is `EfficiencyModeScanner.sln` (open in Visual Studio).

---

## Requirements

| Item | Details |
|------|---------|
| OS | **Windows** (uses Win32 APIs). |
| Runtime | **.NET 8** SDK for build; target framework `net8.0-windows`. |
| CPU (for `-ecore`) | Hybrid topology with **non-zero `EfficiencyClass`** for cores, or affinity logic falls back to documented errors. E-core mask is built for **processor group 0** only. |

---

## Quick start

```powershell
cd path\to\EfficiencyModeScanner
dotnet build
dotnet run              # print help (no arguments)
dotnet run -- -v        # scan + update appsettings.json -> processes
dotnet run -- -ecore    # apply E-core affinity from appsettings.json -> processes
dotnet run -- -eq       # apply Efficiency Mode from appsettings.json -> processes
dotnet run -- -rt       # set Realtime priority from appsettings.json -> realtimeProcesses
dotnet run -- -v -ecore
dotnet run -- -v -eq
dotnet run -- -v -rt
```

From Visual Studio: set **Application arguments** (e.g. `-v`, `-ecore`, `-eq`, or `-rt`) in project **Debug** properties.

---

## Command-line reference

| Invocation | Behavior |
|------------|----------|
| *(no arguments)* | Prints built-in help (includes version) and exits **0**. |
| **`-v`** | **Scan mode**: enumerates processes, detects efficiency mode, then updates `appsettings.json` (`processes` + `generatedAt`). **Requires** `appsettings.json` in the executable folder. |
| **`-ecore`** | **Affinity mode**: reads `appsettings.json` -> `processes`, resolves **current PIDs** by name, sets affinity to the computed E-core mask for group 0. |
| **`-eq`** | **Efficiency mode apply**: reads `appsettings.json` -> `processes`, resolves **current PIDs** by name, applies Efficiency Mode (`IDLE_PRIORITY_CLASS` + `ProcessPowerThrottling` execution speed flag). |
| **`-rt`** | **Realtime priority mode**: reads `appsettings.json` -> `realtimeProcesses`, resolves **current PIDs** by name, sets priority class to **REALTIME** (0x100). |
| **`-v` `-ecore`** | Runs scan first, then E-core affinity (same run). |
| **`-v` `-eq`** | Runs scan first, then applies Efficiency Mode (same run). |
| **`-v` `-rt`** | Runs scan first, then sets Realtime priority (same run). |

Any argument other than `-v`, `-ecore`, `-eq`, or `-rt` (case-insensitive) is **invalid**: message on stderr, help text, exit code **1**.

---

## Configuration: `appsettings.json`

Placed next to the built executable (the project copies it with **Copy to Output Directory: PreserveNewest**).

### `processExclusions` (string array)

Process names to **skip** when applying **`-ecore`**, **`-eq`**, or **`-rt`**. Matching is **case-insensitive** and uses the same convention as `System.Diagnostics.Process.ProcessName` (**no `.exe` suffix**).

Example:

```json
{
  "processExclusions": [ "MsMpEng", "SearchIndexer" ]
}
```

- **`-v`**: exclusions are **not** used to filter the scan; they are **snapshotted** into the output JSON under `processExclusions` for documentation and reuse.
- **`-ecore` / `-eq` / `-rt`**: any process name in the target list that matches an exclusion entry is **skipped entirely** (all instances of that name).

### `processes` (string array)

Distinct process names found in efficiency mode (written by `-v`, consumed by `-ecore` / `-eq`).

### `realtimeProcesses` (string array)

Distinct process names to set to **REALTIME priority class** (consumed by `-rt`).

### `generatedAt` (ISO 8601 string)

UTC timestamp of last successful `-v` refresh.

---

## `appsettings.json` schema (current)

Written/updated by **`-v`** and read by **`-ecore` / `-eq` / `-rt`**:

| Property | Type | Description |
|----------|------|-------------|
| `generatedAt` | ISO 8601 string | UTC timestamp when the file was written. |
| `processExclusions` | `string[]` | Process names to skip for `-ecore` / `-eq` / `-rt` (trimmed, distinct, sorted by app behavior). |
| `realtimeProcesses` | `string[]` | **Distinct** process names to set to REALTIME priority (sorted case-insensitively). |
| `processes` | `string[]` | **Distinct** process names in efficiency mode, sorted case-insensitively. **No `pid` field** (PIDs change across reboots). |

Example:

```json
{
  "generatedAt": "2026-04-23T12:00:00.0000000+00:00",
  "processExclusions": [],
  "processes": [ "chrome", "ms-teams", "Widgets" ],
  "realtimeProcesses": [ "high-priority-app" ]
}
```

## How efficiency mode is detected (`-v`)

Aligned with Windows / Task Manager behavior for “efficiency mode”:

1. Open the target process with **`PROCESS_QUERY_LIMITED_INFORMATION`**.
2. **`GetPriorityClass`** must return **`IDLE_PRIORITY_CLASS`** (`0x40`).
3. **`GetProcessInformation`** with **`ProcessPowerThrottling`** must succeed, and **`StateMask`** must include **`PROCESS_POWER_THROTTLING_EXECUTION_SPEED`** (`0x1`).

Processes that cannot be opened (permissions, exit race) are skipped silently during the scan.

---

## How E-core affinity works (`-ecore`)

### Mask computation

- Uses **`GetLogicalProcessorInformationEx`** with **`RelationProcessorCore`**.
- For each logical core entry, reads **`EfficiencyClass`** (heterogeneous CPUs).
- Treats the **lowest** efficiency class as **E-core tier**, unions **group 0** affinity bits into one **`ulong`** mask.
- If all classes are **0** (homogeneous / no reporting), mask computation **fails** with an explanatory error.

### Applying affinity

1. Parse **`processes`** from `appsettings.json`.
2. For each distinct name (not in `processExclusions`), call **`Process.GetProcessesByName`** to get **current** instances.
3. For each instance, **`OpenProcess`** with **`PROCESS_SET_INFORMATION | PROCESS_QUERY_LIMITED_INFORMATION`**, then **`SetProcessAffinityMask`** with the E-core mask.

The tool attempts **`SeDebugPrivilege`** on startup (helps some cross-account scenarios when **elevated**). Many **SYSTEM / service** processes still return **Win32 error 5** without **Administrator** elevation.

**Error 87** (`ERROR_INVALID_PARAMETER`) on `OpenProcess` can appear for some processes (e.g. multi–processor-group edge cases).

---

## How Efficiency Mode apply works (`-eq`)

`-eq` uses the same name resolution flow as `-ecore`:

1. Parse **`processes`** from `appsettings.json`.
2. For each distinct name (not in `processExclusions`), call **`Process.GetProcessesByName`** to get **current** instances.
3. For each instance, **`OpenProcess`** with **`PROCESS_SET_INFORMATION | PROCESS_QUERY_LIMITED_INFORMATION`**.
4. Apply Efficiency Mode by:
   - **`SetPriorityClass(..., IDLE_PRIORITY_CLASS)`**
   - **`SetProcessInformation(ProcessPowerThrottling, ...)`** with
     `PROCESS_POWER_THROTTLING_EXECUTION_SPEED` in both `ControlMask` and `StateMask`.

It also attempts **`SeDebugPrivilege`** on startup (same behavior as `-ecore`).

---

## How Realtime priority works (`-rt`)

`-rt` uses the same name resolution flow as `-ecore` and `-eq`:

1. Parse **`realtimeProcesses`** from `appsettings.json`.
2. For each distinct name (not in `processExclusions`), call **`Process.GetProcessesByName`** to get **current** instances.
3. For each instance, **`OpenProcess`** with **`PROCESS_SET_INFORMATION | PROCESS_QUERY_LIMITED_INFORMATION`**.
4. Apply Realtime priority by:
   - **`SetPriorityClass(..., REALTIME_PRIORITY_CLASS)`** (0x100)

**WARNING**: Realtime priority can cause system unresponsiveness if too many processes or critical system processes are set to this priority. Use with caution.

It also attempts **`SeDebugPrivilege`** on startup (same behavior as `-ecore`).

---

## Permissions and security

| Scenario | Typical result |
|----------|----------------|
| Normal user, user-session apps | Often succeeds for same-user processes. |
| Services / SYSTEM / protected | **`OpenProcess` error 5** unless elevated and policy allows. |
| Elevation | Run Visual Studio / terminal **as Administrator** for best results. |

This tool **modifies** other processes’ CPU affinity. Use only on machines and accounts where you are authorized to do so.

---

## Limitations

- **Single processor group**: affinity is applied with **`SetProcessAffinityMask`**, which targets **group 0** for the mask built here. E-cores mapped only to other groups are not covered by this implementation.
- **Detection vs. Task Manager**: relies on OS-reported **efficiency class** and throttling state; extremely old or unusual SKUs may differ.
- **`-ecore` / `-eq` / `-rt`** do not use stored PIDs; they resolve current instances by name each run.
- **`-ecore`** does not validate that a process is still in “efficiency mode”; it only uses the **name list** produced earlier (or hand-edited JSON).

---

## Project layout

| Path | Role |
|------|------|
| `Program.cs` | CLI routing, settings load, orchestration. |
| `EfficiencyScanCommand.cs` | `-v` scan loop and JSON write. |
| `EcoreAffinityCommand.cs` | `-ecore` name resolution and affinity. |
| `EqModeCommand.cs` | `-eq` name resolution and Efficiency Mode apply. |
| `RealtimePriorityCommand.cs` | `-rt` name resolution and Realtime priority apply. |
| `ProcessModifier.cs` | Shared process modification logic. |
| `HybridCpuTopology.cs` | `GetLogicalProcessorInformationEx` parsing, E-core mask. |
| `NativeInterop.cs` | P/Invoke declarations and access-mask constants. |
| `PrivilegeHelper.cs` | `SeDebugPrivilege` enablement. |
| `AppSettings.cs` | `appsettings.json` model. |
| `Usage.cs` | Help text (mirrors much of this README at a high level). |
| `appsettings.json` | User configuration (exclusions). |
| `EfficiencyModeScanner.sln` | Visual Studio solution. |

---

## Further reading (Microsoft)

- [Reduce Process Interference with Task Manager Efficiency Mode](https://devblogs.microsoft.com/performance-diagnostics/reduce-process-interference-with-task-manager-efficiency-mode/)
- [Introducing EcoQoS](https://devblogs.microsoft.com/performance-diagnostics/introducing-ecoqos/)
- [`GetLogicalProcessorInformationEx`](https://learn.microsoft.com/en-us/windows/win32/api/sysinfoapi/nf-sysinfoapi-getlogicalprocessorinformationex)
- [`SetProcessAffinityMask`](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-setprocessaffinitymask)
- [`SetPriorityClass`](https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-setpriorityclass)
- [`SetProcessInformation`](https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-setprocessinformation)
