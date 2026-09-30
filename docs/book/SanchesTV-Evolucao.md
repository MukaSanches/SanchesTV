<!-- page:cover -->
<!-- eyebrow: SANCHESTV · CADERNO DE ENGENHARIA E DESIGN -->
# A evolução de uma experiência de cinema

SanchesTV

Da primeira biblioteca de canais a uma central multimídia com reprodução resiliente, ferramentas locais e descoberta de conteúdo.

Edição de {{edition_date}} · Evolução preparada para v8.3.0

227 commits de origem · 15 marcos versionados · Uma evolução que preserva o que já existe

<!-- page:reading -->
<!-- eyebrow: 01 · A IDEIA E O COMPROMISSO -->
# Um aplicativo ganha força quando evolui com memória

O SanchesTV começou como um cliente Windows de TV e IPTV e acumulou capacidades de uma central multimídia. A melhor continuação desse percurso liga o catálogo ao player, o programa ao horário e o favorito à experiência cotidiana, mantendo as funções que já atendem ao usuário.

Este livro registra dois recortes diferentes: a história que está no Git e a evolução preparada nesta etapa. O ponto de partida é o commit **{{baseline_commit}}**, identificado como v8.2.0. Antes das mudanças desta sessão, o repositório continha **{{baseline_commits}} commits**, **{{baseline_tags}} tags** e 105 arquivos versionados. Os commits desse acervo se concentram entre 20 e 23 de setembro de 2026; essa cadência registra trabalho no repositório e não comprova adoção por usuários.

As decisões descritas aqui seguem três compromissos: preservar as funções anteriores, acrescentar descoberta sem acoplar o catálogo ao pipeline de vídeo e explicar com precisão o que foi validado. Tecnologias têm papéis específicos: a qualidade vem da integração entre elas, do tratamento de falhas e da clareza da interface.

> A ambição é uma experiência de cinema. O método é engenharia verificável: fontes identificadas, testes rastreáveis e limites declarados.

As três imagens da galeria visual são **prévias em HTML com dados demonstrativos**. Elas ajudam a revisar direção de arte e organização da informação. O cliente de produção continua sendo WPF para Windows; essas três imagens não são capturas de uma execução desse cliente.

A página 24 acrescenta uma **captura nativa WPF com dados demonstrativos**, produzida no CI Windows. Ela registra a renderização do Cinema Hub real, sem iniciar reprodução de mídia nem validar hardware.

<!-- diagram:principles -->

<!-- page:contents -->
<!-- eyebrow: 02 · GUIA DE LEITURA -->
# O caminho da evolução

| Página | Assunto |
| --- | --- |
| 02 | Ideia, recorte e compromisso |
| 04 | As primeiras versões: base, catálogo e identidade |
| 05 | O motor multimídia e o Cinema OS |
| 06 | A matriz de preservação das funções |
| 07 | Arquitetura e fronteiras entre os componentes |
| 08 | O player e a inteligência de reprodução |
| 09 | Fontes, busca e catálogo |
| 10 | EPG, gravação e memória do ao vivo |
| 11 | Streaming P2P e saúde do swarm |
| 12 | Media Lab e inteligência artificial local |
| 13 | Orquestração durável |
| 14 | Cinema Hub: descoberta e continuidade |
| 15 | Integração da nova experiência |
| 16 | Privacidade e dados locais |
| 17 | Qualidade e evidências de validação |
| 18 | Distribuição e proveniência dos runtimes |
| 19–21 | Galeria visual comentada |
| 22 | Próximos passos planejados |
| 23 | Fontes e reprodução deste livro |
{{native_index}}

**Como ler as evidências.** “Existente” descreve código ou documentação no ponto de partida. “Adicionado” descreve o trabalho integrado nesta etapa. “Planejado” indica uma proposta futura. “Dependente de ambiente” identifica resultados que precisam de Windows, hardware ou uma fonte de mídia real.

O histórico aponta tags como marcos do código; este livro não afirma que cada tag corresponde a uma publicação verificada de instaladores. Esta edição acompanha a preparação da v8.3.0. O estado dos instaladores e da publicação pode ser consultado na [página GitHub Releases]({{releases_url}}).

<!-- page:history_early -->
<!-- eyebrow: 03 · HISTÓRIA NO GIT -->
# A base, o catálogo e a identidade

O primeiro ciclo resolve o acesso à mídia e torna o catálogo mais útil para quem usa o aplicativo em português. A importação permanece importante durante toda a evolução: cada nova camada precisa conviver com listas e serviços fornecidos pelo usuário.

| Marco | Commit | Evolução registrada |
| --- | --- | --- |
| v1.0.0 | 15d07ea | Base do cliente, importação e instalador. |
| v2.0.0 | 467db0b | Catálogo lusófono e tratamento de headers das fontes. |
| v3.0.0 | c47e2be | Premium Hub com destinos oficiais. |
| v4.0.0 | df42b58 | Organização de fontes públicas. |
| v5.0.0 | 398f61d | Identidade Fluent Cinema. |
| v5.1.0 | accd48d | Mapeamento da interface e evolução incremental. |
| v5.2.0 | a465ad7 | Refinamentos de responsividade. |

