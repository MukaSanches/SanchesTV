# Build e instalador

Requisitos de desenvolvimento: .NET 8 SDK, Windows x64 e Inno Setup 6 para empacotamento manual.

O CI executa restore, build, testes, publish self-contained win-x64, self-test, compilação do Inno Setup, instalação silenciosa em diretório limpo, self-test da instalação, desinstalação, SHA-256 e upload do instalador.
