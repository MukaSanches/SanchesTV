# Build e instalador

Requisitos de desenvolvimento: .NET 10 SDK, Windows x64 e Inno Setup 6 para empacotamento manual. O Desktop usa `net10.0-windows`/WPF; o Core e os testes usam `net8.0`. Para executar os testes, instale também o runtime .NET 8.

O CI executa restore, build, testes, publish self-contained win-x64, self-test, compilação do Inno Setup, instalação silenciosa em diretório limpo, self-test da instalação, desinstalação, SHA-256 e upload do instalador.

```powershell
dotnet restore SanchesTV.sln
dotnet build SanchesTV.sln -c Release --no-restore
dotnet test tests/SanchesTV.Tests/SanchesTV.Tests.csproj -c Release --no-build
```

No Linux é possível validar o Core e compilar o Desktop com `-p:EnableWindowsTargeting=true`. Isso não executa WPF, os motores nativos nem os recursos de hardware. A validação de instalação e reprodução continua exigindo Windows.

O workflow `quality.yml` valida Core no Linux e compila a solução no Windows. O pipeline original `windows-release.yml` mantém os testes de runtimes/instaladores e só publica release após um push para `main`.

O livro e a prévia visual são reproduzíveis com as instruções em [docs/book/README.md](book/README.md) e [docs/showcase/README.md](showcase/README.md).
