using System.Windows;

namespace ResetExplode;

public partial class MainWindow
{
    private void RefreshTemporaryHistory()
    {
        try
        {
            var history = TemporaryServiceSession.History();
            OptTemporaryHistory.ItemsSource = history.Take(10).Select(h => h.Summary).ToList();
            OptTemporaryHistoryStatus.Text = history.Count == 0 ? "Nenhuma sessão de serviços temporários registrada." :
                "Últimas sessões e tentativas de restauração. Falhas preservam os originais para nova tentativa pelo botão RESTAURAR SESSÃO PENDENTE.";
        }
        catch (Exception ex) { OptTemporaryHistoryStatus.Text = "Histórico indisponível; arquivo preservado: " + ex.Message; }
    }
    private void BtnTemporaryHistory_Click(object sender, RoutedEventArgs e) => RefreshTemporaryHistory();
}
