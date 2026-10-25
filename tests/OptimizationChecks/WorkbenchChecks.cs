using ResetExplode;

internal static class WorkbenchChecks
{
    public static void Run(Action<bool, string> check)
    {
        Tweak Fake(string id, bool? value, bool supported = true)
        { var tweak = new Tweak { Id = id, Read = () => value, IsSupported = supported }; tweak.Refresh(); return tweak; }
        var rows = OptimizationWorkbench.WindowsRows(new[] { Fake("gamedvr", false), Fake("gamemode", false), Fake("hags", null), Fake("windowed", false, false), Fake("mouse", true) });
        var capture = rows.Single(i => i.Id == "gamedvr");
        check(capture.DisplayState == "Captura ativada ou parcial" && capture.Action.Contains("Desativar captura"), "Lista separa o estado real da captura do booleano interno de otimização");
        check(capture.AfterState == "Manter estado atual" && !capture.Selected, "Estado previsto preserva o item até ele entrar na fila");
        bool notified = false; capture.PropertyChanged += (_, e) => notified |= e.PropertyName == nameof(LiveOptimizationItem.AfterState);
        capture.Selected = true;
        check(notified && capture.AfterState.Contains("Desativar captura"), "Marcar atualiza o resultado previsto sem executar alteração");
        check(!capture.Suggested && rows.Single(i => i.Id == "gamemode").Suggested, "Captura condicional não entra nas sugestões gerais automaticamente");
        check(!rows.Single(i => i.Id == "hags").Available && rows.Single(i => i.Id == "hags").DisplayState == "Desconhecido", "Estado desconhecido permanece visível e bloqueado");
        var hags = OptimizationWorkbench.WindowsRows(new[] { Fake("hags", false) }).Single();
        check(hags.ReviewLabel == "EXPERIMENTAL" && !hags.Suggested, "Ajuste experimental é identificado e não entra na seleção de sugestões");
        check(!rows.Single(i => i.Id == "windowed").Available && rows.Single(i => i.Id == "windowed").DisplayState == "Indisponível", "Ajuste incompatível é identificado na lista");
        var mouse = rows.Single(i => i.Id == "mouse"); mouse.Selected = true;
        check(mouse.AtTarget && mouse.ReviewLabel == "JÁ NO ALVO" && !mouse.Selected && mouse.DisplayState == "Desativada", "Recurso já desativado é reconhecido sem propor repetição");
        check(!OptimizationWorkbench.WindowsRows(new[] { Fake("gamemode", false) }, false).Single().Available, "Configurações em recuperação não permitem novo ajuste sem original");
        check(OptimizationWorkbench.Matches(capture, "captura", "Selecionados") && !OptimizationWorkbench.Matches(mouse, "", "Selecionados"), "Filtro de fila inclui somente seleções e respeita busca");
        check(OptimizationWorkbench.Matches(mouse, "", "Preservados") && !OptimizationWorkbench.Matches(mouse, "", "Disponíveis"), "Filtros distinguem itens preservados de candidatos disponíveis");
        check(OptimizationWorkbench.Matches(capture, "", "Windows") && !OptimizationWorkbench.Matches(capture, "", "Serviços"), "Categorias do painel não misturam ajustes e serviços");
        check(LiveOptimizationAudit.Preview(rows).Contains("1 ajustes do Windows") && LiveOptimizationAudit.Preview(rows).Contains("Antes: Captura ativada"), "Revisão unificada inclui antes/depois dos ajustes Windows");
        var protectedService = new AuditService(new("WinDefend", 4, false, "Stopped", true, true, new()), "Defender", "", "", "Microsoft", 0);
        var inventory = new AuditInventory(DateTimeOffset.Now, 2, new() { protectedService }, new(), new(), true, true, true);
        var security = LiveOptimizationAudit.Build(inventory).Single();
        check(!security.AtTarget && security.ReviewLabel == "SEGURANÇA" && !security.Available, "Segurança parada nunca é apresentada como alvo de otimização cumprido");
    }
}