A documentação das primeiras versões conserva o desenho básico de **Core + Desktop + Tests**. O núcleo contém parsing e modelos; a interface monta a experiência de uso; os testes permitem modificar regras sem depender de uma janela aberta.

Esse desenho fornece continuidade. M3U/M3U8, URLs de listas, Xtream fornecido pelo usuário e XMLTV são formas diferentes de entrada. Favoritos, Recentes e Minha TV organizam a relação do usuário com essas fontes. Os hubs oficiais acrescentam caminhos de descoberta, mas a disponibilidade e os direitos de acesso continuam definidos pelos serviços de origem.

O valor desse período está em estabelecer uma base que suporta mudança. A identidade visual passa a orientar hierarquia e navegação, e as regras de layout passam a merecer atenção própria. Uma biblioteca maior precisa continuar legível na resolução em que o aplicativo será usado.

<!-- page:history_engine -->
<!-- eyebrow: 04 · HISTÓRIA NO GIT -->
# Da reprodução ao Cinema OS

O segundo ciclo expande o motor multimídia, introduz persistência operacional e melhora a relação entre a interface e o vídeo. A preferência por componentes locais evita exigir infraestrutura de servidor para tarefas que cabem no desktop.

| Marco | Commit | Evolução registrada |
| --- | --- | --- |
| v6.0.0 | bf371bf | libmpv, LibVLC, FFmpeg e NAudio. |
| v6.1.0 | db58e60 | MonoTorrent incorporado ao aplicativo. |
| v6.1.1 | fa176a8 | Buffer inicial e watchdog de streaming P2P. |
| v7.0.0 | 39cb10f | Media Lab, EPG, gravação, timeshift, IA local, observabilidade e Velopack. |
| v7.0.1 | 3856e4c | Ajustes incrementais. |
| v8.0.0 | 0a959e8 | Playback Intelligence e Command Center com Ctrl+K. |
| v8.1.0 | 26027fe | Cinema OS Refined. |
| v8.2.0 | c76469e | Boot durável com SQLite/WAL. |

A v8.1 trata um limite concreto de WPF com vídeo nativo: elementos de metadados e estado precisam permanecer fora da superfície do libmpv para não desaparecerem por problemas de airspace. O player continua ocupando a região principal em resoluções como 1366 × 768 e 1536 × 864; busca, navegação e rodapé recebem ajustes de densidade.

A v8.2 adiciona um motor de workflows local. O boot passa pelo fluxo **database → seed → channels → home**, com estado e eventos persistidos. O orquestrador não substitui o pipeline de reprodução.

O trabalho desta etapa parte desse conjunto já amplo. O Cinema Hub torna a descoberta mais expressiva e integra caminhos já existentes, enquanto a revisão de qualidade cuida de comportamentos de falha e recuperação. A história anterior permanece uma parte ativa do projeto.

<!-- page:preservation -->
<!-- eyebrow: 05 · CONTINUIDADE -->
# Preservar é um requisito de produto

O pedido de evolução inclui uma condição clara: **não remover o que já existe**, preservando todas as listas IPTV e todos os ícones. A verificação da alteração confirmou **zero arquivos removidos**, os diretórios **Catalog, Parsing e Import sem modificações** e os **44 glifos originais de MainWindow preservados**. O novo hub é acrescentado à navegação e reutiliza os fluxos do aplicativo.

| Área | Capacidades que compõem a base |
| --- | --- |
| Biblioteca | Catálogos, fontes públicas/oficiais, M3U/M3U8, URL M3U e Xtream. |
| Organização | Favoritos, Recentes, Minha TV e busca tolerante a erros. |
| Reprodução | libmpv, fallback LibVLC, perfis, faixas, delays e captura de frame. |
| Ao vivo | XMLTV/EPG, timeshift local e retorno ao LIVE. |
| Gravação | FFmpeg, agendamento por programa e persistência dos horários. |
| P2P | Magnet e .torrent do usuário, seleção de arquivo, seek e gestão de cache. |
| Janelas | PiP, multiview, configurações de áudio/vídeo e controle remoto. |
| Ferramentas | Media Lab, roteamento local, DLNA, legendas e processamento offline. |
| Operação | Command Center, saúde de fontes, diagnósticos e workflows duráveis. |

Preservação não significa congelar o código. É possível corrigir cancelamento, melhorar limpeza de histórico e adicionar testes sem reduzir a superfície do produto. O critério é que as rotas anteriores continuem disponíveis e que uma adição não assuma o controle indevido de outra área.

O catálogo de descoberta trabalha sobre canais e programas já conhecidos. A ação de reproduzir volta ao mecanismo existente; a ação de favoritar usa a persistência da biblioteca; o acesso ao EPG utiliza o guia existente. Essa relação diminui duplicação e torna o comportamento mais previsível.

