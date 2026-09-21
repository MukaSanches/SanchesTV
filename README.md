# SanchesTV 5.0

SanchesTV 5.0 é uma central de TV/IPTV para Windows com foco em português, catálogo aberto agregado e acesso oficial a serviços premium do usuário.

## Interface Fluent Cinema

A V5 reestrutura completamente a interface sem trocar o motor de reprodução já validado. O visual segue princípios atuais do Windows 11: Mica, navegação lateral, title bar integrada, hierarquia por superfícies, cards, miniaturas e player central.

### Identidade visual

- Logo vetorial próprio SanchesTV: bloco em gradiente violeta/azul com monograma `S` e símbolo de play.
- Title bar personalizada.
- Mica/DWM no Windows 11, com fallback sólido quando indisponível.
- Home cinematográfica com hero.
- Miniaturas/logos vindos de `tvg-logo`.
- Placeholder visual quando a fonte não possui logo.
- Seções `Continuar assistindo`, `Brasil em destaque` e `Filmes`.

### Navegação principal

- Início
- Ao vivo
- Filmes
- Brasil
- Português
- Premium oficial
- Favoritos
- Minha TV
- Recentes

## Fontes automáticas

A V5 mantém as fontes da V4 e acrescenta duas novas:

1. IPTV-org Brasil — `https://iptv-org.github.io/iptv/countries/br.m3u`
2. IPTV-org Português — `https://iptv-org.github.io/iptv/languages/por.m3u`
3. IPTV-org Portugal — `https://iptv-org.github.io/iptv/countries/pt.m3u`
4. IPTV-org Movies — `https://iptv-org.github.io/iptv/categories/movies.m3u`
5. Brasil Full — `https://github.com/iptv-com/iptv/raw/refs/heads/main/lists/brazil.m3u`
6. M3UPT Portugal/Lusofonia
7. FTA IPTV Brasil
8. Free-TV Brasil/Portugal

O Brasil Full passa por filtro conservador no SanchesTV para remover nomes de canais evidentemente premium quando a origem não representa um serviço oficial autenticado.

## Recursos preservados

- LibVLC com aceleração por hardware.
- M3U/M3U8 por arquivo e URL.
- Xtream do próprio usuário.
- XMLTV/EPG.
- Failover.
- Favoritos, Recentes e Minha TV.
- Health-check.
- PiP.
- Multiview.
- Gravação.
- Seek/timeshift quando a fonte permitir.
- Controle remoto pelo celular.
- Diagnóstico técnico.
- Hub Premium oficial.
- SQLite local.

## Build

O pipeline Windows executa restore, build, testes, publish x64 self-contained, self-test, criação do instalador, instalação limpa, self-test pós-instalação, desinstalação, SHA-256 e publicação da release.
