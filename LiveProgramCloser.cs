namespace ResetExplode;

public sealed record LiveProgramState(AuditProcess Process, bool HostsServices, bool SecurityCheckComplete);
public interface ILiveProgramAccess
{
    LiveProgramState? Inspect(int pid);
    string RequestClose(AuditProcess expected);
}

public static class LiveProgramCloser
{
    public static string Close(LiveOptimizationItem item, ILiveProgramAccess access)
    {
        if (!item.Available || item.Kind != "Aplicativo" || !int.TryParse(item.Id, out int pid)) return "Item não autorizado para fechamento.";
        var current = access.Inspect(pid);
        if (current == null) return "Processo já encerrado; nenhuma ação.";
        var process = current.Process;
        if (!current.SecurityCheckComplete || current.HostsServices || process.SecurityProduct ||
            LiveOptimizationAudit.SecurityMetadata(process.Company + " " + process.Path + " " + process.Name)) return "Preservado: segurança ou vínculo com serviço não permitem fechamento.";
        if (!LiveOptimizationAudit.KnownProgram(process.Name) || !process.CurrentSession || !process.HasWindow ||
            item.StartedUtcTicks == null || process.StartedUtcTicks != item.StartedUtcTicks || string.IsNullOrEmpty(item.Path) ||
            !process.Path.Equals(item.Path, StringComparison.OrdinalIgnoreCase)) return "Identidade ou estado mudou desde a análise. Reanalise antes de fechar.";
        return access.RequestClose(process);
    }
}
