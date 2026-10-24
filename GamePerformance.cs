using System.IO;
using System.Text.Json;

namespace ResetExplode;

public sealed record CapturedFrames(int Count, double? AverageFps, double? Low1Fps, double? MeanMs, double? P99Ms, bool Overflow, int InvalidEvents, double ObservedSeconds = 0);

/// <summary>Uma observação por intervalo de Present; não calcula médias de médias de FPS.</summary>
public sealed class FrameCapture
{
    private readonly object _gate = new();
    private readonly List<double> _intervals = new();
    private double? _previous;
    private double? _first;
    private bool _overflow;
    private int _invalid;
    private readonly int _limit;
    public FrameCapture(int limit = 500_000) => _limit = limit;
    public void AddPresent(double timestampMs)
    {
        lock (_gate)
        {
            if (!double.IsFinite(timestampMs) || timestampMs < 0 || (_previous is double old && (timestampMs <= old || !double.IsFinite(timestampMs - old)))) { _invalid++; return; }
            if (_previous is double before)
            {
                if (_intervals.Count < _limit) _intervals.Add(timestampMs - before);
                else _overflow = true;
            }
            _previous = timestampMs;
            _first ??= timestampMs;
        }
    }
    public CapturedFrames Finish()
    {
        lock (_gate)
        {
            if (_intervals.Count == 0) return new(0, null, null, null, null, _overflow, _invalid);
            var sorted = _intervals.OrderBy(t => t).ToArray();
            double mean = sorted.Average();
            double slow = sorted.Skip(sorted.Length - Math.Max(1, (int)Math.Ceiling(sorted.Length * .01))).Average();
            double p99 = sorted[Math.Max(0, (int)Math.Ceiling(sorted.Length * .99) - 1)];
            if (!double.IsFinite(mean) || !double.IsFinite(slow)) return new(sorted.Length, null, null, null, null, _overflow, _invalid + 1);
            return new(sorted.Length, 1000 / mean, 1000 / slow, mean, p99, _overflow, _invalid, (_previous!.Value - _first!.Value) / 1000);
        }
    }
}

public sealed record TemperatureResult(int Samples, double? Average, double? Max)
{
    public static TemperatureResult From(IEnumerable<double> values)
    {
        var valid = values.Where(t => double.IsFinite(t) && t is > -50 and < 200).ToArray();
        return valid.Length == 0 ? new(0, null, null) : new(valid.Length, valid.Average(), valid.Max());
    }
}

public sealed class GamePerformanceRun
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public DateTimeOffset Date { get; init; } = DateTimeOffset.Now;
    public string GameKey { get; init; } = "";
    public string Game { get; init; } = "";
    public string Stage { get; init; } = "Antes";
    public string Scenario { get; init; } = "";
    public string Hardware { get; init; } = "";
    public string Executable { get; init; } = "";
    public int Pid { get; init; }
    public double Seconds { get; init; }
    public bool Complete { get; init; }
    public CapturedFrames Frames { get; init; } = new(0, null, null, null, null, false, 0);
    public TemperatureResult Cpu { get; init; } = new(0, null, null);
    public TemperatureResult Gpu { get; init; } = new(0, null, null);
    public string ProfileSource { get; init; } = "";
    public string ProfileJson { get; init; } = "";
    public Dictionary<string, bool?> WindowsState { get; init; } = new();
    public Dictionary<string, string> ProcessState { get; init; } = new();
    public List<string> Warnings { get; init; } = new();
    public bool Comparable => Complete && double.IsFinite(Seconds) && Seconds >= 59 && Frames.Count >= 300 &&
        double.IsFinite(Frames.ObservedSeconds) && Frames.ObservedSeconds >= Seconds - 2 &&
        new[] { Frames.AverageFps, Frames.Low1Fps, Frames.MeanMs, Frames.P99Ms }.All(v => v is > 0 && double.IsFinite(v.Value)) &&
        !Frames.Overflow && Frames.InvalidEvents == 0 && Warnings.Count == 0;
    public string Summary => $"{Date.ToLocalTime():dd/MM HH:mm} · {Stage} · {Scenario} · {Number(Frames.AverageFps)} FPS" + (Comparable ? "" : " · REVISAR");
    public string Details => $"{Game} · {Stage} · janela {Seconds:0.0}s · eventos cobrem {Frames.ObservedSeconds:0.0}s · {Frames.Count} intervalos\n" +
        $"FPS médio: {Number(Frames.AverageFps)} · 1% low: {Number(Frames.Low1Fps)} · tempo médio: {Number(Frames.MeanMs)} ms · P99: {Number(Frames.P99Ms)} ms\n" +
        $"CPU média/máxima: {Number(Cpu.Average)} / {Number(Cpu.Max)} °C ({Cpu.Samples} amostras) · GPU: {Number(Gpu.Average)} / {Number(Gpu.Max)} °C ({Gpu.Samples} amostras)\n" +
        $"Hardware: {Hardware}\nExecutável: {Executable}\nPerfil: {ProfileSource}\n{ProfileJson}\nWindows: " +
        string.Join(", ", WindowsState.Select(p => p.Key + "=" + (p.Value is bool v ? v ? "ativo" : "inativo" : "desconhecido"))) + "\nProcesso/configuração por executável: " +
        string.Join(", ", ProcessState.Select(p => p.Key + "=" + p.Value)) + "\n" + string.Join("\n", Warnings);
    internal static string Number(double? value) => value is double n && double.IsFinite(n) ? n.ToString("0.0") : "—";
}

