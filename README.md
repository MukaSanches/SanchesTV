# SanchesTV

SanchesTV 1.0 é uma central de TV/IPTV para Windows, focada em português brasileiro e em reprodução de fontes legítimas fornecidas pelo próprio usuário ou por emissoras públicas/oficiais.

## Recursos implementados

- Player nativo baseado em LibVLC com aceleração de hardware quando disponível.
- M3U/M3U8 por arquivo ou URL.
- Xtream do próprio usuário.
- XMLTV/EPG por arquivo ou URL.
- Catálogo inicial de transmissões públicas/oficiais.
- Favoritos, Recentes e Minha TV.
- Pesquisa por canal, categoria, estado e região.
- Failover entre múltiplas fontes.
- Health-check HTTP/HTTPS.
- Picture-in-Picture.
- Multiview de até quatro canais.
- Gravação TS quando a fonte permitir.
- Pause/seek/timeshift quando a transmissão expuser seek.
- Controle remoto pelo navegador do celular via rede local e QR Code.
- Diagnóstico técnico.
- SQLite local.
- Instalador Windows x64.

## Privacidade e conteúdo

O SanchesTV não incorpora credenciais de terceiros e não deve ser usado para burlar DRM, paywall ou autenticação. Listas e servidores adicionados pelo usuário são responsabilidade do próprio usuário.

## Build

Use dotnet restore, dotnet build e dotnet test a partir da solução SanchesTV.sln.

O GitHub Actions executa build, testes, publish self-contained, self-test, criação do instalador, instalação limpa, self-test instalado e desinstalação.
