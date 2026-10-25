using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Microsoft.Win32;

namespace ResetExplode;

public partial class MainWindow
{
    private Task? _liveAuditTask;
    private CancellationTokenSource? _liveAuditCts;
    private AuditInventory? _liveInventory;
    private List<LiveOptimizationItem> _liveItems = new();
    private bool _liveAuditValid;
    private bool _workbenchInitialized;
    private readonly Dictionary<string, WorkbenchResult> _workbenchResults = new();
    private readonly List<(string Title, WorkbenchResult Result)> _workbenchBatch = new();

    private void RecordWorkbenchResult(LiveOptimizationItem item, WorkbenchResult result)
    {
        item.Result = result;
        _workbenchResults[OptimizationWorkbench.Key(item)] = result;
        _workbenchBatch.Add((item.Title, result));
        OptWorkbenchBatchSummary.Text = string.Join(" · ", _workbenchBatch.GroupBy(r => r.Result.Label).Select(g => $"{g.Count()} {g.Key.ToLowerInvariant()}"));
        OptWorkbenchBatchDetails.Text = string.Join("\n\n", _workbenchBatch.Select(r => r.Title + "\n" + r.Result.Summary));
        OptimizationLog.Write("RESULTADO DO ITEM", result.Label, item.Title, result.Detail, result.Before, result.After);
    }

    private void BeginWorkbenchBatch()
    {
        _workbenchBatch.Clear();
        OptWorkbenchBatchSummary.Text = "Executando seleção confirmada…";
        OptWorkbenchBatchDetails.Text = "Os resultados aparecerão conforme cada tentativa terminar.";
    }

    private void BtnWorkbenchConsole_Click(object sender, RoutedEventArgs e)
    {
        OptWorkbenchConsole.IsExpanded = true;
        OptConsoleFilter.Text = "";
        OptConsoleText.Focus();
    }

