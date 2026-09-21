# SanchesTV 6.1

SanchesTV é uma central multimídia para Windows x64 focada em TV/IPTV em português, fontes abertas, serviços oficiais do usuário e reprodução local/P2P.

## Player

A arquitetura principal usa:

- libmpv como motor preferencial;
- gpu-next/libplacebo para renderização por GPU;
- FFmpeg, dav1d e libass no stack de mídia;
- NAudio/WASAPI para integração avançada de áudio no Windows;
- LibVLC como fallback automático;
- FFmpeg para gravação/remux quando possível sem recompressão.

## Streaming P2P

A V6.1 adiciona um motor BitTorrent embutido com MonoTorrent.

Fluxo:

magnet ou .torrent do usuário -> MonoTorrent -> stream HTTP local -> libmpv -> LibVLC fallback

Recursos:

- magnet links fornecidos pelo usuário;
- arquivos .torrent locais e drag-and-drop;
- reprodução antes de 100% do download;
- seek inteligente: o motor prioriza os pieces da nova posição;
- seleção do arquivo dentro de torrents com múltiplos itens;
- identificação de vídeos e legendas;
- peers, velocidade, progresso, recebido/enviado e uso de cache;
- pré-buffer configurável;
- limite de upload configurável;
- cache máximo configurável;
- limpeza automática de sessões antigas;
- opção de manter o cache e reutilizá-lo ao abrir novamente o mesmo torrent;
- DHT e fast-resume;
- UPnP/NAT-PMP opcional e desligado por padrão;
- Local Peer Discovery desligado;
- servidor de reprodução vinculado somente a 127.0.0.1.

O SanchesTV não inclui busca em indexadores de torrents. O recurso é destinado a conteúdo que o usuário tenha direito de acessar ou distribuir.

## IPTV e biblioteca

São preservados M3U/M3U8, URL M3U, Xtream do usuário, XMLTV/EPG, catálogo público configurado, favoritos, Recentes, Minha TV, health-check, failover, PiP, Multiview, gravação, controle remoto e Hub Premium oficial.

## Interface

A interface Fluent Cinema permanece responsiva, com modos adaptativos para janelas pequenas e grandes, menu compacto, tela cheia por F11/duplo clique e respeito à área útil do Windows.

## Build e validação

O pipeline executa restore, build, testes, publish Windows x64 self-contained, validação dos runtimes de mídia, self-test, Inno Setup, instalação limpa, self-test pós-instalação, desinstalação, SHA-256 e publicação da release.

Consulte também `docs/P2P_STREAMING.md`.
