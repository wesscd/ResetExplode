using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ResetExplode;

public sealed record GamingServiceRule(string Name, string Title, string Impact, bool Suggested = false, int TargetStart = 4);
public sealed record GamingServiceSnapshot(string Name, int Start, bool Delayed, string State, bool CanStop, bool IsUserService, List<string> Dependents);
public sealed record GamingServiceResult(string Name, bool Success, string Detail);

public interface IGamingServiceAccess
{
    GamingServiceSnapshot? Read(string name);
    void Configure(string name, int start, bool delayed);
    void Stop(string name);
    void Start(string name);
}

public static class GamingServiceCatalog
{
    // Lista fechada. Nomes externos, curingas, drivers e serviços por usuário nunca são aplicados.
    public static IReadOnlyList<GamingServiceRule> Rules { get; } = new List<GamingServiceRule>
    {
        new("DiagTrack", "Experiências conectadas e telemetria", "Reduz coleta de diagnóstico/feedback; não comprova ganho de FPS.", true),
        new("MapsBroker", "Mapas baixados", "Mapas offline deixam de ser atualizados/acessados por aplicativos que dependem do serviço.", true),
        new("RetailDemo", "Demonstração de varejo", "Somente se o computador não é uma unidade de demonstração de loja.", true),
        new("wisvc", "Windows Insider", "Somente se você não participa nem pretende participar do programa Insider."),
        new("AJRouter", "Roteador AllJoyn", "Pode interromper comunicação de dispositivos/aplicativos AllJoyn."),
        new("TrkWks", "Rastreamento de links distribuídos", "Atalhos e links para arquivos movidos podem deixar de ser acompanhados; não é um acelerador de rede."),
        new("lfsvc", "Geolocalização", "Aplicativos e recursos dependentes de localização deixam de funcionar corretamente."),
        new("tzautoupdate", "Atualização automática de fuso", "Fuso horário deixa de acompanhar mudanças de localização; sincronização de relógio W32Time é preservada."),
        new("Spooler", "Fila de impressão", "Desliga impressão física e virtual, incluindo Microsoft Print to PDF. Não use se precisa imprimir."),
        new("PrintNotify", "Notificações de impressão", "Extensões e notificações de impressoras podem parar."),
        new("Fax", "Fax", "Envio e recebimento de fax ficam indisponíveis."),
        new("FrameServer", "Servidor de quadros de câmera", "Pode interromper webcam, videochamadas e captura de câmera em streams."),
        new("WSearch", "Indexação do Windows Search", "Busca de arquivos/e-mail perde indexação e pode ficar mais lenta. Use somente para investigar consumo de disco medido."),
        new("WerSvc", "Relatório de erros do Windows", "Pode perder relatórios de falhas e dados úteis para investigar crashes. Log de Eventos é preservado."),
        new("WMPNetworkSvc", "Compartilhamento de mídia", "Compartilhamento de mídia do Windows Media Player na rede deixa de funcionar."),
        new("SSDPSRV", "Descoberta SSDP", "Descoberta UPnP de dispositivos de rede pode falhar."),
        new("upnphost", "Host de dispositivos UPnP", "Hospedagem UPnP e recursos de dispositivos de rede podem falhar."),
        new("PhoneSvc", "Serviço de telefone", "Pode afetar funções de telefonia e integração com celular."),
        new("NetTcpPortSharing", "Compartilhamento de portas .NET TCP", "Aplicativos WCF/.NET que compartilham portas podem falhar."),
        new("RemoteRegistry", "Registro remoto", "Administração remota do Registro fica indisponível; muitas instalações já o mantêm parado."),
        new("SNMPTRAP", "Traps SNMP", "Ferramentas de monitoramento que recebem traps SNMP deixam de funcionar.")
    }.AsReadOnly();

