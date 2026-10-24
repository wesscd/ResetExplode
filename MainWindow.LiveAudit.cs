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

    private bool CanRunLiveAudit()
    {
        if (!AnyBenchRunning && !TestRunning && !(GameModeSession.HasPending || TemporaryServiceSession.HasPending)) return true;
        OptLiveStatus.Text = "Termine os testes, a otimização ou o jogo e restaure sessões pendentes antes de continuar.";
        return false;
    }

    private async void BtnLiveScan_Click(object sender, RoutedEventArgs e)
    {
        if (!CanRunLiveAudit()) return;
        _liveAuditValid = false;
        _liveAuditTask = LiveOperationAsync(async token =>
        {
            var progress = new Progress<string>(message => OptLiveStatus.Text = message);
            var inventory = await Task.Run(() => WindowsOptimizationInventory.CaptureAsync(token, message => ((IProgress<string>)progress).Report(message)), token);
            foreach (var row in _liveItems) row.PropertyChanged -= LiveSelectionChanged;
            _liveInventory = inventory;
            _liveItems = LiveOptimizationAudit.Build(inventory);
            foreach (var row in _liveItems) row.PropertyChanged += LiveSelectionChanged;
            OptLiveList.ItemsSource = _liveItems;
            FilterLiveItems();
            _liveAuditValid = true;
            OptLiveStatus.Text = $"Análise de {inventory.CapturedAt.ToLocalTime():dd/MM HH:mm:ss} · amostra de {inventory.SampleSeconds:0.0}s · " +
                $"{inventory.Services.Count} serviços · {inventory.Processes.Count(p => p.CurrentSession)} processos na sessão · {_liveItems.Count(i => i.Available)} candidatos.\n" +
                string.Join("\n", inventory.Warnings);
        });
        await _liveAuditTask;
    }

    private async Task LiveOperationAsync(Func<CancellationToken, Task> action)
    {
        _optimizationBusy = true;
        _liveAuditCts = new CancellationTokenSource();
        BtnLiveScan.IsEnabled = BtnLiveApply.IsEnabled = false;
        BtnLiveCancel.IsEnabled = true;
        try { await action(_liveAuditCts.Token); }
        catch (OperationCanceledException) { OptLiveStatus.Text = "Operação cancelada. Ações já concluídas permanecem registradas no resultado."; }
        catch (Exception ex) { OptLiveStatus.Text = "Não foi possível concluir: " + ex.Message; }
        finally
        {
            _optimizationBusy = false;
            _liveAuditCts.Dispose(); _liveAuditCts = null;
            BtnLiveScan.IsEnabled = true; BtnLiveCancel.IsEnabled = false;
            UpdateLivePreview();
        }
    }

    private void LiveSelectionChanged(object? sender, PropertyChangedEventArgs e) => UpdateLivePreview();
    private void UpdateLivePreview()
    {
        if (OptLivePreview == null) return;
        OptLivePreview.Text = LiveOptimizationAudit.Preview(_liveItems);
        BtnLiveApply.IsEnabled = !_optimizationBusy && _liveAuditValid && _liveItems.Any(i => i.Selected && i.Available);
    }
    private void OptLiveFilter_Changed(object sender, TextChangedEventArgs e) => FilterLiveItems();
    private void FilterLiveItems()
    {
        if (OptLiveList?.ItemsSource == null) return;
        string filter = OptLiveFilter.Text.Trim();
        CollectionViewSource.GetDefaultView(OptLiveList.ItemsSource).Filter = value => value is LiveOptimizationItem item &&
            (filter.Length == 0 || (item.Title + " " + item.Company + " " + item.Classification + " " + item.Kind).Contains(filter, StringComparison.OrdinalIgnoreCase));
    }
    private async void BtnLiveApply_Click(object sender, RoutedEventArgs e)
    {
        if (!CanRunLiveAudit()) return;
        if (!_liveAuditValid || _liveInventory == null || DateTimeOffset.Now - _liveInventory.CapturedAt > TimeSpan.FromMinutes(2))
        { OptLiveStatus.Text = "A análise expirou. Analise novamente antes de aplicar."; _liveAuditValid = false; UpdateLivePreview(); return; }
        var selected = _liveItems.Where(i => i.Selected && i.Available).ToList();
        if (selected.Count == 0) return;
        if (!ReviewExtremeSelection(LiveOptimizationAudit.Preview(selected), "PRÉVIA · SERVIÇOS E APLICATIVOS",
            "Serviços: início e estado originais serão guardados antes da alteração; use RESTAURAR SERVIÇOS DO PERFIL para desfazer. Aplicativos: salve seu trabalho; o fechamento é normal, pode ser recusado e não tem reabertura automática. Nenhum ganho de FPS foi medido.")) return;
        _liveAuditValid = false;
        _liveAuditTask = LiveOperationAsync(async token =>
        {
            var results = new List<string>();
            await Task.Run(() =>
            {
                var profile = new GamingServiceProfile(ServiceJournalPath, new WindowsServiceAccess());
                foreach (var item in selected)
                {
                    if (token.IsCancellationRequested) { results.Add("Interrompido entre itens."); break; }
                    try
                    {
                        string detail = item.Kind == "Serviço" ? profile.Apply(item.Id).Detail : LiveProgramCloser.Close(item, new WindowsLiveProgramAccess());
                        results.Add(item.Title + ": " + detail);
                    }
                    catch (Exception ex) { results.Add(item.Title + ": falhou · " + ex.Message); }
                }
            });
            OptLiveStatus.Text = string.Join("\n", results) + "\nAnalise novamente para atualizar os estados e o consumo.";
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
            AtomicFile.Write(dialog.FileName, JsonSerializer.Serialize(new { Inventory = _liveInventory, Items = _liveItems,
                Preview = LiveOptimizationAudit.Preview(_liveItems) }, new JsonSerializerOptions { WriteIndented = true }));
            OptLiveStatus.Text = "Relatório exportado com estados, caminhos locais e prévia: " + dialog.FileName;
        }
        catch (Exception ex) { OptLiveStatus.Text = "Falha ao exportar: " + ex.Message; }
    }
}