**Limite da evidência:** a inspeção de código e a compilação podem confirmar que recursos permanecem integrados. Uma garantia completa de comportamento em todos os fluxos exige também regressão manual no cliente Windows com mídia real.

<!-- page:architecture -->
<!-- eyebrow: 06 · ARQUITETURA -->
# Fronteiras claras para uma experiência mais rica

O projeto mantém uma separação entre lógica do catálogo e plataforma Windows. Essa organização permite validar o núcleo em Linux sem confundir essa execução com uma validação completa de WPF e dos runtimes nativos.

<!-- diagram:architecture -->

**SanchesTV.Core — net8.0.** Modelos, parsing M3U/XMLTV, busca, catálogo, SQLite, política de fontes, política P2P, layout e orquestração durável vivem no núcleo. Polly participa das operações HTTP. A descoberta nova também pode ser testada sem iniciar o player.

**SanchesTV.Desktop — net10.0-windows e WPF.** A interface, as janelas auxiliares, o acesso ao áudio Windows, a hospedagem de vídeo e o controle de processos externos residem no cliente desktop. LibVLCSharp e libmpv são integrados nessa camada.

**SanchesTV.Tests — xUnit.** Os testes automatizados verificam regras do núcleo e contratos importantes: parsing, normalização, busca, catalogação, prioridades, agendamentos, políticas de mídia, layout e workflows. Novos testes devem representar resultados observáveis, especialmente onde há persistência e recuperação de falhas.

Os executáveis externos têm responsabilidade delimitada. FFmpeg processa ou grava mídia; MediaMTX roteia; Whisper transcreve; os processadores Vulkan trabalham offline. A interface os coordena sem transformar toda operação pesada em uma função interna do player.

Essa divisão também evidencia riscos. Cancelamentos precisam atravessar fronteiras corretamente. Banco e processos externos exigem limpeza e tratamento de exceção. Uma janela bonita só se sustenta se essas relações continuarem simples de entender e de verificar.

<!-- page:playback -->
<!-- eyebrow: 07 · REPRODUÇÃO -->
# Um caminho de vídeo com memória de saúde

O motor preferencial é **libmpv**, com **LibVLC** como alternativa automática quando necessário. O coordenador centraliza essa decisão; a troca de motor precisa respeitar a fonte selecionada e o cancelamento da operação atual.

<!-- diagram:playback -->

O Playback Intelligence considera status, prioridade, latência, histórico de sucesso, falhas consecutivas e tempo médio até iniciar, conhecido como TTFF. Essa política ordena fontes de forma mais informada do que uma lista estática. A saúde é local e usa uma identidade SHA-256; o ledger não precisa guardar URLs completas, query strings, headers ou tokens.

O Cinema Engine oferece perfis Balanceado, Qualidade Máxima, Baixa Latência e Baixo Consumo. A base de reprodução reúne gpu-next/libplacebo, D3D11, FFmpeg, dav1d e libass. Há controles de idioma e faixa de áudio/legenda, delays, deinterlace, interpolação temporal do player, HDR configurável e captura de frame.

Na camada de áudio, NAudio/WASAPI e os tratamentos Normalizar, Night e Dialogue acrescentam ajustes de uso cotidiano. O modo exclusivo depende de suporte do dispositivo. O monitor de hardware pode orientar o perfil de qualidade conforme carga e temperatura.

**O que exige aparelho real:** qualidade HDR, aceleração da GPU, estabilidade dos drivers, modo exclusivo de áudio e desempenho de decodificação. A presença dessas opções no código não prova que todo computador as executará. Fontes ao vivo também podem desaparecer, exigir headers ou ter comportamento diferente entre motores.

O novo caminho de descoberta não passa a reproduzir por conta própria. Ele seleciona um canal; o pipeline já existente cuida da reprodução, da saúde e das alternativas.

A revisão desta etapa reproduziu uma falha: cancelar por solicitação do usuário podia penalizar a saúde da fonte no fallback VLC. A correção propaga o cancelamento sem registrar uma falha de mídia. Um harness com engines controlados verificou cancelamento, timeout e reprodução seguinte; ele não substitui uma sessão com vídeo real.

<!-- page:catalog -->
<!-- eyebrow: 08 · FONTES E DESCOBERTA -->
# Encontrar conteúdo começa por organizar a entrada

As listas M3U/M3U8 e o serviço Xtream fornecido pelo usuário convivem com os catálogos públicos e oficiais já configurados. Cada formato carrega características próprias, como nome, grupo, logotipo, headers e várias fontes associadas ao mesmo canal.

A busca tolerante a erros reduz o atrito de digitar nomes e ajuda a localizar canais em catálogos grandes. A interface já usa debounce e cancelamento para evitar uma refiltragem a cada tecla. A continuidade do produto inclui Favoritos, Recentes e Minha TV.

| Entrada ou ação | Responsabilidade |
| --- | --- |
| M3U / URL de lista | Interpretar canais e metadados de origem. |
| Xtream | Consultar o serviço informado pelo usuário e importar fontes. |
| XMLTV | Relacionar horários e programas aos identificadores de canais. |
| Catálogo público/oficial | Apresentar destinos e fontes existentes no projeto. |
| Busca | Resolver uma intenção de nome ou categoria. |
| Cinema Hub | Organizar conteúdo conhecido em caminhos de descoberta. |

