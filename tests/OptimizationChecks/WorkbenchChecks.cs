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
        var summary = WorkbenchAnalysisSummary.From(rows.Append(security));
        check(summary.Evaluated == 6 && summary.Recognized == 4 && summary.AtTarget == 1 && summary.Available == 2 && summary.Preserved == 3,
            "Resumo diferencia estados reconhecidos, já no alvo, disponíveis e preservados sem contar segurança como otimizada");
        check(summary.AtTarget + summary.Available + summary.Preserved == summary.Evaluated,
            "Categorias do resumo dividem todo o inventário e não dependem da seleção");
        var empty = WorkbenchAnalysisSummary.From(Array.Empty<LiveOptimizationItem>());
        check(empty.Evaluated == 0 && empty.Recognized == 0, "Inventário vazio não inventa itens reconhecidos");
        var applied = WorkbenchResult.Windows(new("x", "Teste", "applied", "Leitura confirmada"), false, "Antes", "Depois");
        var restart = WorkbenchResult.Windows(new("x", "Teste", "applied", "Reinicie"), true, "Antes", "Depois");
        check(applied.State == WorkbenchResultState.Applied && restart.State == WorkbenchResultState.Restart, "Resultado confirmado distingue ajustes que exigem reinicialização");
        check(WorkbenchResult.Windows(new("x", "Teste", "failed", "Leitura divergente"), true, "Antes", "Desconhecido").State == WorkbenchResultState.Failed,
            "Falha de verificação nunca aparece como aplicada ou apenas pendente de reinício");
        check(WorkbenchResult.Windows(new("x", "Teste", "unchanged", "Já ajustado"), true, "Antes", "Antes").State == WorkbenchResultState.Unchanged,
            "Estado alcançado por outra ação antes de executar é mostrado como já ajustado");
        check(WorkbenchResult.Windows(new("x", "Teste", "unsupported", "Incompatível"), false, "Antes", "Antes").State == WorkbenchResultState.Blocked,
            "Incompatibilidade permanece bloqueio no resultado");
        bool resultNotified = false; capture.PropertyChanged += (_, e) => resultNotified |= e.PropertyName == nameof(LiveOptimizationItem.ResultLabel);
        capture.Result = applied;
        check(resultNotified && capture.ResultLabel == applied.Label && capture.LastResult.Contains("Antes: Antes") && capture.LastResult.Contains("Depois: Depois"),
            "Resultado notifica a lista e mantém horário e estados da tentativa nos detalhes");
        capture.Result = null;
        check(capture.ResultLabel.Contains("Sem tentativa") && !capture.LastResult.Contains("Leitura confirmada"), "Item sem tentativa não herda mensagem de sucesso");
        AuditProcess app = new(100, "Discord", 1234, @"C:\Apps\Discord.exe", "Discord", true, true, 2, 104857600, 0);
        var appRow = LiveOptimizationAudit.Build(inventory with { Services = new(), Processes = new() { app } }).Single();
        var access = new ResultAccess { Current = new(app, false, true) };
        check(LiveProgramCloser.CloseWithResult(appRow, access).State == WorkbenchResultState.Pending && access.Calls == 1,
            "Pedido de fechamento com processo ainda presente é pendente, nunca sucesso");
        access.CloseOnRequest = true;
        check(LiveProgramCloser.CloseWithResult(appRow, access).State == WorkbenchResultState.Applied,
            "Encerramento só é confirmado após releitura constatar ausência do processo");
        check(LiveProgramCloser.CloseWithResult(appRow, access).State == WorkbenchResultState.Unchanged,
            "Processo já ausente não conta como encerramento aplicado pelo lote");
        access.Current = new(app, true, true); int calls = access.Calls;
        check(LiveProgramCloser.CloseWithResult(appRow, access).State == WorkbenchResultState.Blocked && access.Calls == calls,
            "Vínculo com serviço é bloqueio explícito sem pedido de encerramento");
        access.Current = new(app, false, true); access.ThrowAfterRequest = true; access.CloseOnRequest = false; access.Calls = 0;
        check(LiveProgramCloser.CloseWithResult(appRow, access).State == WorkbenchResultState.Pending,
            "Falha na releitura do processo deixa encerramento não confirmado");
        var newProcess = LiveOptimizationAudit.Build(inventory with { Services = new(), Processes = new() { app with { StartedUtcTicks = 5678 } } }).Single();
        check(OptimizationWorkbench.Key(appRow) != OptimizationWorkbench.Key(newProcess),
            "PID reutilizado não herda seleção ou resultado do aplicativo anterior");
        check(WorkbenchResult.Service(new("DiagTrack", true, "Confirmado"), "Em execução").State == WorkbenchResultState.Applied &&
            WorkbenchResult.Service(new("DiagTrack", true, "Já ajustado") { Status = "SEM ALTERAÇÃO" }, "Parado").State == WorkbenchResultState.Unchanged,
            "Serviço confirmado e serviço já ajustado têm resultados distintos");
        check(WorkbenchResult.Service(new("DiagTrack", false, "Dependência") { Status = "BLOQUEADO" }, "Em execução").State == WorkbenchResultState.Blocked &&
            WorkbenchResult.Service(new("DiagTrack", false, "Falha na releitura") { Status = "FALHA" }, "Em execução").State == WorkbenchResultState.Failed,
            "Serviço bloqueado e falha de confirmação nunca contam como aplicados");
    }

    private sealed class ResultAccess : ILiveProgramAccess
    {
        public LiveProgramState? Current;
        public bool CloseOnRequest, ThrowAfterRequest;
        public int Calls;
        public LiveProgramState? Inspect(int pid)
        {
            if (ThrowAfterRequest && Calls > 0) throw new InvalidOperationException("Leitura indisponível");
            return Current;
        }
        public string RequestClose(AuditProcess expected) { Calls++; if (CloseOnRequest) Current = null; return "Pedido enviado"; }
    }
}
