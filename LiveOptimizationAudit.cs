using System.ComponentModel;

namespace ResetExplode;

public sealed record AuditProcess(int Pid, string Name, long? StartedUtcTicks, string Path, string Company,
    bool CurrentSession, bool HasWindow, double? CpuPercent, long? WorkingSet, double? IoBytesPerSecond, bool SecurityProduct = false);
public sealed record AuditService(GamingServiceSnapshot Snapshot, string DisplayName, string Description,
    string Path, string Company, int Pid);
public sealed record AuditInventory(DateTimeOffset CapturedAt, double SampleSeconds, List<AuditService> Services,
    List<AuditProcess> Processes, List<string> Warnings, bool ServicesComplete, bool DependenciesComplete, bool SecurityInventoryComplete);

public sealed class LiveOptimizationItem : INotifyPropertyChanged
{
    public required string Kind { get; init; }
    public required string Id { get; init; }
    public required string Title { get; init; }
    public string Company { get; init; } = "Não identificado";
    public string Path { get; init; } = "";
    public string State { get; init; } = "";
    public string Classification { get; init; } = "";
    public string Action { get; init; } = "Preservar";
    public string Impact { get; init; } = "";
    public string Benefit { get; init; } = "";
    public string Evidence { get; init; } = "";
    public bool Available { get; init; }
    public int Pid { get; init; }
    public long? StartedUtcTicks { get; init; }
    public double? CpuPercent { get; init; }
    public long? WorkingSet { get; init; }
    public double? IoBytesPerSecond { get; init; }
    public bool SharedHost { get; init; }
    public string StateLabel { get; init; } = "";
    public string Recognition { get; init; } = "";
    public string BlockReason { get; init; } = "";
    public bool AtTarget { get; init; }
    public bool Suggested { get; init; }
    public bool Experimental { get; init; }
    public bool HasBackup { get; set; }
    public string DisplayState => string.IsNullOrEmpty(StateLabel) ? State : StateLabel;
    public string ReviewLabel => AtTarget ? "JÁ NO ALVO" : Classification.Contains("SEGURANÇA") ? "SEGURANÇA" : Available ? Experimental ? "EXPERIMENTAL" : Suggested ? "SUGESTÃO" : "CONDICIONAL" : Classification.Contains("NÃO VALIDADO") ? "SEM REGRA" : "PRESERVAR";
    public string AfterState => Selected ? Action : "Manter estado atual";
    public string RestoreInfo => HasBackup ? "Original guardado pelo aplicativo. Use RESTAURAR ITEM para recuperar." : "Nenhum original disponível para restaurar este item.";
    private string _lastResult = "";
    public string LastResult { get => _lastResult; set { _lastResult = value; PropertyChanged?.Invoke(this, new(nameof(LastResult))); } }
    public string Resources => Kind == "Windows" ? "Configuração do sistema; sem consumo isolado atribuível." : $"CPU {(CpuPercent is double cpu ? $"{cpu:0.00}%" : "—")} · RAM {(WorkingSet is long ram ? $"{ram / 1048576.0:0} MB" : "—")} · E/S {(IoBytesPerSecond is double io ? $"{io / 1048576.0:0.00} MB/s" : "—")}";
    public string Details => $"Estado atual: {State}\nAção proposta: {Action}\nImpacto: {Impact}\nBenefício esperado: {Benefit}\nEvidência: {Evidence}\nExecutável: {Path}\nFabricante declarado: {Company} (metadados; assinatura não verificada)";
    private bool _selected;
    public bool Selected
    {
        get => _selected;
        set { bool next = Available && value; if (_selected == next) return; _selected = next; PropertyChanged?.Invoke(this, new(nameof(Selected))); PropertyChanged?.Invoke(this, new(nameof(AfterState))); }
    }
    public event PropertyChangedEventHandler? PropertyChanged;
}

