using System.Windows;

namespace ResetExplode;

public partial class MainWindow
{
    private void BtnAboutFork_Click(object sender, RoutedEventArgs e) => MessageBox.Show(this,
        "BaraoTechExplode · Barão Tech\n\n" +
        "Este projeto é um fork do RESET EXPLODE. O código principal foi desenvolvido por Gustavo da Reset (GustavoReset).\n\n" +
        "O fork aproveita a leitura de sensores, hardware e desempenho do projeto original para desenvolver diagnóstico e otimização para jogos, com prévia de impactos, backup e restauração.\n\n" +
        "Projeto original: https://github.com/GustavoReset/ResetExplode\nBarão Tech: https://baraotech.online/\n\n" +
        "Licença MIT. Copyright (c) 2026 Gustavo (GustavoReset). Aviso original preservado em LICENSE. Este fork não é uma versão oficial do projeto original.",
        "BaraoTechExplode · Origem e créditos", MessageBoxButton.OK, MessageBoxImage.Information);
}
