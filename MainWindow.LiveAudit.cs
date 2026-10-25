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
        string? reason = OptimizationActivityBlockReason();
        if (reason == null) return true;
        ShowOptimizationBlock(reason);
        return false;
    }

    private async void BtnLiveScan_Click(object sender, RoutedEventArgs e)
    {
        if (!CanRunLiveAudit()) return;
        _liveAuditValid = false;
        OptimizationLog.Write("ANÁLISE", "INÍCIO", "Este PC", "Inventário somente leitura; nenhuma mudança será aplicada.");
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
            OptimizationLog.Write("ANÁLISE", "CONCLUÍDO", "Este PC", OptLiveStatus.Text);
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
        catch (OperationCanceledException) { OptLiveStatus.Text = "Operação cancelada. Ações já concluídas permanecem registradas no resultado."; OptimizationLog.Write("ANÁLISE / APLICAÇÃO", "CANCELADO", "Operação", OptLiveStatus.Text); }
        catch (Exception ex) { OptLiveStatus.Text = "Não foi possível concluir: " + ex.Message; OptimizationLog.Write("ANÁLISE / APLICAÇÃO", "FALHA", "Operação", OptLiveStatus.Text); }
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
        OptWorkflowStatus.Text = !_liveAuditValid ? "1. Analise este PC para obter uma seleção atual." : !_liveItems.Any(i => i.Available)
            ? "Nenhum candidato disponível neste inventário. Os itens foram preservados; clique nas linhas para conferir o motivo."
            : _liveItems.Any(i => i.Selected && i.Available)
            ? $"2. {_liveItems.Count(i => i.Selected && i.Available)} item(ns) marcado(s). Confira os impactos e clique em REVISAR E APLICAR PRÉVIA."
            : "2. Análise pronta. Marque as caixas dos itens disponíveis; clicar na linha apenas exibe os detalhes.";
        BtnLiveApply.IsEnabled = !_optimizationBusy && _liveAuditValid && _liveItems.Any(i => i.Selected && i.Available);
        BtnLiveApply.ToolTip = !_liveAuditValid ? "Analise este PC antes de aplicar." : !_liveItems.Any(i => i.Selected && i.Available) ? "Marque pelo menos uma caixa de um candidato disponível." : "Revise os impactos e confirme a seleção. A análise vale por dois minutos.";
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
        { _liveAuditValid = false; UpdateLivePreview(); ShowOptimizationBlock("A análise expirou após dois minutos. Clique em ANALISAR ESTE PC novamente antes de aplicar."); return; }
        var selected = _liveItems.Where(i => i.Selected && i.Available).ToList();
        if (selected.Count == 0) return;
        if (!ReviewExtremeSelection(LiveOptimizationAudit.Preview(selected), "PRÉVIA · SERVIÇOS E APLICATIVOS",
            "Serviços: início e estado originais serão guardados antes da alteração; use RESTAURAR SERVIÇOS DO PERFIL para desfazer. Aplicativos: salve seu trabalho; o fechamento é normal, pode ser recusado e não tem reabertura automática. Nenhum ganho de FPS foi medido.")) return;
        _liveAuditValid = false;
        OptimizationLog.Write("APLICAÇÃO", "INÍCIO", "Prévia confirmada", $"Executando {selected.Count} ações selecionadas; originais dos serviços em backup.");
        _liveAuditTask = LiveOperationAsync(async token =>
        {
            var results = new List<string>();
            int failures = 0;
            await Task.Run(() =>
            {
                var profile = new GamingServiceProfile(ServiceJournalPath, new WindowsServiceAccess());
                foreach (var item in selected)
                {
                    if (token.IsCancellationRequested) { results.Add("Interrompido entre itens."); break; }
                    try
                    {
                        string detail;
                        if (item.Kind == "Serviço") { var outcome = profile.Apply(item.Id); detail = outcome.Detail; if (!outcome.Success) failures++; }
                        else detail = LiveProgramCloser.Close(item, new WindowsLiveProgramAccess());
                        results.Add(item.Title + ": " + detail);
                        if (item.Kind == "Aplicativo") OptimizationLog.Write("APLICATIVOS", "RESULTADO", item.Title, detail);
                    }
                    catch (Exception ex) { failures++; results.Add(item.Title + ": falhou · " + ex.Message); OptimizationLog.Write("APLICAÇÃO", "FALHA", item.Title, ex.Message); }
                }
            });
            OptLiveStatus.Text = string.Join("\n", results) + "\nAnalise novamente para atualizar os estados e o consumo.";
            OptimizationLog.Write("APLICAÇÃO", token.IsCancellationRequested ? "CANCELADO" : failures > 0 ? "FINALIZADO COM FALHAS" : "FINALIZADO · CONFIRA OS ITENS", "Lote", OptLiveStatus.Text);
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
