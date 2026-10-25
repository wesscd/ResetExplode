using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace ResetExplode;

/// <summary>
/// Ajuste do Windows para jogos: lê o estado atual, aplica e guarda o valor original (em AppSettings.TweakBackup)
/// na primeira vez que é alterado, para "Restaurar originais" desfazer tudo.
/// Só ajustes documentados e reversíveis; nada que reduza a segurança (Defender, VBS/HVCI, UAC, Windows Update).
/// </summary>
public sealed class Tweak : INotifyPropertyChanged
{
    public string Id { get; init; } = "";
    public string Title { get; init; } = "";
    public string Description { get; init; } = "";
    public string Effect { get; init; } = "";
    public bool NeedsRestart { get; init; }
    public bool IsSupported { get; init; } = true;
    public string Compatibility { get; init; } = "Windows 10/11";
    public string Guidance { get; init; } = "Opcional · avalie o resultado no seu jogo";
    internal Action<AppSettings> Capture { get; init; } = _ => { };
    internal Func<bool?> Read { get; init; } = () => null;
    internal Action<bool, AppSettings> Write { get; init; } = (_, _) => { };
    internal Action<AppSettings> Restore { get; init; } = _ => { };

    private bool _isOn;
    public bool IsOn { get => _isOn; set { _isOn = value; Changed(nameof(IsOn)); } }
    private string _status = "";
    public string Status { get => _status; set { _status = value; Changed(nameof(Status)); } }
    public string Badge => NeedsRestart ? "REQUER REINICIAR" : "";
    private bool _hasBackup;
    public bool HasBackup { get => _hasBackup; set { _hasBackup = value; Changed(nameof(HasBackup)); } }
    public string State => !IsSupported ? "INDISPONÍVEL" : _readKnown ? (IsOn ? "ATIVO" : "INATIVO") : "ESTADO NÃO IDENTIFICADO";
    public bool? CurrentValue => _readKnown ? IsOn : null;
    private bool _readKnown;

    public void Refresh()
    {
        _readKnown = false;
        try
        {
            bool? state = IsSupported ? Read() : null;
            _readKnown = state.HasValue;
            IsOn = state ?? false;
        }
        finally { Changed(nameof(State)); Changed(nameof(CurrentValue)); }
    }

    private void Changed(string p) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
    public event PropertyChangedEventHandler? PropertyChanged;
}

public static class WindowsTweaks
{
    private const string HighPerformance = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";

