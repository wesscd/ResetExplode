using System.IO;
using System.Text.Json;

namespace ResetExplode;

public sealed record ServiceSessionHistory(Guid Id, string Game, DateTimeOffset Started, DateTimeOffset Updated,
    bool Restored, List<string> Events)
{
    public string Summary => $"{Updated.ToLocalTime():dd/MM HH:mm} · {Game} · {(Restored ? "RESTAURADO" : "RESTAURAÇÃO PENDENTE")}\n" + string.Join("\n", Events);
}

/// <summary>Transação de serviços por jogo, isolada do perfil permanente. Nunca fecha aplicativos.</summary>
public sealed class TemporaryServiceSession
{
    public sealed class Journal
    {
        public int Version { get; set; } = 1;
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Game { get; set; } = "";
        public DateTimeOffset Started { get; set; } = DateTimeOffset.Now;
        public List<string> Services { get; set; } = new();
        public List<string> Events { get; set; } = new();
    }
    private readonly string _path;
    private readonly IGamingServiceAccess _access;
    private readonly Journal _journal;
    private static string DefaultPath => Path.Combine(AppSettings.Folder, "game-services-session.json");
    private static string HistoryPath(string path) => Path.Combine(Path.GetDirectoryName(path)!, "game-services-history.json");
    public static bool HasPending => File.Exists(DefaultPath) || File.Exists(DefaultPath + ".services");
    private TemporaryServiceSession(string path, IGamingServiceAccess access, Journal journal)
    { _path = path; _access = access; _journal = journal; }
    private void Save() => AtomicFile.Write(_path, JsonSerializer.Serialize(_journal), keepBackup: false);

    public static TemporaryServiceSession Start(string game, IEnumerable<string> services) => Start(game, services, DefaultPath, new WindowsServiceAccess());
    internal static TemporaryServiceSession Start(string game, IEnumerable<string> services, string path, IGamingServiceAccess access)
    {
        if (File.Exists(path) || File.Exists(path + ".services")) throw new InvalidOperationException("Há uma sessão de serviços pendente. Restaure antes de jogar.");
        var names = services.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (string.IsNullOrWhiteSpace(game) || names.Count == 0 || names.Any(n => GamingServiceCatalog.Find(n) == null))
            throw new InvalidDataException("Seleção temporária inválida; nenhuma alteração aplicada.");
        var session = new TemporaryServiceSession(path, access, new() { Game = game, Services = names });
        session.Save(); // manifesto antes de qualquer configuração
        try
        {
            var profile = new GamingServiceProfile(path + ".services", access);
            foreach (string name in names)
            {
                var result = profile.Apply(name);
                session._journal.Events.Add(name + ": " + result.Detail);
                session.Save();
                if (!result.Success) throw new IOException(name + ": " + result.Detail);
            }
            return session;
        }
        catch (Exception ex)
        {
            var errors = session.Restore();
            throw new InvalidOperationException("Perfil temporário incompleto: " + ex.Message + (errors.Count == 0 ? " Alterações restauradas." : " Restauração pendente: " + string.Join("; ", errors)), ex);
        }
    }

    public static List<string> Recover() => Recover(DefaultPath, new WindowsServiceAccess());
    internal static List<string> Recover(string path, IGamingServiceAccess access)
    {
        if (!File.Exists(path) && !File.Exists(path + ".services")) return new();
        try
        {
            // Um backup sem manifesto não é descartado ou aplicado por adivinhação.
            var journal = JsonSerializer.Deserialize<Journal>(File.ReadAllText(path)) ?? throw new InvalidDataException("Manifesto vazio.");
            if (journal.Version != 1 || journal.Id == Guid.Empty || string.IsNullOrWhiteSpace(journal.Game) || journal.Events == null ||
                journal.Services == null || journal.Services.Count == 0 || journal.Services.Any(n => GamingServiceCatalog.Find(n) == null))
                throw new InvalidDataException("Manifesto temporário inválido; arquivos preservados.");
            return new TemporaryServiceSession(path, access, journal).Restore();
        }
        catch (Exception ex) { return new() { "Serviços temporários: " + ex.Message }; }
    }

    public List<string> Restore()
    {
        var errors = new List<string>();
        try
        {
            var profile = new GamingServiceProfile(_path + ".services", _access);
            if (profile.Originals.Any(s => !_journal.Services.Contains(s.Name, StringComparer.OrdinalIgnoreCase)))
                throw new InvalidDataException("Backup não corresponde à seleção temporária; arquivos preservados.");
            foreach (var original in profile.Originals.ToArray().Reverse())
            {
                var result = profile.Restore(original.Name);
                _journal.Events.Add(original.Name + ": " + result.Detail);
                if (!result.Success) errors.Add(original.Name + ": " + result.Detail);
            }
            Save();
            var historyPath = HistoryPath(_path);
            var history = ReadHistory(historyPath);
            history.RemoveAll(r => r.Id == _journal.Id);
            history.Insert(0, new(_journal.Id, _journal.Game, _journal.Started, DateTimeOffset.Now, errors.Count == 0, _journal.Events.ToList()));
            AtomicFile.Write(historyPath, JsonSerializer.Serialize(history.Take(50).ToList()));
            if (errors.Count == 0)
            {
                File.Delete(_path + ".services");
                File.Delete(_path + ".services.bak");
                File.Delete(_path);
            }
        }
        catch (Exception ex) { errors.Add("Serviços temporários: " + ex.Message); }
        return errors;
    }

    public static List<ServiceSessionHistory> History() => ReadHistory(HistoryPath(DefaultPath));
    internal static List<ServiceSessionHistory> ReadHistory(string path)
    {
        if (!File.Exists(path)) return new();
        var history = JsonSerializer.Deserialize<List<ServiceSessionHistory>>(File.ReadAllText(path)) ?? throw new InvalidDataException("Histórico vazio; arquivo preservado.");
        if (history.Any(h => h == null || h.Events == null)) throw new InvalidDataException("Histórico inválido; arquivo preservado.");
        return history;
    }
}
