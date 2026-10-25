using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace ResetExplode;

public partial class MainWindow
{
    private readonly List<OptimizationLogEntry> _consoleEntries = new();
    private OptimizationLogStore? _consoleStore;
    private string _consoleReadWarning = "";
    private void InitOptimizationConsole()
    {
        _consoleStore = new(Path.Combine(AppSettings.Folder, "logs"));
        OptimizationLog.Store = _consoleStore;
        try
        {
            var history = _consoleStore.ReadRecent();
            _consoleEntries.AddRange(history.Entries);
            if (history.Unreadable > 0) _consoleReadWarning = $"{history.Unreadable} registro(s) ilegível(is) preservados. ";
        }
        catch (Exception ex) { _consoleReadWarning = "Não foi possível ler o histórico: " + ex.Message + ". "; }
        _consoleStore.Written += ConsoleEntryWritten;
        RenderOptimizationConsole();
        OptimizationLog.Write("SISTEMA", "INFO", "Sessão", "BaraoTechExplode iniciado. Analisar não aplica mudanças. Marque itens e confirme a revisão para aplicar.");
    }
    private void ConsoleEntryWritten(OptimizationLogEntry entry)
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            _consoleEntries.Add(entry);
            if (_consoleEntries.Count > 500) _consoleEntries.RemoveAt(0);
            OptConsoleLatest.Text = entry.Line;
            OptConsoleStorage.Text = _consoleReadWarning + (entry.StorageError is string error ? "ATENÇÃO: registro exibido, mas não salvo em disco: " + error : "Logs locais em " + _consoleStore?.Folder);
            RenderOptimizationConsole();
        }));
    }
    private void RenderOptimizationConsole()
    {
        if (OptConsoleText == null || OptConsoleFilter == null) return;
        double vertical = OptConsoleText.VerticalOffset, horizontal = OptConsoleText.HorizontalOffset;
        int selection = OptConsoleText.SelectionStart, length = OptConsoleText.SelectionLength;
        string filter = OptConsoleFilter.Text.Trim();
        OptConsoleText.Text = string.Join(Environment.NewLine, _consoleEntries.Where(e => filter.Length == 0 || e.Line.Contains(filter, StringComparison.OrdinalIgnoreCase)).Select(e => e.Line));
        if (OptConsoleFollow?.IsChecked != false) OptConsoleText.ScrollToEnd();
        else { OptConsoleText.ScrollToVerticalOffset(vertical); OptConsoleText.ScrollToHorizontalOffset(horizontal); OptConsoleText.Select(Math.Min(selection, OptConsoleText.Text.Length), Math.Min(length, Math.Max(0, OptConsoleText.Text.Length - selection))); }
    }
    private void OptConsoleFilter_Changed(object sender, TextChangedEventArgs e) => RenderOptimizationConsole();
    private void BtnCopyConsole_Click(object sender, RoutedEventArgs e)
    {
        try { Clipboard.SetText(OptConsoleText.Text); OptConsoleStorage.Text = "Console copiado. O texto pode conter caminhos locais e nomes de programas."; }
        catch (Exception ex) { OptConsoleStorage.Text = "Falha ao copiar: " + ex.Message; }
    }
    private void BtnExportConsole_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog { Filter = "Console de otimização|*.txt", FileName = "console-otimizacao.txt" };
        if (dialog.ShowDialog(this) != true) return;
        try { AtomicFile.Write(dialog.FileName, OptConsoleText.Text); OptConsoleStorage.Text = "Console exportado: " + dialog.FileName; }
        catch (Exception ex) { OptConsoleStorage.Text = "Falha ao exportar: " + ex.Message; }
    }
    private void BtnOpenLogs_Click(object sender, RoutedEventArgs e)
    {
        try { Directory.CreateDirectory(_consoleStore!.Folder); Process.Start(new ProcessStartInfo(_consoleStore.Folder) { UseShellExecute = true }); }
        catch (Exception ex) { OptConsoleStorage.Text = "Não foi possível abrir a pasta: " + ex.Message; }
    }
    private void BtnOptimizationSection_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string target }) return;
        if (target == "game") { NavLauncher.IsChecked = true; return; }
        if (FindName(target) is FrameworkElement section) { if (section is Expander expander) expander.IsExpanded = true; section.BringIntoView(new Rect(0, 0, 1, 1)); }
    }
    private void ShowOptimizationBlock(string message)
    {
        OptTweakResult.Text = OptExtremeResult.Text = OptLiveStatus.Text = OptServicesResult.Text = message;
        OptWorkflowStatus.Text = message;
        OptimizationLog.Write("CONTROLE", "BLOQUEADO", "Otimização", message);
    }

    private string? OptimizationActivityBlockReason()
    {
        if (_measurementCts != null) return "Há uma medição de desempenho em andamento. Aguarde os 60 segundos ou cancele a medição na Biblioteca.";
        if (_launchCts != null) return "Há um jogo em acompanhamento. Termine o jogo ou pare o acompanhamento na Biblioteca para restaurar o perfil temporário antes de alterar o Windows.";
        if (_optimizationBusy || _serviceBusy) return "Há uma operação de otimização em andamento. Aguarde o resultado no console antes de iniciar outra.";
        if (AnyBenchRunning || TestRunning) return "Há um teste ou diagnóstico em andamento. Termine ou cancele a atividade antes de aplicar otimizações.";
        if (GameModeSession.HasPending || TemporaryServiceSession.HasPending) return "Existe uma sessão temporária pendente. Use RESTAURAR SESSÃO PENDENTE no aviso superior antes de aplicar novas otimizações.";
        return null;
    }
}