public static class GamePerformanceComparison
{
    public static string Compare(GamePerformanceRun before, GamePerformanceRun after)
    {
        var warnings = new List<string>();
        if (before.Id == after.Id || before.Stage != "Antes" || after.Stage != "Depois") warnings.Add("Escolha uma sessão Antes e outra Depois.");
        if (before.GameKey != after.GameKey) warnings.Add("Jogos diferentes.");
        if (!before.Scenario.Trim().Equals(after.Scenario.Trim(), StringComparison.OrdinalIgnoreCase)) warnings.Add("Cenários declarados diferentes.");
        if (before.Hardware != after.Hardware) warnings.Add("Hardware, driver ou Windows diferentes.");
        if (!before.Executable.Equals(after.Executable, StringComparison.OrdinalIgnoreCase)) warnings.Add("Executáveis diferentes.");
        if (!before.Comparable || !after.Comparable) warnings.Add("Sessão incompleta, sem quadros suficientes ou com avisos; porcentagens indisponíveis.");
        string Delta(double? first, double? last) => warnings.Count == 0 && first is > 0 && last != null
            ? $" ({(last.Value / first.Value - 1) * 100:+0.0;-0.0;0.0}%)" : "";
        string Row(string name, double? a, double? b, string unit) => $"{name}: {GamePerformanceRun.Number(a)} → {GamePerformanceRun.Number(b)} {unit}{Delta(a, b)}";
        bool sameProfile = before.ProfileJson == after.ProfileJson && before.WindowsState.OrderBy(p => p.Key).SequenceEqual(after.WindowsState.OrderBy(p => p.Key)) && before.ProcessState.OrderBy(p => p.Key).SequenceEqual(after.ProcessState.OrderBy(p => p.Key));
        return "ANTES → DEPOIS\n" + Row("FPS médio", before.Frames.AverageFps, after.Frames.AverageFps, "FPS") + "\n" +
            Row("1% low (média do 1% mais lento)", before.Frames.Low1Fps, after.Frames.Low1Fps, "FPS") + "\n" +
            Row("Tempo médio (menor é melhor)", before.Frames.MeanMs, after.Frames.MeanMs, "ms") + "\n" +
            Row("P99 (menor é melhor)", before.Frames.P99Ms, after.Frames.P99Ms, "ms") + "\n" +
            $"CPU máxima: {GamePerformanceRun.Number(before.Cpu.Max)} → {GamePerformanceRun.Number(after.Cpu.Max)} °C\nGPU máxima: {GamePerformanceRun.Number(before.Gpu.Max)} → {GamePerformanceRun.Number(after.Gpu.Max)} °C\n" +
            (sameProfile ? "Os perfis registrados são iguais.\n" : "Os perfis/estados registrados diferem; consulte os detalhes.\n") +
            string.Join("\n", warnings) + "\nUma comparação isolada não comprova causa. Repita o mesmo trecho, configurações gráficas, limite de FPS e condições térmicas.\n" +
            "Medição de intervalos de Present por processo via ETW, não de quadros exibidos. Múltiplas swapchains, menus e geração de quadros podem afetar os valores. Vulkan/OpenGL podem não fornecer eventos.";
    }
}

public sealed class GamePerformanceStore
{
    private readonly string _path;
    public GamePerformanceStore(string? path = null) => _path = path ?? Path.Combine(AppSettings.Folder, "game-performance.json");
    public List<GamePerformanceRun> Read()
    {
        if (!File.Exists(_path)) return new();
        var runs = JsonSerializer.Deserialize<List<GamePerformanceRun>>(File.ReadAllText(_path)) ?? throw new InvalidDataException("Histórico vazio; arquivo preservado.");
        if (runs.Any(r => r == null || r.Frames == null || r.Cpu == null || r.Gpu == null || r.WindowsState == null || r.ProcessState == null || r.Warnings == null ||
            string.IsNullOrWhiteSpace(r.GameKey) || r.Scenario == null || r.Hardware == null || r.Executable == null)) throw new InvalidDataException("Histórico inválido; arquivo preservado.");
        return runs;
    }
    public void Add(GamePerformanceRun run)
    {
        var runs = Read();
        runs.RemoveAll(r => r.Id == run.Id); runs.Insert(0, run);
        AtomicFile.Write(_path, JsonSerializer.Serialize(runs.Take(100).ToList()));
    }
}
