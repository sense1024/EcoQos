using System.Runtime.Versioning;
using System.Text.Json;
using EfficiencyModeScanner;

[assembly: SupportedOSPlatform("windows")]

Console.OutputEncoding = System.Text.Encoding.UTF8;

string[] argv = Environment.GetCommandLineArgs();
string[] userArgs = argv.Skip(1).ToArray();

static bool IsSwitch(string a, string name) =>
    string.Equals(a, name, StringComparison.OrdinalIgnoreCase);

bool runScan = userArgs.Any(a => IsSwitch(a, "-v"));
bool runEcore = userArgs.Any(a => IsSwitch(a, "-ecore"));
bool runEq = userArgs.Any(a => IsSwitch(a, "-eq"));

foreach (string a in userArgs)
{
    if (IsSwitch(a, "-v") || IsSwitch(a, "-ecore") || IsSwitch(a, "-eq"))
        continue;

    Console.Error.WriteLine($"Invalid argument: {a}");
    Console.Error.WriteLine();
    Usage.Print(Console.Error);
    Environment.Exit(1);
}

if (!runScan && !runEcore && !runEq)
{
    Usage.Print(Console.Out);
    return;
}

string settingsPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "appsettings.json"));
AppSettings? appSettings = null;
string[] processExclusions = [];
string[] processNamesFromSettings = [];

if (!File.Exists(settingsPath))
{
    Console.Error.WriteLine($"Settings file not found: {settingsPath}");
    Environment.Exit(1);
}

string settingsJson = await File.ReadAllTextAsync(settingsPath);
var settingsDeserializeOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
appSettings = JsonSerializer.Deserialize<AppSettings>(settingsJson, settingsDeserializeOptions) ?? new AppSettings();
processExclusions = appSettings.ProcessExclusions ?? [];
processNamesFromSettings = appSettings.Processes ?? [];

string[] nonEmpty = processExclusions.Where(static s => !string.IsNullOrWhiteSpace(s)).Select(static s => s.Trim()).ToArray();
Console.WriteLine($"Loaded appsettings.json: processExclusions = [{string.Join(", ", nonEmpty.Select(s => $"\"{s}\""))}] ({nonEmpty.Length} entries)");
Console.WriteLine($"Loaded appsettings.json: processes = [{processNamesFromSettings.Length} entries]");

string[] normalizedExclusions = processExclusions
    .Where(static s => !string.IsNullOrWhiteSpace(s))
    .Select(static s => s.Trim())
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .OrderBy(static s => s, StringComparer.OrdinalIgnoreCase)
    .ToArray();

if (runScan)
{
    Console.WriteLine();
    string[] scannedProcesses = await EfficiencyScanCommand.RunAsync();
    processNamesFromSettings = scannedProcesses;

    var updatedSettings = new AppSettings
    {
        ProcessExclusions = normalizedExclusions,
        Processes = scannedProcesses,
        GeneratedAt = DateTimeOffset.UtcNow,
    };
    var saveOptions = new JsonSerializerOptions { WriteIndented = true };
    string updatedJson = JsonSerializer.Serialize(updatedSettings, saveOptions);
    await File.WriteAllTextAsync(settingsPath, updatedJson);
    Console.WriteLine($"Updated appsettings.json: {settingsPath}");
    Console.WriteLine();
}

if (runEcore)
{
    if (processNamesFromSettings.Length == 0)
    {
        Console.Error.WriteLine("appsettings.json has no process names. Run with -v first to refresh \"processes\".");
        Environment.Exit(1);
    }

    if (!HybridCpuTopology.TryGetEcoreAffinityMaskGroup0(out ulong affinityMask, out string? topoError))
    {
        Console.Error.WriteLine(topoError ?? "Could not obtain E-core affinity mask.");
        Environment.Exit(1);
    }

    Console.WriteLine($"Using process list from appsettings.json ({processNamesFromSettings.Length} entries)");
    Console.WriteLine();
    await EcoreAffinityCommand.RunAsync(processNamesFromSettings, affinityMask, normalizedExclusions);
}

if (runEq)
{
    if (processNamesFromSettings.Length == 0)
    {
        Console.Error.WriteLine("appsettings.json has no process names. Run with -v first to refresh \"processes\".");
        Environment.Exit(1);
    }

    Console.WriteLine($"Using process list from appsettings.json ({processNamesFromSettings.Length} entries)");
    Console.WriteLine();
    await EqModeCommand.RunAsync(processNamesFromSettings, normalizedExclusions);
}
