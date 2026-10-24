using System.Diagnostics;
using System.Management;

namespace ResetExplode;

public sealed class WindowsLiveProgramAccess : ILiveProgramAccess
{
    public LiveProgramState? Inspect(int pid)
    {
        Process p;
        try { p = Process.GetProcessById(pid); } catch (ArgumentException) { return null; }
        using (p)
        {
            string path = p.MainModule?.FileName ?? "";
            var security = WindowsOptimizationInventory.SecurityPaths(out bool securityKnown);
            using var query = new ManagementObjectSearcher($"SELECT Name FROM Win32_Service WHERE ProcessId={pid}");
            query.Options.Timeout = TimeSpan.FromSeconds(5);
            using var services = query.Get();
            using var self = Process.GetCurrentProcess();
            var process = new AuditProcess(pid, p.ProcessName, p.StartTime.ToUniversalTime().Ticks, path,
                p.MainModule?.FileVersionInfo.CompanyName ?? "", p.SessionId == self.SessionId, p.MainWindowHandle != IntPtr.Zero,
                null, null, null, security.Contains(path));
            return new(process, services.Count > 0, securityKnown);
        }
    }
    public string RequestClose(AuditProcess expected)
    {
        using var process = Process.GetProcessById(expected.Pid);
        _ = process.SafeHandle; // mantém a identidade nativa durante a operação
        if (process.HasExited) return "Processo já encerrado.";
        if (process.StartTime.ToUniversalTime().Ticks != expected.StartedUtcTicks ||
            !string.Equals(process.MainModule?.FileName, expected.Path, StringComparison.OrdinalIgnoreCase)) return "Identidade mudou; processo preservado.";
        if (!process.CloseMainWindow()) return "Pedido não aceito ou sem janela; não houve encerramento forçado.";
        return process.WaitForExit(2500) ? "Aplicativo encerrado normalmente. Reabra manualmente quando precisar." :
            "Pedido enviado; aplicativo ainda aberto (pode aguardar confirmação ou salvar dados). Não foi forçado.";
    }
}
