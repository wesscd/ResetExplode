using ResetExplode;

internal static class ConsoleChecks
{
    public static void Run(string folder, Action<bool, string> check)
    {
        string path = Path.Combine(folder, "console");
        var store = new OptimizationLogStore(path);
        var observed = new List<OptimizationLogEntry>();
        store.Written += observed.Add;
        store.Write("SERVIÇOS", "APLICADO", "Exemplo", "Alteração confirmada", "automático/Running", "desabilitado/Stopped");
        var loaded = store.ReadRecent();
        check(loaded.Entries.Count == 1 && loaded.Entries[0].Before == "automático/Running" && loaded.Entries[0].After == "desabilitado/Stopped", "Console persiste estado anterior e posterior com horário e resultado");
        check(observed.Count == 1 && observed[0].StorageError == null, "Console publica resultado em tempo real após persistir");
        var file = Directory.GetFiles(path).Single();
        File.AppendAllText(file, "{interrompido");
        store.Write("WINDOWS", "FALHA", "Exemplo", "Acesso negado\nDetalhe complementar");
        loaded = store.ReadRecent();
        check(loaded.Unreadable == 1 && loaded.Entries.Count == 2 && loaded.Entries[1].Status == "FALHA", "Linha interrompida é preservada sem impedir leitura dos demais resultados");
        check(File.ReadAllLines(file).Length == 3, "Mensagens multilinha não quebram o formato JSONL");
        store.Written += _ => throw new Exception("Observador falhou");
        store.Write("CONTROLE", "BLOQUEADO", "Perfil", "Sessão pendente");
        check(store.ReadRecent().Entries.Count == 3, "Falha de observador não interrompe registro nem operações");
        var broken = new OptimizationLogStore(file); // Um arquivo no lugar da pasta simula falha de armazenamento.
        OptimizationLogEntry? failed = null; broken.Written += entry => failed = entry;
        broken.Write("RECUPERAÇÃO", "RESTAURADO", "Serviço", "Resultado deve continuar visível");
        check(broken.LastError != null && failed?.Line.Contains("NÃO SALVO EM DISCO") == true, "Falha de gravação mantém resultado visível e identifica que não foi salvo");
        var parallel = new OptimizationLogStore(Path.Combine(folder, "parallel-console"));
        Parallel.For(0, 30, i => parallel.Write("LOTE", "INFO", i.ToString(), "Teste concorrente"));
        check(parallel.ReadRecent().Entries.Count == 30 && parallel.ReadRecent().Unreadable == 0, "Registro concorrente não mistura linhas nem perde resultados");
        for (int i = 0; i < 510; i++) store.Write("HISTÓRICO", "INFO", i.ToString(), "Limite de visualização");
        check(store.ReadRecent().Entries.Count == 500 && File.ReadAllLines(file).Length > 500, "Limite visual não apaga o histórico persistido");
        var trace = new OptimizationLogStore(Path.Combine(folder, "execution-console"));
        OptimizationLog.Store = trace;
        try
        {
            bool value = false, wrote = false;
            var settings = new AppSettings { StoragePath = Path.Combine(folder, "console-settings.json") };
            var tweak = new Tweak { Id = "console-test", Title = "Teste", Read = () => value,
                Capture = s => s.TweakBackup["console-test"] = "false",
                Write = (enabled, _) => { check(trace.ReadRecent().Entries.Any(e => e.Status == "BACKUP"), "Console registra backup antes de executar a alteração"); value = enabled; wrote = true; } };
            WindowsTweaks.Apply(tweak, true, settings);
            check(wrote && trace.ReadRecent().Entries.Last().Status == "APLICADO", "Console distingue estado confirmado de mera tentativa");
            wrote = false;
            try { WindowsTweaks.Apply(tweak, false, new AppSettings { StoragePath = folder }); } catch { }
            check(!wrote && trace.ReadRecent().Entries.Last().Status == "FALHA", "Falha ao guardar original aparece no console sem aplicar a mudança");
        }
        finally { OptimizationLog.Store = null; }
    }
}