As operações HTTP críticas possuem retry, timeout, backoff exponencial e jitter com Polly. Essas medidas toleram interrupções transitórias; elas não garantem a disponibilidade de um provedor externo.

O catálogo não autoriza o acesso à mídia. O usuário precisa de permissão para consumir cada fonte. Premium Hub e outros destinos oficiais podem encaminhar ao serviço correspondente, onde assinaturas, login e disponibilidade territorial são definidos.

**Atenção factual aos dados locais:** na implementação existente, a URL de stream Xtream pode incluir usuário e senha no caminho. Essas URLs são armazenadas no banco de canais. A página de privacidade deste livro detalha essa condição para que documentação e comportamento não se contradigam.

<!-- page:epg -->
<!-- eyebrow: 09 · A MEMÓRIA DO AO VIVO -->
# Guia, pausa e gravação trabalham juntos

XMLTV dá uma dimensão temporal à biblioteca. O guia pode apresentar até 14 dias quando a fonte oferece esses dados. Título, início e fim permitem navegar pela programação e escolher um programa para gravar.

**Timeshift.** Um ring buffer HLS local mantém uma janela do ao vivo. Quando a fonte permite, o usuário pode pausar, buscar, voltar 30 segundos e retornar ao LIVE. A janela depende de armazenamento e da capacidade da fonte; ela não equivale a uma gravação permanente.

**Gravação manual.** FFmpeg é usado para capturar/remuxar a mídia. A viabilidade depende dos codecs e da origem. Quando é possível copiar streams, o aplicativo evita recompressão desnecessária.

**Agendamento.** Programas do EPG podem virar gravações agendadas, incluindo ocorrências do mesmo título. Os horários são persistidos no SQLite e um serviço acompanha as tarefas em segundo plano.

<!-- diagram:live -->

A descoberta nova pode mostrar o programa atual do canal, mantendo o guia como fonte de verdade dos horários. Se não existir correspondência XMLTV, a interface precisa apresentar um estado honesto, sem inventar título ou progresso.

Falhas aqui têm impacto diferente das falhas de navegação. Um relógio incorreto, um computador desligado, falta de espaço ou uma fonte indisponível podem impedir uma gravação. A experiência completa exige testes no Windows cobrindo reinício, mudança de programação e armazenamento limitado.

O compromisso de evolução preserva tanto as janelas existentes quanto as responsabilidades: o hub ajuda a encontrar; o EPG ajuda a planejar; o serviço de gravação executa; o player apresenta.

<!-- page:p2p -->
<!-- eyebrow: 10 · STREAMING P2P -->
# Um fluxo sensível ao buffer e à rede

MonoTorrent está incorporado ao aplicativo para mídia em **magnet ou .torrent fornecidos pelo usuário**. O projeto não inclui indexadores de torrents. O módulo é destinado a conteúdo que o usuário tenha direito de acessar ou distribuir.

O fluxo começa com metadata e seleção de arquivo. O streaming pode iniciar antes de todo o download terminar; o buffer inicial e a priorização de pieces ajudam a preparar a região que o player precisa ler. Quando o usuário busca outro ponto, as prioridades mudam para atender ao seek.

| Capacidade existente | Efeito esperado |
| --- | --- |
| Buffer inicial configurável | Preparar mídia suficiente antes de iniciar. |
| Watchdog de stall | Detectar ausência de progresso. |
| DHT, tracker e Peer Exchange | Localizar peers e recuperar conexões. |
| Fast resume e cache de metadata | Reduzir trabalho repetido entre sessões. |
| Limite e limpeza de cache | Controlar uso de armazenamento. |
| Limite de upload | Ajustar contribuição à rede. |
| UPnP / NAT-PMP configurável | Solicitar mapeamento de portas quando habilitado. |

Saúde do swarm, número de peers, velocidade e progresso tornam o estado legível. Nenhuma combinação de tecnologia substitui um swarm com dados disponíveis: desempenho varia com rede, peers, arquivo, tracker, NAT e política local.

O P2P introduz uma privacidade própria. A participação em uma rede distribuída implica comunicação com outros participantes e descoberta de peers. O fato de diagnósticos e processamento de IA serem locais não torna as conexões P2P privadas por definição.

Na nova experiência, a porta de entrada existente continua acessível. A descoberta visual de canais não redefine nem oculta o comportamento do módulo P2P.

<!-- page:media_lab -->
<!-- eyebrow: 11 · PROGRAMAS EXTERNOS E IA -->
# O Media Lab amplia o desktop sem sobrecarregar o player

O Media Lab reúne ferramentas de inspeção, distribuição e processamento. Processos externos têm vida própria e dependem de executáveis, modelos e capacidades realmente disponíveis no build.

