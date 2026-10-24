using System.IO;
using System.Text.Json;

namespace ResetExplode;

public static class PendingRecovery
{
    /// <summary>Consulta somente os diários locais; não lê nem altera o SCM/Registro.</summary>
    public static List<string> Describe(string folder)
    {
        var items = new List<string>();
        string energyPath = Path.Combine(folder, "game-session.json");
        if (File.Exists(energyPath))
        {
            try
            {
                var session = JsonSerializer.Deserialize<GameModeSession>(File.ReadAllText(energyPath)) ?? throw new InvalidDataException("Registro vazio.");
                if (session.Version != 1 || string.IsNullOrWhiteSpace(session.Game) || (session.OriginalPlan == null && !session.PauseCapture)) throw new InvalidDataException("Registro incompleto.");
                items.Add(session.Game + ": " + string.Join(" e ", new[] { session.OriginalPlan != null ? "plano de energia" : null, session.PauseCapture ? "captura do Windows" : null }.Where(s => s != null)) + " aguardam restauração.");
            }
            catch { items.Add("Energia/captura: diário ilegível ou incompatível. Arquivo preservado; a recuperação pode exigir revisão."); }
        }
        string servicesPath = Path.Combine(folder, "game-services-session.json");
        if (File.Exists(servicesPath) || File.Exists(servicesPath + ".services"))
        {
            try
            {
                var session = JsonSerializer.Deserialize<TemporaryServiceSession.Journal>(File.ReadAllText(servicesPath)) ?? throw new InvalidDataException("Manifesto vazio.");
                if (session.Version != 1 || session.Services == null || session.Services.Count == 0 || session.Services.Any(n => GamingServiceCatalog.Find(n) == null)) throw new InvalidDataException("Manifesto inválido.");
                items.Add(session.Game + ": sessão de serviços pendente. Seleção: " + string.Join(", ", session.Services) + ".");
                if (File.Exists(servicesPath + ".services"))
                {
                    using var document = JsonDocument.Parse(File.ReadAllText(servicesPath + ".services"));
                    foreach (var original in document.RootElement.GetProperty("Originals").EnumerateArray())
                    {
                        string name = original.GetProperty("Name").GetString() ?? "";
                        if (GamingServiceCatalog.Find(name) == null) throw new InvalidDataException("Nome não validado.");
                        int start = original.GetProperty("Start").GetInt32();
                        string startup = start switch { 2 => original.GetProperty("Delayed").GetBoolean() ? "automático atrasado" : "automático", 3 => "manual", 4 => "desabilitado", _ => "não identificado" };
                        items.Add($"• {name}: restaurar início {startup}, estado {original.GetProperty("State").GetString()}.");
                    }
                }
            }
            catch { items.Add("Serviços temporários: manifesto/backup ilegível, ausente ou incompatível. Arquivos preservados para revisão."); }
        }
        return items;
    }
}
