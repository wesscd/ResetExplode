using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace ResetExplode;

public partial class MainWindow
{
    private Task? _measurementTask;
    private CancellationTokenSource? _measurementCts;
    private string? _activeGameKey;
    private GameProfile? _activeGameProfile;
    private List<GamePerformanceRun> _performanceRuns = new();

    private async void BtnMeasureGame_Click(object sender, RoutedEventArgs e)
    {
        if (_measurementCts != null) return;
        // O jogo acompanhado pode continuar em execução; os outros testes e mudanças ficam bloqueados.
        if (BenchRunning || TestRunning || _gameBenchRunning || _ocRunning || _rtChecking || _netRunning || _optimizationBusy)
        { LMeasurementStatus.Text = "Termine os testes e alterações antes de medir."; return; }
        if (_lg == null || _lp == null) return;
        if (string.IsNullOrWhiteSpace(_cpuName) || _gpus.Count == 0) { LMeasurementStatus.Text = "Aguarde a identificação do hardware antes de medir."; return; }
        string scenario = LMeasureScenario.Text.Trim();
        if (scenario.Length < 3) { LMeasurementStatus.Text = "Descreva o trecho, resolução, preset e limite de FPS para repetir a comparação."; return; }
        string? expectedExe = _lp.ExePath ?? _lg.ExePath;
        if (string.IsNullOrEmpty(expectedExe)) { LMeasurementStatus.Text = "Escolha o executável do jogo na seção Desempenho antes de medir."; return; }
        // Confere persistência antes da contagem, sem substituir histórico inválido.
        try { _ = new GamePerformanceStore().Read(); }
        catch (Exception ex) { LMeasurementStatus.Text = "Histórico indisponível: " + ex.Message; return; }
        var game = _lg;
        bool tracked = _activeGameKey == game.Key && _activeGameProfile != null;
        var profile = tracked ? _activeGameProfile! : GamingOptimization.EffectiveProfile(_lp, false);
        string profileJson = JsonSerializer.Serialize(new { profile.Priority, profile.HighPerfPlan, profile.GameMode, profile.TemporaryServices,
            profile.GpuHigh, profile.DisableFso, profile.WinMode, profile.Res, profile.Hz, profile.Style, profile.Extra,
            Helpers = profile.Helpers.Select(h => new { h.Path, h.Args, h.CloseWithGame }).ToArray() });
        string stage = LMeasureStage.SelectedValue as string == "Depois" ? "Depois" : "Antes";
        string hardware = _cpuName + " | " + string.Join("; ", _gpus.Select(g => g.Name + " / " + g.Sections.SelectMany(s => s.Children).FirstOrDefault(n => n.Header == "Versão do driver")?.Value)) + " | RAM " + Optimizer.MemoryStatus().Total + " | Windows " + Environment.OSVersion.Version;
        _measurementCts = new CancellationTokenSource();
        _measurementTask = MeasureGameAsync(game, expectedExe, scenario, stage, hardware, profileJson, tracked, _measurementCts.Token);
        await _measurementTask;
    }

