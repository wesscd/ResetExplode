using System.Text.Json;
using ResetExplode;

internal static class PerformanceChecks
{
    public static void Run(string folder, Action<bool, string> check, Action<Action, string> throws)
    {
        var capture = new FrameCapture();
        check(capture.Finish().AverageFps == null, "Sem eventos não inventa FPS zero");
        capture.AddPresent(0); capture.AddPresent(10); capture.AddPresent(30);
        var stats = capture.Finish();
        check(stats.Count == 2 && Math.Abs(stats.AverageFps!.Value - 2000.0 / 30) < .0001, "FPS calculado pelos intervalos, sem média de médias");
        check(stats.MeanMs == 15 && stats.Low1Fps == 50 && stats.P99Ms == 20, "Tempo médio, 1% mais lento e P99 têm definições explícitas");
        var tail = new FrameCapture(); tail.AddPresent(0);
        double t = 0;
        for (int i = 0; i < 99; i++) tail.AddPresent(t += 10);
        tail.AddPresent(t + 100);
        check(tail.Finish().Low1Fps == 10 && tail.Finish().P99Ms == 10, "1% low usa a média da cauda e não é confundido com P99");
        var invalid = new FrameCapture(); invalid.AddPresent(0); invalid.AddPresent(0); invalid.AddPresent(-1); invalid.AddPresent(double.NaN); invalid.AddPresent(10);
        check(invalid.Finish().InvalidEvents == 3 && invalid.Finish().Count == 1 && invalid.Finish().MeanMs == 10, "Eventos inválidos não contaminam intervalos nem viram ganho");
        var bounded = new FrameCapture(2); for (int i = 0; i < 5; i++) bounded.AddPresent(i * 10);
        check(bounded.Finish().Overflow && bounded.Finish().Count == 2, "Limite de memória é sinalizado e bloqueia comparação confiável");
        check(TemperatureResult.From(Array.Empty<double>()).Max == null, "Temperatura ausente é desconhecida");
        check(TemperatureResult.From(new[] { 50.0, 70, double.NaN, 400 }).Average == 60, "Temperaturas calculadas somente de leituras válidas");
        GamePerformanceRun Run(string stage, CapturedFrames frames, string scenario = "Mapa X / 1080p / alto") => new()
        { Game = "Jogo A", GameKey = "manual:a", Stage = stage, Scenario = scenario, Hardware = "CPU/GPU/driver", Executable = @"C:\Games\A.exe", Seconds = 60, Complete = true, Frames = frames };
        var before = Run("Antes", new(6000, 100, 80, 10, 12.5, false, 0, 60));
        var after = Run("Depois", new(6600, 110, 88, 1000.0 / 110, 1000.0 / 88, false, 0, 60));
        string result = GamePerformanceComparison.Compare(before, after);
        check(before.Comparable && result.Contains("+10") && result.Contains("não comprova causa"), "Comparação válida mostra variação sem atribuir causalidade");
        var mismatch = Run("Depois", after.Frames, "Outro mapa");
        check(!GamePerformanceComparison.Compare(before, mismatch).Contains("+10"), "Cenários diferentes não exibem porcentagem de melhora");
        var partial = new GamePerformanceRun { GameKey = before.GameKey, Stage = "Depois", Scenario = before.Scenario, Hardware = before.Hardware, Executable = before.Executable, Seconds = 20, Complete = false, Frames = after.Frames };
        check(!partial.Comparable && !GamePerformanceComparison.Compare(before, partial).Contains("+10"), "Sessão parcial preserva dados e bloqueia porcentagens");
        var lostFocus = new GamePerformanceRun { GameKey = before.GameKey, Stage = "Depois", Scenario = before.Scenario, Hardware = before.Hardware, Executable = before.Executable, Seconds = 60, Complete = true, Frames = after.Frames, Warnings = new() { "Perdeu foco" } };
        check(!lostFocus.Comparable, "Perda de primeiro plano exige revisão");
        var overflow = Run("Antes", after.Frames with { Overflow = true });
        check(!overflow.Comparable && !Run("Antes", after.Frames with { InvalidEvents = 1 }).Comparable, "Overflow e eventos inválidos impedem comparabilidade");
        check(!Run("Antes", after.Frames with { AverageFps = double.PositiveInfinity }).Comparable, "Métrica não finita nunca é tratada como resultado comparável");
        check(!Run("Antes", after.Frames with { ObservedSeconds = 20 }).Comparable, "Eventos que cobrem somente parte da janela não geram porcentagem de melhora");
        var otherHardware = new GamePerformanceRun { GameKey = before.GameKey, Stage = "Depois", Scenario = before.Scenario, Hardware = "Outro driver", Executable = before.Executable, Seconds = 60, Complete = true, Frames = after.Frames };
        check(!GamePerformanceComparison.Compare(before, otherHardware).Contains("+10"), "Mudança de hardware ou driver bloqueia porcentagens automáticas");
        string path = Path.Combine(folder, "performance.json");
        var store = new GamePerformanceStore(path); store.Add(before); store.Add(after);
        check(store.Read().Count == 2 && store.Read()[0].Id == after.Id, "Histórico mantém sessões independentes por ID em ordem recente");
        store.Add(after);
        check(store.Read().Count == 2 && File.Exists(path + ".bak"), "Gravação atômica preserva backup sem duplicar a sessão");
        File.WriteAllText(path, "{inválido");
        throws(() => store.Add(before), "Histórico corrompido bloqueia sobrescrita");
        check(File.ReadAllText(path) == "{inválido", "Histórico corrompido permanece preservado");
        var roundTrip = JsonSerializer.Deserialize<GamePerformanceRun>(JsonSerializer.Serialize(before))!;
        check(roundTrip.Frames == before.Frames && roundTrip.Scenario == before.Scenario, "Exportação JSON preserva métricas e cenário");
        string recovery = Path.Combine(folder, "recovery-banner"); Directory.CreateDirectory(recovery);
        check(PendingRecovery.Describe(recovery).Count == 0, "Abertura sem diários não apresenta alerta falso");
        File.WriteAllText(Path.Combine(recovery, "game-session.json"), JsonSerializer.Serialize(new GameModeSession { Game = "Jogo pendente", PauseCapture = true, OriginalCapture = new() { 1, 1 } }));
        check(PendingRecovery.Describe(recovery).Single().Contains("captura do Windows"), "Aviso identifica função pendente sem alterar o sistema");
        File.WriteAllText(Path.Combine(recovery, "game-session.json"), "{inválido");
        check(PendingRecovery.Describe(recovery).Single().Contains("preservado"), "Diário corrompido aparece no alerta e não é descartado");
        File.WriteAllText(Path.Combine(recovery, "game-services-session.json.services"), "{}");
        check(PendingRecovery.Describe(recovery).Any(s => s.Contains("ausente")), "Backup de serviços órfão também aparece no alerta");
        File.WriteAllText(Path.Combine(recovery, "game-services-session.json"), JsonSerializer.Serialize(new TemporaryServiceSession.Journal { Game = "Jogo", Services = new() { "DiagTrack" } }));
        File.WriteAllText(Path.Combine(recovery, "game-services-session.json.services"), JsonSerializer.Serialize(new { Originals = new[] { new GamingServiceSnapshot("DiagTrack", 2, true, "Running", true, true, new()) } }));
        check(PendingRecovery.Describe(recovery).Any(s => s.Contains("automático atrasado") && s.Contains("Running")), "Alerta identifica início e estado originais de cada serviço");
    }
}