| Conjunto | Tecnologias e uso |
| --- | --- |
| Roteamento | MediaMTX: RTSP, HLS e WebRTC conforme a fonte. |
| Televisores | Descoberta SSDP/DLNA/UPnP e comandos AVTransport. |
| Inspeção | FFprobe para streams/codecs; TSDuck para transporte MPEG. |
| Legendas | libass no player; CCExtractor; whisper.cpp com Whisper Tiny multilíngue. |
| Processamento | FFmpeg, loudnorm, SoXR, zscale, BWDIF e VMAF quando disponíveis. |
| Imagem offline | Real-ESRGAN NCNN Vulkan 2× e RIFE NCNN Vulkan 2× FPS. |
| Ambiente | Hyperion.NG para integração configurável com LED/WLED. |
| Modelos e mídia | ONNX Runtime local e TagLibSharp para metadados. |

Whisper pode gerar SRT localmente; os processadores Vulkan também trabalham offline. ONNX Runtime permite validar e carregar modelos locais. A inferência local evita a necessidade de enviar mídia a um serviço de IA, mas ainda depende dos modelos, do executável e do hardware correto.

O aplicativo detecta recursos do FFmpeg antes de usá-los. NVENC, QSV, AMF, Media Foundation ou libx264 variam conforme o build e o computador. As rotas locais são o padrão do Media Router; exposição na LAN é uma escolha habilitada pelo usuário.

Este livro também foi produzido com um programa externo: **Python + ReportLab**, usando tipografia Unicode. A direção de arte do material de apresentação é separada das ferramentas de IA de mídia existentes no aplicativo. Gerar uma arte para o livro não significa acrescentar um novo serviço remoto ao cliente Windows.

<!-- page:orchestration -->
<!-- eyebrow: 12 · OPERAÇÃO DURÁVEL -->
# O aplicativo pode lembrar de uma execução interrompida

A v8.2 introduz workflows locais inspirados nos padrões públicos do ecossistema Conductor. A implementação é C#/.NET com SQLite/WAL; não incorpora o servidor Java do Conductor, Redis ou Elasticsearch.

<!-- diagram:workflow -->

Cada execução guarda um snapshot da definição. Mudar uma definição futura não altera a que já foi usada. O estado é salvo antes de o motor avançar para a próxima decisão. Tarefas que estavam InProgress quando o processo caiu voltam a Scheduled durante a recuperação.

Essa política exige **handlers idempotentes** quando há efeitos externos: a recuperação pode repetir uma tarefa. Persistir estado não promete execução exatamente uma vez.

O motor possui retry FIXED, LINEAR e EXPONENTIAL com jitter, limites de tentativas, timeout por tarefa, tarefas opcionais, dependências em DAG e concorrência por tipo. Pause/resume, terminate, restart, retry de falhas e rerun a partir de uma tarefa fazem parte da API.

O primeiro fluxo integrado é o boot **database → seed → channels → home**. Históricos e eventos ficam em orchestration.db na pasta local da aplicação. O conteúdo de diagnósticos deve continuar limitado, sem senhas, tokens ou URLs privadas nos payloads.

Retenção e limpeza precisam manter relações entre execuções, tarefas e eventos. A revisão desta etapa torna ForeignKeys=true explícito nas conexões e acrescenta cinco testes de reabertura, purga e rejeição de eventos órfãos. O bundle SQLite já passava esses cenários no ambiente usado: essa mudança reforça portabilidade e contrato, sem alegar um defeito de retenção reproduzido.

<!-- page:cinema_hub -->
<!-- eyebrow: 13 · A EVOLUÇÃO DESTA ETAPA -->
# Cinema Hub: o catálogo ganha uma entrada de descoberta

O Cinema Hub acrescenta uma forma de navegar pelo acervo conhecido, aproximando o catálogo de uma experiência editorial. Seu papel é apresentar canais e programas em seções com hierarquia e caminhos para continuar assistindo, sem recriar o sistema de reprodução. Busca e filtros permanecem na biblioteca existente; a janela nativa do hub concentra as seções de descoberta.

{{core_addition}}

As decisões de descoberta devem ser explicáveis. Favoritos e histórico pertencem ao usuário; o conteúdo disponível vem das fontes já cadastradas; a programação atual depende do XMLTV. Uma seção vazia precisa mostrar por que não há resultados e como importar ou escolher outra categoria.

Uma arquitetura local facilita esse compromisso. A interface pede um resultado organizado; o núcleo aplica regras sobre modelos conhecidos. Essa organização mantém a descoberta testável e evita que a apresentação dependa de um endpoint remoto adicional.

**Critérios da nova experiência.** Encontrar um canal deve exigir poucas decisões; o programa atual deve aparecer quando há EPG correspondente; a ação de assistir deve chegar ao coordenador existente; uma preferência deve persistir pelo mecanismo da biblioteca.

<!-- diagram:discovery -->

O visual representa uma intenção de produto e ajuda a revisar legibilidade. A validação funcional pertence ao código integrado e aos testes. O capítulo visual identifica explicitamente a origem das imagens para manter as duas evidências distintas.