    private async Task MeasureGameAsync(LibGame game, string expectedExe, string scenario, string stage, string hardware, string profileJson, bool tracked, CancellationToken token)
    {
        BtnMeasureGame.IsEnabled = false; BtnCancelMeasure.IsEnabled = true;
        LMeasurementStatus.Text = "Preparando a leitura do contexto; em seguida começa a contagem regressiva…";
        FrameCapture? capture = null;
        Process? process = null;
        var cpu = new List<double>(); var gpu = new List<double>(); var warnings = new List<string>();
        var elapsed = new Stopwatch();
        string path = ""; int pid = 0; bool complete = false;
        DateTimeOffset started = DateTimeOffset.Now;
        var windows = new Dictionary<string, bool?>();
        var processState = new Dictionary<string, string>();
        try
        {
            windows = await Task.Run(() => WindowsTweaks.All().ToDictionary(t => t.Id, t => { try { return t.IsSupported ? t.Read() : null; } catch { return null; } }));
            for (int remaining = 10; remaining > 0; remaining--)
            {
                LMeasurementStatus.Text = $"Volte ao jogo e prepare o mesmo trecho. Medição começa em {remaining}s…";
                await Task.Delay(1000, token);
            }
            pid = _frames.ForegroundProcessId();
            process = Process.GetProcessById(pid);
            _ = process.SafeHandle;
            path = process.MainModule?.FileName ?? "";
            if (!path.Equals(expectedExe, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("O jogo selecionado não está em primeiro plano ou o executável difere. Confira o .exe e tente novamente.");
            try { processState["Prioridade lida"] = process.PriorityClass.ToString(); } catch { processState["Prioridade lida"] = "desconhecida"; }
            foreach (var value in new[] { (Label: "Preferência GPU (não confirma GPU usada)", Key: @"Software\Microsoft\DirectX\UserGpuPreferences"), (Label: "Compatibilidade/FSO configurada", Key: @"Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers") })
            {
                try { using var key = Registry.CurrentUser.OpenSubKey(value.Key); processState[value.Label] = key?.GetValue(path)?.ToString() ?? "sem preferência explícita"; }
                catch { processState[value.Label] = "desconhecida"; }
            }
            _frames.Start();
            if (!_frames.Running) throw new InvalidOperationException("ETW indisponível: " + (_frames.Error ?? "aguarde a inicialização"));
            capture = _frames.BeginCapture(pid);
            started = DateTimeOffset.Now;
            elapsed.Start();
            int background = 0;
            while (elapsed.Elapsed.TotalSeconds < 60)
            {
                token.ThrowIfCancellationRequested();
                if (process.HasExited) { warnings.Add("O processo terminou antes do fim da janela."); break; }
                if (tracked && _activeGameKey != game.Key) { warnings.Add("O acompanhamento e seu perfil temporário terminaram durante a medição."); break; }
                if (_frames.ForegroundProcessId() != pid) background++;
                if (_monitor.CpuTemperature is float ct) cpu.Add(ct);
                if (MainGpuTemp() is float gt) gpu.Add(gt);
                LMeasurementStatus.Text = $"Medindo {game.Name}: {elapsed.Elapsed.TotalSeconds:0}/60s · mantenha o mesmo trecho em primeiro plano.";
                await Task.Delay(TimeSpan.FromSeconds(Math.Clamp(60 - elapsed.Elapsed.TotalSeconds, .001, 1)), token);
            }
            complete = elapsed.Elapsed.TotalSeconds >= 59 && !process.HasExited;
            if (background > 0) warnings.Add($"Jogo fora de primeiro plano em {background} amostra(s); resultados exigem revisão.");
            if (_frames.Error != null) warnings.Add("ETW: " + _frames.Error);
        }
        catch (OperationCanceledException) { warnings.Add("Medição cancelada; sessão parcial."); }
        catch (Exception ex) { warnings.Add(ex.Message); }
        finally
        {
            elapsed.Stop();
            if (capture != null)
            {
                var frames = _frames.EndCapture(capture);
                if (frames.ObservedSeconds < elapsed.Elapsed.TotalSeconds - 2) warnings.Add("Eventos não cobrem a janela completa (tolerância de 2s); resultados exigem revisão.");
                if (frames.Count < 300) warnings.Add("Menos de 300 intervalos de Present; amostra insuficiente para comparar.");
                if (frames.Overflow || frames.InvalidEvents > 0) warnings.Add("Limite de captura ou eventos inválidos; resultados exigem revisão.");
                var run = new GamePerformanceRun
                {
                    GameKey = game.Key, Game = game.Name, Date = started, Stage = stage, Scenario = scenario, Hardware = hardware,
                    Executable = path, Pid = pid, Seconds = elapsed.Elapsed.TotalSeconds, Complete = complete, Frames = frames,
                    Cpu = TemperatureResult.From(cpu), Gpu = TemperatureResult.From(gpu), WindowsState = windows,
                    ProcessState = processState,
                    ProfileJson = profileJson, ProfileSource = tracked ? "Perfil solicitado no acompanhamento; consulte os estados lidos (nem toda aplicação foi confirmada)" : "Perfil salvo; aplicação externa não confirmada",
                    Warnings = warnings
                };
                try
                {
                    new GamePerformanceStore().Add(run);
                    LMeasurementStatus.Text = (run.Comparable ? "Medição concluída." : "Medição registrada para revisão; porcentagens bloqueadas.") + "\n" + string.Join("\n", warnings);
                    RefreshPerformanceHistory(run.Id);
                }
                catch (Exception ex) { LMeasurementStatus.Text = "Falha ao salvar. Histórico anterior preservado: " + ex.Message; }
            }
            else LMeasurementStatus.Text = string.Join("\n", warnings);
            process?.Dispose();
            _measurementCts?.Dispose(); _measurementCts = null;
            BtnMeasureGame.IsEnabled = true; BtnCancelMeasure.IsEnabled = false;
            RefreshPendingRecovery();
        }
    }

    private void BtnCancelMeasure_Click(object sender, RoutedEventArgs e) => _measurementCts?.Cancel();
    private void RefreshPerformanceHistory(Guid? select = null)
    {
        try
        {
            _performanceRuns = new GamePerformanceStore().Read().Where(r => r.GameKey == _lg?.Key).ToList();
            LBeforeRun.ItemsSource = _performanceRuns.Where(r => r.Stage == "Antes").ToList();
            LAfterRun.ItemsSource = _performanceRuns.Where(r => r.Stage == "Depois").ToList();
            LBeforeRun.SelectedItem = _performanceRuns.FirstOrDefault(r => r.Stage == "Antes" && r.Id == select) ?? _performanceRuns.FirstOrDefault(r => r.Stage == "Antes");
            LAfterRun.SelectedItem = _performanceRuns.FirstOrDefault(r => r.Stage == "Depois" && r.Id == select) ?? _performanceRuns.FirstOrDefault(r => r.Stage == "Depois");
            UpdatePerformanceComparison();
        }
        catch (Exception ex) { LPerformanceComparison.Text = "Histórico indisponível; arquivo preservado: " + ex.Message; }
    }
    private void LPerformanceSelection_Changed(object sender, SelectionChangedEventArgs e) => UpdatePerformanceComparison();
    private void UpdatePerformanceComparison()
    {
        if (LPerformanceComparison == null || LBeforeRun == null || LAfterRun == null) return;
        var before = LBeforeRun.SelectedItem as GamePerformanceRun;
        var after = LAfterRun.SelectedItem as GamePerformanceRun;
        LPerformanceComparison.Text = before != null && after != null ? GamePerformanceComparison.Compare(before, after) : "Registre e escolha uma sessão Antes e uma Depois deste jogo.";
        LPerformanceDetails.Text = string.Join("\n\n", new[] { before?.Details, after?.Details }.Where(d => d != null));
    }
    private void BtnRefreshPerformance_Click(object sender, RoutedEventArgs e) => RefreshPerformanceHistory();
    private void BtnExportPerformance_Click(object sender, RoutedEventArgs e)
    {
        if (LBeforeRun.SelectedItem is not GamePerformanceRun before || LAfterRun.SelectedItem is not GamePerformanceRun after)
        { LMeasurementStatus.Text = "Escolha as duas sessões antes de exportar."; return; }
        var dialog = new SaveFileDialog { Filter = "Comparação JSON|*.json", FileName = "comparacao-jogo.json" };
        if (dialog.ShowDialog(this) != true) return;
        try { AtomicFile.Write(dialog.FileName, JsonSerializer.Serialize(new { Before = before, After = after, Comparison = GamePerformanceComparison.Compare(before, after) }, new JsonSerializerOptions { WriteIndented = true })); LMeasurementStatus.Text = "Comparação exportada com caminhos locais e perfis."; }
        catch (Exception ex) { LMeasurementStatus.Text = "Falha ao exportar: " + ex.Message; }
    }
}