    private static readonly HashSet<string> Security = new(StringComparer.OrdinalIgnoreCase)
    {
        "BFE", "mpssvc", "WinDefend", "WdNisSvc", "SecurityHealthService", "wscsvc", "SgrmBroker", "SamSs", "KeyIso", "CryptSvc", "BDESVC", "EFS",
        "NgcSvc", "NgcCtnrSvc", "VaultSvc", "WbioSrvc", "PolicyAgent", "IKEEXT", "EventLog", "Wecsvc", "Netlogon", "CmdAgent", "CmdAgentProt", "cmdvirth",
        "WEPHOSTSVC", "Intel(R) TPM Provisioning Service", "jhi_service", "NaturalAuthentication"
    };
    private static readonly HashSet<string> Essential = new(StringComparer.OrdinalIgnoreCase)
    {
        "AppXSvc", "ClipSVC", "LicenseManager", "InstallService", "sppsvc", "wlidsvc", "TokenBroker", "GamingServices", "GamingServicesNet",
        "XblAuthManager", "XblGameSave", "XboxNetApiSvc", "XboxGipSvc", "GameInputSvc", "hidserv", "bthserv", "BTAGService", "BthAvctpSvc",
        "Audiosrv", "AudioEndpointBuilder", "WlanSvc", "Dhcp", "Dnscache", "iphlpsvc", "NlaSvc", "netprofm", "nsi", "Netman", "Wcmsvc", "RmSvc",
        "WinHttpAutoProxySvc", "SharedAccess", "icssvc", "LanmanWorkstation", "LanmanServer", "lmhosts", "RasMan", "SstpSvc", "RemoteAccess",
        "RpcSs", "RpcEptMapper", "DcomLaunch", "PlugPlay", "Power", "Winmgmt", "Appinfo", "Schedule", "ProfSvc", "UserManager", "LSM",
        "BrokerInfrastructure", "CoreMessagingRegistrar", "StateRepository", "SysMain", "Themes", "DispBrokerDesktopSvc", "DisplayEnhancementService",
        "NVDisplay.ContainerLocalSystem", "RstMwService", "igfxCUIService2.0.0.0", "DPS", "PcaSvc", "W32Time", "seclogon", "VSS", "wbengine", "fhsvc",
        "wuauserv", "BITS", "UsoSvc", "WaaSMedicSvc", "DoSvc", "TrustedInstaller", "msiserver", "edgeupdate", "edgeupdatem", "MicrosoftEdgeElevationService", "dmwappushservice", "DmEnrollmentSvc"
    };

    public static GamingServiceRule? Find(string name) => Rules.FirstOrDefault(r => r.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    public static string Classification(string name) => Security.Contains(name) ? "PRESERVAR SEGURANÇA"
        : Essential.Contains(name) ? "PRESERVAR FUNCIONALIDADE"
        : Find(name) is { Suggested: true } ? "CANDIDATO REVISÁVEL"
        : Find(name) != null ? "CONDICIONAL, NÃO PRÉ-MARCADO"
        : "PRESERVAR / NÃO VALIDADO";
    public static string Reason(string name) => Find(name)?.Impact ?? (Security.Contains(name)
        ? "Segurança, autenticação, criptografia, firewall ou auditoria. Fora do perfil."
        : Essential.Contains(name) ? "Preservado para jogos, drivers, rede, áudio, instalação, recuperação ou infraestrutura do Windows."
        : "Identidade/função não validada neste perfil. Inclui serviços de terceiros, anti-cheat e instâncias por usuário; não alterar automaticamente.");

    public static string AuditExport(string text)
    {
        var blocks = Regex.Matches(text, @"(?ms)^Display Name\s+:.*?(?=^=|\z)");
        var rows = new List<string>();
        foreach (Match block in blocks)
        {
            string Field(string key) => Regex.Match(block.Value, @"(?m)^" + Regex.Escape(key) + @"\s+:\s*([^\r\n]+)").Groups[1].Value.Trim();
            string name = Field("Name");
            if (name.Length == 0) continue;
            rows.Add($"{name} | {Classification(name)} | anexo: {Field("Startup Type")} / {Field("Status")}\n  {Reason(name)}");
        }
        return "VALIDAÇÃO DO ARQUIVO DE REFERÊNCIA — PERFIL DE SERVIÇOS PARA JOGOS\n" +
            $"Entradas: {rows.Count}. A lista é material de referência; não é medição de consumo nem inventário confirmado deste PC.\n" +
            "Estados do anexo não são importados/aplicados. Segurança e funções essenciais são preservadas.\n" +
            "Manual/parado não significa consumo contínuo. Ganhos de FPS não foram medidos.\n\n" + string.Join("\n\n", rows);
    }
}

public sealed class GamingServiceProfile
{
    private sealed class Journal
    {
        public Journal() { }
        public int Version { get; set; } = 1;
        public List<GamingServiceSnapshot> Originals { get; set; } = new();
    }
    private readonly string _path;
    private readonly IGamingServiceAccess _access;
    private readonly Journal _journal;
    public IReadOnlyList<GamingServiceSnapshot> Originals => _journal.Originals.AsReadOnly();