<!-- page:integration -->
<!-- eyebrow: 14 · DO CATÁLOGO À JANELA -->
# Uma nova porta de entrada ligada ao aplicativo existente

{{native_addition}}

A integração preserva as janelas auxiliares e o player principal. O hub é uma camada de navegação: ele escolhe conteúdo e apresenta informações; o restante do aplicativo mantém responsabilidades específicas para tocar, agendar, gravar e configurar.

| Interação | Caminho esperado na integração |
| --- | --- |
| Assistir a um canal | Seleção do canal e reprodução pelo pipeline existente. |
| Alterar favorito | Persistência pela biblioteca local e atualização do estado visual. |
| Consultar programação | Uso do XMLTV/EPG já associado ao canal. |
| Abrir ferramentas | Acesso às janelas e comandos existentes. |
| Buscar outro conteúdo | Uso da busca e dos filtros da biblioteca existente. |

A superfície nativa de vídeo impõe limites reais ao layout WPF. Cards, informações e chamadas à ação precisam coexistir com o player sem sobrepor elementos que podem ser ocultados por airspace. Essa foi uma lição explícita da v8.1 e continua relevante.

Para catálogos grandes, consistência é tão importante quanto ornamentação. Filtros precisam produzir resultados determinísticos; cancelamento deve impedir ações obsoletas; o estado de favorito precisa refletir o banco. As decisões visuais devem acompanhar esses contratos.

**Estado desta entrega:** a confirmação de compilação é registrada na página de qualidade. Depois da validação no Linux, o CI Windows compilou a solução, executou os testes e abriu o Cinema Hub WPF em modo demonstrativo de captura. Esse modo não inicia o player. A revisão manual com teclado, mouse, controle remoto e mídia real continua como passo necessário para validar o uso completo.

<!-- page:privacy -->
<!-- eyebrow: 15 · PRIVACIDADE COM PRECISÃO -->
# Local não significa sem dados sensíveis

As preferências e o banco de canais ficam no computador do usuário. Isso reduz a necessidade de um serviço central, mas exige explicar o conteúdo armazenado. Algumas notas antigas descrevem a V1; o comportamento atual precisa ser lido no código.

| Dado ou componente | Comportamento observado na base |
| --- | --- |
| SQLite da biblioteca | Guarda canais, fontes e URLs completas na pasta APPDATA. |
| Xtream | URLs podem conter usuário/senha no caminho e são persistidas. |
| Saúde de fontes | Identidade SHA-256; ledger sem URLs completas, queries ou headers. |
| Logs Serilog | Locais, em LOCALAPPDATA/SanchesTV/Logs, com retenção de 14 arquivos. |
| OpenTelemetry | Métricas/traces locais; sem exporter remoto configurado. |
| Workflows | Histórico local em orchestration.db, dentro de LOCALAPPDATA. |
| Controle remoto | Iniciado por solicitação; token temporário enquanto o app está aberto. |

**URLs Xtream merecem cuidado.** A origem /live/usuario/senha/id.m3u8 incorpora credenciais à URL. Persistir a fonte no banco significa persistir essa informação. O código existente não sustenta a afirmação genérica de que credenciais Xtream nunca são salvas. Este livro também não afirma que o banco possui criptografia em repouso.

Processamento local de mídia e IA não exige upload para inferência. Ainda assim, reprodução HTTP, importação de listas, atualização, DLNA e P2P fazem conexões quando usados. O alcance dessas conexões depende da função acionada.

DiagnosticRedactor já é integrado ao AppTelemetry para redigir dados sensíveis reconhecidos nas mensagens de diagnóstico. Esta etapa inclui regressões para URLs e headers Authorization/Proxy-Authorization, inclusive Basic e Digest. A proteção das credenciais em repouso e controles adicionais de limpeza permanecem propostas futuras; a descrição acima não antecipa criptografia que ainda não foi implementada.

<!-- page:quality -->
<!-- eyebrow: 16 · ENGENHARIA VERIFICÁVEL -->
# A qualidade se prova por resultados e por limites

Testes xUnit validam regras de parsing, busca, catálogo, políticas, layout, agendamento e orquestração. A evolução acrescenta descoberta e regressões de persistência.

> {{test_result}}

> {{desktop_build_result}}

Os **{{test_count}} testes** correspondem à execução informada na integração final. Compilar confirma compatibilidade de código; a qualidade de reprodução depende também dos runtimes e do ambiente.

| Evidência | O que permite concluir |
| --- | --- |
| Testes do Core | Regras e regressões dos cenários automatizados executados. |
| Build do Desktop | Coerência de código WPF/.NET e dependências compiladas. |
| Inspeção estática | Rotas, persistência, integrações e limitações identificáveis no código. |
| Prévia HTML | Direção de arte e navegação demonstrativa do material visual. |
| Teste físico no Windows | Comportamento de janela, player, drivers, dispositivos e rede. |

