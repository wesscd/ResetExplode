using System.Text.Json;
using ResetExplode;

if (args.Length == 1 && args[0] == "--live-audit-read-only")
{
    var inventory = await WindowsOptimizationInventory.CaptureAsync(CancellationToken.None);
    var rows = LiveOptimizationAudit.Build(inventory);
    Console.WriteLine($"Serviços={inventory.Services.Count}; processos={inventory.Processes.Count}; candidatos={rows.Count(r => r.Available)}; amostra={inventory.SampleSeconds:0.00}s");
    Console.WriteLine($"Completo: serviços={inventory.ServicesComplete}; dependências={inventory.DependenciesComplete}; segurança={inventory.SecurityInventoryComplete}");
    foreach (var warning in inventory.Warnings) Console.WriteLine(warning);
    return;
}

if (args.Length == 1 && args[0] == "--services-read-only")
{
    var access = new WindowsServiceAccess();
    foreach (var rule in GamingServiceCatalog.Rules)
    {
        try
        {
            var service = access.Read(rule.Name);
            Console.WriteLine(service == null ? rule.Name + ": não instalado" :
                $"{service.Name}: início={service.Start}, estado={service.State}, Win32={service.IsUserService}, dependentes={service.Dependents.Count}");
        }
        catch (Exception ex) { Console.WriteLine(rule.Name + ": consulta indisponível: " + ex.Message); }
    }
    return;
}

if (args.Length == 3 && args[0] == "--audit")
{
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[2]))!);
    File.WriteAllText(args[2], GamingServiceCatalog.AuditExport(File.ReadAllText(args[1])));
    Console.WriteLine(Path.GetFullPath(args[2]));
    return;
}

