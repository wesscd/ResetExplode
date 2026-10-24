using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace ResetExplode;

public partial class MainWindow
{
    public sealed record OptimizationGpu(string Name, string Vendor);
    private List<OptimizationChoice>? _extremeChoices;

    private void InitExtremeOptimization()
    {
        OptUniversalMode.IsChecked = _settings.UniversalGameMode;
        if (_extremeChoices == null) BuildExtremeSuggestion();
        string? keep = OptGpuTarget.SelectedValue as string;
        var targets = _gpus.Select(g => new OptimizationGpu(g.Name, g.Vendor)).ToList();
        foreach (string vendor in new[] { "AMD", "NVIDIA", "Intel" })
            if (!targets.Any(t => t.Vendor.Equals(vendor, StringComparison.OrdinalIgnoreCase)))
                targets.Add(new(vendor + " · guia (GPU não detectada)", vendor));
        OptGpuTarget.ItemsSource = targets;
        OptGpuTarget.SelectedValue = keep ?? _gpu?.Vendor ?? targets.FirstOrDefault()?.Vendor;
        RefreshExtremeHistory();
        RefreshTemporaryHistory();
    }

    private void BuildExtremeSuggestion()
    {
        _extremeChoices = GamingOptimization.Build(_tweaks, Optimizer.OnBattery, _monitor.CpuTemperature, MainGpuTemp());
        OptExtremeSteps.ItemsSource = _extremeChoices;
    }

    private void RefreshExtremeHistory() => OptExtremeHistory.ItemsSource = _settings.OptimizationHistory.Take(5).Select(r => r.Summary).ToList();

    private void BtnExtremeRebuild_Click(object sender, RoutedEventArgs e)
    {
        if (_optimizationBusy) return;
        RefreshOptimizationState();
        BuildExtremeSuggestion();
        OptExtremeResult.Text = "Sugestão recriada com as leituras atuais. Revise os itens antes de aplicar.";
    }

    private async void BtnExtremeApply_Click(object sender, RoutedEventArgs e)
    {
        if (!CanChangeOptimization()) return;
        var selected = _extremeChoices?.Where(c => c.Selected && c.Available).ToList() ?? new();
        if (selected.Count == 0) { OptExtremeResult.Text = "Nenhum ajuste selecionado. Abra a seleção para escolher os itens."; return; }
        string preview = string.Join("\n\n", selected.Select(c => c.Title + "\n" + c.Preview + "\n" + c.Note));
        if (!ReviewExtremeSelection(preview)) return;
        _optimizationBusy = true;
        BtnExtremeApply.IsEnabled = false;
        try
        {
            await System.Windows.Threading.Dispatcher.Yield(DispatcherPriority.Background);
            var results = GamingOptimization.Apply(selected, _settings);
            var run = new OptimizationRun(DateTime.Now, results);
            _settings.OptimizationHistory.Insert(0, run);
            _settings.OptimizationHistory = _settings.OptimizationHistory.Take(20).ToList();
            string historyError = "";
            try { _settings.Save(); }
            catch (Exception ex) { historyError = "\nNão foi possível gravar o histórico: " + ex.Message; }
            OptExtremeResult.Text = run.Summary + "\n" + string.Join("\n", results.Select(r => r.Title + ": " + r.Detail)) + historyError;
            RefreshOptimizationState();
            UpdateOptimizationDiagnosis();
            RefreshExtremeHistory();
        }
        catch (Exception ex) { OptExtremeResult.Text = "Aplicação incompleta: " + ex.Message + ". Confira os backups e restaure os itens necessários."; }
        finally { _optimizationBusy = false; BtnExtremeApply.IsEnabled = true; }
    }

    private void OptUniversalMode_Click(object sender, RoutedEventArgs e)
    {
        bool old = _settings.UniversalGameMode;
        try { _settings.UniversalGameMode = OptUniversalMode.IsChecked == true; _settings.Save(); }
        catch (Exception ex) { _settings.UniversalGameMode = old; OptUniversalMode.IsChecked = old; OptExtremeResult.Text = ex.Message; }
        LUniversalModeNote.Visibility = _settings.UniversalGameMode ? Visibility.Visible : Visibility.Collapsed;
    }

