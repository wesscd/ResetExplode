namespace ResetExplode;

public enum WorkbenchResultState { Applied, Restart, Unchanged, Failed, Pending, Blocked, Restored, Canceled }

// O resultado descreve uma tentativa com horário; não substitui uma nova leitura do PC.
public sealed record WorkbenchResult(WorkbenchResultState State, string Detail, string Before, string After, DateTimeOffset At)
{
    public string Label => State switch
    {
        WorkbenchResultState.Applied => "Aplicado e verificado",
        WorkbenchResultState.Restart => "Verificado · reiniciar",
        WorkbenchResultState.Unchanged => "Já estava ajustado",
        WorkbenchResultState.Failed => "Falhou / não confirmado",
        WorkbenchResultState.Pending => "Pendente de confirmação",
        WorkbenchResultState.Blocked => "Preservado / bloqueado",
        WorkbenchResultState.Restored => "Restauração concluída",
        _ => "Não executado · cancelado"
    };
    public string Summary => $"{At.ToLocalTime():dd/MM HH:mm:ss} · {Label}\nAntes: {Before}\nDepois: {After}\n{Detail}";
    public static WorkbenchResult Windows(OptimizationOutcome outcome, bool restart, string before, string after) => new(
        outcome.State switch { "applied" => restart ? WorkbenchResultState.Restart : WorkbenchResultState.Applied,
            "unchanged" => WorkbenchResultState.Unchanged, "unsupported" => WorkbenchResultState.Blocked, _ => WorkbenchResultState.Failed },
        outcome.Detail, before, after, DateTimeOffset.Now);
    public static WorkbenchResult Service(GamingServiceResult outcome, string before) => new(
        outcome.Status == "SEM ALTERAÇÃO" && outcome.Success ? WorkbenchResultState.Unchanged : outcome.Success ? WorkbenchResultState.Applied : outcome.Status == "BLOQUEADO" ? WorkbenchResultState.Blocked : WorkbenchResultState.Failed,
        outcome.Detail, "Na análise: " + before, outcome.Success ? "Parado · desabilitado (confirmado pelo Windows)" : "Não confirmado; consulte o motivo", DateTimeOffset.Now);
}

public sealed record WorkbenchAnalysisSummary(int Evaluated, int Recognized, int AtTarget, int Available, int Preserved)
{
    public static WorkbenchAnalysisSummary From(IEnumerable<LiveOptimizationItem> source)
    {
        var rows = source.ToList();
        return new(rows.Count, rows.Count(i => i.StateRecognized),
            rows.Count(i => i.AtTarget), rows.Count(i => !i.AtTarget && i.Available), rows.Count(i => !i.AtTarget && !i.Available));
    }
    public string Text => $"{Recognized} reconhecidos / {Evaluated} avaliados   ·   {AtTarget} já no alvo   ·   {Available} disponíveis   ·   {Preserved} preservados";
}