    public static List<Tweak> All() => new()
    {
        new Tweak
        {
            Id = "power", Title = "Plano de energia: Alto desempenho",
            Description = "Mantém a CPU pronta para subir o clock na hora, sem economia agressiva de energia.",
            Effect = "Pode alterar a resposta da CPU. Ganho precisa ser medido; aumenta consumo em alguns PCs.",
            Guidance = "Comparar · pode aumentar consumo e temperatura",
            Capture = s => { if (!s.TweakBackup.ContainsKey("power")) s.TweakBackup["power"] = ActiveScheme() ?? throw new InvalidOperationException("Não foi possível identificar o plano atual."); },
            Read = () => ActiveScheme()?.Equals(HighPerformance, StringComparison.OrdinalIgnoreCase),
            Write = (on, s) =>
            {
                if (!s.TweakBackup.ContainsKey("power")) s.TweakBackup["power"] = ActiveScheme();
                if (on)
                {
                    // Alguns PCs vêm sem o plano listado: duplicar o modelo padrão o recria com o mesmo GUID.
                    if (!PowerCfg("/list").Contains(HighPerformance, StringComparison.OrdinalIgnoreCase))
                        PowerCfg($"-duplicatescheme {HighPerformance} {HighPerformance}");
                    PowerCfg($"/setactive {HighPerformance}");
                }
                else if (s.TweakBackup["power"] is string prev && !prev.Equals(HighPerformance, StringComparison.OrdinalIgnoreCase))
                    PowerCfg($"/setactive {prev}");
                else PowerCfg("/setactive 381b4222-f694-41f0-9685-ff5bb260df2e"); // Equilibrado
            },
            Restore = s => { if (s.TweakBackup.GetValueOrDefault("power") is string prev) PowerCfg($"/setactive {prev}"); },
        },
        RegTweak("gamemode", "Modo de Jogo",
            "Dá prioridade ao jogo e evita que o Windows Update instale ou reinicie durante a partida.",
            "Recomendado ligado (é o padrão do Windows 11).",
            Registry.CurrentUser, @"Software\Microsoft\GameBar", "AutoGameModeEnabled", on: 1, off: 0, absentMeansOn: true),
        new Tweak
        {
            Id = "gamedvr", Title = "Desativar gravação em segundo plano (Game DVR)",
            Description = "Desliga a captura contínua do Xbox Game Bar, que grava os últimos minutos de jogo o tempo todo.",
            Effect = "Pode reduzir trabalho de captura quando ela está ativa. Você perde o \"gravar o que aconteceu\" do Game Bar.",
            Guidance = "Opcional · desliga a captura de jogos",
            Capture = s =>
            {
                CaptureDword(s, "gamedvr.a", Registry.CurrentUser, @"System\GameConfigStore", "GameDVR_Enabled");
                CaptureDword(s, "gamedvr.b", Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled");
            },
            Read = () => (ReadDword(Registry.CurrentUser, @"System\GameConfigStore", "GameDVR_Enabled") ?? 1) == 0 &&
                         (ReadDword(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled") ?? 1) == 0,
            Write = (on, s) =>
            {
                SetDword(s, "gamedvr.a", Registry.CurrentUser, @"System\GameConfigStore", "GameDVR_Enabled", on ? 0 : 1);
                SetDword(s, "gamedvr.b", Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled", on ? 0 : 1);
            },
            Restore = s =>
            {
                RestoreDword(s, "gamedvr.a", Registry.CurrentUser, @"System\GameConfigStore", "GameDVR_Enabled");
                RestoreDword(s, "gamedvr.b", Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled");
            },
        },
        RegTweak("hags", "Agendamento de GPU acelerado por hardware",
            "A própria placa de vídeo gerencia sua fila de trabalho, em vez da CPU.",
            "O efeito depende da GPU, driver e jogo. Requer suporte do hardware e reinicialização para comparar.",
            Registry.LocalMachine, @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers", "HwSchMode", on: 2, off: 1,
            absentMeansOn: false, needsRestart: true),
        new Tweak
        {
            Id = "windowed", Title = "Otimizações para jogos em janela",
            IsSupported = OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000),
            Compatibility = "Windows 11 · jogos DirectX 10/11 compatíveis",
            Guidance = "Recomendado para jogos compatíveis em janela ou sem bordas",
            Capture = s =>
            {
                using var key = Registry.CurrentUser.OpenSubKey(DxPath);
                if (!s.TweakBackup.ContainsKey("windowed")) s.TweakBackup["windowed"] = key?.GetValue("DirectXUserGlobalSettings") as string ?? "<ausente>";
            },
            Description = "Faz jogos DirectX 10/11 em janela ou \"sem bordas\" usarem o modelo de apresentação moderno (flip).",
            Effect = "Menor latência e suporte a VRR/G-Sync em janela. Recurso do Windows 11.",
            Read = () => DxSettings().TryGetValue("SwapEffectUpgradeEnable", out var value) ? value == "1" : null,
            Write = (on, s) =>
            {
                if (!s.TweakBackup.ContainsKey("windowed"))
                    s.TweakBackup["windowed"] = Registry.CurrentUser.OpenSubKey(DxPath)?.GetValue("DirectXUserGlobalSettings") as string ?? "<ausente>";
                var d = DxSettings();
                d["SwapEffectUpgradeEnable"] = on ? "1" : "0";
                using var k = Registry.CurrentUser.CreateSubKey(DxPath);
                k.SetValue("DirectXUserGlobalSettings", string.Concat(d.Select(kv => $"{kv.Key}={kv.Value};")));
            },
            Restore = s =>
            {
                if (s.TweakBackup.GetValueOrDefault("windowed") is not string prev) return;
                using var k = Registry.CurrentUser.CreateSubKey(DxPath);
                if (prev == "<ausente>") k.DeleteValue("DirectXUserGlobalSettings", false);
                else k.SetValue("DirectXUserGlobalSettings", prev);
            },
        },
        RegTweak("delivery", "Desligar compartilhamento de atualizações (P2P)",
            "Configura Delivery Optimization como HTTP Only: atualizações continuam vindo da Microsoft, sem compartilhar com outros PCs.",
            "Pode reduzir tráfego de compartilhamento. Não limita downloads normais nem garante menor ping. Políticas da organização podem prevalecer.",
            Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\DeliveryOptimization", "DODownloadMode", on: 0, off: 1,
            absentMeansOn: false, needsRestart: true),
        RegTweak("transparency", "Desligar transparências do Windows",
            "Remove transparências de elementos da interface do Windows.",
            "Muda a aparência da área de trabalho. Não há ganho garantido de FPS dentro dos jogos.",
            Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "EnableTransparency", on: 0, off: 1,
            absentMeansOn: false, needsRestart: true),
        new Tweak
        {
            Id = "animations", Title = "Reduzir animações de janelas",
            Description = "Desliga animações da área cliente e de minimizar/maximizar pela API do Windows.",
            Effect = "Preferência de interface; não reduz a qualidade gráfica do jogo nem comprova ganho de FPS.",
            Guidance = "Opcional · altera animações de aplicativos que seguem a preferência do Windows",
            Read = () => !WindowsVisualEffects.ClientAnimations() && !WindowsVisualEffects.MinimizeAnimations(),
            Capture = s =>
            {
                if (!s.TweakBackup.ContainsKey("animations.client")) s.TweakBackup["animations.client"] = WindowsVisualEffects.ClientAnimations().ToString();
                if (!s.TweakBackup.ContainsKey("animations.minimize")) s.TweakBackup["animations.minimize"] = WindowsVisualEffects.MinimizeAnimations().ToString();
            },
            Write = (on, _) => { WindowsVisualEffects.SetClient(!on); WindowsVisualEffects.SetMinimize(!on); },
            Restore = s =>
            {
                if (s.TweakBackup.GetValueOrDefault("animations.client") is string client) WindowsVisualEffects.SetClient(bool.Parse(client));
                if (s.TweakBackup.GetValueOrDefault("animations.minimize") is string minimize) WindowsVisualEffects.SetMinimize(bool.Parse(minimize));
            }
        },
        new Tweak
        {
            Id = "mouse", Title = "Desligar aceleração do mouse",
            Description = "Desmarca \"Aprimorar precisão do ponteiro\": o cursor anda sempre a mesma distância para o mesmo movimento.",
            Effect = "Não muda FPS, mas deixa a mira consistente em jogos de tiro.",
            Guidance = "Preferência de controle · não aumenta FPS",
            Capture = s =>
            {
                foreach (var n in new[] { "MouseSpeed", "MouseThreshold1", "MouseThreshold2" })
                    if (!s.TweakBackup.ContainsKey($"mouse.{n}")) s.TweakBackup[$"mouse.{n}"] = ReadString(Registry.CurrentUser, @"Control Panel\Mouse", n);
            },
            Read = () => ReadString(Registry.CurrentUser, @"Control Panel\Mouse", "MouseSpeed") == "0",
            Write = (on, s) =>
            {
                foreach (var n in new[] { "MouseSpeed", "MouseThreshold1", "MouseThreshold2" })
                    if (!s.TweakBackup.ContainsKey($"mouse.{n}")) s.TweakBackup[$"mouse.{n}"] = ReadString(Registry.CurrentUser, @"Control Panel\Mouse", n);
                var v = on ? new[] { "0", "0", "0" } : new[] { "1", "6", "10" }; // padrão do Windows
                ApplyMouse(v);
            },
            Restore = s =>
            {
                if (!s.TweakBackup.ContainsKey("mouse.MouseSpeed")) return;
                string[] names = { "MouseSpeed", "MouseThreshold1", "MouseThreshold2" };
                ApplyMouse(names.Select(n => s.TweakBackup.GetValueOrDefault($"mouse.{n}") ?? "0").ToArray());
                using var key = Registry.CurrentUser.CreateSubKey(@"Control Panel\Mouse");
                foreach (string name in names)
                    if (s.TweakBackup.GetValueOrDefault($"mouse.{name}") == null) key.DeleteValue(name, false);
            },
        },
    };

    /// <summary>Desfaz tudo que foi alterado pelo app (valores guardados antes da primeira alteração).</summary>
    public static bool HasBackup(Tweak t, AppSettings s) => s.TweakBackup.Keys.Any(k => k == t.Id || k.StartsWith(t.Id + ".", StringComparison.Ordinal));

    public static void Apply(Tweak t, bool enabled, AppSettings s)
    {
        OptimizationLog.Write("WINDOWS", "INÍCIO", t.Title, enabled ? "Solicitado ativar." : "Solicitado desativar.", t.State);
        try
        {
            if (!t.IsSupported) throw new InvalidOperationException("Ajuste incompatível com esta versão do Windows.");
            t.Capture(s);
            // O estado original precisa estar persistido ANTES de tocar no Windows.
            s.Save();
            OptimizationLog.Write("WINDOWS", "BACKUP", t.Title, "Original persistido antes da alteração.");
            t.HasBackup = HasBackup(t, s);
            t.Write(enabled, s);
            t.Refresh();
            OptimizationLog.Write("WINDOWS", t.CurrentValue == enabled ? "APLICADO" : "PENDENTE", t.Title,
                t.CurrentValue == enabled ? (t.NeedsRestart ? "Estado confirmado; requer reiniciar para avaliar." : "Estado confirmado pelo Windows.") : "O estado lido não confirmou a mudança.", after: t.State);
            }
        catch (Exception ex) { OptimizationLog.Write("WINDOWS", "FALHA", t.Title, ex.Message); throw; }
    }

    public static void RestoreOne(Tweak t, AppSettings s)
    {
        if (!HasBackup(t, s)) { OptimizationLog.Write("WINDOWS", "SEM ALTERAÇÃO", t.Title, "Nenhum original salvo para restaurar."); return; }
        OptimizationLog.Write("WINDOWS", "INÍCIO", t.Title, "Restaurando original.", t.State);
        try
        {
            t.Restore(s);
            var removed = s.TweakBackup.Where(kv => kv.Key == t.Id || kv.Key.StartsWith(t.Id + ".", StringComparison.Ordinal)).ToList();
            foreach (var kv in removed) s.TweakBackup.Remove(kv.Key);
            try { s.Save(); }
            catch { foreach (var kv in removed) s.TweakBackup[kv.Key] = kv.Value; throw; }
            t.HasBackup = false;
            t.Refresh();
            OptimizationLog.Write("WINDOWS", "RESTAURADO", t.Title, t.NeedsRestart ? "Original restaurado; requer reiniciar." : "Original restaurado.", after: t.State);
            }
        catch (Exception ex) { OptimizationLog.Write("WINDOWS", "FALHA", t.Title, "Restauração: " + ex.Message); throw; }
    }

    public static List<string> RestoreAll(IEnumerable<Tweak> tweaks, AppSettings s)
    {
        var errors = new List<string>();
        foreach (var t in tweaks.Where(t => HasBackup(t, s)))
        {
            try { RestoreOne(t, s); t.Status = "Restaurado." + (t.NeedsRestart ? " Reinicie o PC." : ""); }
            catch (Exception ex) { t.Status = "Restauração pendente: " + ex.Message; errors.Add(t.Title + ": " + ex.Message); }
        }
        return errors;
    }

    // ------------------------------------------------------------------ registro

    private static Tweak RegTweak(string id, string title, string desc, string effect, RegistryKey hive, string path, string name,
                                  int on, int off, bool absentMeansOn, bool needsRestart = false) => new()
    {
        Id = id, Title = title, Description = desc, Effect = effect, NeedsRestart = needsRestart,
        IsSupported = id != "hags" || OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041),
        Compatibility = id == "hags" ? "Windows 10 2004+ · exige GPU e driver compatíveis" : "Windows 10/11",
        Guidance = id == "hags" ? "Experimental · compare ligado/desligado após reiniciar" : "Configuração do Windows · ganho depende do cenário",
        Capture = s => CaptureDword(s, id, hive, path, name),
        Read = () => (ReadDword(hive, path, name) is int v) ? v == on : id == "hags" ? null : absentMeansOn,
        Write = (enable, s) => SetDword(s, id, hive, path, name, enable ? on : off),
        Restore = s => RestoreDword(s, id, hive, path, name),
    };

    private static int? ReadDword(RegistryKey hive, string path, string name)
    {
        using var k = hive.OpenSubKey(path);
        return k?.GetValue(name) is int v ? v : null;
    }

    private static string? ReadString(RegistryKey hive, string path, string name)
    {
        using var k = hive.OpenSubKey(path);
        return k?.GetValue(name)?.ToString();
    }

    private static void SetDword(AppSettings s, string backupKey, RegistryKey hive, string path, string name, int value)
    {
        if (!s.TweakBackup.ContainsKey(backupKey))
            s.TweakBackup[backupKey] = ReadDword(hive, path, name)?.ToString() ?? "<ausente>";
        using var k = hive.CreateSubKey(path);
        k.SetValue(name, value, RegistryValueKind.DWord);
    }

    private static void CaptureDword(AppSettings s, string id, RegistryKey hive, string path, string name)
    {
        if (s.TweakBackup.ContainsKey(id)) return;
        using var key = hive.OpenSubKey(path);
        object? value = key?.GetValue(name);
        if (value != null && value is not int) throw new InvalidOperationException("Tipo inesperado no Registro; o ajuste foi preservado.");
        s.TweakBackup[id] = value?.ToString() ?? "<ausente>";
    }

    private static void RestoreDword(AppSettings s, string backupKey, RegistryKey hive, string path, string name)
    {
        if (s.TweakBackup.GetValueOrDefault(backupKey) is not string prev) return;
        using var k = hive.CreateSubKey(path);
        if (prev == "<ausente>") k.DeleteValue(name, false);
        else k.SetValue(name, int.Parse(prev), RegistryValueKind.DWord);
    }

    private const string DxPath = @"Software\Microsoft\DirectX\UserGpuPreferences";

    /// <summary>"Chave=Valor;Chave=Valor;" → dicionário (o valor pode ter outras opções do usuário, que preservamos).</summary>
    private static Dictionary<string, string> DxSettings()
    {
        var d = new Dictionary<string, string>();
        string raw = Registry.CurrentUser.OpenSubKey(DxPath)?.GetValue("DirectXUserGlobalSettings") as string ?? "";
        foreach (var part in raw.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = part.Split('=', 2);
            if (kv.Length == 2) d[kv[0].Trim()] = kv[1].Trim();
        }
        return d;
    }

    private static void ApplyMouse(string[] v)
    {
        using (var k = Registry.CurrentUser.CreateSubKey(@"Control Panel\Mouse"))
        {
            k.SetValue("MouseSpeed", v[0]);
            k.SetValue("MouseThreshold1", v[1]);
            k.SetValue("MouseThreshold2", v[2]);
        }
        // Aplica na hora, sem precisar sair da conta.
        var p = v.Select(int.Parse).ToArray();
        if (!SystemParametersInfo(0x0004 /* SPI_SETMOUSE */, 0, p, 0x01 | 0x02))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Não foi possível aplicar a configuração do mouse.");
    }

    // ------------------------------------------------------------------ plano de energia

    internal static string? ActiveScheme()
    {
        var m = System.Text.RegularExpressions.Regex.Match(PowerCfg("/getactivescheme"), @"[0-9a-fA-F\-]{36}");
        return m.Success ? m.Value.ToLowerInvariant() : null;
    }

    internal static string PowerCfg(string args)
    {
        var psi = new ProcessStartInfo("powercfg.exe", args)
        {
            RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true
        };
        using var p = Process.Start(psi)!;
        var output = p.StandardOutput.ReadToEndAsync();
        var error = p.StandardError.ReadToEndAsync();
        if (!p.WaitForExit(5000))
        {
            try { p.Kill(); } catch { }
            throw new TimeoutException("O plano de energia não respondeu em 5 segundos.");
        }
        if (p.ExitCode != 0) throw new InvalidOperationException("O Windows recusou o ajuste de energia: " + error.GetAwaiter().GetResult().Trim());
        return output.GetAwaiter().GetResult();
    }

    [DllImport("user32.dll", SetLastError = true)] private static extern bool SystemParametersInfo(uint action, uint param, int[] data, uint winIni);
}
