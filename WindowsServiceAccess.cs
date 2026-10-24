using System.ComponentModel;
using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace ResetExplode;

/// <summary>Somente serviços locais da lista fechada; usa SCM, sem scripts, ACLs ou encerramento de processos.</summary>
public sealed class WindowsServiceAccess : IGamingServiceAccess
{
    private static void Validate(string name)
    {
        if (GamingServiceCatalog.Find(name) == null || !Regex.IsMatch(name, @"^[A-Za-z0-9_.-]+$"))
            throw new InvalidOperationException("Serviço fora do catálogo validado.");
    }

    public GamingServiceSnapshot? Read(string name)
    {
        Validate(name);
        try
        {
            using var service = new ManagementObject($"Win32_Service.Name='{name}'");
            service.Options.Timeout = TimeSpan.FromSeconds(5);
            service.Get();
            int start = service["StartMode"]?.ToString() switch { "Auto" => 2, "Manual" => 3, "Disabled" => 4, _ => -1 };
            string type = service["ServiceType"]?.ToString() ?? "";
            using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\" + name);
            bool delayed = start == 2 && key?.GetValue("DelayedAutoStart") is int value && value != 0;
            var dependents = new List<string>();
            using var search = new ManagementObjectSearcher($"ASSOCIATORS OF {{Win32_Service.Name='{name}'}} WHERE AssocClass=Win32_DependentService Role=Antecedent");
            search.Options.Timeout = TimeSpan.FromSeconds(5);
            using var found = search.Get();
            foreach (ManagementObject dependent in found)
                using (dependent)
                    if (dependent["Name"] is string id) dependents.Add(id);
            return new(name, start, delayed, service["State"]?.ToString() ?? "Unknown", service["AcceptStop"] is true,
                type is "Own Process" or "Share Process", dependents);
        }
        catch (ManagementException ex) when (ex.ErrorCode == ManagementStatus.NotFound) { return null; }
    }

    public void Configure(string name, int start, bool delayed)
    {
        Validate(name);
        if (start is < 2 or > 4) throw new ArgumentOutOfRangeException(nameof(start));
        IntPtr manager = OpenSCManager(null, null, 1);
        if (manager == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
        try
        {
            IntPtr service = OpenService(manager, name, 2); // SERVICE_CHANGE_CONFIG
            if (service == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
            try
            {
                if (!ChangeServiceConfig(service, uint.MaxValue, (uint)start, uint.MaxValue, null, null, IntPtr.Zero, null, null, null, null))
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                if (start == 2)
                {
                    int flag = delayed ? 1 : 0;
                    if (!ChangeServiceConfig2(service, 3, ref flag)) throw new Win32Exception(Marshal.GetLastWin32Error());
                }
            }
            finally { CloseServiceHandle(service); }
        }
        finally { CloseServiceHandle(manager); }
    }

    public void Stop(string name) => Invoke(name, "StopService", "Stopped");
    public void Start(string name) => Invoke(name, "StartService", "Running");
    private void Invoke(string name, string method, string desired)
    {
        Validate(name);
        // Reconfere dependentes antes da parada; nunca usa parada em cascata.
        var snapshot = Read(name) ?? throw new InvalidOperationException("Serviço não instalado.");
        if (snapshot.State == desired) return;
        if (method == "StopService" && (!snapshot.CanStop || snapshot.Dependents.Count > 0))
            throw new InvalidOperationException("Serviço não aceita parada isolada; preservado.");
        using var service = new ManagementObject($"Win32_Service.Name='{name}'");
        service.Options.Timeout = TimeSpan.FromSeconds(5);
        using var response = service.InvokeMethod(method, null, new InvokeMethodOptions { Timeout = TimeSpan.FromSeconds(15) });
        uint code = response?["ReturnValue"] is object returned ? Convert.ToUInt32(returned) : uint.MaxValue;
        if (code != 0) throw new InvalidOperationException($"O Windows recusou {method} (código {code}).");
        var clock = Stopwatch.StartNew();
        while (clock.Elapsed < TimeSpan.FromSeconds(15))
        {
            service.Get();
            if (service["State"]?.ToString() == desired) return;
            Thread.Sleep(150);
        }
        throw new TimeoutException("O serviço não confirmou o novo estado em 15 segundos. Backup mantido.");
    }

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr OpenSCManager(string? machine, string? database, uint access);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr OpenService(IntPtr manager, string name, uint access);
    [DllImport("advapi32.dll", EntryPoint = "ChangeServiceConfigW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool ChangeServiceConfig(IntPtr service, uint type, uint start, uint error, string? binary,
        string? group, IntPtr tag, string? dependencies, string? account, string? password, string? display);
    [DllImport("advapi32.dll", EntryPoint = "ChangeServiceConfig2W", SetLastError = true)]
    private static extern bool ChangeServiceConfig2(IntPtr service, uint level, ref int info);
    [DllImport("advapi32.dll")] private static extern bool CloseServiceHandle(IntPtr handle);
}
