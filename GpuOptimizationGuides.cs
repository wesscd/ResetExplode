namespace ResetExplode;

public sealed record GpuOptimizationGuide(string Title, string Scope, string Advice, string Source);

public static class GpuOptimizationGuides
{
    public static List<GpuOptimizationGuide> ForVendor(string vendor) => vendor.ToUpperInvariant() switch
    {
        "AMD" => new()
        {
            new("Base Radeon: antialiasing, texturas e tesselação", "No Adrenalin · Gráficos globais ou perfil do jogo", "Comece deixando o aplicativo controlar antialiasing e filtragem anisotrópica. Compare Qualidade da filtragem de textura em Padrão e Desempenho: há troca de qualidade. Revise otimização de formato de superfície e tesselação conforme as opções expostas pelo driver. Overrides antigos podem não afetar jogos e APIs atuais.", "https://www.amd.com/en/resources/support-articles/faqs/DH-012.html"),
            new("Radeon Anti-Lag", "Latência · testar por jogo", "Compare ligado/desligado, principalmente quando a GPU for o limite. Depende de GPU, driver e API. Anti-Lag 2 exige integração no jogo. Não habilite recursos antigos ou modificados de Anti-Lag+ por scripts.", "https://www.amd.com/en/products/software/adrenalin/radeon-software-anti-lag.html"),
            new("Chill, Boost e limite de FPS", "Energia, resolução dinâmica e compatibilidade", "Chill regula FPS para economizar energia: confira se está limitando sua meta. Boost pode reduzir resolução durante movimentos em jogos compatíveis. Compare separadamente; não presuma que todos os recursos podem funcionar juntos.", "https://www.amd.com/en/resources/support-articles/faqs/DH3-033.html"),
            new("FSR no jogo e Radeon Super Resolution", "Escalonamento · verificar suporte", "FSR integrado é configurado nos jogos compatíveis. RSR é um recurso separado do driver; não existe uma chave global que acrescente FSR a todos os jogos. Compare resolução, nitidez e qualidade antes de manter o ajuste.", "https://community.amd.com/t5/gaming/introducing-amd-software-adrenalin-edition-with-radeon-super/ba-p/516319/page/2"),
            new("HYPR-RX e geração de quadros AFMF", "Hardware e jogos compatíveis", "HYPR-RX combina recursos do driver. AFMF gera quadros adicionais; avalie qualidade e resposta separadamente. Mais FPS exibidos não comprovam menor latência. Esta tela orienta a configuração no Adrenalin; não aplica essas opções automaticamente.", "https://www.amd.com/en/products/software/adrenalin/hypr-rx.html"),
            new("Sincronização e monitor", "FreeSync, V-Sync e taxa de atualização", "Confira FreeSync no monitor e no Adrenalin quando houver suporte. Compare limite de FPS e sincronização conforme a preferência por resposta ou ausência de tearing. Não force a mesma combinação em todos os jogos e APIs.", "https://www.amd.com/en/products/graphics/technologies/freesync.html"),
            new("Driver, RAM e plataforma", "Diagnóstico antes de overclock", "Confira o driver da GPU e o chipset correto para a placa-mãe. Revise a velocidade configurada da RAM e o manual antes de alterar EXPO/XMP ou slots. Verifique ReBAR/SAM quando suportados. Não aplique overclock automático, mudanças de BIOS ou limpeza rotineira de cache como receita geral.", "https://www.amd.com/en/support"),
        },
        "NVIDIA" => new()
        {
            new("Reflex e configurações de latência", "No jogo e no painel NVIDIA", "Compare Reflex nos jogos que oferecem a opção. Ajuste sincronização e limite de FPS conforme sua preferência por resposta ou ausência de tearing; não há um valor universal.", "https://www.nvidia.com/en-in/geforce/guides/system-latency-optimization-guide/"),
            new("Energia, filtros e qualidade gráfica", "No perfil de cada aplicativo", "Compare o modo de energia e os filtros no painel do driver, acompanhando consumo e temperatura. DLSS e geração de quadros exigem suporte específico; o programa não presume que estão disponíveis.", "https://www.nvidia.com/content/Control-Panel-Help/vLatest/en-us/mergedProjects/nv3d/Manage_3D_Settings_%28reference%29.htm")
        },
        "INTEL" => new()
        {
            new("Driver Intel e opções do jogo", "Intel Graphics Software e jogos compatíveis", "Revise o driver adequado para sua GPU/notebook. Confira no jogo a disponibilidade de XeSS e opções de latência. O perfil global do Windows funciona independentemente desses recursos.", "https://www.intel.com/content/www/us/en/support/articles/000100501/graphics.html")
        },
        _ => new()
        {
            new("Selecione a GPU que executa seus jogos", "Fabricante não identificado", "O perfil do Windows continua disponível. Em PCs com várias GPUs, escolha a placa usada pelo jogo para ver a orientação correspondente.", "https://support.microsoft.com/en-us/windows/hardware/display-graphics/optimizations-for-windowed-games-in-windows-11")
        }
    };
}
