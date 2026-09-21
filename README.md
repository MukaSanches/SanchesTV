# SanchesTV 3.0

SanchesTV é uma central de TV/IPTV para Windows com foco em conteúdo em português.

## V3 — IPTV aberto + Hub Premium

A versão 3 mantém todo o catálogo aberto/PT-BR da V2 e adiciona um Hub Premium para serviços oficiais de assinatura.

### Provedores integrados no Hub Premium

- Claro tv+
- Zapping
- SKY+
- Vivo Play
- Globoplay

O Hub Premium mantém um diretório pesquisável de canais e abre o serviço oficial correspondente para autenticação e reprodução protegida.

### Exemplos de canais presentes no diretório premium

- ESPN, ESPN 2, ESPN 3 e ESPN 4
- TNT, TNT Séries, TNT Novelas, Warner Channel, Space e Cinemax
- Discovery Channel, Discovery Home & Health, Discovery Kids, ID, Food Network e HGTV
- History, History 2, A&E, AXN, Sony Channel, Sony Movies e Paramount Network
- Cartoon Network, Cartoonito, Nickelodeon, Nick Jr., Tooncast, Adult Swim e TV Rá Tim Bum
- GloboNews, Multishow, GNT, sportv, Canal Brasil, Canal OFF e Gloob
- Telecine e Premiere por provedores que os comercializam como adicionais/assinaturas compatíveis

## Política de conteúdo premium

O SanchesTV não distribui URLs clandestinas de canais pagos, credenciais de terceiros, chaves DRM ou técnicas de bypass. O assinante usa sua própria conta no provedor oficial.

## Catálogo aberto mantido da V2

- Sincronização PT/BR automática a cada 12 horas.
- Fontes selecionadas: FTA-IPTV-Brasil, M3UPT e Free-TV/IPTV.
- Deduplicação e múltiplas fontes por canal.
- User-Agent, Referer e Origin de M3U preservados.
- Filtros Brasil e Portugal/Lusofonia.

## Recursos gerais

- LibVLC com aceleração de hardware.
- M3U/M3U8 arquivo/URL.
- Xtream do próprio usuário.
- XMLTV/EPG.
- Favoritos, Recentes e Minha TV.
- Pesquisa.
- Failover.
- Health-check.
- PiP.
- Multiview.
- Gravação.
- Seek/timeshift quando suportado.
- Controle remoto via celular.
- Diagnóstico técnico.
- SQLite.
- Instalador Windows x64 self-contained.

## Build

O GitHub Actions executa restore, build, testes, publish, self-test, criação do instalador, instalação limpa, self-test instalado, desinstalação, SHA-256 e publicação da release.
