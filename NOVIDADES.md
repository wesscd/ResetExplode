## v0.21 · Resumo da análise e confirmação dos resultados

- Resumo global dos itens reconhecidos, avaliados, já no alvo, disponíveis e preservados, independente dos filtros.
- Último resultado na linha e no painel lateral, com horário e antes/depois da tentativa.
- Estados explícitos para aplicação verificada, reinício necessário, já ajustado, bloqueio, falha, pendência, restauração e cancelamento.
- Pedido de fechamento de aplicativo só conta como aplicado após uma nova consulta confirmar a ausência do processo.
- Resumo e detalhes do último lote mantêm os aplicativos que saíram da lista; exportação JSON inclui esse lote.
- Atalho para abrir o console e aviso quando uma leitura falha, é cancelada ou expira ao tentar aplicar.
- Identidade do processo inclui início e caminho: um PID reutilizado não herda seleção ou resultado antigo.
- Validação com backends simulados e prévia visual com dados de demonstração; nenhum ganho de FPS presumido.

## Novidades da v0.16

**Nova página · RESET FERRAMENTAS › Inicialização**
- Lista os programas que abrem junto com o Windows: chaves Run do registro (usuário, todos os usuários, 32 e 64 bits)
  e pastas Inicializar, com descrição, fabricante e aviso quando o arquivo do programa não existe mais.
- Interruptor para ativar/desativar, igual ao Gerenciador de Tarefas: a entrada continua lá e pode ser religada.
- Botão **REMOVER** com confirmação; o programa continua instalado e tudo pode ser desfeito em **Restaurar removidos**.

**Nova página · RESET FERRAMENTAS › Instalar apps**
- Os essenciais depois de formatar o PC: Steam, Google Chrome, Discord, Lightshot, 7-Zip, WinRAR e VLC.
- Instala pelo winget (instalador de pacotes da Microsoft que já vem no Windows): versão oficial mais recente,
  sem janelas, um app de cada vez, com o resultado de cada um na lista.
- Mostra o que já está instalado; se o winget ainda não existir (PC recém-formatado), um botão abre a Microsoft Store
  para atualizar o "Instalador de Aplicativo".

**Nova categoria · RESET MAKER** (Arduino, ESP32 e conversores USB-serial)
- **Portas COM**: a limpeza de dispositivos fantasmas e de números COM presos saiu de Otimização e ganhou página própria.
- **Drivers seriais**: diagnóstico das placas com CH340/CH341, CP2102/CP210x, FTDI, Prolific PL2303 e Arduino
  (sem driver, código 10, código 43, FTDI falsificado travado em PID 0000), com a orientação de cada caso.
- **REINSTALAR** remove o dispositivo e faz o Windows detectar de novo com o melhor driver.
- Lista as versões de driver guardadas no Windows para cada chip, com **REMOVER** (backup antes): volta para uma versão
  anterior quando a nova não funciona com clones, ou desinstala de vez. **Restaurar removidos** desfaz.
- **Drivers incluídos no app**: CH340 4.0, CP210x 11.6, FTDI 2.12.36.20 e Arduino, oficiais e assinados, instalam sem internet
  (ideal logo depois de formatar), com a placa conectada ou não. Também abre a página oficial de cada fabricante.
- **Arduino IDE**: instalar/atualizar pelo winget, fechar programas que prendem a porta COM e limpar o cache de índices
  quando a IDE trava em "Downloading index".

**Novo benchmark · Ray tracing (GPU)**
- Teste em Direct3D 12 com DXR 1.1 que usa os núcleos de ray tracing (RTX 20+, Radeon RX 6000+, Intel Arc): 4 mil objetos,
  reflexos de reflexos, sombras suaves, oclusão de ambiente e iluminação indireta, com a cena refeita a cada frame.
- Feito para comparar com confiança: aquecimento sem medir, 3 passadas iguais, nota pela mediana e variação entre passadas
  (selo de "resultado confiável" abaixo de 3%); tempo de frame medido pelo relógio da própria GPU.
- Presets Normal, Alta e Extrema em 1080p, 1440p e 4K; histórico comparando só resultados do mesmo preset e resolução.
- Referência: RX 9070, Alta 1440p = 878 pts (87,8 FPS), variação 0,5%.

**Overlay personalizável**
- Na aba Overlay, escolha o que aparece: FPS, frametime, 1% low, gráfico; GPU (uso, temperatura, hotspot, clock do núcleo,
  clock da memória, consumo, tensão, ventoinha, limitador NVIDIA); VRAM (usada, %, temperatura); CPU (uso, núcleo mais
  ocupado, temperatura, clock médio, clock máximo, consumo); RAM (GB, %); nome do jogo e hora. Atalhos SÓ FPS, PADRÃO e TUDO.
