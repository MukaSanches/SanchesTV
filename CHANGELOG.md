# Histórico de lançamentos

## 8.4.0 — Streaming Intelligence

- M3U: atributos repetidos, títulos com vírgula, BOM, URLs inválidas e cabeçalhos por canal.
- XMLTV: processamento por programa, segurança contra DTD e entidades externas, horários válidos e títulos em português.
- Monitor de fontes: User-Agent/Referer/Origin na verificação HTTP, sem marcar RTSP como erro de HTTP.
- Controle remoto local: chave aleatória de 256 bits no fragmento do QR, POST autenticado e limites/timeout de rede.
- Ampliação dos testes de regressão; instaladores e release atualizados para 8.4.0.

## 8.3.0 — Cinema Hub

- Cinema Hub nativo WPF com descoberta local, favoritos, recentes e programação XMLTV atual/próxima.
- Recomendações por afinidade e diversidade usando o catálogo local e o status registrado das fontes.
- Arte original, navegação com foco visível e atalho `Ctrl+H`.
- Cancelamento de reprodução sem penalizar a saúde das fontes e persistência SQLite com foreign keys explícitas.
- Diagnósticos com redação de URLs e padrões comuns de credenciais, incluindo Authorization Basic/Bearer/Digest.
- Testes de descoberta, preservação de dados, EPG, diagnósticos e SQLite real: 103 casos aprovados em Linux e Windows.
- Livro ilustrado, prévia HTML desktop/mobile e capturas da janela WPF com catálogo demonstrativo.
- Instaladores Inno/Velopack, livro e pacote de imagens preparados para o GitHub Releases, acompanhados de hashes SHA-256.

Todas as listas IPTV e todos os ícones existentes foram preservados. `Catalog`, `Parsing` e `Import` permanecem inalterados em relação à 8.2. Nenhum arquivo anterior foi removido.

Os módulos anteriores — libmpv/LibVLC, P2P, timeshift, gravação, EPG, PiP, multiview, controle remoto, Media Lab, IA local, Playback Intelligence e workflows duráveis — continuam integrados.

A captura nativa usa dados fictícios e não reproduz streams. Recursos de GPU/HDR, áudio exclusivo, DLNA, Ambilight, Vulkan e swarm real continuam dependendo do ambiente do usuário.

O histórico detalhado das versões 1.0–8.2 está no [livro da evolução](docs/SanchesTV-Evolucao.pdf) e no Git.
