# SanchesTV 2.0

SanchesTV é uma central de TV/IPTV para Windows, focada em conteúdo em português e em fontes gratuitas/públicas selecionadas, além das listas e credenciais fornecidas pelo próprio usuário.

## Novidades da versão 2.0

- Catálogo PT/BR sincronizado automaticamente a partir de fontes GitHub selecionadas.
- Atualização automática a cada 12 horas e atualização manual pelo botão **Atualizar PT/BR**.
- Filtros rápidos **Brasil** e **Portugal/Lusofonia**.
- Deduplicação por EPG ID e nome normalizado, preservando múltiplas URLs como fontes alternativas.
- Suporte aos cabeçalhos M3U `http-user-agent`, `http-referrer` e `http-origin`.
- Failover melhorado usando as fontes agregadas.
- Migração não destrutiva do banco da V1 para a V2.
- Contagem de canais e de fontes na interface.

### Fontes automáticas selecionadas

1. `joaoguidugli/FTA-IPTV-Brasil` — projeto focado em emissoras brasileiras FTA/gratuitas.
2. `LITUATUI/M3UPT` — projeto que declara streams públicos/oficiais e conteúdo em português; o SanchesTV importa apenas entradas de TV ligadas à lusofonia.
3. `Free-TV/IPTV` — projeto focado em canais gratuitos; o SanchesTV importa apenas grupos de países lusófonos.

A pesquisa da V2 encontrou 182 entradas candidatas nessas fontes no snapshot de desenvolvimento, correspondendo a aproximadamente 163 identidades de canal antes dos testes de disponibilidade. Esses números mudam conforme os repositórios são atualizados.

Listas amplas que misturam emissoras abertas com canais premium não são importadas automaticamente.

## Recursos mantidos

- Player baseado em LibVLC com aceleração de hardware quando disponível.
- M3U/M3U8 por arquivo ou URL.
- Xtream do próprio usuário.
- XMLTV/EPG por arquivo ou URL.
- Favoritos, Recentes e Minha TV.
- Pesquisa por canal, categoria, país, estado e região.
- Health-check HTTP/HTTPS.
- Picture-in-Picture.
- Multiview de até quatro canais.
- Gravação TS quando a fonte permitir.
- Pause/seek/timeshift quando a transmissão expuser seek.
- Controle remoto pelo navegador do celular via rede local e QR Code.
- Diagnóstico técnico.
- SQLite local.
- Instalador Windows x64 self-contained.

## Segurança e conteúdo

O SanchesTV não incorpora credenciais de terceiros e não implementa bypass de DRM, paywall ou autenticação. O catálogo automático é limitado a fontes que se apresentam como gratuitas, FTA, públicas ou oficiais e ainda passa por filtragem de país/idioma no aplicativo.

## Build

O GitHub Actions executa restore, build, testes, publish self-contained, self-test, criação do instalador, instalação limpa, self-test instalado, desinstalação, SHA-256 e publicação da release.