    private bool ReviewExtremeSelection(string preview, string heading = "AJUSTES GLOBAIS DO WINDOWS", string? note = null)
    {
        var dialog = new Window
        {
            Owner = this, Title = "Revisar perfil Extremo", WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Width = Math.Min(740, SystemParameters.WorkArea.Width * .9), Height = Math.Min(640, SystemParameters.WorkArea.Height * .85),
            Background = Card, Foreground = Fg, FontFamily = FontFamily, FontSize = 13
        };
        var root = new DockPanel { Margin = new Thickness(24) };
        var footer = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 18, 0, 0) };
        var cancel = new Button { Content = "VOLTAR", IsCancel = true, Style = (Style)FindResource("OutlineButton"), Margin = new Thickness(0, 0, 8, 0) };
        var apply = new Button { Content = "APLICAR AJUSTES", Style = (Style)FindResource("PrimaryButton") };
        apply.Click += (_, _) => dialog.DialogResult = true;
        footer.Children.Add(cancel); footer.Children.Add(apply);
        DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer);
        root.Children.Add(new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                  Text = heading + "\n\n" + preview + "\n\n" + (note ??
                      "Permanecem aplicados até restaurar. Os originais serão guardados antes de cada mudança. Não há ganho de FPS medido.")
            }
        });
        dialog.Content = root;
        return dialog.ShowDialog() == true;
    }

    private void OptGpuTarget_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (OptGpuGuides != null) OptGpuGuides.ItemsSource = GpuOptimizationGuides.ForVendor(OptGpuTarget.SelectedValue as string ?? "");
    }

    private void OpenOptimizationTarget(string target)
    {
        try { Process.Start(new ProcessStartInfo(target) { UseShellExecute = true }); }
        catch (Exception ex) { OptExtremeResult.Text = "Não foi possível abrir: " + ex.Message; }
    }

    private void OptGuideSource_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string source }) OpenOptimizationTarget(source);
    }

    private void BtnOptGpuPanel_Click(object sender, RoutedEventArgs e)
    {
        string vendor = (OptGpuTarget.SelectedValue as string ?? "").ToUpperInvariant();
        string folder = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        string? panel = vendor switch
        {
            "AMD" => Path.Combine(folder, @"AMD\CNext\CNext\RadeonSoftware.exe"),
            "NVIDIA" => Path.Combine(folder, @"NVIDIA Corporation\Control Panel Client\nvcplui.exe"),
            _ => null
        };
        OpenOptimizationTarget(panel != null && File.Exists(panel) ? panel : vendor switch
        {
            "AMD" => "https://www.amd.com/en/support",
            "NVIDIA" => "https://www.nvidia.com/en-us/software/nvidia-app/",
            "INTEL" => "https://www.intel.com/content/www/us/en/support/articles/000100501/graphics.html",
            _ => "ms-settings:display-advancedgraphics"
        });
    }

    private async void BtnOptSecurity_Click(object sender, RoutedEventArgs e)
    {
        BtnOptSecurity.IsEnabled = false;
        OptSecurityText.Text = "Consultando o estado de segurança do Windows…";
        try { OptSecurityText.Text = await Task.Run(GamingSecurity.Read); }
        finally { BtnOptSecurity.IsEnabled = true; }
    }

    private void BtnOptSettings_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string key })
            OpenOptimizationTarget(key == "security" ? "ms-settings:windowsdefender" : "ms-settings:display-advancedgraphics");
    }
    private void BtnOptDrivers_Click(object sender, RoutedEventArgs e) => NavDriver.IsChecked = true;
    private void BtnOptNetwork_Click(object sender, RoutedEventArgs e) => NavNet.IsChecked = true;
    private void BtnOptStartup_Click(object sender, RoutedEventArgs e) => NavStartup.IsChecked = true;
}
