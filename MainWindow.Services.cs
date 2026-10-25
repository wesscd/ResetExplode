using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace ResetExplode;

public partial class MainWindow
{
    public sealed class ServiceProfileRow
    {
        public string Name { get; init; } = "";
        public string Title { get; init; } = "";
        public string Impact { get; init; } = "";
        public string State { get; init; } = "";
        public bool Available { get; init; }
        public bool Selected { get; set; }
        public bool HasBackup { get; init; }
    }
    private List<ServiceProfileRow> _serviceRows = new();
    private Task? _serviceTask;
    private CancellationTokenSource? _serviceCts;
    private bool _serviceBusy;
    private static string ServiceJournalPath => Path.Combine(AppSettings.Folder, "services-profile.json");

    private static List<ServiceProfileRow> InspectServices(GamingServiceProfile profile, CancellationToken token)
    {
        var rows = new List<ServiceProfileRow>();
        foreach (var rule in GamingServiceCatalog.Rules)
        {
            if (token.IsCancellationRequested) break;
            try
            {
                var service = profile.Inspect(rule.Name);
                string? blocked = service == null ? "Não instalado"
                    : !service.IsUserService || service.Start is < 2 or > 4 ? "Tipo não suportado; preservado"
                    : service.State is not ("Running" or "Stopped") ? "Estado transitório; preservado"
                    : service.Dependents.Count > 0 ? "Dependentes instalados: " + string.Join(", ", service.Dependents)
                    : service.State == "Running" && !service.CanStop ? "Não aceita parada isolada"
                    : service.Start == 4 && service.State == "Stopped" ? "Já desabilitado e parado"
                    : null;
                string startup = service?.Start switch { 2 => service.Delayed ? "Automático (atrasado)" : "Automático", 3 => "Manual", 4 => "Desabilitado", _ => "Indisponível" };
                rows.Add(new()
                {
                    Name = rule.Name, Title = rule.Title + " · " + rule.Name, Impact = rule.Impact,
                    State = blocked ?? $"{startup} · {service!.State} → desabilitar e parar",
                    Available = blocked == null,
                    Selected = blocked == null && rule.Suggested && service?.Start == 2 && service.State == "Running",
                    HasBackup = profile.Originals.Any(s => s.Name.Equals(rule.Name, StringComparison.OrdinalIgnoreCase))
                });
            }
            catch (Exception ex) { rows.Add(new() { Name = rule.Name, Title = rule.Title, Impact = rule.Impact, State = "Leitura indisponível: " + ex.Message, HasBackup = profile.Originals.Any(s => s.Name == rule.Name) }); }
        }
        return rows;
    }

    private bool CanManageServices()
    {
        string? reason = OptimizationActivityBlockReason();
        if (reason == null) return true;
        ShowOptimizationBlock(reason);
        return false;
    }

    private async Task ServiceOperationAsync(Func<GamingServiceProfile, CancellationToken, List<GamingServiceResult>> action)
    {
        _serviceBusy = true; _optimizationBusy = true;
        _serviceCts = new CancellationTokenSource();
        BtnServicesScan.IsEnabled = BtnServicesApply.IsEnabled = BtnServicesRestore.IsEnabled = false;
        BtnServicesCancel.IsEnabled = true;
        OptServicesResult.Text = "Processando. A parada nunca é forçada nem propagada para dependentes…";
        OptimizationLog.Write("SERVIÇOS", "INÍCIO", "Operação", OptServicesResult.Text);
        try
        {
            var result = await Task.Run(() =>
            {
                var profile = new GamingServiceProfile(ServiceJournalPath, new WindowsServiceAccess());
                var applied = action(profile, _serviceCts.Token);
                return (Results: applied, Rows: InspectServices(profile, _serviceCts.Token), Backups: profile.Originals.Count);
            });
            _serviceRows = result.Rows;
            OptServicesList.ItemsSource = _serviceRows;
            OptServicesSummary.Text = $"{_serviceRows.Count(r => r.Available)} candidatos disponíveis · {result.Backups} originais guardados. " +
                "Itens condicionais começam desmarcados. Alterações persistem até restaurar; ganho em FPS não foi medido.";
            OptServicesResult.Text = result.Results.Count == 0 ? "Nenhuma alteração de serviço nesta operação. Confira os candidatos e os backups disponíveis." :
                string.Join("\n", result.Results.Select(r => $"{r.Name}: {(r.Success ? "OK" : "PENDENTE")} · {r.Detail}"));
            if (_serviceCts.IsCancellationRequested) OptServicesResult.Text += "\nOperação interrompida entre itens. Mudanças concluídas continuam com backup.";
            OptimizationLog.Write("SERVIÇOS", _serviceCts.IsCancellationRequested ? "CANCELADO" : result.Results.Any(r => !r.Success) ? "FINALIZADO COM PENDÊNCIAS" : "CONCLUÍDO", "Operação", OptServicesResult.Text);
        }
        catch (Exception ex) { OptServicesResult.Text = "Não foi possível concluir: " + ex.Message + "\nBackup preservado em " + ServiceJournalPath; OptimizationLog.Write("SERVIÇOS", "FALHA", "Operação", OptServicesResult.Text); }
        finally
        {
            _serviceBusy = false; _optimizationBusy = false;
            BtnServicesScan.IsEnabled = BtnServicesRestore.IsEnabled = true;
            BtnServicesApply.IsEnabled = _serviceRows.Any(r => r.Available);
            BtnServicesCancel.IsEnabled = false;
            _serviceCts.Dispose(); _serviceCts = null;
        }
    }

