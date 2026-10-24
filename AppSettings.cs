using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ResetExplode;

public sealed record BenchRun(DateTime Date, double? Multi, double? Single, bool Sustained, string Cpu);

/// <summary>Resultado dos benchmarks de jogo (Test = "gpu", "sim" ou "mem"). Score é usado para a barra do histórico.</summary>
public sealed record BenchRecord(DateTime Date, string Test, double Score, string Summary);

/// <summary>Programa extra que abre antes do jogo (ex.: ferramenta de comando/monitoramento) e pode fechar junto com ele.</summary>
public sealed class HelperProgram
{
    public string Path { get; set; } = "";
    public string Args { get; set; } = "";
    public bool CloseWithGame { get; set; } = true;
    public string Display => System.IO.Path.GetFileName(Path) + (string.IsNullOrWhiteSpace(Args) ? "" : " " + Args);
}

/// <summary>Perfil de um jogo na aba RESET JOGOS: desempenho, argumentos de gráficos e programas auxiliares.</summary>
public sealed class GameProfile
{
    public string Priority { get; set; } = "normal";   // normal | above | high
    public bool HighPerfPlan { get; set; }
    public bool GameMode { get; set; }
    public List<string> TemporaryServices { get; set; } = new();
    public bool GpuHigh { get; set; }
    public bool DisableFso { get; set; }
    public string? ExePath { get; set; }
    public string WinMode { get; set; } = "default";   // default | full | borderless | window
    public string Res { get; set; } = "default";       // default | 1280x720 | 1920x1080 | 2560x1440 | 3840x2160
    public int Hz { get; set; }                        // 0 = não definir
    public string Style { get; set; } = "source";      // source | unity | unreal
    public string Extra { get; set; } = "";
    public List<HelperProgram> Helpers { get; set; } = new();
}

/// <summary>Jogo adicionado à mão (qualquer .exe).</summary>
public sealed class CustomGame
{
    public string Name { get; set; } = "";
    public string Exe { get; set; } = "";
    public string Args { get; set; } = "";
}

/// <summary>Configurações em %AppData%\ResetExplode. A chave da API é cifrada com DPAPI (só este usuário do Windows a lê).</summary>
public sealed class AppSettings
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string? LoadWarning { get; private set; }
    [System.Text.Json.Serialization.JsonIgnore]
    public bool RecoveryRequired { get; private set; }
    public static string Folder { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ResetExplode");
    private static string FilePath => Path.Combine(Folder, "settings.json");
    [System.Text.Json.Serialization.JsonIgnore]
    internal string StoragePath { get; set; } = FilePath;

    public string? HowManyFpsKeyProtected { get; set; }

    // RESET BENCH: últimos resultados (mais recente primeiro)
    public List<BenchRun> BenchRuns { get; set; } = new();
    public List<BenchRecord> BenchRecords { get; set; } = new();

    // RESET OTIMIZA: valores originais dos ajustes do Windows antes da primeira alteração ("<ausente>" = não existia)
    public Dictionary<string, string?> TweakBackup { get; set; } = new();
    /// <summary>ComDB original (reservas de números COM) antes da primeira "liberação".</summary>
    public byte[]? ComDbBackup { get; set; }
    /// <summary>Programas de inicialização removidos, para "Restaurar removidos".</summary>
    public List<StartupBackup> StartupRemoved { get; set; } = new();
    public bool UniversalGameMode { get; set; }
    public List<OptimizationRun> OptimizationHistory { get; set; } = new();

    // RESET JOGOS
    public Dictionary<string, GameProfile> GameProfiles { get; set; } = new();
    public List<CustomGame> CustomGames { get; set; } = new();

    // Aparência
    public bool LightTheme { get; set; }

    // Overlay
    public string OverlayCorner { get; set; } = "TL";
    public double OverlayScale { get; set; } = 1.0;
    /// <summary>Itens escolhidos para o overlay (chaves de OverlayMetrics); null = padrão.</summary>
    public List<string>? OverlayItems { get; set; }

    [System.Text.Json.Serialization.JsonIgnore] // nunca gravar a chave em texto puro
    public string? HowManyFpsKey
    {
        get
        {
            if (string.IsNullOrEmpty(HowManyFpsKeyProtected)) return null;
            try
            {
                var bytes = ProtectedData.Unprotect(Convert.FromBase64String(HowManyFpsKeyProtected), null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(bytes);
            }
            catch { return null; }
        }
        set
        {
            HowManyFpsKeyProtected = string.IsNullOrWhiteSpace(value)
                ? null
                : Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(value.Trim()), null, DataProtectionScope.CurrentUser));
        }
    }

    public static AppSettings Load() => LoadFrom(FilePath);

    internal static AppSettings LoadFrom(string filePath)
    {
        Exception? failure = null;
        foreach (string path in new[] { filePath, filePath + ".bak" })
        {
            if (!File.Exists(path)) continue;
            try
            {
                var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path))
                    ?? throw new InvalidDataException("Configurações vazias.");
                if (settings.TweakBackup == null || settings.GameProfiles == null || settings.BenchRecords == null ||
                    settings.BenchRuns == null || settings.CustomGames == null || settings.StartupRemoved == null)
                    throw new InvalidDataException("Configurações incompletas.");
                settings.StoragePath = filePath;
                settings.OptimizationHistory ??= new();
                if (path != filePath)
                {
                    // Preserva a cópia válida: não a substitui pelo arquivo corrompido.
                    AtomicFile.Write(filePath, JsonSerializer.Serialize(settings), keepBackup: false);
                    settings.LoadWarning = "Configurações recuperadas da cópia de segurança.";
                }
                return settings;
            }
            catch (Exception ex) { failure = ex; }
        }
        return new AppSettings
        {
            StoragePath = filePath,
            RecoveryRequired = failure != null,
            LoadWarning = failure == null ? null : "Não foi possível recuperar as configurações. Os arquivos foram preservados em " + Folder + ": " + failure.Message
        };
    }

    public void Save()
    {
        if (RecoveryRequired) throw new InvalidOperationException(LoadWarning);
        AtomicFile.Write(StoragePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }
}