- Cada componente vira uma linha só com os itens escolhidos; a escolha fica salva e vale na hora.

**RESET JOGOS**
- Novo atalho em Extras: **-allow_third_party_software** (CS2), que libera captura de jogo do OBS, ReShade e overlays
  bloqueados pelo modo confiável; o aviso sobre o Fator de Confiança aparece ao passar o mouse.

**Abertura mais rápida**
- A janela aparece em ~0,5 s (antes ~4,6 s). Informações do sistema, sensores e placa de vídeo carregam em paralelo,
  em segundo plano, e cada página é preenchida quando a sua parte fica pronta.
- O aviso "sem sensor da CPU" não aparece mais por engano enquanto o hardware ainda está sendo detectado.

**Correção · Overlay de jogos**
- O overlay agora fica por cima de jogos em janela sem borda que também se marcam como "sempre no topo"
  (ex.: GTA V e Cyberpunk 2077), inclusive depois de Alt+Tab.
- Aparece no monitor onde o jogo está, não só no monitor principal.
# BaraoTechExplode · fork da Barão Tech

## v0.20 · Lista de estado e alterações previstas

- Tela principal em lista agrupada por Windows, serviços e aplicativos, com estado lido, resultado previsto e avaliação em cada linha.
- Seleção de fila separada do estado real: marcar não aplica; a revisão apresenta todas as mudanças antes da confirmação.
- Painel lateral com impactos, motivos de preservação, backup e último resultado da lista na sessão.
- Busca e filtros por categoria, disponíveis, preservados e selecionados, além de seleção de sugestões conservadoras.
- Aplicação unificada pelos backends existentes, releitura após o lote e restauração individual pelo backup.
- Console recolhível e ferramentas anteriores mantidas em uma aba avançada.

## v0.19 · Otimização guiada e console persistente

- Página reorganizada com instruções por etapas, análise em primeiro plano e ferramentas anteriores recolhidas em áreas avançadas.
- Console fixo com horário, item, início, backup, aplicação, falha, bloqueio, cancelamento e restauração, incluindo estados quando disponíveis.
- Mensagens de bloqueio junto à operação, com motivos específicos para jogo, medição, outra operação e sessão pendente.
- Filtro, cópia e exportação do console, além de logs diários locais que permanecem após fechar o aplicativo.
- Diagnóstico de falha na gravação do log sem esconder o resultado ou interferir nos backups e na recuperação.

## v0.18 · Comparação de sessões e recuperação visível

- Aviso global ao abrir o aplicativo com ajustes temporários pendentes, funções afetadas, estados originais e botão de restauração.
- Medição por jogo em janelas de 60 segundos, com contagem regressiva de dez segundos, rótulos Antes/Depois e descrição do cenário.
- Comparação de FPS, 1% low, tempos médio/P99 e temperaturas; sessões incompletas ou contextos diferentes não geram percentuais de melhora.
- Registro do perfil, estados lidos do Windows e processo, histórico atômico e exportação JSON da comparação.
- Métricas calculadas a partir dos intervalos individuais de Present do ETW; limitações explicitadas no aplicativo e na documentação.

## v0.17 · Identidade e otimização revisável

O código principal é de Gustavo da Reset (GustavoReset), autor do RESET EXPLODE. Este fork aproveita sensores e ferramentas de desempenho da base original para desenvolver diagnóstico e otimização para jogos. Licença MIT e copyright original preservados. Projeto original: https://github.com/GustavoReset/ResetExplode · Barão Tech: https://baraotech.online/

- Nova identidade BaraoTechExplode com logotipo do site da Barão Tech, ícone do Windows e painel Sobre / Créditos.
- Análise atual de serviços e aplicativos, amostra de CPU/RAM/E/S, classificação, impactos e prévia antes da aplicação.
- Perfil de serviços com segurança preservada, validação de dependências, backup e restauração.
- Serviços temporários selecionados por jogo na Biblioteca, restauração ao terminar o acompanhamento e recuperação de sessões interrompidas.
- Histórico persistente das sessões de serviços, incluindo falhas e novas tentativas de restauração.
- Ajustes globais revisáveis, sessão de energia e captura reversível, guias para GPUs AMD, NVIDIA e Intel.
- Testes de persistência, falhas parciais, dependências, identidade de processos e restauração com backends simulados.

O executável deste fork é `BaraoTechExplode.exe`. A pasta de dados `%AppData%\ResetExplode` é mantida para compatibilidade com os backups existentes. Feche a versão anterior antes de abrir esta. A análise não promete ganho de FPS; compare resultados no seu jogo e hardware.

---
