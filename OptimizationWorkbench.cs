namespace ResetExplode;

public static class OptimizationWorkbench
{
    // "Ativo" descreve o recurso real, não o booleano interno do ajuste de otimização.
    private static readonly Dictionary<string, (string Title, string On, string Off, string Action)> Definitions = new()
    {
        ["gamemode"] = ("Modo de Jogo do Windows", "Ativado", "Desativado", "Ativar Modo de Jogo"),
        ["gamedvr"] = ("Captura de jogos em segundo plano", "Captura desativada", "Captura ativada ou parcial", "Desativar captura em segundo plano"),
        ["power"] = ("Plano de energia", "Alto desempenho ativo", "Outro plano ativo", "Ativar Alto desempenho"),
        ["hags"] = ("Agendamento de GPU por hardware", "Ativado", "Desativado", "Ativar agendamento de GPU"),
        ["windowed"] = ("Otimizações para jogos em janela", "Ativadas", "Desativadas", "Ativar otimizações em janela"),
        ["delivery"] = ("Compartilhamento de atualizações P2P", "P2P desativado por este ajuste", "Outro modo / sem política explícita", "Desativar compartilhamento P2P"),
        ["transparency"] = ("Transparências do Windows", "Desativadas", "Ativadas ou padrão", "Desativar transparências"),
        ["animations"] = ("Animações de janelas", "Reduzidas", "Ativadas ou parciais", "Reduzir animações"),
        ["mouse"] = ("Aceleração do ponteiro do mouse", "Desativada", "Ativada ou parcial", "Desativar aceleração do ponteiro")
    };

    public static List<LiveOptimizationItem> WindowsRows(IEnumerable<Tweak> tweaks, bool settingsWritable = true)
    {
        return tweaks.Where(t => Definitions.ContainsKey(t.Id)).Select(t =>
        {
            var d = Definitions[t.Id];
            bool atTarget = t.IsSupported && t.CurrentValue == true;
            string reason = !t.IsSupported ? "Não compatível com esta versão do Windows."
                : t.CurrentValue == null ? "Estado não identificado. Nenhuma alteração automática nesta análise."
                : !settingsWritable ? "Configurações em recuperação; não é possível guardar um novo original."
                : atTarget ? "A configuração lida já corresponde à ação proposta."
                : "Confira os efeitos antes de selecionar. " + t.Guidance;
            return new LiveOptimizationItem
            {
                Kind = "Windows", Id = t.Id, Title = d.Title,
                State = t.CurrentValue is bool on ? on ? d.On : d.Off : "Estado não identificado",
                StateLabel = !t.IsSupported ? "Indisponível" : t.CurrentValue is bool state ? state ? d.On : d.Off : "Desconhecido",
                Recognition = t.IsSupported ? "Configuração do Windows reconhecida" : "Não compatível neste Windows",
                Available = t.IsSupported && t.CurrentValue == false && settingsWritable,
                Suggested = t.Id is "gamemode" or "windowed", AtTarget = atTarget, Experimental = t.Id == "hags",
                Action = d.Action + (t.NeedsRestart ? " · reiniciar depois" : ""),
                Classification = "AJUSTE DO WINDOWS", BlockReason = reason,
                Impact = t.Description + (t.NeedsRestart ? " Requer reiniciar para avaliar." : ""),
                Benefit = t.Effect, Evidence = t.Compatibility + " · " + t.Guidance,
                Company = "Microsoft Windows", HasBackup = t.HasBackup
            };
        }).ToList();
    }

    public static bool Matches(LiveOptimizationItem item, string query, string scope)
    {
        bool matchesScope = scope switch
        {
            "Windows" => item.Kind == "Windows", "Serviços" => item.Kind == "Serviço", "Aplicativos" => item.Kind == "Aplicativo",
            "Selecionados" => item.Selected, "Disponíveis" => item.Available, "Preservados" => !item.Available, _ => true
        };
        return matchesScope && (string.IsNullOrWhiteSpace(query) ||
            (item.Title + " " + item.Id + " " + item.Company + " " + item.Classification + " " + item.Kind + " " + item.DisplayState + " " + item.BlockReason).Contains(query.Trim(), StringComparison.OrdinalIgnoreCase));
    }
}