    private async Task RefreshWorkbenchAsync(CancellationToken token, bool preserveQueue = false)
    {
        var previous = OptLiveList.SelectedItem as LiveOptimizationItem;
        var queued = preserveQueue ? _liveItems.Where(i => i.Selected).Select(OptimizationWorkbench.Key).ToHashSet() : new HashSet<string>();
        var progress = new Progress<string>(message => OptLiveStatus.Text = message);
        var snapshot = await Task.Run(async () =>
        {
            var inventory = await WindowsOptimizationInventory.CaptureAsync(token, message => ((IProgress<string>)progress).Report(message));
            var tweaks = WindowsTweaks.All();
            foreach (var tweak in tweaks) { token.ThrowIfCancellationRequested(); try { tweak.Refresh(); } catch (Exception ex) { tweak.Status = ex.Message; } }
            return (Inventory: inventory, Tweaks: tweaks);
        }, token);
        foreach (var row in _liveItems) row.PropertyChanged -= LiveSelectionChanged;
        _liveInventory = snapshot.Inventory;
        foreach (var tweak in snapshot.Tweaks) tweak.HasBackup = WindowsTweaks.HasBackup(tweak, _settings);
        _liveItems = OptimizationWorkbench.WindowsRows(snapshot.Tweaks, !_settings.RecoveryRequired).Concat(LiveOptimizationAudit.Build(snapshot.Inventory)).ToList();
        try
        {
            var originals = new GamingServiceProfile(ServiceJournalPath, new WindowsServiceAccess()).Originals.Select(s => s.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var row in _liveItems.Where(r => r.Kind == "Serviço")) row.HasBackup = originals.Contains(row.Id);
        }
        catch (Exception ex) { _liveInventory.Warnings.Add("Backups de serviços indisponíveis: " + ex.Message); }
        foreach (var row in _liveItems)
        {
            row.Selected = queued.Contains(OptimizationWorkbench.Key(row));
            row.Result = _workbenchResults.GetValueOrDefault(OptimizationWorkbench.Key(row));
            row.PropertyChanged += LiveSelectionChanged;
        }
        OptLiveList.ItemsSource = _liveItems;
        var view = CollectionViewSource.GetDefaultView(_liveItems);
        view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(LiveOptimizationItem.Kind)));
        FilterLiveItems();
        OptLiveList.SelectedItem = _liveItems.FirstOrDefault(i => i.Kind == previous?.Kind && i.Id == previous?.Id) ?? _liveItems.FirstOrDefault(i => i.Available) ?? _liveItems.FirstOrDefault();
        _liveAuditValid = true;
        OptWorkbenchSummary.Text = WorkbenchAnalysisSummary.From(_liveItems).Text;
        OptLiveStatus.Text = $"Leitura de {_liveInventory.CapturedAt.ToLocalTime():HH:mm:ss} · {_liveItems.Count} itens avaliados · {_liveInventory.Services.Count} serviços e {_liveInventory.Processes.Count(p => p.CurrentSession)} processos detectados · {_liveItems.Count(i => i.Available)} disponíveis.\n" + string.Join("\n", _liveInventory.Warnings);
    }

    private bool CanRunLiveAudit()
    {
        string? reason = OptimizationActivityBlockReason();
        if (reason == null) return true;
        ShowOptimizationBlock(reason);
        return false;
    }

    private async void BtnLiveScan_Click(object sender, RoutedEventArgs e)
        => await ScanWorkbenchAsync();

    private async Task ScanWorkbenchAsync(bool preserveQueue = false)
    {
        if (!CanRunLiveAudit()) return;
        _liveAuditValid = false;
        OptWorkbenchSummary.Text = "Atualizando análise · os estados anteriores não devem orientar novas alterações.";
        OptimizationLog.Write("ANÁLISE", "INÍCIO", "Este PC", "Inventário somente leitura; nenhuma mudança será aplicada.");
        _liveAuditTask = LiveOperationAsync(async token =>
        {
            await RefreshWorkbenchAsync(token, preserveQueue);
            OptimizationLog.Write("ANÁLISE", "CONCLUÍDO", "Este PC", OptLiveStatus.Text);
        });
        await _liveAuditTask;
    }

    private async Task LiveOperationAsync(Func<CancellationToken, Task> action)
    {
        _optimizationBusy = true;
        OptWorkbenchSummary.Text = "Operação em andamento · aguarde a nova leitura dos estados.";
        _liveAuditCts = new CancellationTokenSource();
        BtnLiveScan.IsEnabled = BtnLiveApply.IsEnabled = false;
        OptLiveList.IsEnabled = BtnWorkbenchRestore.IsEnabled = BtnWorkbenchSuggestions.IsEnabled = BtnWorkbenchClear.IsEnabled = false;
        BtnLiveCancel.IsEnabled = true;
        try { await action(_liveAuditCts.Token); }
        catch (OperationCanceledException) { OptLiveStatus.Text = "Operação cancelada. Ações já concluídas permanecem registradas no resultado."; OptimizationLog.Write("ANÁLISE / APLICAÇÃO", "CANCELADO", "Operação", OptLiveStatus.Text); }
        catch (Exception ex) { OptLiveStatus.Text = "Não foi possível concluir: " + ex.Message; OptimizationLog.Write("ANÁLISE / APLICAÇÃO", "FALHA", "Operação", OptLiveStatus.Text); }
        finally
        {
            if (!_liveAuditValid) OptWorkbenchSummary.Text = "Leitura desatualizada ou incompleta · atualize a análise antes de selecionar novas alterações.";
            _optimizationBusy = false;
            _liveAuditCts.Dispose(); _liveAuditCts = null;
            BtnLiveScan.IsEnabled = true; BtnLiveCancel.IsEnabled = false;
            OptLiveList.IsEnabled = BtnWorkbenchSuggestions.IsEnabled = BtnWorkbenchClear.IsEnabled = true;
            UpdateLivePreview();
        }
    }

    private void LiveSelectionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(LiveOptimizationItem.Selected)) return;
        if (OptWorkbenchScope.SelectedValue as string == "Selecionados") Dispatcher.BeginInvoke(new Action(FilterLiveItems));
        UpdateLivePreview();
    }
    private void UpdateLivePreview()
    {
        if (OptLivePreview == null) return;
        OptLivePreview.Text = LiveOptimizationAudit.Preview(_liveItems);
        OptWorkflowStatus.Text = !_liveAuditValid ? "1. Analise este PC para obter uma seleção atual." : !_liveItems.Any(i => i.Available)
            ? "Nenhuma alteração disponível. Confira os itens já no alvo e os motivos de preservação nas linhas."
            : _liveItems.Any(i => i.Selected && i.Available)
            ? $"2. {_liveItems.Count(i => i.Selected && i.Available)} item(ns) marcado(s). Confira os impactos e clique em REVISAR ALTERAÇÕES."
            : "2. Análise pronta. Marque as caixas dos itens disponíveis; clicar na linha apenas exibe os detalhes.";
        BtnLiveApply.IsEnabled = !_optimizationBusy && _liveAuditValid && _liveItems.Any(i => i.Selected && i.Available);
        int queued = _liveItems.Count(i => i.Selected && i.Available);
        BtnLiveApply.Content = queued == 0 ? "REVISAR ALTERAÇÕES" : $"REVISAR {queued} ALTERAÇÃO(ÕES)";
        OptWorkbenchQueue.Text = queued == 0 ? "Fila vazia · marcar uma caixa não altera o Windows." : $"{queued} item(ns) na fila, incluindo itens fora do filtro atual. A seleção exige revisão e confirmação.";
        BtnWorkbenchRestore.IsEnabled = !_optimizationBusy && OptLiveList.SelectedItem is LiveOptimizationItem { HasBackup: true };
        BtnLiveApply.ToolTip = !_liveAuditValid ? "Analise este PC antes de aplicar." : !_liveItems.Any(i => i.Selected && i.Available) ? "Marque pelo menos uma caixa de um candidato disponível." : "Revise os impactos e confirme a seleção. A análise vale por dois minutos.";
    }
    private void OptLiveFilter_Changed(object sender, TextChangedEventArgs e) => FilterLiveItems();
    private void FilterLiveItems()
    {
        if (OptLiveList?.ItemsSource == null) return;
        string filter = OptLiveFilter.Text.Trim();
        string scope = OptWorkbenchScope.SelectedValue as string ?? "Todos";
        CollectionViewSource.GetDefaultView(OptLiveList.ItemsSource).Filter = value => value is LiveOptimizationItem item && OptimizationWorkbench.Matches(item, filter, scope);
    }
    private void OptWorkbenchScope_Changed(object sender, SelectionChangedEventArgs e) => FilterLiveItems();
    private async void OptWorkbenchTabs_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!ReferenceEquals(e.Source, OptWorkbenchTabs) || !_workbenchInitialized || OptWorkbenchTabs.SelectedIndex != 0 || _optimizationBusy) return;
        await ScanWorkbenchAsync(preserveQueue: true);
    }
    private void OptWorkbenchRow_Changed(object sender, SelectionChangedEventArgs e) { if (BtnWorkbenchRestore != null) BtnWorkbenchRestore.IsEnabled = !_optimizationBusy && OptLiveList.SelectedItem is LiveOptimizationItem { HasBackup: true }; }
    private void BtnWorkbenchSuggestions_Click(object sender, RoutedEventArgs e) { if (_optimizationBusy) return; foreach (var row in _liveItems.Where(i => i.Suggested && i.Available)) row.Selected = true; }
    private void BtnWorkbenchClear_Click(object sender, RoutedEventArgs e) { if (_optimizationBusy) return; foreach (var row in _liveItems) row.Selected = false; }
    private async void BtnLiveApply_Click(object sender, RoutedEventArgs e)
    {
        if (!CanRunLiveAudit()) return;
        if (!_liveAuditValid || _liveInventory == null || DateTimeOffset.Now - _liveInventory.CapturedAt > TimeSpan.FromMinutes(2))
        { _liveAuditValid = false; OptWorkbenchSummary.Text = "Análise expirada · atualize para conferir os estados atuais."; UpdateLivePreview(); ShowOptimizationBlock("A análise expirou após dois minutos. Clique em ATUALIZAR ANÁLISE novamente antes de aplicar."); return; }
        var selected = _liveItems.Where(i => i.Selected && i.Available).ToList();
        if (selected.Count == 0) return;
        if (!ReviewExtremeSelection(LiveOptimizationAudit.Preview(selected), "REVISÃO · ALTERAÇÕES SELECIONADAS",
            "Windows e serviços: originais guardados antes de cada mudança; permanecem aplicados até restaurar. Aplicativos: salve seu trabalho; o fechamento é normal, pode ser recusado e não tem reabertura automática. Nenhum ganho de FPS foi medido.")) return;
        _liveAuditValid = false;
        OptimizationLog.Write("APLICAÇÃO", "INÍCIO", "Prévia confirmada", $"Executando {selected.Count} ações selecionadas; originais dos serviços em backup.");
        _liveAuditTask = LiveOperationAsync(async token =>
        {
            BeginWorkbenchBatch();
            var results = new List<string>();
            var windowsOutcomes = new List<OptimizationOutcome>();
            int failures = 0;
            GamingServiceProfile? profile = null;
            foreach (var item in selected)
            {
                if (token.IsCancellationRequested)
                {
                    RecordWorkbenchResult(item, new(WorkbenchResultState.Canceled, "Cancelado entre itens; nenhuma tentativa para este item.", item.DisplayState, "Não executado", DateTimeOffset.Now));
                    results.Add(item.Title + ": não executado; cancelado."); continue;
                }
                OptLiveStatus.Text = "Aplicando: " + item.Title;
                try
                {
                    WorkbenchResult result;
                    if (item.Kind == "Windows")
                    {
                        await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.Background);
                        token.ThrowIfCancellationRequested();
                        var tweak = WindowsTweaks.All().Single(t => t.Id == item.Id);
                        tweak.Refresh();
                        if (!tweak.IsSupported || tweak.CurrentValue == null) throw new InvalidOperationException("Compatibilidade ou estado mudou desde a análise. Item preservado.");
                        string before = OptimizationWorkbench.WindowsRows(new[] { tweak }).Single().DisplayState;
                        var outcome = GamingOptimization.Apply(new[] { new OptimizationChoice { Tweak = tweak, Selected = true } }, _settings).Single();
                        windowsOutcomes.Add(outcome);
                        result = WorkbenchResult.Windows(outcome, tweak.NeedsRestart, before, OptimizationWorkbench.WindowsRows(new[] { tweak }).Single().DisplayState);
                        if (outcome.State is "failed" or "unsupported") failures++;
                    }
                    else if (item.Kind == "Serviço")
                    {
                        profile ??= await Task.Run(() => new GamingServiceProfile(ServiceJournalPath, new WindowsServiceAccess()));
                        var outcome = await Task.Run(() => profile.Apply(item.Id));
                        result = WorkbenchResult.Service(outcome, item.DisplayState);
                        if (!outcome.Success) failures++;
                    }
                    else
                    {
                        result = await Task.Run(() => LiveProgramCloser.CloseWithResult(item, new WindowsLiveProgramAccess()));
                        if (result.State is WorkbenchResultState.Failed or WorkbenchResultState.Blocked) failures++;
                    }
                    RecordWorkbenchResult(item, result);
                    results.Add(item.Title + ": " + result.Summary);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    var result = new WorkbenchResult(WorkbenchResultState.Canceled, "Cancelado antes de iniciar a alteração.", item.DisplayState, "Não executado", DateTimeOffset.Now);
                    RecordWorkbenchResult(item, result); results.Add(item.Title + ": " + result.Summary);
                }
                catch (Exception ex)
                {
                    failures++; if (item.Kind == "Windows") windowsOutcomes.Add(new(item.Id, item.Title, "failed", ex.Message));
                    var result = new WorkbenchResult(WorkbenchResultState.Failed, ex.Message, "Na análise: " + item.DisplayState, "Não confirmado; atualize a análise", DateTimeOffset.Now);
                    RecordWorkbenchResult(item, result); results.Add(item.Title + ": " + result.Summary);
                }
            }
            if (windowsOutcomes.Count > 0)
            {
                _settings.OptimizationHistory.Insert(0, new(DateTime.Now, windowsOutcomes));
                _settings.OptimizationHistory = _settings.OptimizationHistory.Take(20).ToList();
                try { _settings.Save(); RefreshExtremeHistory(); }
                catch (Exception ex) { failures++; OptimizationLog.Write("HISTÓRICO", "FALHA", "Lista de otimizações", ex.Message); }
            }
            OptimizationLog.Write("APLICAÇÃO", token.IsCancellationRequested ? "CANCELADO" : failures > 0 ? "FINALIZADO COM FALHAS" : "FINALIZADO · CONFIRA OS ITENS", "Lote", string.Join("\n", results));
            if (!token.IsCancellationRequested) { await RefreshWorkbenchAsync(token); OptLiveStatus.Text += $"\nLote finalizado · {failures} falha(s)/bloqueio(s) · {_workbenchBatch.Count(r => r.Result.State == WorkbenchResultState.Pending)} pendente(s). A lista foi relida; confira os resultados abaixo e o console."; }
            else OptLiveStatus.Text = "Lote interrompido. A leitura exibida é anterior às mudanças; atualize a análise.";
        });
        await _liveAuditTask;
    }

    private async void BtnWorkbenchRestore_Click(object sender, RoutedEventArgs e)
    {
        if (!CanRunLiveAudit() || OptLiveList.SelectedItem is not LiveOptimizationItem { HasBackup: true } item) return;
        if (MessageBox.Show(this, "Restaurar o original guardado para " + item.Title + "?", "Restaurar item", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        _liveAuditValid = false;
        _liveAuditTask = LiveOperationAsync(async token =>
        {
            BeginWorkbenchBatch();
            WorkbenchResult result;
            try
            {
                if (item.Kind == "Windows")
                {
                    var tweak = WindowsTweaks.All().Single(t => t.Id == item.Id);
                    WindowsTweaks.RestoreOne(tweak, _settings);
                    result = new(WorkbenchResultState.Restored, "Original restaurado." + (tweak.NeedsRestart ? " Reinicie para avaliar." : ""), "Na análise: " + item.DisplayState,
                        OptimizationWorkbench.WindowsRows(new[] { tweak }).Single().DisplayState, DateTimeOffset.Now);
                }
                else
                {
                    var outcome = await Task.Run(() => new GamingServiceProfile(ServiceJournalPath, new WindowsServiceAccess()).Restore(item.Id));
                    result = new(outcome.Success ? WorkbenchResultState.Restored : WorkbenchResultState.Failed, outcome.Detail, "Na análise: " + item.DisplayState,
                        outcome.Success ? "Original confirmado pelo serviço" : "Restauração não confirmada", DateTimeOffset.Now);
                }
            }
            catch (Exception ex)
            {
                result = new(WorkbenchResultState.Failed, "Restauração: " + ex.Message, "Na análise: " + item.DisplayState, "Não confirmado", DateTimeOffset.Now);
            }
            RecordWorkbenchResult(item, result);
            if (!token.IsCancellationRequested) await RefreshWorkbenchAsync(token);
            OptLiveStatus.Text += "\n" + result.Label + ": " + result.Detail;
        });
        await _liveAuditTask;
    }
    private void BtnLiveCancel_Click(object sender, RoutedEventArgs e) => _liveAuditCts?.Cancel();
    private void BtnLiveExport_Click(object sender, RoutedEventArgs e)
    {
        if (_liveInventory == null) { OptLiveStatus.Text = "Analise antes de exportar."; return; }
        var dialog = new SaveFileDialog { Filter = "Relatório JSON|*.json", FileName = "analise-otimizacao.json" };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            AtomicFile.Write(dialog.FileName, JsonSerializer.Serialize(new { Inventory = _liveInventory,
                AnalysisValid = _liveAuditValid && DateTimeOffset.Now - _liveInventory.CapturedAt <= TimeSpan.FromMinutes(2),
                Summary = WorkbenchAnalysisSummary.From(_liveItems), Items = _liveItems,
                LastBatch = _workbenchBatch.Select(r => new { r.Title, r.Result }),
                Preview = LiveOptimizationAudit.Preview(_liveItems) }, new JsonSerializerOptions { WriteIndented = true }));
            OptLiveStatus.Text = "Relatório exportado com estados, caminhos locais e prévia: " + dialog.FileName;
        }
        catch (Exception ex) { OptLiveStatus.Text = "Falha ao exportar: " + ex.Message; }
    }
}
