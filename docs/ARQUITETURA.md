# Arquitetura

A V1 usa uma separação entre núcleo e interface Windows.

- **SanchesTV.Core**: parsing, catálogo, modelos, health-check e contratos de playback.
- **SanchesTV.Desktop**: cliente Windows.
- **SanchesTV.Tests**: testes automatizados.

A arquitetura de reprodução usa `IPlaybackEngine` para permitir integração com mpv/libmpv como mecanismo principal e um fallback futuro compatível quando necessário.
