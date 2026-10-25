# BaraoTechExplode

**[Versão compilada pronta para uso → pasta compilado](compilado/)** · Windows x64 · baixe o ZIP, extraia e abra `BaraoTechExplode.exe`.

Este projeto é um **fork do [RESET EXPLODE](https://github.com/GustavoReset/ResetExplode)**. O código principal foi desenvolvido por **Gustavo da Reset (GustavoReset)**. O BaraoTechExplode, da [Barão Tech](https://baraotech.online/), aproveita a leitura de sensores, hardware e desempenho da base original para desenvolver diagnóstico e otimização para jogos, com prévia dos impactos, backup e restauração.

O fork tem identidade e alterações próprias e não é uma versão oficial do projeto original. A licença [MIT](LICENSE) e o copyright original de Gustavo permanecem preservados. O crédito também aparece em **Sobre / Créditos** e nos relatórios HTML.

O ícone e a marca deste fork usam o [logotipo da Barão Tech](https://baraotech.online/assets/logo.png), fornecido pelo site indicado pelo responsável pelo fork; isso não atribui a marca ao autor original nem altera a licença do código-base. Os arquivos visuais originais foram mantidos no repositório.

### Serviços temporários por jogo e histórico

Na Biblioteca, abra um jogo e expanda **Serviços temporários deste jogo**. Revise o impacto de cada opção e salve a seleção no perfil. Nada vem selecionado por padrão. Ao clicar em **Jogar**, uma revisão apresenta os serviços escolhidos; o backend valida novamente os candidatos e as dependências antes de aplicar.

Os originais ficam em `game-services-session.json.services`, separados de `services-profile.json`, e o manifesto em `game-services-session.json`. Ao terminar o acompanhamento, cancelar ou fechar normalmente o aplicativo, a restauração é tentada. Se o aplicativo ou o Windows forem interrompidos, use **Restaurar sessão pendente** em Otimização. As falhas preservam os originais e bloqueiam nova sessão até a recuperação. O histórico das últimas 50 sessões e tentativas é persistido em `game-services-history.json`; as dez últimas aparecem no painel de serviços.

A restauração devolve o estado anterior à sessão, inclusive ajustes permanentes previamente aplicados. Aplicativos fechados manualmente não são reabertos. O acompanhamento não mede um ganho de FPS automaticamente; compare cenários equivalentes usando as ferramentas de desempenho.

Para preservar configurações e backups desta cópia existente, o diretório de dados continua `%AppData%\ResetExplode` e o namespace técnico continua `ResetExplode`. O mutex de instância é compartilhado com a versão anterior: feche o aplicativo antigo antes de abrir o fork. O executável distribuído passa a ser `BaraoTechExplode.exe`.

### Comparar sessões de desempenho (v0.18)

Na Biblioteca, selecione o jogo e confira seu executável em **Desempenho**. Abra o jogo, prepare um trecho repetível e, em **Comparação de sessões**, descreva o trecho, resolução, preset e limite de FPS. Escolha **Antes** ou **Depois** e clique em **Medir jogo · 60 segundos**. Há dez segundos para voltar ao jogo antes da coleta. A medição pode acompanhar um jogo iniciado pelo aplicativo ou um jogo já aberto; o executável em primeiro plano precisa corresponder ao escolhido.

Os rótulos Antes/Depois não alteram o Windows. Prepare cada configuração explicitamente e repita as mesmas condições térmicas. As sessões registram FPS médio, 1% low, tempo médio, P99, temperaturas médias/máximas e contagens de amostras, perfil solicitado/salvo, estados lidos dos ajustes do Windows, prioridade lida e preferências por executável. O contexto do hardware e driver é o detectado na abertura do aplicativo; reabra após trocar hardware ou drivers. Preferências do Registro não comprovam qual GPU foi usada nem que todo ajuste foi efetivamente aplicado.

Escolha uma sessão de cada tipo para ver a comparação. Sessões parciais, com perda de primeiro plano, eventos inválidos, limite de captura atingido, menos de 300 intervalos ou eventos que não cobrem a janela (tolerância de dois segundos nas bordas) não recebem porcentagens. Jogos, cenários, executáveis ou contextos de hardware diferentes também bloqueiam os percentuais. Valores ausentes aparecem como desconhecidos. A comparação isolada não atribui causalidade à otimização.

Os tempos vêm dos eventos Present por processo do ETW, sem injeção. FPS médio = intervalos/tempo total; 1% low = inverso da média do 1% de intervalos mais lentos; P99 usa o percentil de posto mais próximo. A definição de 1% low desta comparação difere da aproximação por P99 usada no overlay existente. Esses dados não são uma medição dos quadros efetivamente exibidos: menus, múltiplas swapchains e geração de quadros podem afetar os valores. Vulkan/OpenGL podem não fornecer eventos. Não se soma nem faz média de janelas sobrepostas do overlay.

As últimas 100 sessões são persistidas em `game-performance.json` com gravação atômica e cópia anterior `.bak`. Arquivos inválidos são preservados e bloqueiam novas gravações até serem revisados. O botão de exportação salva o par escolhido e o contexto em JSON, incluindo caminhos locais. Os testes automatizados usam eventos e arquivos simulados; a precisão em um jogo real ainda depende do ETW e do suporte do título/driver.

### Recuperação pendente na abertura

Um aviso no topo de todas as páginas identifica diários pendentes de energia, captura e serviços temporários, com os estados originais disponíveis e botão de restauração. Arquivos corrompidos ou órfãos também são sinalizados e preservados. A abertura apenas consulta os registros; a restauração ocorre ao clicar no botão e fica bloqueada durante testes e medições. O aviso não aparece para uma sessão de jogo que está sendo acompanhada normalmente.

### Otimização guiada e console (v0.19)

A página começa com **Comece aqui**, seguida da análise de serviços/aplicativos e dos ajustes permanentes do Windows. O catálogo alternativo de serviços e as ferramentas anteriores ficam recolhidos em áreas avançadas. A seleção de ajustes do Windows fica aberta para revisão. Atalhos levam ao perfil temporário por jogo e à restauração dos serviços.

O fluxo é: **analisar → marcar caixas → conferir impactos → revisar e confirmar → conferir console**. Clicar numa linha mostra detalhes; marcar a caixa seleciona a ação. Um inventário sem candidatos e uma seleção vazia são explicados. Botões de aplicação indisponíveis mostram o motivo ao passar o mouse. Bloqueios por jogo, medição, outra operação ou sessão pendente aparecem na área da operação e no console.

O **console de otimização**, fixo no rodapé da página, é somente leitura. Exibe horário, área, etapa/resultado, item, motivo e estados anterior/posterior quando disponíveis. Registra backups antes de mudanças, confirmações lidas do Windows, itens já configurados, bloqueios, falhas, reversões e restaurações, além do perfil temporário por jogo. Preferências de GPU configuradas não são apresentadas como prova de que o jogo usou aquela GPU.

Os registros ficam em `%AppData%\ResetExplode\logs\optimization-AAAA-MM-DD.jsonl`, sem envio automático. A tela exibe até 500 registros recentes de até sete arquivos diários; os arquivos anteriores não são apagados. É possível filtrar, copiar, exportar a visualização em TXT ou abrir a pasta. Os registros podem conter caminhos locais e nomes de programas. Logs antigos não são reconstruídos: alterações de versões anteriores continuam nos backups/históricos disponíveis, mas não ganham um registro retroativo completo.

Falha de gravação do log é identificada na própria linha e mantém o resultado visível, sem interromper a restauração. Os backups obrigatórios continuam independentes: se não puderem ser persistidos, a alteração correspondente não é executada. Linhas incompletas de log são preservadas e sinalizadas ao carregar o histórico.

## Funcionalidades herdadas da base v0.16
- **Inicialização** (RESET FERRAMENTAS): veja, desative ou remova os programas que abrem junto com o Windows, com opção de desfazer.
- **Instalar apps** (RESET FERRAMENTAS): Steam, Chrome, Discord, Lightshot, 7-Zip, WinRAR e VLC de uma vez depois de formatar, pelo winget.
- **RESET MAKER** (nova categoria): Portas COM (fantasmas e números COM presos, que saiu de Otimização) e Drivers seriais
  (CH340, CP2102, FTDI, Prolific e Arduino: diagnóstico, reinstalar, trocar ou remover versão do driver, Arduino IDE).
- **Abertura mais rápida**: a janela aparece em ~0,5 s (antes ~4,6 s); o hardware é lido em paralelo, em segundo plano.
- **Overlay corrigido**: fica por cima de jogos em janela sem borda como GTA V e Cyberpunk 2077 e segue o monitor do jogo.

Detalhes em [NOVIDADES.md](NOVIDADES.md) (o mesmo texto vai nas notas da release).

App WPF (.NET 8) com:
- **Informações do sistema** (categorias + detalhes): SO, placa-mãe/BIOS, CPU, RAM (por módulo), GPU, discos — via WMI.
  Em Armazenamento, cartões por unidade (C:, pendrive, HD externo, rede) com anel de cheio/vazio, tipo e disco físico (`DriveMap.cs`).
- **Avisos de dispositivo** (`ToastWindow.xaml`): ao conectar/remover pendrive, HD externo ou qualquer dispositivo USB (ex.: Arduino),
  aparece um aviso no canto da tela e a lista de unidades se atualiza (via `WM_DEVICECHANGE`, sem ficar consultando).
- **Sensores em tempo real**: temperatura, clock, carga, voltagem, ventoinhas, consumo etc. — via LibreHardwareMonitorLib. Atualiza a cada 1 s, com filtro por tipo.
- **Placa de vídeo** (estilo GPU-Z): chip, arquitetura, processo, die, transistores, shaders/SMs/Tensor/RT, ROPs/TMUs, fillrates, TFLOPS,
  memória (tamanho, tipo, barramento, clock, banda, Resizable BAR), clocks, PCIe (máximo e em uso), limites de energia,
  VBIOS, driver, assinatura WHQL, CUDA, DirectX (feature level), APIs e recursos da arquitetura.
  Sensores ao vivo da GPU + P-state, PerfCap, NVENC/NVDEC. Fontes: NVML (NVIDIA), DXGI/Direct3D 12, WMI/PnP, registro e banco de specs em `GpuSpecDb.cs`.
- **Stress test**: carga de CPU em todos os threads (FPU + inteiros) e/ou RAM (grava e verifica padrões em 40–80% da memória livre).
  Para sozinho quando a temperatura da CPU atinge o limite configurado, quando o tempo acaba ou se aparecer erro de memória.
  Mostra temperatura atual/máxima, carga, consumo, gráfico dos últimos 5 min e um log dos eventos.
- **Stress de GPU** (inspirado no FurMark, `GpuBurn.cs`): torus com pelos renderizado por *shells* em Direct3D 11 (Vortice),
  com overdraw massivo, cadeia de FMAs no pixel shader e alpha blending, sem limite de FPS, numa janela própria.
  Opções: resolução (720p/1080p/1440p/tela cheia), MSAA (off/4x/8x), intensidade (Normal/Alta/Extrema) e limite de temperatura da GPU.
  Mostra FPS, temperatura, consumo e PerfCap; ao final registra frames e FPS médio. ESC ou fechar a janela encerra o teste.
  Medido numa RTX 5070 (1080p, 4x MSAA, Alta): ~99% de uso e 220–232 W de 250 W.
- **Jogos**: FPS esperado em 32 jogos populares para a sua GPU + CPU, por resolução (1080p/1440p/4K), gráficos e upscaling (DLSS/FSR).
  Com chave da [HowManyFPS API](https://howmanyfps.com/developer-api) (pedida em info@howmanyfps.com) mostra FPS médio e 1% low medidos em sessões reais;
  sem chave, usa uma estimativa local aproximada (`GameCatalog.cs`). A chave fica cifrada com DPAPI em `%AppData%\ResetExplode` e as respostas em cache por 7 dias.
- **Detector de gargalo**: equilíbrio teórico GPU × CPU; leitura ao vivo (carga da GPU, CPU total, núcleo mais ocupado, VRAM, RAM,
  temperaturas, limitador térmico) com veredito; e análise de sessão (1/3/5 min) com % do tempo limitado por GPU, CPU, VRAM, RAM,
  temperatura ou trava de FPS, mais recomendações (`BottleneckAnalyzer.cs`).
- **Overlay de jogos** (estilo RTSS/Afterburner, `OverlayWindow.xaml`): FPS, frametime, 1% low, gráfico de frametime, GPU/CPU/RAM/VRAM.
  Janela transparente, sempre no topo e que ignora cliques; atalho global **Ctrl+Shift+O**. **Itens escolhidos pelo usuário** (`OverlayMetrics.cs`,
  `MainWindow.Overlay.cs`): FPS/frametime/1% low/gráfico, GPU (uso, temperatura, hotspot, clocks do núcleo e da memória, consumo, tensão,
  ventoinha, PerfCap), VRAM, CPU (uso, núcleo mais ocupado, temperatura, clock médio e máximo, consumo), RAM, nome do jogo e hora. Volta para o topo a cada troca de janela ativa
  (jogos em janela sem borda também se marcam como "sempre no topo") e fica no monitor do jogo. Não aparece em tela cheia exclusiva. O FPS de qualquer jogo é medido via ETW
  (eventos Present de DXGI/D3D9, como o PresentMon, `FrameMonitor.cs`), sem injetar nada no jogo.
- **RESET BENCH** (`BenchRenderer.cs`): benchmark de CPU no princípio do Cinebench, com renderizador próprio. Path tracer monocromático
  (iluminação global, sombras suaves, metal e vidro com refração), 960×540 a 1024 amostras/pixel, imagem em blocos de 32 px
  distribuídos entre threads do centro para fora. Multi-core (todas as threads) e single-core (1 thread, imagem inteira, ~1min40s num 7700X),
  1 passada ou mínimo de 10 min. Nota = amostras/s ÷ 10.000, determinística (sementes fixas por pixel). Histórico dos últimos 10 resultados.
  Referência: Ryzen 7 7700X ≈ 4.500 multi / 530 single.
- **RESET BENCH · benchmarks de jogo**:
  - *Cena de jogo (GPU)* (`GpuGameBench.cs`): vale em Direct3D 11 com terreno fractal, 40 mil árvores por instancing, sombras 4096² com PCF,
    água, neblina e pós HDR; câmera em trajeto fixo por tempo, 1080p/1440p/4K, 30/60 s. Nota = FPS médio × 10. RTX 5070 em 1080p ≈ 680 FPS.
    Os sensores ficam pausados durante o teste (consultas ao driver geravam engasgos no 1% low).
  - *Ray tracing (GPU)* (`GpuRtBench.cs`, `MainWindow.Rt.cs`): Direct3D 12 com **DXR 1.1** (raios inline num compute shader), usa os núcleos
    de RT (RTX 20+, Radeon RX 6000+, Intel Arc). 4 mil objetos (fosco, metal, espelho, luzes), path tracing com sombra suave, oclusão de
    ambiente, reflexos e iluminação indireta; TLAS refeita a cada frame. Presets Normal/Alta/Extrema (2/4/8 amostras, 2/3/4 rebatidas).
    **Confiabilidade**: aquecimento sem medir + 3 passadas; nota pela mediana (FPS × 10), variação entre passadas (confiável abaixo de 3%),
    tempo de frame pelos timestamps da GPU. Referência: RX 9070, Alta 1440p, modo Padrão = 878 pts (87,8 FPS), variação 0,5%.
  - *Simulação de jogo (CPU)* (`GameSimBench.cs`): 30 mil unidades em frente de batalha; thread principal (grade, lógica, preparo do desenho)
    + workers (física, colisão, IA). Nota = FPS da simulação. Ryzen 7 7700X ≈ 290 FPS.
  - *Memória e carregamento* (`MemoryBench.cs`): latência e banda da RAM; carregamento de mapa (pacote de 512 MB + mil arquivos de 64 KB)
    lido sem cache do Windows, com passada prévia para o antivírus e 8 threads. Arquivos temporários apagados no final.
  - *Disco* (`DiskBench.cs`): leitura e escrita sequencial (1 MB) e aleatória 4K (QD1 e QD32), sem cache do Windows
    (`FILE_FLAG_NO_BUFFERING`), em arquivo temporário de 512 MB / 1 GB / 4 GB na unidade escolhida; apagado no final. Histórico dos últimos resultados.
- **RESET OC** (`MainWindow.Oc.cs`, `OcCheck.cs`): ajuste de limite de energia e travamento de clock da GPU NVIDIA (via NVML) com botão de restaurar,
  e **verificação de estabilidade** de overclock (CPU, GPU ou ambos; 5/15/30 min) com limites de segurança de temperatura, leitura de erros
  de hardware (WHEA, reset de driver de vídeo), veredito, histórico e guia de BIOS conforme a placa-mãe e a CPU.
- **RESET OTIMIZA** (`Optimizer.cs`, `WindowsTweaks.cs`): um botão que limpa a memória em espera (como o ISLC) e enxuga a RAM dos
  processos, e fecha os programas marcados (pré-marca só os seguros: OneDrive, Teams, Widgets, Adobe...; programas com janela são
  fechados sem forçar). Ajustes do Windows com interruptor e "Restaurar originais": plano Alto desempenho, Modo de Jogo, desativar
  Game DVR, HAGS, otimizações para jogos em janela, aceleração do mouse. Nada que reduza a segurança (Defender, VBS, UAC, Update).
- **RESET MAKER** (Arduino, ESP32 e conversores USB-serial):
  - *Portas COM* (`UsbCleaner.cs`): lista e remove registros de dispositivos USB/seriais ausentes ("fantasmas" de Arduino,
    ESP32, CH340, FTDI, CP210x...) e libera os números COM presos no ComDB, com backup para desfazer. Resolve a IDE do Arduino
    não achar a placa ou a porta ir para COM20+.
  - *Drivers seriais* (`MakerDrivers.cs`, `MainWindow.Maker.cs`): diagnóstico das placas conectadas com **CH340/CH341, CP2102/CP210x,
    FTDI, Prolific PL2303 e Arduino** pelo código do Gerenciador de Dispositivos (sem driver, código 10, 43, FTDI falsificado com PID 0000),
    com orientação para cada caso; **REINSTALAR** (remove e detecta de novo); lista das versões de driver guardadas no Windows com
    **REMOVER** (backup antes, para voltar a uma versão anterior ou desinstalar de vez) e "Restaurar removidos"; **drivers oficiais incluídos no app** (CH340 4.0, CP210x 11.6,
    FTDI 2.12.36.20 e Arduino, em `Drivers`, veja `Drivers/LEIA-ME.md`) que instalam sem internet; e link para a página oficial de cada fabricante. **Arduino IDE**: instalar/atualizar pelo winget (traz os drivers das placas oficiais),
    fechar programas que prendem a porta (monitor serial, PuTTY, Cura...) e limpar o cache de índices quando a IDE trava em "Downloading index".
- **Inicialização** (`StartupManager.cs`, `MainWindow.Startup.cs`): lista os programas que abrem com o Windows (chaves Run do usuário e
  de todos os usuários, 32 e 64 bits, e pastas Inicializar) com descrição e fabricante. Interruptor para ativar/desativar igual ao
  Gerenciador de Tarefas (chaves StartupApproved) e botão REMOVER, com cópia em `%AppData%\ResetExplode` para "Restaurar removidos".
- **Instalar apps** (`AppInstaller.cs`, `MainWindow.Apps.cs`): instala os essenciais pós-formatação (Steam, Google Chrome, Discord,
  Lightshot, 7-Zip, WinRAR, VLC) pelo **winget**, em silêncio e um de cada vez, mostrando o resultado de cada um. Detecta o que já está
  instalado (Aplicativos instalados do Windows). Para incluir outro app, acrescente uma linha em `AppInstaller.Catalog()` com o ID do winget.
- **Saúde dos discos (S.M.A.R.T.)** (`DiskHealth.cs`, `MainWindow.Disks.cs`): página própria com um cartão por disco físico —
  veredito (Bom/Atenção/Ruim) com os motivos, temperatura, horas ligado, ciclos, desgaste do SSD, setores realocados, erros de leitura/escrita
  e previsão de falha. Fontes: Windows Storage (MSFT_PhysicalDisk, MSFT_StorageReliabilityCounter) e MSStorageDriver_FailurePredictStatus.
- **Relatório exportável** (`ReportBuilder.cs`): botão **EXPORTAR RELATÓRIO** em Sistema. Gera um arquivo HTML (com a marca RESET) ou JSON
  com as informações do sistema, RAM por módulo, discos e saúde dos discos.
- **RAM detalhada**: resumo (capacidade, canais, tipo DDR4/DDR5/LPDDR5, velocidade) e, por módulo, fabricante, part number, formato,
  voltagem, clock configurado e localização do slot.
- **Energia** (`BatteryInfo.cs`, `MainWindow.Net.cs`): bateria de notebook com desgaste (capacidade atual × de projeto), ciclos de carga, estado,
  autonomia, tensão e potência, com veredito; fonte de energia (tomada/bateria), plano de energia do Windows e consumo dos sensores de CPU e GPU.
  Em PC de mesa mostra só o cartão de energia.
- **Rede** (`NetworkTest.cs`): diagnóstico de lag em ~30 s — latência, jitter e perda de pacotes até o roteador e até a internet (1.1.1.1 e 8.8.8.8),
  tempo de DNS, velocidade de download/upload (servidor público speed.cloudflare.com) e latência sob carga (bufferbloat). O veredito aponta se o
  problema está na rede local (Wi-Fi/cabo/roteador), no provedor ou se a conexão está estável.
- **RESET INTEGRAÇÃO** (`LightBackends.cs`, `CorsairSdk.cs`, `CorsairSdkInstaller.cs`, `MainWindow.Integ.cs`): controle de iluminação e efeitos
  de todo o PC numa tela só (em teste). Pontes: **Corsair iCUE / iCUE LINK** (iCUE SDK v4; a DLL é baixada pelo programa, com confirmação e
  verificação de assinatura), **OpenRGB** (servidor SDK local; é o caminho para **MSI Mystic Light, Gigabyte RGB Fusion, ASUS Aura**, RAM, Razer,
  Logitech e outras, já que essas marcas não têm SDK público) e **Razer Chroma** (API REST do Synapse). Efeitos: cor fixa, respiração, arco-íris em onda,
  e cores que seguem a **temperatura da CPU/GPU** ou a **carga da CPU**, com brilho e velocidade. Ao devolver ou fechar, cada fabricante reassume as luzes.
  Para MSI/Gigabyte/ASUS: abra o OpenRGB com o servidor ligado e feche MSI Center, Armoury Crate e RGB Fusion.
- **RESET JOGOS** (`GameLibrary.cs`, `GameLauncher.cs`, `Vdf.cs`, `MainWindow.Launcher.cs`): inicializador que **detecta os jogos instalados** (Steam, Epic Games, GOG,
  Ubisoft Connect e `.exe` adicionados à mão) e guarda um **perfil por jogo**: prioridade do processo, plano de energia Alto desempenho só durante o jogo,
  GPU dedicada e sem otimizações de tela cheia (por `.exe`, no registro do usuário), **menu de gráficos** (modo de janela, resolução, taxa de atualização e extras
  convertidos em argumentos de inicialização no estilo Source/CS2, Unity ou Unreal) e **programas auxiliares** (ferramentas de terceiros que abrem antes do jogo
  e fecham junto). Em jogos da Steam, um botão grava os argumentos nas **opções de inicialização** (`localconfig.vdf`, com backup, fechando a Steam antes).
  O botão JOGAR aplica o perfil, inicia e desfaz o que for temporário quando o jogo fecha.

## RESET OTIMIZA · diagnóstico e Modo Jogo

- Em **Otimização**, atualize o diagnóstico para revisar RAM, temperaturas, alimentação e plano de energia. As leituras representam o momento atual; use **Gargalo** durante uma partida para investigar limitações.
- Cada ajuste mostra estado, compatibilidade, orientação e opção de restaurar individualmente. HAGS exige suporte da GPU/driver e comparação após reiniciar; o programa não presume ganho de FPS.
- Os originais são gravados **antes** da alteração. As configurações usam substituição atômica e cópia `.bak`; restaurações com falha mantêm o backup para nova tentativa. Se ambas as cópias estiverem inválidas, elas são preservadas e novas gravações são bloqueadas.
- A limpeza de RAM é opcional e começa desmarcada. Ela pode exigir recarregar dados e não comprova ganho de desempenho.
- Em **Biblioteca → Desempenho**, marque **Modo Jogo** por jogo: usa Alto desempenho e pausa a captura do Windows durante o acompanhamento. Ao terminar, cancelar ou fechar normalmente o app, restaura os valores anteriores. Pode aumentar consumo e temperatura.
- Se o app for encerrado à força, abra **Otimização → Restaurar sessão pendente**. O registro `game-session.json` é preservado enquanto houver falhas na restauração. Não há alterações automáticas na abertura do app.
- **Extremo · perfil global** permite revisar e aplicar um conjunto de ajustes do Windows que permanece ativo até restaurar os originais, inclusive para jogos abertos fora do RESET. Não há dependência de CS2, engine, loja ou fabricante de GPU. Cada ajuste mantém suas condições de compatibilidade.
- O perfil sugere Modo de Jogo, desativação de captura (desmarque se usa gravação) e otimizações de janela quando compatíveis. Alto desempenho é sugerido somente na tomada, com leituras de CPU abaixo de 85 °C e de GPU abaixo de 80 °C. Se a leitura faltar ou estiver alta, a opção permanece disponível para seleção manual.
- P2P de atualizações, transparências, animações, HAGS e aceleração do mouse são opções explícitas. P2P usa `DODownloadMode=0` (HTTP Only), preservando Windows Update; animações usam `SystemParametersInfo`, sem editar máscaras binárias. Esses ajustes não são uma promessa de FPS ou ping.
- A área **GPU** oferece orientação no Adrenalin (AMD), painel NVIDIA e Intel, com documentação oficial. A escolha de GPU pode ser manual em sistemas híbridos. Anti-Lag, Chill, Boost, FSR/RSR e HYPR-RX/AFMF não são alterados automaticamente; disponibilidade e combinações dependem do hardware, driver e jogo.
- **Usar Modo Jogo temporário em qualquer jogo da Biblioteca** habilita a sessão temporária como padrão também para `.exe` adicionados manualmente, preservando o perfil salvo de cada jogo. A execução usa uma cópia do perfil para evitar que edições na interface alterem uma sessão já aberta.
- O histórico registra aplicações e falhas, sem tratar alteração de configuração como ganho medido. A consulta de VBS/Integridade de Memória é somente leitura e não desliga proteções. Comparação automática antes/depois fica para uma etapa posterior.

Referências consultadas: [Hone](https://hone.gg/pt), [Medium](https://medium.com/@sethterranova2166/how-to-optimize-windows-for-better-gaming-performance-9ea20e074143), [VinfoSmart](https://vinfosmart.com.br/aprenda-a-otimizar-o-windows-11-para-jogos-e-ganhe-mais-desempenho-guia-completo-sobre-modo-jogo-vbs-drivers-e-mais-conte-com-a-vinfosmart/), [XboxPlay](https://xboxplay.games/pt/news/como-otimizar-as-configuraes-do-amd-radeon-69597) e [discussão AMDHelp](https://www.reddit.com/r/AMDHelp/comments/1lnxb8o/ultimate_amd_performance_fix_guide_stop_lag_fps/). São referências de requisitos e hipóteses; scripts e receitas não foram importados. O vídeo informado não pôde ser acessado e não foi auditado.

As decisões técnicas foram conferidas nas fontes oficiais: [otimizações de janela](https://support.microsoft.com/en-us/windows/hardware/display-graphics/optimizations-for-windowed-games-in-windows-11), [Delivery Optimization](https://learn.microsoft.com/en-us/windows/deployment/do/waas-delivery-optimization-reference), [SystemParametersInfo](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-systemparametersinfow), [gráficos Radeon](https://www.amd.com/en/resources/support-articles/faqs/DH-012.html), [Anti-Lag](https://www.amd.com/en/products/software/adrenalin/radeon-software-anti-lag.html) e [HYPR-RX](https://www.amd.com/en/products/software/adrenalin/hypr-rx.html).

### Perfil de serviços para jogos

Em **Otimização → Serviços · Segurança preservada**, analise os serviços locais antes de aplicar. O catálogo fechado contém candidatos opcionais e mostra os efeitos da perda de cada função. A sugestão inicial considera apenas os poucos candidatos marcados como sugeridos que estejam automáticos e em execução; não presume ganho de FPS. Impressão, câmera, busca, relatórios de erros e descoberta de dispositivos são condicionais e começam desmarcados.

Segurança (inclusive antivírus de terceiros não reconhecido), autenticação, firewall, auditoria, drivers, áudio, entrada, rede, atualizações, instalação/licenciamento de jogos, serviços por usuário e serviços não validados ficam fora da lista de alterações. Em particular, AppXSvc, ClipSVC, Gaming Services/Xbox, GameInputSvc, WinHttpAutoProxySvc, iphlpsvc, SysMain, NVIDIA Display Container e serviços de gerenciamento corporativo são preservados.

O perfil não importa os estados de inicialização de listas externas, não modifica permissões/ACLs e não força parada de processos. Dependentes instalados, drivers, estados transitórios e serviços que não aceitam parada bloqueiam a mudança. Os originais (tipo de inicialização, automático atrasado e estado parado/em execução) são gravados em `%AppData%\ResetExplode\services-profile.json` antes de qualquer alteração. Falhas de aplicação tentam reverter a tentativa; falhas de restauração mantêm os originais. A restauração é individual ou em lote e funciona após reabrir o app. O botão **Restaurar originais** dos ajustes de Registro é separado de **Restaurar serviços do perfil**.

**Parar após item atual** interrompe o lote entre serviços, depois da tentativa de mudança/restauração em andamento. Ao fechar o app, ele aguarda essa tentativa terminar. Alterações concluídas persistem até restaurar; não são automaticamente revertidas ao fechar o jogo. Nenhum serviço do computador foi alterado durante o desenvolvimento e os testes usam um backend simulado.

A lista fornecida contém 254 registros, sem medições de CPU/disco. A classificação completa está em [reports/services-validation.txt](reports/services-validation.txt): 24 preservados como segurança, 73 como funcionalidade, 15 condicionais e 142 preservados por falta de validação/identidade específica. Essa classificação não confirma que todos existem neste PC. O botão **Validar lista .txt** exporta uma auditoria de referências no formato ServiWin, sem executar ou importar instruções presentes no arquivo.

Referências de validação: [tópico fornecido](https://www.reddit.com/r/Windows10HowTo/comments/119tetf/which_windows_11_services_to_disable_for_gaming/), [guia Microsoft para dispositivos IoT de função específica](https://learn.microsoft.com/windows/iot/iot-enterprise/optimize/services) e [configuração oficial de serviços](https://learn.microsoft.com/en-us/windows/win32/services/service-configuration). Recomendações de IoT/servidor não foram tratadas como receita universal para Windows de uso geral.

### Executar verificações

Verificações de persistência, recuperação e restauração, com arquivos temporários e ajustes simulados (sem alterar configurações reais):

```powershell
dotnet run --project tests/OptimizationChecks/OptimizationChecks.csproj -c Release
```

## Interface

- Visual preto e branco minimalista em **blocos** (cartões arredondados, inspirado em painéis de dashboard). Um único menu lateral
  com *Visão geral* no topo e categorias **RESET** recolhíveis (começam fechadas; clique para ver as sub-abas):
  - *Visão geral* (`MainWindow.Dash.cs`): gráfico de área CPU × GPU (temperatura, carga ou consumo; 1/5/15 min),
    desempenho do RESET BENCH, tabela de componentes, anéis de RAM/VRAM/disco, eventos recentes e saúde do PC.
  - **RESET EXPLODE** · Sistema, Sensores, Placa de vídeo, Discos, Energia, Rede.
  - **RESET JOGOS** · Biblioteca, FPS estimado, Gargalo, Overlay.
  - **RESET DESEMPENHO** · Benchmark, Stress test, Overclock.
  - **RESET FERRAMENTAS** · Otimização, Inicialização, Instalar apps, Drivers, Integração.
  - **RESET MAKER** · Portas COM, Drivers seriais.
- Botão de **modo escuro/claro** no canto superior direito (`ThemeManager.cs`); a escolha fica salva. O overlay de jogos e os avisos de dispositivo ficam sempre escuros.
- Leitura ao vivo de temperatura e carga da CPU e da GPU sempre visível no rodapé do menu.
- Sensores com busca e filtros por tipo (chips); tema em `Theme.xaml`.

## Base utilizada
- Interface, stress test e gráficos: código próprio.
- Sensores: [LibreHardwareMonitorLib](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor) (MPL 2.0) — mantenha o aviso de licença ao distribuir.
- Informações do sistema: WMI do Windows.
- Medição de FPS: [TraceEvent](https://github.com/microsoft/perfview) (MIT), da Microsoft, para ler eventos ETW.
- Renderização do stress de GPU: [Vortice.Windows](https://github.com/amerkoleci/Vortice.Windows) (MIT), bindings de Direct3D 11 para .NET. Direct3D 12 e DXC (compilador de shaders da Microsoft) também via Vortice, no teste de ray tracing.

## Requisitos
- Windows 10/11
- .NET 8 SDK (https://dotnet.microsoft.com/download)
- Driver **PawnIO** (temperatura/consumo da CPU): `winget install namazso.PawnIO`
  A LibreHardwareMonitorLib 0.9.6+ usa o PawnIO no lugar do antigo WinRing0, que o Windows Defender bloqueia.

## Como rodar

### Análise atual de serviços e aplicativos

Em **Otimização → Análise atual**, use **Analisar este PC**. O inventário consulta todos os serviços instalados e mostra processos da sessão atual, estado, fabricante declarado, caminho e uma amostra de aproximadamente dois segundos de CPU, RAM e E/S. A análise é somente leitura e começa sem seleções.

Selecione uma linha para ver ação proposta, impacto funcional e benefício esperado. A prévia permite revisar antes de aplicar. Somente serviços do catálogo validado e aplicativos opcionais conhecidos com janela podem receber ações; segurança, dependências e itens desconhecidos são preservados. A identidade dos processos e o estado dos serviços são conferidos novamente ao aplicar. A prévia expira após dois minutos.

Serviços alterados têm os originais persistidos em `services-profile.json` na pasta de dados do aplicativo e podem ser restaurados pelo painel do catálogo. Aplicativos recebem pedido normal de fechamento, sem encerramento forçado ou reabertura automática; salve o trabalho. O relatório JSON exporta o inventário e a seleção, incluindo caminhos locais.

CPU é normalizada pela capacidade total do processador. RAM é working set e pode incluir páginas compartilhadas; E/S inclui operações além de disco. Consumo de um host compartilhado pertence ao processo inteiro. Esses números não são uma previsão de memória liberada ou ganho em FPS. Falhas de leitura são indicadas, e validações incompletas bloqueiam as ações correspondentes.

```
cd ResetExplode
dotnet run
```
O app pede privilégio de administrador (necessário para sensores de baixo nível).
Para levar a outro PC: dê dois cliques em `publicar.bat`. Ele gera `..\BaraoTechExplode-dist\BaraoTechExplode.exe` (arquivo único, com o .NET embutido,
sem caminhos do seu usuário) + `LEIA-ME.txt`. No outro PC só é preciso instalar o PawnIO. Rode de novo após cada alteração no código.

### Baixar a versão mais recente
A cada push na `main`, o GitHub Actions (`.github/workflows/publicar.yml`) gera o `BaraoTechExplode.exe` e atualiza a release
**latest** (aba *Releases* do repositório), com o `.exe` e um `.zip`. É só baixar de lá, sem compilar.
Ao criar uma tag (`git tag v0.12` e `git push --tags`), também sai uma release fixa dessa versão.

## Observações
- Se algum sensor não aparecer, depende do suporte da biblioteca à sua placa-mãe.

## Já feito
- ✔ Informações do sistema, sensores, placa de vídeo, jogos, gargalo, overlay
- ✔ Stress test com limite de temperatura (CPU, RAM, GPU)
- ✔ Benchmarks: CPU, GPU, simulação, memória e **disco**
- ✔ Saúde dos discos (S.M.A.R.T.)
- ✔ Relatório exportável (HTML/JSON)
- ✔ RESET OC (limite de energia, clock e verificação de estabilidade)
- ✔ Energia (bateria e fonte) e Rede (latência, perda e velocidade)
- ✔ RESET INTEGRAÇÃO: iluminação Corsair iCUE, OpenRGB (MSI/Gigabyte/ASUS) e Razer Chroma (primeira versão, em teste)
- ✔ RESET DRIVER: inventário de drivers, NVIDIA (verifica a versão, baixa, confere a assinatura e abre o instalador), AMD/ASUS/MSI/Gigabyte pela página oficial, Windows Update e backup (`DriverManager.cs`, primeira versão, em teste)
- ✔ RESET JOGOS: inicializador com detecção de jogos, perfil por jogo e opções de inicialização da Steam (primeira versão, em teste)
- ✔ Inicialização do Windows: ativar, desativar e remover programas, com desfazer (v0.16)
- ✔ Abertura rápida: janela na hora, hardware carregado em paralelo (v0.16)
- ✔ Instalar apps essenciais pós-formatação pelo winget (v0.16)
- ✔ RESET MAKER: portas COM e drivers seriais CH340/CP210x/FTDI/Prolific/Arduino (v0.16)

## Próximas ideias
- Relatório em CSV e com gráficos
- Comparar resultados de benchmark entre PCs / histórico de relatórios
- Leitura direta de SPD / Super I/O
- RESET INTEGRAÇÃO: perfis salvos, efeitos por jogo, SDKs nativos da MSI/ASUS/Gigabyte se forem liberados
- Alertas de temperatura, bateria e saúde de disco em segundo plano
- Versão com instalador e assinatura digital

## Licença

MIT — veja o arquivo [LICENSE](LICENSE).
