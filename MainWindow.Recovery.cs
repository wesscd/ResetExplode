using System.Windows;

namespace ResetExplode;

public partial class MainWindow
{
    private void RefreshPendingRecovery()
    {
        if (RecoveryBanner == null) return;
        var items = PendingRecovery.Describe(AppSettings.Folder);
        bool pending = items.Count > 0 && _launchCts == null;
        RecoveryBanner.Visibility = pending ? Visibility.Visible : Visibility.Collapsed;
        RecoveryBannerDetails.Text = string.Join("\n", items);
        BtnBannerRecover.IsEnabled = !_optimizationBusy && !AnyBenchRunning && !TestRunning;
    }
}
