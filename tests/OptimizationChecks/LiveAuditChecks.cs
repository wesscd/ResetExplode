using ResetExplode;

internal static class LiveAuditChecks
{
    public static void Run(Action<bool, string> check)
    {
        AuditProcess app = new(100, "Discord", 1234, @"C:\Apps\Discord.exe", "Discord", true, true, 2, 104857600, 0);
        AuditService service = new(new("DiagTrack", 2, false, "Running", true, true, new()), "Telemetria", "", "", "Microsoft", 200);
        AuditInventory inventory = new(DateTimeOffset.Now, 2, new() { service }, new() { app }, new(), true, true, true);
        var rows = LiveOptimizationAudit.Build(inventory);
        var a = rows.Single(r => r.Kind == "Aplicativo");
        var s = rows.Single(r => r.Kind == "Serviço");
        check(rows.Count == 2 && a.Available && s.Available, "Inventário inclui aplicativo e serviço opcional elegíveis");
        check(rows.All(r => !r.Selected), "Análise não seleciona nem aplica ações automaticamente");
        string report = System.Text.Json.JsonSerializer.Serialize(new { Inventory = inventory, Items = rows });
        using (var parsed = System.Text.Json.JsonDocument.Parse(report))
            check(parsed.RootElement.GetProperty("Inventory").GetProperty("Services").GetArrayLength() == 1 && parsed.RootElement.GetProperty("Items").GetArrayLength() == 2, "Relatório JSON mantém inventário e ações sem executar alterações");
        a.Selected = true;
        check(LiveOptimizationAudit.Preview(rows).Contains("Voz, mensagens") && LiveOptimizationAudit.Preview(rows).Contains("Antes:"), "Prévia inclui estado anterior e impacto funcional");
        check(s.CpuPercent == null && s.Resources.Contains("—"), "Ausência de amostra é desconhecida, não consumo zero");
        LiveOptimizationItem AppRow(AuditProcess p, AuditInventory? source = null) => LiveOptimizationAudit.Build((source ?? inventory) with { Processes = new() { p } }).Single(r => r.Kind == "Aplicativo");
        check(!AppRow(app with { Name = "SoftwareDesconhecido" }).Available, "Software desconhecido permanece no inventário e é preservado");
        check(!AppRow(app with { SecurityProduct = true }).Available, "Produto de segurança registrado é preservado");
        check(!AppRow(app with { Company = "Antivirus Vendor" }).Available, "Metadados de segurança bloqueiam seleção conservadoramente");
        check(!AppRow(app with { HasWindow = false }).Available, "Aplicativo sem janela não é encerrado à força");
        check(!AppRow(app with { StartedUtcTicks = null }).Available, "Identidade incompleta bloqueia fechamento");
        check(!AppRow(app, inventory with { SecurityInventoryComplete = false }).Available, "Consulta incompleta de segurança bloqueia fechamento");
        check(!AppRow(app, inventory with { Services = new() { service with { Pid = 100 } } }).Available, "Processo que hospeda serviço é preservado como aplicativo");
        check(!LiveOptimizationAudit.Build(inventory with { DependenciesComplete = false }).Single(r => r.Kind == "Serviço").Available, "Dependências incompletas bloqueiam alteração de serviço");
        var dependent = service with { Snapshot = service.Snapshot with { Dependents = new() { "Outro" } } };
        check(!LiveOptimizationAudit.Build(inventory with { Services = new() { dependent } }).Single(r => r.Kind == "Serviço").Available, "Dependentes instalados impedem parada isolada");
        var essential = service with { Snapshot = service.Snapshot with { Name = "WinDefend" } };
        var blocked = LiveOptimizationAudit.Build(inventory with { Services = new() { essential } }).Single(r => r.Kind == "Serviço");
        blocked.Selected = true;
        check(!blocked.Available && !blocked.Selected, "Serviço protegido não aceita seleção");
        var shared = LiveOptimizationAudit.Build(inventory with { Services = new() { service, service with { Snapshot = service.Snapshot with { Name = "MapsBroker" } } } });
        check(shared.Where(r => r.Kind == "Serviço").All(r => r.SharedHost && r.Evidence.Contains("processo inteiro")), "Host compartilhado não atribui consumo total a cada serviço");
        check(LiveOptimizationAudit.CpuUsage(0, 1000, 1, 4) == 25, "CPU normalizada pelo número de processadores lógicos");
        check(LiveOptimizationAudit.CpuUsage(5, 5, 2, 4) == 0, "CPU medida ociosa é zero legítimo");
        check(LiveOptimizationAudit.CpuUsage(5, 1, 2, 4) == null && LiveOptimizationAudit.CpuUsage(0, 1, 0, 4) == null, "Contador reiniciado e intervalo inválido não geram medição falsa");
        var access = new FakeAccess { Current = new(app, false, true) };
        LiveProgramCloser.Close(a, access);
        check(access.Calls == 1, "Aplicação revalida e solicita fechamento normal uma vez");
        foreach (var state in new[] { new LiveProgramState(app with { StartedUtcTicks = 5678 }, false, true), new(app with { Path = @"C:\Outro.exe" }, false, true), new(app, true, true), new(app, false, false), new(app with { SecurityProduct = true }, false, true), new(app with { CurrentSession = false }, false, true) })
        {
            access.Current = state;
            LiveProgramCloser.Close(a, access);
        }
        check(access.Calls == 1, "Reutilização de PID, caminho alterado, serviço e segurança impedem fechamento");
    }
    private sealed class FakeAccess : ILiveProgramAccess
    {
        public LiveProgramState? Current;
        public int Calls;
        public LiveProgramState? Inspect(int pid) => Current;
        public string RequestClose(AuditProcess expected) { Calls++; return "Solicitado"; }
    }
}
