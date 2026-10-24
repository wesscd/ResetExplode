using System.Diagnostics;
using System.IO;
using System.Management;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace ResetExplode;

/// <summary>Inventário e amostragem locais; não modifica serviços nem encerra processos.</summary>
public static class WindowsOptimizationInventory
{
    private sealed record Point(int Pid, string Name, long? Started, string Path, string Company, bool CurrentSession,
        bool Window, double? CpuMs, long? Memory, double? IoBytes, long Timestamp);

    public static async Task<AuditInventory> CaptureAsync(CancellationToken ct, Action<string>? progress = null)
    {
        var warnings = new List<string>();
        var companies = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string Company(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || path.StartsWith(@"\\", StringComparison.Ordinal)) return "";
            if (!companies.TryGetValue(path, out var company))
            {
                try { company = FileVersionInfo.GetVersionInfo(path).CompanyName ?? ""; } catch { company = ""; }
                companies[path] = company;
            }
            return company;
        }
        progress?.Invoke("Lendo todos os serviços locais e suas dependências…");
        var dependencies = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        bool dependenciesComplete = true, servicesComplete = true;
        try
        {
            using var query = new ManagementObjectSearcher("SELECT Antecedent, Dependent FROM Win32_DependentService");
            query.Options.Timeout = TimeSpan.FromSeconds(8);
            using var found = query.Get();
            foreach (ManagementObject item in found)
                using (item)
                {
                    ct.ThrowIfCancellationRequested();
                    string Name(object? path) => Regex.Match(path?.ToString() ?? "", "Name=\"([^\"]+)\"", RegexOptions.IgnoreCase).Groups[1].Value;
                    string parent = Name(item["Antecedent"]), child = Name(item["Dependent"]);
                    if (parent.Length == 0 || child.Length == 0) { dependenciesComplete = false; continue; }
                    if (!dependencies.TryGetValue(parent, out var children)) dependencies[parent] = children = new();
                    children.Add(child);
                }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { dependenciesComplete = false; warnings.Add("Dependências incompletas: " + ex.Message); }
        var services = new List<AuditService>();
        try
        {
            using var query = new ManagementObjectSearcher("SELECT Name, DisplayName, Description, State, StartMode, PathName, ProcessId, ServiceType, AcceptStop FROM Win32_Service");
            query.Options.Timeout = TimeSpan.FromSeconds(8);
            using var found = query.Get();
            foreach (ManagementObject item in found)
                using (item)
                {
                    ct.ThrowIfCancellationRequested();
                    string name = item["Name"]?.ToString() ?? "";
                    if (name.Length == 0) { servicesComplete = false; continue; }
                    int start = item["StartMode"]?.ToString() switch { "Auto" => 2, "Manual" => 3, "Disabled" => 4, _ => -1 };
                    bool delayed = false;
                    try { using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\" + name); delayed = start == 2 && key?.GetValue("DelayedAutoStart") is int flag && flag != 0; }
                    catch { /* sem necessidade de estimar: a aplicação relê pelo backend validado */ }
                    string path = StartupManager.ExeFromCommand(item["PathName"]?.ToString() ?? "") ?? "";
                    var snapshot = new GamingServiceSnapshot(name, start, delayed, item["State"]?.ToString() ?? "Unknown", item["AcceptStop"] is true,
                        item["ServiceType"]?.ToString() is "Own Process" or "Share Process", dependencies.GetValueOrDefault(name)?.Distinct(StringComparer.OrdinalIgnoreCase).ToList() ?? new());
                    services.Add(new(snapshot, item["DisplayName"]?.ToString() ?? name, item["Description"]?.ToString() ?? "", path, Company(path), Convert.ToInt32(item["ProcessId"] ?? 0)));
                }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { servicesComplete = false; warnings.Add("Inventário de serviços incompleto: " + ex.Message); }
        var security = SecurityPaths(out bool securityComplete);
        if (!securityComplete) warnings.Add("Produtos de segurança não puderam ser consultados; fechamento automático de aplicativos indisponível.");
        progress?.Invoke("Amostrando CPU, RAM e E/S por aproximadamente 2 segundos…");
        var before = CaptureProcesses(Company, ct);
        await Task.Delay(2000, ct).ConfigureAwait(false);
        var after = CaptureProcesses(Company, ct);
        var processes = new List<AuditProcess>();
        var durations = new List<double>();
        foreach (var point in after.Values)
        {
            double? cpu = null, io = null;
            if (before.TryGetValue(point.Pid, out var previous) && point.Started != null && point.Started == previous.Started)
            {
                double seconds = Stopwatch.GetElapsedTime(previous.Timestamp, point.Timestamp).TotalSeconds;
                durations.Add(seconds);
                if (previous.CpuMs is double first && point.CpuMs is double last) cpu = LiveOptimizationAudit.CpuUsage(first, last, seconds, Environment.ProcessorCount);
                if (previous.IoBytes is double oldIo && point.IoBytes is double newIo && seconds > 0 && newIo >= oldIo) io = (newIo - oldIo) / seconds;
            }
            processes.Add(new(point.Pid, point.Name, point.Started, point.Path, point.Company, point.CurrentSession, point.Window,
                cpu, point.Memory, io, security.Contains(point.Path)));
        }
        return new(DateTimeOffset.Now, durations.Count > 0 ? durations.Average() : 0, services, processes, warnings, servicesComplete, dependenciesComplete, securityComplete);
    }

    private static Dictionary<int, Point> CaptureProcesses(Func<string, string> company, CancellationToken ct)
    {
        using var self = Process.GetCurrentProcess();
        int session = self.SessionId;
        var result = new Dictionary<int, Point>();
        var all = Process.GetProcesses();
        try
        {
            foreach (var process in all)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    int pid = process.Id;
                    if (pid <= 0) continue;
                    string name = process.ProcessName, path = "";
                    long? started = null, memory = null; double? cpu = null, io = null;
                    bool current = false, window = false;
                    try { started = process.StartTime.ToUniversalTime().Ticks; } catch { }
                    try { cpu = process.TotalProcessorTime.TotalMilliseconds; } catch { }
                    try { memory = process.WorkingSet64; } catch { }
                    try { path = process.MainModule?.FileName ?? ""; } catch { }
                    try { current = process.SessionId == session; window = process.MainWindowHandle != IntPtr.Zero; } catch { }
                    try { if (GetProcessIoCounters(process.Handle, out var counters)) io = (double)counters.ReadBytes + counters.WriteBytes + counters.OtherBytes; } catch { }
                    result[pid] = new(pid, name, started, path, company(path), current, window, cpu, memory, io, Stopwatch.GetTimestamp());
                }
                catch { /* processo pode sair durante a enumeração */ }
            }
        }
        finally { foreach (var process in all) process.Dispose(); }
        return result;
    }

    internal static HashSet<string> SecurityPaths(out bool complete)
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var query = new ManagementObjectSearcher(@"root\SecurityCenter2", "SELECT pathToSignedProductExe, pathToSignedReportingExe FROM AntiVirusProduct");
            query.Options.Timeout = TimeSpan.FromSeconds(5);
            using var found = query.Get();
            foreach (ManagementObject item in found)
                using (item)
                    foreach (string field in new[] { "pathToSignedProductExe", "pathToSignedReportingExe" })
                    {
                        string? path = StartupManager.ExeFromCommand(item[field]?.ToString() ?? "");
                        if (path != null) paths.Add(path);
                    }
            complete = true;
        }
        catch { complete = false; }
        return paths;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct IoCounters { public ulong Reads, Writes, Others, ReadBytes, WriteBytes, OtherBytes; }
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool GetProcessIoCounters(IntPtr process, out IoCounters counters);
}