public static class LiveOptimizationAudit
{
    private static readonly Dictionary<string, string> Programs = new(StringComparer.OrdinalIgnoreCase)
    {
        ["OneDrive"] = "Sincronização do OneDrive será interrompida enquanto o aplicativo estiver fechado.",
        ["GoogleDriveFS"] = "Sincronização e acesso a arquivos sob demanda do Google Drive podem ser interrompidos.",
        ["Dropbox"] = "Sincronização do Dropbox será interrompida.",
        ["Teams"] = "Chamadas, mensagens e notificações do Teams serão interrompidas.",
        ["ms-teams"] = "Chamadas, mensagens e notificações do Teams serão interrompidas.",
        ["Skype"] = "Chamadas e mensagens do Skype serão interrompidas.",
        ["Discord"] = "Voz, mensagens e overlay do Discord serão interrompidos.",
        ["chrome"] = "Abas, downloads e aplicativos web do Chrome poderão ser fechados. Salve seu trabalho.",
        ["msedge"] = "Abas, downloads e aplicativos web do Edge poderão ser fechados. Salve seu trabalho.",
        ["firefox"] = "Abas, downloads e aplicativos web do Firefox poderão ser fechados. Salve seu trabalho.",
        ["Spotify"] = "Reprodução e downloads do Spotify serão interrompidos."
    };
    public static bool KnownProgram(string name) => Programs.ContainsKey(name);
    public static bool SecurityMetadata(string text) => new[] { "defender", "antivirus", "anti-virus", "antimalware", "anti-malware", "kaspersky", "eset", "avast", "avira", "bitdefender", "symantec", "malwarebytes", "comodo", "crowdstrike", "sentinelone", "sophos", "norton", "mcafee" }
        .Any(part => text.Contains(part, StringComparison.OrdinalIgnoreCase));

    public static double? CpuUsage(double beforeMs, double afterMs, double elapsedSeconds, int processors)
        => elapsedSeconds > 0 && processors > 0 && afterMs >= beforeMs
            ? Math.Clamp((afterMs - beforeMs) / (elapsedSeconds * 1000 * processors) * 100, 0, 100) : null;

    public static List<LiveOptimizationItem> Build(AuditInventory inventory)
    {
        var processes = inventory.Processes.ToDictionary(p => p.Pid);
        var hosts = inventory.Services.Where(s => s.Pid > 0).GroupBy(s => s.Pid).ToDictionary(g => g.Key, g => g.Count());
        var rows = new List<LiveOptimizationItem>();
        foreach (var service in inventory.Services)
        {
            var s = service.Snapshot;
            var rule = GamingServiceCatalog.Find(s.Name);
            processes.TryGetValue(service.Pid, out var process);
            bool shared = hosts.GetValueOrDefault(service.Pid) > 1;
            bool protectedIdentity = process?.SecurityProduct == true || SecurityMetadata(service.Company + " " + service.DisplayName + " " + service.Path);
            string? block = rule == null ? GamingServiceCatalog.Reason(s.Name)
                : protectedIdentity ? "Identificado como relacionado a segurança; preservado."
                : !inventory.ServicesComplete || !inventory.DependenciesComplete ? "Inventário/dependências incompletos; não é possível validar a alteração."
                : !s.IsUserService || s.Start is < 2 or > 4 ? "Tipo de serviço não suportado."
                : s.State is not ("Running" or "Stopped") ? "Estado transitório; aguarde e analise novamente."
                : s.Dependents.Count > 0 ? "Dependentes instalados: " + string.Join(", ", s.Dependents)
                : s.State == "Running" && !s.CanStop ? "Não aceita parada isolada."
                : s.Start == 4 && s.State == "Stopped" ? "Já desabilitado e parado."
                : null;
            string startup = s.Start switch { 2 => s.Delayed ? "automático atrasado" : "automático", 3 => "manual", 4 => "desabilitado", _ => "não identificado" };
            rows.Add(new()
            {
                Kind = "Serviço", Id = s.Name, Title = service.DisplayName + " · " + s.Name,
                Company = Company(service.Company), Path = service.Path,
                State = $"{startup} · {s.State} · PID {service.Pid}",
                Classification = protectedIdentity ? "PRESERVAR SEGURANÇA" : GamingServiceCatalog.Classification(s.Name),
                Available = block == null,
                StateLabel = (s.State == "Running" ? "Em execução" : s.State == "Stopped" ? "Parado" : s.State) + " · " + startup,
                Recognition = rule == null ? "Instalado · sem regra validada" : "Instalado · catálogo validado",
                BlockReason = block ?? "Pode ser selecionado. Confira a função que será interrompida.",
                AtTarget = rule != null && !protectedIdentity && s.Start == 4 && s.State == "Stopped", Suggested = rule?.Suggested == true,
                Action = block == null ? "Desabilitar inicialização e parar o serviço" : "Preservar",
                Impact = rule?.Impact ?? GamingServiceCatalog.Reason(s.Name),
                Benefit = block != null ? "Nenhuma alteração proposta: " + block : s.State != "Running"
                    ? "Nenhuma carga atual atribuível. A mudança apenas impede início futuro e pode remover a função descrita."
                    : shared ? "Consumo individual não conhecido: o processo hospeda outros serviços. Não há estimativa de recursos liberados."
                    : Benefit(process),
                Evidence = shared ? $"Host compartilhado por {hosts[service.Pid]} serviços. Os valores exibidos são do processo inteiro, não deste serviço." : process == null
                    ? "Sem processo ativo ou sem leitura disponível; consumo desconhecido, não zero." : "Medição do processo hospedeiro nesta amostra. E/S inclui operações do processo e não equivale somente a disco.",
                Pid = service.Pid, SharedHost = shared, StartedUtcTicks = process?.StartedUtcTicks,
                CpuPercent = process?.CpuPercent, WorkingSet = process?.WorkingSet, IoBytesPerSecond = process?.IoBytesPerSecond
            });
        }
        foreach (var p in inventory.Processes.Where(p => p.CurrentSession))
        {
            bool known = Programs.TryGetValue(p.Name, out string? impact);
            bool security = p.SecurityProduct || SecurityMetadata(p.Company + " " + p.Path + " " + p.Name);
            string? block = security ? "Produto de segurança preservado."
                : !known ? "Software não validado neste catálogo; revisar manualmente."
                : !inventory.ServicesComplete || !inventory.SecurityInventoryComplete ? "Não foi possível validar vínculos com serviços e produtos de segurança."
                : hosts.ContainsKey(p.Pid) ? "O processo hospeda serviço(s); não será fechado como aplicativo."
                : !p.HasWindow ? "Sem janela principal: não será encerrado à força. Use a opção de sair no próprio aplicativo."
                : p.StartedUtcTicks == null || string.IsNullOrEmpty(p.Path) ? "Identidade do processo incompleta; não será fechado."
                : null;
            rows.Add(new()
            {
                Kind = "Aplicativo", Id = p.Pid.ToString(), Title = p.Name + $" · PID {p.Pid}",
                Company = Company(p.Company), Path = p.Path, State = "Em execução na sessão atual",
                Classification = security ? "PRESERVAR SEGURANÇA" : known ? "SOFTWARE OPCIONAL · REVISAR" : "NÃO VALIDADO · PRESERVAR",
                Action = block == null ? "Solicitar fechamento normal da janela" : "Preservar / revisar manualmente",
                Impact = impact ?? "A função deste programa e suas dependências não foram validadas; nenhuma alteração automática.",
                Benefit = block == null ? Benefit(p) : block,
                Evidence = "Amostra do processo. RAM é working set, pode incluir páginas compartilhadas e não é memória garantida para liberação. E/S não equivale somente a disco.",
                Available = block == null, Pid = p.Pid, StartedUtcTicks = p.StartedUtcTicks,
                StateLabel = "Em execução", Recognition = known ? "Processo conhecido · sessão atual" : "Processo detectado · sem regra validada",
                BlockReason = block ?? "Pode solicitar fechamento normal. Salve seu trabalho antes.",
                CpuPercent = p.CpuPercent, WorkingSet = p.WorkingSet, IoBytesPerSecond = p.IoBytesPerSecond,
                SharedHost = hosts.ContainsKey(p.Pid)
            });
        }
        return rows.OrderByDescending(r => r.Available).ThenByDescending(r => r.CpuPercent ?? -1).ThenBy(r => r.Title).ToList();
    }

