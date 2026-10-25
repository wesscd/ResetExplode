using System.IO;
using System.Text.Json;
using Microsoft.Win32;

namespace ResetExplode;

/// <summary>Diário persistente de ajustes temporários. Sobrevive a fechamento forçado.</summary>
public sealed class GameModeSession
{
    private const string HighPerformance = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";
    private static string JournalPath => Path.Combine(AppSettings.Folder, "game-session.json");
    private static readonly (string Path, string Name)[] CaptureValues =
    {
        (@"System\GameConfigStore", "GameDVR_Enabled"),
        (@"Software\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled")
    };
    internal sealed record Access(Func<string?> ReadPlan, Action<string> SetPlan, Action EnsurePlan,
        Func<int, int?> ReadCapture, Action<int, int?> SetCapture);

    private static Access NativeAccess => new(
        WindowsTweaks.ActiveScheme,
        plan => WindowsTweaks.PowerCfg($"/setactive {plan}"),
        () =>
        {
            if (!WindowsTweaks.PowerCfg("/list").Contains(HighPerformance, StringComparison.OrdinalIgnoreCase))
                WindowsTweaks.PowerCfg($"/duplicatescheme {HighPerformance} {HighPerformance}");
        },
        i =>
        {
            using var key = Registry.CurrentUser.OpenSubKey(CaptureValues[i].Path);
            object? value = key?.GetValue(CaptureValues[i].Name);
            if (value != null && value is not int) throw new InvalidOperationException("A captura possui um valor de Registro inesperado.");
            return value as int?;
        },
        (i, value) =>
        {
            using var key = Registry.CurrentUser.CreateSubKey(CaptureValues[i].Path);
            if (value is int number) key.SetValue(CaptureValues[i].Name, number, RegistryValueKind.DWord);
            else key.DeleteValue(CaptureValues[i].Name, false);
        });

    private Access _access = NativeAccess;
    private string _journalPath = JournalPath;

    public string Game { get; set; } = "";
    public int Version { get; set; } = 1;
    public string? OriginalPlan { get; set; }
    public bool PauseCapture { get; set; }
    public List<int?> OriginalCapture { get; set; } = new();
    public static bool HasPending => File.Exists(JournalPath);

    public static GameModeSession Start(string game, bool power, bool pauseCapture)
        => Start(game, power, pauseCapture, JournalPath, NativeAccess);

    internal static GameModeSession Start(string game, bool power, bool pauseCapture, string journalPath, Access access)
    {
        if (File.Exists(journalPath)) throw new InvalidOperationException("Há ajustes temporários pendentes. Restaure a sessão na aba Otimização antes de jogar novamente.");
        var session = new GameModeSession { Game = game, PauseCapture = pauseCapture, _journalPath = journalPath, _access = access };
        if (power)
        {
            session.OriginalPlan = access.ReadPlan()
                ?? throw new InvalidOperationException("Não foi possível guardar o plano de energia atual.");
        }
        if (pauseCapture)
        {
            for (int i = 0; i < CaptureValues.Length; i++) session.OriginalCapture.Add(access.ReadCapture(i));
        }
        AtomicFile.Write(journalPath, JsonSerializer.Serialize(session), keepBackup: false);
        OptimizationLog.Write("MODO JOGO", "BACKUP", game, "Originais da sessão persistidos antes de alterar energia/captura.", session.OriginalPlan);
        try
        {
            if (power)
            {
                access.EnsurePlan();
                access.SetPlan(HighPerformance);
            }
            if (pauseCapture)
                for (int i = 0; i < CaptureValues.Length; i++) access.SetCapture(i, 0);
            OptimizationLog.Write("MODO JOGO", "APLICADO", game, $"Energia temporária={power}; pausa de captura={pauseCapture}.");
            return session;
        }
        catch (Exception ex)
        {
            var pending = session.Restore();
            throw new InvalidOperationException(ex.Message + (pending.Count > 0 ? " Restauração pendente: " + string.Join("; ", pending) : " Ajustes temporários restaurados."), ex);
        }
    }

    public static List<string> Recover() => RecoverFrom(JournalPath);

    internal static List<string> RecoverFrom(string path, Access? access = null)
    {
        if (!File.Exists(path)) return new();
        try
        {
            var session = JsonSerializer.Deserialize<GameModeSession>(File.ReadAllText(path))
                ?? throw new InvalidDataException("Registro de sessão vazio.");
            if (session.Version != 1 || string.IsNullOrWhiteSpace(session.Game) ||
                (session.OriginalPlan == null && !session.PauseCapture))
                throw new InvalidDataException("Registro de sessão incompleto ou incompatível; arquivo preservado.");
            session._access = access ?? NativeAccess;
            return session.Restore(path);
        }
        catch (Exception ex) { return new() { ex.Message }; }
    }

    public List<string> Restore() => Restore(_journalPath);

    private List<string> Restore(string journalPath)
    {
        var errors = new List<string>();
        if (OriginalPlan != null)
        {
            try
            {
                if (!Guid.TryParse(OriginalPlan, out var plan)) throw new InvalidDataException("Plano de energia inválido no registro da sessão.");
                _access.SetPlan($"{plan:D}");
            }
            catch (Exception ex) { errors.Add("Energia: " + ex.Message); }
        }
        if (PauseCapture)
        {
            if (OriginalCapture == null || OriginalCapture.Count != CaptureValues.Length)
                errors.Add("Backup de captura incompleto; registro de sessão preservado.");
            else
                for (int i = 0; i < CaptureValues.Length; i++)
                {
                    try
                    {
                        _access.SetCapture(i, OriginalCapture[i]);
                    }
                    catch (Exception ex) { errors.Add("Captura: " + ex.Message); }
                }
        }
        if (errors.Count == 0)
            try { File.Delete(journalPath); } catch (Exception ex) { errors.Add(ex.Message); }
        OptimizationLog.Write("MODO JOGO", errors.Count == 0 ? "RESTAURADO" : "PENDENTE", Game, errors.Count == 0 ? "Energia/captura restauradas." : string.Join("; ", errors));
        return errors;
    }
}