    private async void BtnServicesScan_Click(object sender, RoutedEventArgs e)
    {
        if (!CanManageServices()) return;
        _serviceTask = ServiceOperationAsync((_, _) => new());
        await _serviceTask;
    }
    private async void BtnServicesApply_Click(object sender, RoutedEventArgs e)
    {
        if (!CanManageServices()) return;
        var selected = _serviceRows.Where(r => r.Selected && r.Available).ToList();
        if (selected.Count == 0) { ShowOptimizationBlock("Marque as caixas dos serviços desejados após analisar este PC. Clicar na linha apenas mostra os detalhes."); return; }
        if (!ReviewExtremeSelection("PERFIL DE SERVIÇOS\nDesabilitar e parar os itens selecionados:\n\n" +
            string.Join("\n\n", selected.Select(r => r.Title + "\n" + r.Impact)) +
            "\n\nSegurança e serviços essenciais ficam fora da seleção. Não há encerramento forçado nem parada de dependentes. Os originais ficam em backup separado.")) return;
        _serviceTask = ServiceOperationAsync((profile, token) =>
        {
            var results = new List<GamingServiceResult>();
            foreach (var row in selected) { if (token.IsCancellationRequested) break; results.Add(profile.Apply(row.Name)); }
            return results;
        });
        await _serviceTask;
    }
    private async Task RestoreServicesAsync(string? only)
    {
        if (!CanManageServices()) return;
        _serviceTask = ServiceOperationAsync((profile, token) =>
        {
            var results = new List<GamingServiceResult>();
            foreach (var saved in profile.Originals.Where(s => only == null || s.Name == only).ToList())
            { if (token.IsCancellationRequested) break; results.Add(profile.Restore(saved.Name)); }
            return results;
        });
        await _serviceTask;
    }
    private async void BtnServicesRestore_Click(object sender, RoutedEventArgs e) => await RestoreServicesAsync(null);
    private async void BtnServiceRestoreOne_Click(object sender, RoutedEventArgs e)
    { if (sender is FrameworkElement { Tag: string name }) await RestoreServicesAsync(name); }
    private void BtnServicesCancel_Click(object sender, RoutedEventArgs e) => _serviceCts?.Cancel();

    private void BtnServicesAudit_Click(object sender, RoutedEventArgs e)
    {
        var input = new OpenFileDialog { Filter = "Lista de serviços (*.txt)|*.txt", Title = "Validar referência de serviços (somente leitura)" };
        if (input.ShowDialog(this) != true) return;
        try
        {
            string report = GamingServiceCatalog.AuditExport(File.ReadAllText(input.FileName));
            var output = new SaveFileDialog { Filter = "Relatório (*.txt)|*.txt", FileName = "servicos-validacao.txt" };
            if (output.ShowDialog(this) == true) { File.WriteAllText(output.FileName, report); OptServicesResult.Text = "Validação exportada. Nenhuma configuração foi importada ou aplicada."; }
        }
        catch (Exception ex) { OptServicesResult.Text = "Não foi possível validar a lista: " + ex.Message; }
    }
}