    private static string Company(string company) => string.IsNullOrWhiteSpace(company) ? "Não identificado" : company;
    private static string Benefit(AuditProcess? process) => process?.CpuPercent == null && process?.IoBytesPerSecond == null
        ? "Sem amostra suficiente para avaliar carga. Ganho em FPS desconhecido."
        : process?.CpuPercent >= 1 || process?.IoBytesPerSecond >= 1048576
            ? "Pode reduzir concorrência por recursos observada nesta amostra. Confirme durante o jogo; não há previsão de ganho em FPS."
            : "Pouca atividade de CPU/E/S observada nesta amostra. Ganho em FPS não demonstrado; RAM ocupada pode importar se houver pressão de memória.";

    public static string Preview(IEnumerable<LiveOptimizationItem> items)
    {
        var selected = items.Where(i => i.Selected && i.Available).ToList();
        if (selected.Count == 0) return "Selecione itens disponíveis para montar a prévia. Nada foi alterado.";
        int shared = selected.Count(i => i.SharedHost);
        return $"{selected.Count} ações: {selected.Count(i => i.Kind == "Windows")} ajustes do Windows, {selected.Count(i => i.Kind == "Serviço")} serviços e {selected.Count(i => i.Kind == "Aplicativo")} aplicativos.\n" +
            (shared > 0 ? $"{shared} serviço(s) em hosts compartilhados: não é possível atribuir liberação de recursos individualmente.\n" : "") +
            "Consumo observado não é ganho previsto. FPS e RAM liberada precisam ser medidos após a mudança.\n\n" +
            string.Join("\n\n", selected.Select(i => $"{i.Title}\nAntes: {i.State}\nDepois: {i.Action}\nImpacto: {i.Impact}\nBenefício esperado: {i.Benefit}\n{(i.SharedHost ? "HOST COMPARTILHADO · " : "")}{i.Resources}"));
    }
}
