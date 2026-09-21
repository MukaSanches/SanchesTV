# SanchesTV 7.0 — Media Platform

SanchesTV é uma central multimídia para Windows x64 focada em TV/IPTV em português, fontes abertas e oficiais, mídia local e streaming P2P fornecido pelo usuário.

A V7 preserva a base estável da linha 6.x e acrescenta módulos isolados para vídeo avançado, áudio, broadcast, gravação, transmissão para outros dispositivos, processamento offline e atualização incremental.

## Cinema Engine

O player principal usa:

- libmpv como motor preferencial e LibVLC como fallback;
- gpu-next/libplacebo com D3D11;
- FFmpeg, dav1d e libass;
- perfis Balanceado, Qualidade Máxima, Baixa Latência e Baixo Consumo;
- tone mapping HDR configurável;
- deinterlace automático;
- interpolação temporal do player;
- preferências de idioma de áudio e legenda;
- troca de faixa de áudio/legenda;
- delay de áudio e legenda;
- captura de frame;
- NAudio/WASAPI e modo exclusivo quando suportado;
- tratamentos Normalizar, Night e Dialogue;
- perfil automático de qualidade conforme temperatura/carga do hardware.

## P2P Streaming 2

MonoTorrent continua embutido no aplicativo.

- magnet e .torrent fornecidos pelo usuário;
- seleção de arquivo;
- streaming antes de 100% do download;
- seek com priorização de pieces;
- buffer inicial configurável;
- watchdog de stall;
- recuperação automática por tracker e DHT;
- Peer Exchange, DHT, fast resume e cache de metadata;
- cache com limite e limpeza;
- saúde do swarm, peers, velocidade e progresso;
- limite de upload;
- UPnP/NAT-PMP configurável.

O SanchesTV não inclui indexadores de torrents. O módulo é destinado a conteúdo que o usuário tenha direito de acessar ou distribuir.

## Timeshift e gravação

- timeshift HLS local com ring buffer;
- pause/seek em TV ao vivo quando a fonte permite;
- voltar 30 segundos e voltar ao LIVE;
- gravação manual por FFmpeg/remux;
- guia XMLTV de até 14 dias;
- gravação agendada por programa;
- agendamento das ocorrências do mesmo título;
- persistência das gravações agendadas no SQLite;
- execução automática do agendador em segundo plano.

## Media Lab 7

O Media Lab concentra as ferramentas avançadas sem acoplar processos pesados ao player.

### Media Router
MediaMTX é empacotado como processo separado e fornece, conforme compatibilidade da fonte:

- RTSP;
- HLS;
- WebRTC;
- acesso somente local por padrão;
- acesso LAN somente quando o usuário habilita.

### DLNA / UPnP
- descoberta SSDP de Media Renderers;
- envio do HLS atual para TVs/renderers compatíveis;
- Play/Stop via AVTransport.

### Broadcast
- FFprobe para diagnóstico técnico;
- TSDuck/tsanalyze para MPEG Transport Stream;
- identificação de codecs, streams e problemas de transporte.

### Legendas
- libass no player;
- CCExtractor para closed captions;
- whisper.cpp offline;
- modelo Whisper Tiny multilíngue incluído;
- geração local de SRT PT-BR.

### Processamento
O aplicativo detecta recursos realmente existentes no FFmpeg antes de usá-los.

- EBU R128/loudnorm;
- SoXR quando disponível;
- zimg/zscale;
- BWDIF;
- VMAF quando disponível;
- H.264 por NVENC, QSV, AMF, Media Foundation ou libx264 conforme o build/hardware;
- clips sem recompressão quando possível;
- Real-ESRGAN NCNN Vulkan para upscale offline 2×;
- RIFE NCNN Vulkan para interpolação offline 2× FPS.

### Ambilight
Hyperion.NG é empacotado e pode ser iniciado pelo Media Lab. O usuário configura o hardware LED/WLED no painel local do Hyperion.

### IA local
- ONNX Runtime para validar/carregar modelos ONNX locais;
- nenhuma mídia precisa ser enviada para um serviço de IA;
- Whisper e os processadores Vulkan trabalham localmente.

### Metadados
TagLibSharp permite inspecionar metadados de mídia local; FFprobe fornece a parte técnica de codecs/bitrate/streams.

## Busca e catálogo

- busca tolerante a erros de digitação;
- catálogo público configurado;
- M3U/M3U8;
- URL M3U;
- Xtream fornecido pelo usuário;
- XMLTV/EPG;
- Favoritos, Recentes e Minha TV;
- retry, timeout, backoff exponencial e jitter com Polly nas operações HTTP críticas;
- health check e failover das fontes.

## Diagnóstico e observabilidade

- Serilog com logs estruturados locais;
- retenção limitada de logs;
- OpenTelemetry local para métricas/traces internos;
- falhas de playback e recuperações são contabilizadas sem registrar tokens ou URLs completas;
- LibreHardwareMonitor para CPU/GPU/temperatura/carga;
- diagnóstico dos runtimes no próprio Media Lab.

## Atualização

A V7 possui duas formas de instalação:

1. **Velopack** — instalador principal, com suporte a pacote completo e atualizações delta via GitHub Releases.
2. **Inno Setup** — instalador tradicional de recuperação/compatibilidade.

O pipeline testa os dois formatos em instalação limpa antes de publicar a release.

## Runtimes verificados no build

O pipeline fixa e verifica por SHA-256 os artefatos externos empacotados:

- libmpv/stack multimídia;
- FFmpeg/FFprobe;
- MediaMTX;
- TSDuck;
- CCExtractor;
- whisper.cpp e modelo Tiny multilíngue;
- Real-ESRGAN NCNN Vulkan;
- RIFE NCNN Vulkan;
- Hyperion.NG.

Consulte o arquivo de proveniência incluído no instalador para as versões/URLs/hashes do build.

## Validação

O GitHub Actions executa:

restore → build → testes → publish self-contained win-x64 → preparação/verificação dos runtimes → self-test do aplicativo publicado → Inno Setup → instalação limpa Inno → self-test pós-instalação → Velopack pack → instalação limpa Velopack → self-test pós-instalação → hashes SHA-256 → GitHub Release.

Alguns recursos dependem do ambiente real: HDR, aceleração de GPU, WASAPI exclusivo, DLNA, Ambilight, NVENC/QSV/AMF, Vulkan e desempenho P2P dependem de hardware, drivers, rede e disponibilidade das fontes. O CI valida compilação, empacotamento e inicialização dos runtimes, mas não substitui testes físicos de todos esses caminhos.
