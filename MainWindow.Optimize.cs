using System.Windows;

namespace ResetExplode;

public partial class MainWindow
{
    private bool _optimizationBusy;
    private bool _closeCleanupRunning, _closingAfterCleanup;

    private bool CanChangeOptimization()
    {
        string? reason = _settings.RecoveryRequired ? _settings.LoadWarning
            : AnyBenchRunning || TestRunning || _netRunning ? "Termine o teste, otimização ou acompanhamento do jogo antes de alterar o sistema."
            : (GameModeSession.HasPending || TemporaryServiceSession.HasPending) ? "Restaure primeiro a sessão de jogo pendente."
            : null;
        if (reason == null) return true;
        OptTweakResult.Text = reason;
        return false;
    }

    private void RefreshOptimizationState()
    {
        foreach (var tweak in _tweaks)
        {
            tweak.HasBackup = WindowsTweaks.HasBackup(tweak, _settings);
            try { tweak.Refresh(); }
            catch (Exception ex) { tweak.Status = "Leitura indisponível: " + ex.Message; }
        }
        BtnRecoverGameMode.IsEnabled = (GameModeSession.HasPending || TemporaryServiceSession.HasPending) && _launchCts == null;
        OptExtremeSteps?.Items.Refresh();
    }

    private void UpdateOptimizationDiagnosis()
    {
        var lines = new List<string>
        {
            $"Windows {Environment.OSVersion.Version} · {_cpuName} · {_gpuName}",
            "O diagnóstico é uma leitura do estado atual. Confirme o gargalo durante uma partida na aba Gargalo."
        };
        if (_settings.LoadWarning != null) lines.Add(_settings.LoadWarning);
        lines.Add(Optimizer.OnBattery switch
        {
            true => "Alimentação pela bateria: conecte à tomada antes de comparar desempenho. Alto desempenho pode reduzir a autonomia.",
            false => "Alimentação pela tomada. Ao comparar planos, acompanhe também temperatura e consumo.",
            _ => "Fonte de alimentação não identificada."
        });
        if ((GameModeSession.HasPending || TemporaryServiceSession.HasPending))
            lines.Add(_launchCts != null ? "Modo Jogo em acompanhamento. Ajustes temporários serão restaurados ao terminar." : "Sessão pendente: use RESTAURAR SESSÃO PENDENTE para recuperar os ajustes temporários.");
        var (total, available) = Optimizer.MemoryStatus();
        if (total > 0)
        {
            double used = 1 - available / (double)total;
            lines.Add($"RAM: {used:P0} em uso · {available / 1073741824.0:0.0} GB disponíveis." +
                (used >= .85 ? " Revise os programas abertos antes de jogar." : " Sem pressão elevada neste momento."));
        }
        if (!_monitor.IsOpen) lines.Add("Sensores ainda indisponíveis; temperatura não avaliada.");
        else
        {
            lines.Add(_monitor.CpuTemperature is float cpu
                ? $"CPU: {cpu:0} °C." + (cpu >= 90 ? " Verifique refrigeração antes de aumentar consumo." : "")
                : "Temperatura da CPU indisponível; confira o driver PawnIO.");
            if (MainGpuTemp() is float gpu) lines.Add($"GPU: {gpu:0} °C." + (gpu >= 85 ? " Verifique refrigeração." : ""));
        }
        try { lines.Add("Plano de energia atual: " + (WindowsTweaks.ActiveScheme() ?? "não identificado") + ". Compare o efeito do Alto desempenho no seu PC."); }
        catch (Exception ex) { lines.Add("Plano de energia indisponível: " + ex.Message); }
        lines.Add($"{_apps.Count(a => a.Suggested)} grupos de aplicativos sugeridos para revisão. Confira a lista antes de fechar.");
        lines.Add("HAGS requer GPU/driver compatíveis e teste após reiniciar. Configurar a opção não comprova suporte nem ganho de FPS.");
        lines.Add("Modo Jogo: escolha um jogo na Biblioteca e marque o perfil temporário. Para comparar, repita o mesmo cenário com as mesmas configurações gráficas.");
        OptDiagnosticReport.Text = string.Join("\n\n", lines);
    }

    private void BtnOptDiagnose_Click(object sender, RoutedEventArgs e)
    {
        RefreshApps();
        RefreshOptimizationState();
        UpdateOptimizationDiagnosis();
    }

    private void BtnOptLibrary_Click(object sender, RoutedEventArgs e) => NavLauncher.IsChecked = true;

    private void BtnRestoreTweak_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: Tweak tweak } || !CanChangeOptimization()) return;
        try
        {
            WindowsTweaks.RestoreOne(tweak, _settings);
            tweak.Status = "Restaurado." + (tweak.NeedsRestart ? " Reinicie o PC." : "");
        }
        catch (Exception ex) { tweak.Status = "Restauração pendente: " + ex.Message; }
        RefreshOptimizationState();
    }

    private async void BtnRecoverGameMode_Click(object sender, RoutedEventArgs e)
    {
        if (AnyBenchRunning || TestRunning || _netRunning) { OptTweakResult.Text = "Termine a atividade em andamento antes de restaurar a sessão."; return; }
        _serviceTask = RecoverTemporarySessionAsync();
        await _serviceTask;
    }

    private async Task RecoverTemporarySessionAsync()
    {
        _optimizationBusy = true;
        BtnRecoverGameMode.IsEnabled = false;
        try
        {
            var errors = await Task.Run(() => GameModeSession.Recover().Concat(TemporaryServiceSession.Recover()).ToList());
            OptTweakResult.Text = errors.Count == 0 ? "Sessão restaurada: energia, captura e serviços temporários voltaram ao estado anterior." : "Restauração pendente: " + string.Join("; ", errors);
        }
        catch (Exception ex) { OptTweakResult.Text = "Restauração pendente: " + ex.Message; }
        finally { _optimizationBusy = false; }
        RefreshOptimizationState();
        UpdateOptimizationDiagnosis();
        RefreshTemporaryHistory();
    }
}