**Dependências ainda sensíveis ao ambiente:** HDR, GPU, WASAPI exclusivo, DLNA, Ambilight, Vulkan, NVENC/QSV/AMF e desempenho P2P. Elas precisam de máquinas e redes representativas.

O registro desta página cobre o workflow de qualidade. O estado dos instaladores e da publicação v8.3.0 pode ser consultado na [página Releases]({{releases_url}}) e no workflow Windows Release. Nenhum desses resultados equivale a uma validação física completa de mídia e hardware.

<!-- page:distribution -->
<!-- eyebrow: 17 · ENTRE O CÓDIGO E O INSTALADOR -->
# Proveniência faz parte da experiência

O aplicativo depende de componentes nativos e executáveis externos. Um build confiável precisa explicar de onde eles vieram, quais versões foram usadas e como a integridade foi verificada.

O pipeline existente prepara artefatos como libmpv e sua stack, FFmpeg/FFprobe, MediaMTX, TSDuck, CCExtractor, whisper.cpp e o modelo Tiny multilíngue, Real-ESRGAN, RIFE e Hyperion.NG. A preparação fixa e verifica hashes SHA-256; o material de proveniência identifica versões, URLs e hashes do build.

**Velopack** é o caminho de instalador e atualizações completas/delta via GitHub Releases. **Inno Setup** permanece como instalador tradicional de compatibilidade e recuperação. A coexistência desses caminhos faz parte da base preservada.

<!-- diagram:distribution -->

O fluxo documentado inclui restore, build, testes, publish self-contained win-x64, runtimes, self-test, empacotamento, instalação limpa e self-test depois da instalação. Hashes precedem a publicação. Os relatórios do workflow Windows Release registram o resultado de cada etapa.

Licenças e avisos também são parte do pacote. Um desktop com MonoTorrent, bibliotecas de mídia, modelos e ferramentas externas precisa conservar a proveniência e as obrigações dos componentes distribuídos.

Esta edição foi preparada para acompanhar a v8.3.0 com instaladores, livro e imagens. O livro, sua fonte editável e a galeria ficam no repositório; os pacotes e seu estado de publicação são apresentados na [página GitHub Releases]({{releases_url}}). As evidências de interface e de engenharia permanecem identificadas separadamente.

<!-- page:visual_desktop -->
<!-- eyebrow: 18 · GALERIA VISUAL -->
# A sala principal

Uma prévia editorial organiza descoberta, navegação e presença de cinema em uma mesma composição. O destaque visual abre espaço para o conteúdo, enquanto os caminhos de busca e biblioteca continuam visíveis.

<!-- image:desktop -->

**Legenda:** prévia visual HTML/dados demonstrativos, não captura do WPF.

A imagem documenta a direção de arte preparada nesta etapa. Os textos, canais e programas exibidos podem ser demonstrativos e não comprovam disponibilidade real de uma fonte. A execução nativa deve ser revisada no Windows para confirmar layout, interação e integração com a superfície de vídeo.

<!-- page:visual_mobile -->
<!-- eyebrow: 19 · GALERIA VISUAL -->
# A experiência visual em espaço compacto

O material de apresentação também explora uma largura compacta. A hierarquia precisa manter títulos, ações e navegação compreensíveis quando há menos espaço, sem reduzir toda a experiência a uma miniatura do desktop.

<!-- image:mobile -->

**Legenda:** prévia visual HTML/dados demonstrativos, não captura do WPF.

Essa composição é uma exploração responsiva da galeria HTML. O projeto principal continua sendo um aplicativo WPF para Windows. A imagem não afirma a existência de uma versão Android, iOS ou de um aplicativo móvel publicado.

<!-- page:visual_gallery -->
<!-- eyebrow: 20 · GALERIA VISUAL -->
# Um acervo visual para revisar e compartilhar

A galeria reúne a apresentação do SanchesTV e os detalhes da nova direção de descoberta. Ela funciona como uma superfície de revisão que pode ser aberta no navegador e renderizada por ferramentas externas.

<!-- image:gallery -->

**Legenda:** prévia visual HTML/dados demonstrativos, não captura do WPF.

As imagens do livro preservam a diferença entre visualização e execução. A arte de identidade amplia a apresentação; o código e os testes registram comportamentos. Juntos, esses materiais permitem discutir decisões estéticas sem perder precisão sobre o estado da implementação.

<!-- page:roadmap -->
<!-- eyebrow: 21 · PRÓXIMOS PASSOS PLANEJADOS -->
# Crescer com prioridades que podem ser verificadas

As propostas abaixo são um roteiro de continuidade. Elas não são descritas como recursos concluídos nesta entrega e devem ser ajustadas depois da revisão de uso no Windows.