// Testa persistência e transações com arquivos isolados e ajustes simulados.
// Não inicia jogos, não altera o Registro, energia ou configurações reais do usuário.
string folder = Path.Combine(Path.GetTempPath(), "ResetExplode-checks-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(folder);
int checks = 0;
void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
    Console.WriteLine("OK: " + message);
    checks++;
}
void Throws(Action action, string message)
{
    bool threw = false;
    try { action(); } catch { threw = true; }
    Check(threw, message);
}
try
{
    string path = Path.Combine(folder, "settings.json");
    var settings = new AppSettings { StoragePath = path };
    settings.TweakBackup["power"] = "original";
    settings.Save();
    settings.LightTheme = true;
    settings.Save();
    Check(File.Exists(path + ".bak") && AppSettings.LoadFrom(path).LightTheme, "Gravação atômica mantém cópia anterior");
    File.WriteAllText(path, "{corrompido");
    var recovered = AppSettings.LoadFrom(path);
    Check(!recovered.RecoveryRequired && recovered.LoadWarning != null && recovered.TweakBackup["power"] == "original", "Recupera backup e originais após corrupção");
    Check(JsonDocument.Parse(File.ReadAllText(path + ".bak")).RootElement.ValueKind == JsonValueKind.Object, "Recuperação preserva backup válido");
    File.WriteAllText(path, "inválido");
    File.WriteAllText(path + ".bak", "inválido");
    var broken = AppSettings.LoadFrom(path);
    Throws(broken.Save, "Bloqueia sobrescrita quando as duas cópias estão corrompidas");

    var state = new AppSettings { StoragePath = Path.Combine(folder, "transaction.json") };
    bool changed = false;
    var tweak = new Tweak
    {
        Id = "fake", Title = "Simulado", Read = () => changed,
        Capture = s => { if (!s.TweakBackup.ContainsKey("fake")) s.TweakBackup["fake"] = "original"; },
        Write = (_, s) =>
        {
            Check(AppSettings.LoadFrom(s.StoragePath).TweakBackup["fake"] == "original", "Original persistido antes de aplicar");
            changed = true;
        },
        Restore = _ => changed = false
    };
    WindowsTweaks.Apply(tweak, true, state);
    WindowsTweaks.RestoreOne(tweak, state);
    Check(!changed && !state.TweakBackup.ContainsKey("fake"), "Restauração individual remove somente backup concluído");
    state.TweakBackup["bad.a"] = "1";
    state.TweakBackup["good"] = "2";
    state.TweakBackup["unknown"] = "3";
    var bad = new Tweak { Id = "bad", Title = "Falha", Restore = _ => throw new IOException("simulada") };
    var good = new Tweak { Id = "good", Title = "Sucesso" };
    var errors = WindowsTweaks.RestoreAll(new[] { bad, good }, state);
    Check(errors.Count == 1 && state.TweakBackup.ContainsKey("bad.a") && state.TweakBackup.ContainsKey("unknown") && !state.TweakBackup.ContainsKey("good"), "Restauração parcial preserva falhas e backups desconhecidos");
    var blocked = new AppSettings { StoragePath = folder }; // diretório não pode ser substituído por arquivo
    changed = false;
    Throws(() => WindowsTweaks.Apply(tweak, true, blocked), "Falha de gravação impede alteração do sistema");
    Check(!changed, "Nenhuma alteração foi aplicada sem backup persistido");
    state.TweakBackup["fake"] = "original";
    state.StoragePath = folder;
    Throws(() => WindowsTweaks.RestoreOne(tweak, state), "Falha ao salvar restauração é reportada");
    Check(state.TweakBackup.ContainsKey("fake"), "Backup mantido em memória quando salvar restauração falha");
    Check(!Directory.EnumerateFiles(folder, "*.tmp").Any(), "Não deixa arquivos temporários após falha");
    string journal = Path.Combine(folder, "session.json");
    File.WriteAllText(journal, "{inválido");
    Check(GameModeSession.RecoverFrom(journal).Count > 0 && File.Exists(journal), "Sessão corrompida é reportada e preservada");
    File.WriteAllText(journal, "{}");
    Check(GameModeSession.RecoverFrom(journal).Count > 0 && File.Exists(journal), "Sessão incompleta não é descartada como restaurada");
    File.WriteAllText(journal, JsonSerializer.Serialize(new GameModeSession { Game = "Simulado", OriginalPlan = "plano-inválido" }));
    Check(GameModeSession.RecoverFrom(journal).Count > 0 && File.Exists(journal), "Plano inválido é rejeitado sem executar powercfg");
    File.Delete(journal);
    string originalPlan = "381b4222-f694-41f0-9685-ff5bb260df2e";
    string planNow = originalPlan;
    int?[] capture = { 1, null };
    bool failRestore = false, failApply = false;
    var access = new GameModeSession.Access(
        () => planNow,
        plan =>
        {
            Check(File.Exists(journal), "Diário persistido antes de mudar energia");
            planNow = plan;
        },
        () => { },
        i => capture[i],
        (i, value) =>
        {
            if (failRestore && value != 0) throw new IOException("Falha simulada de restauração");
            if (failApply && i == 1 && value == 0) throw new IOException("Falha simulada de aplicação");
            capture[i] = value;
        });
    var session = GameModeSession.Start("Simulado", true, true, journal, access);
    Check(planNow != originalPlan && capture.All(v => v == 0), "Modo Jogo aplica energia e pausa captura");
    Throws(() => GameModeSession.Start("Outro", true, true, journal, access), "Sessão pendente impede sobreposição");
    Check(session.Restore().Count == 0 && planNow == originalPlan && capture[0] == 1 && capture[1] == null && !File.Exists(journal), "Fim da sessão restaura plano, valor existente e valor ausente");
    failApply = true;
    Throws(() => GameModeSession.Start("Simulado", true, true, journal, access), "Falha parcial ao aplicar é reportada");
    Check(planNow == originalPlan && capture[0] == 1 && capture[1] == null && !File.Exists(journal), "Falha parcial desfaz o que já foi aplicado");
    failApply = false;
    GameModeSession.Start("Simulado", true, true, journal, access);
    failRestore = true;
    Check(GameModeSession.RecoverFrom(journal, access).Count > 0 && File.Exists(journal), "Falha ao recuperar preserva diário de sessão");
    failRestore = false;
    Check(GameModeSession.RecoverFrom(journal, access).Count == 0 && capture[0] == 1 && capture[1] == null && !File.Exists(journal), "Nova tentativa recupera sessão persistida após falha");
    Tweak Fake(string id, bool supported = true, bool? value = false) => new()
    { Id = id, Title = id, IsSupported = supported, Read = () => value };
    var globalTweaks = new[] { Fake("gamemode"), Fake("gamedvr"), Fake("windowed", false), Fake("power"), Fake("hags"), Fake("delivery"), Fake("animations") };
    foreach (var t in globalTweaks) t.Refresh();
    var batteryPlan = GamingOptimization.Build(globalTweaks, true, 50, 40);
    Check(!batteryPlan.Single(c => c.Tweak.Id == "power").Selected, "Perfil não sugere Alto desempenho automaticamente na bateria");
    Check(!batteryPlan.Single(c => c.Tweak.Id == "windowed").Selected && !batteryPlan.Single(c => c.Tweak.Id == "windowed").Available, "Perfil exclui ajuste incompatível com o Windows");
    Check(!batteryPlan.Single(c => c.Tweak.Id == "hags").Selected && !batteryPlan.Single(c => c.Tweak.Id == "delivery").Selected, "Experimentos e ajustes de rede exigem seleção explícita");
    Check(!GamingOptimization.Build(globalTweaks, false, 95, 40).Single(c => c.Tweak.Id == "power").Selected &&
          !GamingOptimization.Build(globalTweaks, false, null, 40).Single(c => c.Tweak.Id == "power").Selected, "Temperatura alta ou leitura ausente impede sugestão automática de Alto desempenho");
    Check(GamingOptimization.Build(globalTweaks, false, 50, 40).Single(c => c.Tweak.Id == "power").Selected, "Sugere Alto desempenho na tomada com temperatura adequada");
    Check(!GamingOptimization.Build(globalTweaks, false, 50, null).Single(c => c.Tweak.Id == "power").Selected, "GPU sem leitura não é presumida fria na sugestão de energia");
    var alreadyOn = Fake("gamemode", value: true);
    alreadyOn.Refresh();
    Check(!GamingOptimization.Build(new[] { alreadyOn }, false, 50, 40).Single().Selected, "Sugestão não remarca ajuste já aplicado");
    var sourceProfile = new GameProfile { GameMode = false, Priority = "high", Helpers = new() { new HelperProgram { Path = "exemplo.exe" } } };
    var effective = GamingOptimization.EffectiveProfile(sourceProfile, true);
    effective.Helpers.Clear();
    Check(effective.GameMode && !sourceProfile.GameMode && sourceProfile.Helpers.Count == 1 && effective.Priority == "high", "Modo universal aplica em cópia e preserva perfil e programas auxiliares");
    Check(!GamingOptimization.EffectiveProfile(sourceProfile, false).GameMode, "Desligar modo universal respeita o perfil individual");
    var amd = GpuOptimizationGuides.ForVendor("AMD");
    Check(amd.Any(g => g.Title.Contains("Anti-Lag")) && amd.Any(g => g.Title.Contains("FSR")) && amd.Any(g => g.Title.Contains("AFMF")), "Guia AMD inclui latência, escalonamento e geração de quadros");
    Check(GpuOptimizationGuides.ForVendor("Intel").Count > 0 && GpuOptimizationGuides.ForVendor("NVIDIA").Count > 0 && GpuOptimizationGuides.ForVendor("").Count > 0, "Guia suporta outros fabricantes e GPU desconhecida");
    var batchSettings = new AppSettings { StoragePath = Path.Combine(folder, "batch.json") };
    bool live = false;
    var batchTweak = new Tweak
    {
        Id = "batch", Title = "Batch", Read = () => live,
        Capture = s => s.TweakBackup.TryAdd("batch", live.ToString()),
        Write = (v, s) =>
        {
            Check(AppSettings.LoadFrom(s.StoragePath).TweakBackup.ContainsKey("batch"), "Aplicação em lote persiste backup antes da mudança");
            live = v;
        }
    };
    var failTweak = new Tweak { Id = "fail", Title = "Falha", Read = () => false, Capture = s => s.TweakBackup.TryAdd("fail", "False"), Write = (_, _) => throw new IOException("falha simulada") };
    var outcomes = GamingOptimization.Apply(new[]
    {
        new OptimizationChoice { Tweak = batchTweak, Selected = true },
        new OptimizationChoice { Tweak = failTweak, Selected = true },
        new OptimizationChoice { Tweak = Fake("incompatible", false), Selected = true },
        new OptimizationChoice { Tweak = Fake("unselected"), Selected = false },
        new OptimizationChoice { Tweak = alreadyOn, Selected = true }
    }, batchSettings);
    Check(outcomes.Count == 4 && outcomes.Count(o => o.State == "applied") == 1 && outcomes.Count(o => o.State == "failed") == 1 &&
        outcomes.Count(o => o.State == "unsupported") == 1 && outcomes.Count(o => o.State == "unchanged") == 1, "Lote distingue sucesso, falha, incompatibilidade e estado já aplicado");
    Check(batchSettings.TweakBackup.ContainsKey("fail"), "Falha parcial do perfil mantém backup para restauração");
    Check(GamingOptimization.Apply(new[] { new OptimizationChoice { Tweak = batchTweak, Selected = true } }, batchSettings).Single().State == "unchanged", "Reaplicar perfil é idempotente");
    batchSettings.OptimizationHistory.Add(new OptimizationRun(DateTime.Now, outcomes));
    batchSettings.UniversalGameMode = true;
    batchSettings.Save();
    var savedProfile = AppSettings.LoadFrom(batchSettings.StoragePath);
    Check(savedProfile.UniversalGameMode && savedProfile.OptimizationHistory.Single().Items.Count == 4, "Histórico e modo universal sobrevivem ao recarregar configurações");
    ServiceProfileChecks.Run(folder, Check, Throws);
    LiveAuditChecks.Run(Check);
    TemporarySessionChecks.Run(folder, Check, Throws);
    PerformanceChecks.Run(folder, Check, Throws);
    ConsoleChecks.Run(folder, Check);
    Console.WriteLine($"{checks} verificações aprovadas.");
}
finally
{
    // Somente o diretório exclusivo desta execução, criado acima.
    Directory.Delete(folder, recursive: true);
}
