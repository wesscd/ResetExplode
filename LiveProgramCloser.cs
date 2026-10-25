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
        => CloseWithResult(item, access).Detail;

    public static WorkbenchResult CloseWithResult(LiveOptimizationItem item, ILiveProgramAccess access)
    {
        WorkbenchResult Result(WorkbenchResultState state, string detail, string after) => new(state, detail, "Processo presente na análise", after, DateTimeOffset.Now);
        if (!item.Available || item.Kind != "Aplicativo" || !int.TryParse(item.Id, out int pid)) return Result(WorkbenchResultState.Blocked, "Item não autorizado para fechamento.", "Preservado");
        var current = access.Inspect(pid);
        if (current == null) return Result(WorkbenchResultState.Unchanged, "Processo já encerrado; nenhuma ação.", "Ausente antes da tentativa");
        var process = current.Process;
        if (!current.SecurityCheckComplete || current.HostsServices || process.SecurityProduct ||
            LiveOptimizationAudit.SecurityMetadata(process.Company + " " + process.Path + " " + process.Name)) return Result(WorkbenchResultState.Blocked, "Preservado: segurança ou vínculo com serviço não permitem fechamento.", "Preservado");
        if (!LiveOptimizationAudit.KnownProgram(process.Name) || !process.CurrentSession || !process.HasWindow ||
            item.StartedUtcTicks == null || process.StartedUtcTicks != item.StartedUtcTicks || string.IsNullOrEmpty(item.Path) ||
            !process.Path.Equals(item.Path, StringComparison.OrdinalIgnoreCase)) return Result(WorkbenchResultState.Blocked, "Identidade ou estado mudou desde a análise. Reanalise antes de fechar.", "Preservado");
        string detail = access.RequestClose(process);
        // A mensagem do pedido não comprova que o processo saiu. Uma segunda leitura é obrigatória.
        try
        {
            return access.Inspect(pid) == null
                ? Result(WorkbenchResultState.Applied, detail, "Processo ausente na verificação")
                : Result(WorkbenchResultState.Pending, detail, "PID ainda presente; encerramento não confirmado");
        }
        catch (Exception ex) { return Result(WorkbenchResultState.Pending, detail + " Verificação indisponível: " + ex.Message, "Não confirmado"); }
    }
}
