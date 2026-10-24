using ResetExplode;

internal static class TemporarySessionChecks
{
    private sealed class FakeAccess : IGamingServiceAccess
    {
        public Dictionary<string, GamingServiceSnapshot> Data = new(StringComparer.OrdinalIgnoreCase);
        public int Writes;
        public bool FailRestore;
        public Action? BeforeWrite;
        public GamingServiceSnapshot? Read(string name) => Data.GetValueOrDefault(name);
        public void Configure(string name, int start, bool delayed)
        {
            BeforeWrite?.Invoke();
            if (FailRestore && start != 4) throw new IOException("Falha simulada ao restaurar");
            Writes++; Data[name] = Data[name] with { Start = start, Delayed = delayed };
        }
        public void Stop(string name) { Writes++; Data[name] = Data[name] with { State = "Stopped" }; }
        public void Start(string name) { Writes++; Data[name] = Data[name] with { State = "Running" }; }
    }
    public static void Run(string folder, Action<bool, string> check, Action<Action, string> throws)
    {
        string directory = Path.Combine(folder, "temporary");
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "session.json");
        string historyPath = Path.Combine(directory, "game-services-history.json");
        var original = new GamingServiceSnapshot("DiagTrack", 2, true, "Running", true, true, new());
        var access = new FakeAccess(); access.Data[original.Name] = original;
        access.BeforeWrite = () => check(File.Exists(path) && File.Exists(path + ".services"), "Manifesto e original persistidos antes de alterar serviço temporário");
        var session = TemporaryServiceSession.Start("Jogo A", new[] { "DiagTrack" }, path, access);
        check(access.Data[original.Name].Start == 4 && access.Data[original.Name].State == "Stopped", "Sessão por jogo aplica somente serviço selecionado");
        throws(() => TemporaryServiceSession.Start("Jogo B", new[] { "DiagTrack" }, path, access), "Sessão pendente impede sobreposição de serviços temporários");
        access.BeforeWrite = null;
        check(session.Restore().Count == 0 && access.Data[original.Name] == original, "Fim da sessão restaura início atrasado e estado anterior");
        check(!File.Exists(path) && !File.Exists(path + ".services"), "Restauração confirmada remove os registros temporários");
        var history = TemporaryServiceSession.ReadHistory(historyPath);
        check(history.Count == 1 && history[0].Restored && history[0].Game == "Jogo A" && history[0].Events.Count == 2, "Histórico registra aplicação e restauração por jogo");
        TemporaryServiceSession.Start("Jogo interrompido", new[] { "DiagTrack" }, path, access);
        access.FailRestore = true;
        check(TemporaryServiceSession.Recover(path, access).Count > 0 && File.Exists(path) && File.Exists(path + ".services"), "Falha após interrupção conserva manifesto e originais");
        check(!TemporaryServiceSession.ReadHistory(historyPath)[0].Restored, "Histórico distingue restauração pendente de concluída");
        access.FailRestore = false;
        check(TemporaryServiceSession.Recover(path, access).Count == 0 && access.Data[original.Name] == original, "Recuperação após reinício restaura por diário persistido");
        check(TemporaryServiceSession.ReadHistory(historyPath).Count == 2 && TemporaryServiceSession.ReadHistory(historyPath)[0].Restored, "Nova tentativa atualiza a mesma sessão no histórico");
        access.Data[original.Name] = original with { Start = 4, State = "Stopped", Delayed = false };
        session = TemporaryServiceSession.Start("Perfil permanente", new[] { "DiagTrack" }, path, access);
        check(session.Restore().Count == 0 && access.Data[original.Name].Start == 4 && access.Data[original.Name].State == "Stopped", "Sessão não desfaz serviço já desabilitado pelo perfil permanente");
        int writes = access.Writes;
        throws(() => TemporaryServiceSession.Start("Protegido", new[] { "WinDefend" }, path, access), "Serviço de segurança é rejeitado no perfil temporário");
        check(access.Writes == writes && !File.Exists(path), "Seleção inválida não cria sessão nem altera Windows");
        access.Data[original.Name] = original;
        throws(() => TemporaryServiceSession.Start("Falha parcial", new[] { "DiagTrack", "MapsBroker" }, path, access), "Falha em candidato aborta início do perfil temporário");
        check(access.Data[original.Name] == original && !File.Exists(path), "Início parcial restaura serviços anteriores antes de falhar");
        TemporaryServiceSession.Start("Corrompido", new[] { "DiagTrack" }, path, access);
        string valid = File.ReadAllText(path);
        File.WriteAllText(path, "{inválido");
        writes = access.Writes;
        check(TemporaryServiceSession.Recover(path, access).Count > 0 && access.Writes == writes && File.Exists(path + ".services"), "Manifesto corrompido é preservado sem executar instruções ou perder originais");
        File.WriteAllText(path, valid);
        check(TemporaryServiceSession.Recover(path, access).Count == 0, "Manifesto recuperado permite concluir restauração");
        var profile = new GameProfile { TemporaryServices = new() { "DiagTrack" } };
        var effective = GamingOptimization.EffectiveProfile(profile, true);
        effective.TemporaryServices.Clear();
        check(profile.TemporaryServices.Count == 1, "Cópia de sessão universal preserva lista de serviços no perfil original");
        var settings = new AppSettings { StoragePath = Path.Combine(directory, "settings.json") };
        settings.GameProfiles["game"] = profile; settings.Save();
        check(AppSettings.LoadFrom(settings.StoragePath).GameProfiles["game"].TemporaryServices.Single() == "DiagTrack", "Seleção de serviços por jogo persiste nas configurações");
    }
}
