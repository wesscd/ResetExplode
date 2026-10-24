using System.Management;

namespace ResetExplode;

/// <summary>Consulta somente leitura; não modifica recursos de segurança.</summary>
public static class GamingSecurity
{
    public static string Read()
    {
        try
        {
            using var search = new ManagementObjectSearcher(@"root\Microsoft\Windows\DeviceGuard", "SELECT VirtualizationBasedSecurityStatus, SecurityServicesRunning FROM Win32_DeviceGuard");
            foreach (ManagementObject item in search.Get())
            {
                using (item)
                {
                    var running = item["SecurityServicesRunning"] as uint[];
                    uint vbsState = item["VirtualizationBasedSecurityStatus"] is object status ? Convert.ToUInt32(status) : uint.MaxValue;
                    string vbs = vbsState switch
                    { 0 => "desabilitado", 1 => "configurado, sem execução", 2 => "em execução", _ => "estado desconhecido" };
                    string memory = running == null ? "não identificada" : running.Contains(2u) ? "em execução" : "não consta em execução";
                    return $"VBS: {vbs}. Integridade de Memória: {memory}.\n" +
                        "Esses recursos protegem o sistema. O custo de desempenho depende do hardware e precisa ser medido. " +
                        "O perfil Extremo não os desativa; alterações manuais podem exigir reinicialização e afetar virtualização ou requisitos de jogos.";
                }
            }
            return "O Windows não informou o estado de VBS/Integridade de Memória. Nenhuma conclusão sobre ativação pode ser feita.";
        }
        catch (Exception ex) { return "Consulta de segurança indisponível: " + ex.Message; }
    }
}