    public GamingServiceProfile(string path, IGamingServiceAccess access)
    {
        _path = path; _access = access;
        _journal = File.Exists(path) ? JsonSerializer.Deserialize<Journal>(File.ReadAllText(path))
            ?? throw new InvalidDataException("Backup de serviços vazio; arquivo preservado.") : new Journal();
        if (_journal.Version != 1 || _journal.Originals == null || _journal.Originals.Any(s =>
            s == null || GamingServiceCatalog.Find(s.Name) == null || s.Start is < 2 or > 4 || s.State is not ("Running" or "Stopped") || !s.IsUserService) ||
            _journal.Originals.Select(s => s.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != _journal.Originals.Count)
            throw new InvalidDataException("Backup de serviços incompatível; arquivo preservado.");
    }

    private void Save() => AtomicFile.Write(_path, JsonSerializer.Serialize(_journal, new JsonSerializerOptions { WriteIndented = true }));
    public GamingServiceSnapshot? Inspect(string name) => _access.Read(name);

    public GamingServiceResult Apply(string name)
    {
        if (GamingServiceCatalog.Find(name) is not { } rule) return new(name, false, "Serviço protegido ou fora da lista validada.");
        GamingServiceSnapshot? before = null;
        bool changed = false, added = false;
        try
        {
            before = _access.Read(rule.Name);
            if (before == null) return new(name, false, "Não instalado neste PC.");
            if (!before.Name.Equals(rule.Name, StringComparison.OrdinalIgnoreCase)) throw new IOException("Identidade do serviço não confirmada.");
            if (!before.IsUserService || before.Start is < 2 or > 4 || before.State is not ("Running" or "Stopped"))
                return new(name, false, "Driver, estado transitório ou configuração não suportada; preservado.");
            if (before.Dependents.Count > 0) return new(name, false, "Preservado: outros serviços dependem dele (" + string.Join(", ", before.Dependents) + ").");
            if (before.State == "Running" && !before.CanStop) return new(name, false, "Não aceita parada; preservado sem forçar.");
            if (before.Start == rule.TargetStart && before.State == "Stopped") return new(name, true, "Já estava configurado; nenhuma mudança.");
            if (!_journal.Originals.Any(s => s.Name.Equals(rule.Name, StringComparison.OrdinalIgnoreCase)))
            { _journal.Originals.Add(before); added = true; }
            Save(); // obrigatório antes de alterar o SCM
            changed = true;
            _access.Configure(rule.Name, rule.TargetStart, false);
            if (before.State == "Running") _access.Stop(rule.Name);
            var after = _access.Read(rule.Name);
            if (after?.Start != rule.TargetStart || after.State != "Stopped") throw new IOException("O Windows não confirmou a alteração.");
            return new(name, true, "Parado e desabilitado. Original salvo; efeito em FPS não medido.");
        }
        catch (Exception ex)
        {
            string recovery = "";
            if (changed && before != null)
            {
                try
                {
                    RestoreState(before);
                    if (added) RemoveSaved(before);
                    recovery = " Estado desta tentativa restaurado.";
                }
                catch (Exception restore) { recovery = " Restauração pendente: " + restore.Message; }
            }
            return new(name, false, ex.Message + recovery);
        }
    }

    public GamingServiceResult Restore(string name)
    {
        var snapshot = _journal.Originals.FirstOrDefault(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (snapshot == null) return new(name, false, "Nenhum original salvo por este perfil.");
        try { RestoreState(snapshot); RemoveSaved(snapshot); return new(name, true, "Inicialização e estado de execução restaurados."); }
        catch (Exception ex) { return new(name, false, "Original preservado; restauração pendente: " + ex.Message); }
    }

    private void RemoveSaved(GamingServiceSnapshot snapshot)
    {
        _journal.Originals.Remove(snapshot);
        try { Save(); } catch { _journal.Originals.Add(snapshot); throw; }
    }
    private void RestoreState(GamingServiceSnapshot snapshot)
    {
        var current = _access.Read(snapshot.Name) ?? throw new IOException("Serviço não encontrado; backup mantido.");
        if (!current.IsUserService || current.State is not ("Running" or "Stopped")) throw new IOException("Estado transitório ou tipo não suportado.");
        if (snapshot.State == "Stopped" && current.State == "Running" && (!current.CanStop || current.Dependents.Count > 0))
            throw new IOException("Parada exige afetar dependentes ou serviço não aceita parar.");
        // Um serviço pode ter sido desabilitado antes do perfil, mas ainda estar em execução.
        bool runningDisabled = snapshot.State == "Running" && snapshot.Start == 4 && current.State != "Running";
        _access.Configure(snapshot.Name, runningDisabled ? 3 : snapshot.Start, snapshot.Delayed);
        if (snapshot.State == "Running" && current.State != "Running") _access.Start(snapshot.Name);
        else if (snapshot.State == "Stopped" && current.State == "Running") _access.Stop(snapshot.Name);
        if (runningDisabled) _access.Configure(snapshot.Name, 4, false);
        var restored = _access.Read(snapshot.Name);
        if (restored?.Start != snapshot.Start || (snapshot.Start == 2 && restored.Delayed != snapshot.Delayed) || restored.State != snapshot.State)
            throw new IOException("O Windows não confirmou a restauração completa.");
    }
}
