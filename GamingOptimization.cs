using System.ComponentModel;
using System.Text.Json;

namespace ResetExplode;

public sealed class OptimizationChoice : INotifyPropertyChanged
{
    public required Tweak Tweak { get; init; }
    public bool Target { get; init; } = true;
    public string Note { get; init; } = "";
    public string Title => Tweak.Title;
    public bool Available => Tweak.IsSupported;
    public string Preview => Tweak.CurrentValue == Target ? "JÁ CONFIGURADO" :
        $"{Tweak.State} → {(Target ? "ATIVAR" : "DESATIVAR")}" + (Tweak.NeedsRestart ? " · reiniciar" : "");
    private bool _selected;
    public bool Selected { get => _selected; set { _selected = value; PropertyChanged?.Invoke(this, new(nameof(Selected))); } }
    public event PropertyChangedEventHandler? PropertyChanged;
}

public sealed record OptimizationOutcome(string Id, string Title, string State, string Detail);
public sealed record OptimizationRun(DateTime Date, List<OptimizationOutcome> Items)
{
    public string Summary => $"{Date:dd/MM HH:mm} · {Items.Count(i => i.State == "applied")} aplicados · " +
        $"{Items.Count(i => i.State == "unchanged")} já configurados · {Items.Count(i => i.State == "failed")} falhas · {Items.Count(i => i.State == "unsupported")} indisponíveis";
}

/// <summary>Perfil global: não depende de engine, loja, jogo ou fabricante de GPU.</summary>
public static class GamingOptimization
{
    public static List<OptimizationChoice> Build(IEnumerable<Tweak> tweaks, bool? onBattery, float? cpuTemp, float? gpuTemp)
    {
        bool powerDefault = onBattery == false && cpuTemp is < 85 && gpuTemp is < 80;
        var catalog = new (string Id, bool Default, string Note)[]
        {
            ("gamemode", true, "Habilita o Modo de Jogo do Windows para qualquer jogo reconhecido pelo sistema."),
            ("gamedvr", true, "Desliga a captura do Windows. Desmarque se usa gravação ou o recurso de salvar os últimos momentos."),
            ("windowed", true, "Somente Windows 11 e jogos DX10/11 compatíveis em janela/sem bordas; outros jogos podem não ser afetados."),
            ("power", powerDefault, powerDefault ? "Alto desempenho global: acompanhe consumo e temperaturas." : "Opcional: bateria, temperatura elevada ou leitura ausente. Revise antes de ativar Alto desempenho."),
            ("delivery", false, "Rede: desliga o compartilhamento P2P de atualizações. Não bloqueia Windows Update nem promete reduzir ping."),
            ("animations", false, "Interface: reduz animações de janelas. Não equivale a aumentar o FPS do jogo."),
            ("transparency", false, "Interface: remove transparências do Windows; o ganho em jogos não é garantido."),
            ("hags", false, "Experimento: exige GPU/driver compatíveis e reinicialização. Compare ligado/desligado."),
            ("mouse", false, "Preferência de controle: desliga aceleração do ponteiro. Jogos com entrada bruta podem ignorar essa opção.")
        };
        var byId = tweaks.ToDictionary(t => t.Id);
        return catalog.Where(c => byId.ContainsKey(c.Id)).Select(c => new OptimizationChoice
        {
            Tweak = byId[c.Id], Note = c.Note,
            Selected = c.Default && byId[c.Id].IsSupported && byId[c.Id].CurrentValue != true
        }).ToList();
    }

    public static List<OptimizationOutcome> Apply(IEnumerable<OptimizationChoice> choices, AppSettings settings)
    {
        var results = new List<OptimizationOutcome>();
        foreach (var choice in choices.Where(c => c.Selected))
        {
            var tweak = choice.Tweak;
            if (!tweak.IsSupported)
            { results.Add(new(tweak.Id, tweak.Title, "unsupported", "Indisponível neste Windows.")); continue; }
            try
            {
                tweak.Refresh(); // a prévia pode ter envelhecido
                if (tweak.CurrentValue == choice.Target)
                { results.Add(new(tweak.Id, tweak.Title, "unchanged", "Já estava no estado escolhido.")); continue; }
                WindowsTweaks.Apply(tweak, choice.Target, settings);
                if (tweak.CurrentValue != choice.Target) throw new InvalidOperationException("O estado lido não confirmou a alteração.");
                results.Add(new(tweak.Id, tweak.Title, "applied", tweak.NeedsRestart ? "Reinicie para avaliar o efeito." : "Configuração aplicada; ganho não medido."));
            }
            catch (Exception ex) { results.Add(new(tweak.Id, tweak.Title, "failed", ex.Message)); }
        }
        return results;
    }

    public static GameProfile EffectiveProfile(GameProfile source, bool universalMode)
    {
        var profile = JsonSerializer.Deserialize<GameProfile>(JsonSerializer.Serialize(source))!;
        if (universalMode) profile.GameMode = true;
        return profile;
    }
}