| Prioridade | Proposta | Evidência necessária |
| --- | --- | --- |
| P1 | Regressão da interface nativa, incluindo o Cinema Hub. | Capturas WPF e cenários com teclado, mouse e catálogo grande. |
| P1 | Validar dual-engine, cancelamento, failover e fontes com headers. | Sessões reais com libmpv e LibVLC, registros sem credenciais. |
| P1 | Definir proteção de credenciais Xtream em repouso. | Modelo de dados, migração e testes de recuperação. |
| P1 | Ampliar validação de upgrade e recuperação entre versões. | Instalações existentes, migração de dados e continuidade das listas. |
| P2 | Rever discovery com bibliotecas reais e dados incompletos. | Estados vazios, favoritos, EPG ausente e desempenho observado. |
| P2 | Validar agendamento, timeshift e espaço em disco limitado. | Reinícios, relógio, gravação e limpeza em situações controladas. |
| P2 | Montar matriz de hardware e rede. | HDR/GPU, WASAPI, Vulkan, DLNA, Ambilight e P2P. |
| P3 | Ampliar acessibilidade e documentação de uso. | Contraste, foco, leitores de tela e orientação consistente. |

Um roteiro assim transforma ambição em decisões concretas. Cada capacidade tem um comportamento a observar e uma evidência que pode ser compartilhada. Ao manter o mapa das funções existentes, o projeto cresce sem perder continuidade.

O próximo marco deve ser definido pelo conjunto de resultados alcançados, e não apenas pela quantidade de bibliotecas ou pelo número da versão. Qualidade de uso, recuperação de falhas, clareza dos dados locais e manutenção do código têm valor direto para quem abre o aplicativo todos os dias.

<!-- page:sources -->
<!-- eyebrow: 22 · FONTES E REPRODUÇÃO -->
# Este livro também é um artefato do projeto

**Fonte histórica.** Git do próprio repositório: commit de referência {{baseline_commit}}, tags v1.0.0 a v8.2.0 e histórico associado. O recorte de origem contém 227 commits e 15 tags. As contagens descrevem esse recorte, não o total depois de novas alterações.

**Fonte técnica.** README.md, arquivos .csproj, código de Core/Desktop/Tests e documentação de player, EPG, P2P, catálogo, testes, build e Conductor Engine. Notas antigas de privacidade foram confrontadas com AppDatabase e XtreamImporter para descrever o comportamento atual.

**Evidência de CI.** [Workflow Quality aprovado em Linux e Windows]({{ci_quality_url}}). O job Windows inclui build nativo, os 103 testes e execução WPF demonstrativa. Ele não substitui o pipeline de instaladores nem testes de playback e hardware.

**Artefatos desta edição.** A fonte editorial completa está em docs/book/SanchesTV-Evolucao.md. Os fatos de integração estão em docs/book/build-facts.json. O renderizador está em scripts/build-evolution-book.py. O PDF final é docs/SanchesTV-Evolucao.pdf. A galeria e as imagens têm origem em docs/showcase.

**Reprodução.** Em uma máquina com Python 3, instale ReportLab e Pillow a partir de docs/book/requirements.txt. Execute python scripts/build-evolution-book.py --strict-assets na raiz do repositório. O comando exige as imagens e aplica os fatos registrados no JSON. Fontes TrueType Unicode são incorporadas ao documento para manter os acentos legíveis.

O script gera um PDF A4 de {{page_count}} páginas com índice fixo, tabelas, diagramas e imagens. A diagramação foi desenhada para separar o acervo histórico, a integração nova e as propostas futuras. Se a fonte editorial for ampliada, o script verifica páginas excedentes para que um corte de texto não passe despercebido.

**Créditos de ferramentas.** Python, ReportLab e tipografia DejaVu; ferramentas de navegador para as prévias HTML; geração externa de arte para a identidade de apresentação. As tecnologias do aplicativo possuem sua própria proveniência e seus avisos de licença no repositório.

Edição de {{edition_date}}, preparada para a v8.3.0. O estado dos instaladores está disponível na [página GitHub Releases]({{releases_url}}). Este material registra evidências de evolução; a validação física das capacidades multimídia depende de testes específicos.

<!-- page:visual_native -->
<!-- eyebrow: 23 · EVIDÊNCIA NATIVA WPF -->
# O Cinema Hub renderizado por WPF

A captura desta página foi confirmada no CI Windows. A imagem registra a interface real do Cinema Hub, carregada com um conjunto demonstrativo e renderizada por WPF RenderTargetBitmap. Para evitar limites de recorte da sessão CI, a árvore visual é medida separadamente depois do carregamento da janela.

<!-- image:native -->

**Legenda:** captura nativa WPF no Windows com dados demonstrativos; não demonstra reprodução de mídia nem validação de hardware.

Os testes do núcleo, a compilação da solução e a captura nativa respondem a perguntas diferentes. Esta imagem confirma a aparência e o layout da interface no ambiente usado. HDR, áudio exclusivo, GPU, Vulkan, fontes reais, gravação e estabilidade de uma sessão longa ainda exigem testes próprios.

O PNG desta página tem 1160 × 850 pixels. Há também um panorama de 1160 × 3000 pixels: o conteúdo medido tinha 3396 pixels de altura, portanto esse panorama é truncado no limite configurado. As dimensões e esse estado são registrados em capture-info.json. A captura e os 103 testes foram confirmados no [CI Quality Windows/Linux]({{ci_quality_url}}).
