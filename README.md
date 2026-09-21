# SanchesTV 4.0

SanchesTV 4.0 amplia o catálogo automático em português e mantém todos os recursos das versões anteriores, inclusive o Hub Premium oficial da V3.

## Catálogo automático pré-configurado

A V4 já vem com 6 fontes remotas cadastradas e sincroniza no primeiro início:

1. IPTV-org — Brasil: `https://iptv-org.github.io/iptv/countries/br.m3u`
2. IPTV-org — Português: `https://iptv-org.github.io/iptv/languages/por.m3u`
3. IPTV-org — Portugal: `https://iptv-org.github.io/iptv/countries/pt.m3u`
4. M3UPT Portugal/Lusofonia: arquivo oficial `M3U/M3UPT.m3u` do repositório `LITUATUI/M3UPT`
5. FTA IPTV Brasil
6. Free-TV Brasil/Portugal

O primeiro início da V4 força nova sincronização. Depois, o catálogo atualiza automaticamente a cada 12 horas.

## Agregação

- EPG ID e nome normalizado reduzem duplicatas.
- URLs diferentes do mesmo canal são preservadas como fontes alternativas.
- User-Agent, Referer e Origin presentes nas playlists são preservados.
- Se uma fonte remota falhar, o banco existente não é apagado.
- O botão `Atualizar 6 fontes PT/BR` permite atualização manual.

## Premium

O Hub Premium continua com acesso oficial a Claro tv+, Zapping, SKY+, Vivo TV e Globoplay. O SanchesTV não incorpora credenciais de terceiros, chaves DRM ou URLs clandestinas de canais pagos.

## Recursos mantidos

- Player LibVLC com aceleração de hardware.
- M3U/M3U8 local ou URL.
- Xtream do próprio usuário.
- XMLTV/EPG.
- Favoritos, Recentes e Minha TV.
- Filtros Brasil e Portugal/Lusofonia.
- Failover e health-check.
- PiP e Multiview.
- Gravação.
- Seek/timeshift quando suportado.
- Controle remoto pelo celular.
- Diagnóstico.
- SQLite.
- Instalador Windows x64 self-contained.
