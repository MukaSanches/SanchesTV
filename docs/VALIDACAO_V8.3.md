# Validação da evolução 8.3 — Cinema Hub

Base: `c76469e500b02038a366ca2bfddf825c698c43a1` (8.2.0). Data da edição: 30/09/2026.

## Código validado

Ambiente Linux, SDK .NET 10.0.401 e runtime .NET 8.0.31:

```bash
dotnet restore SanchesTV.sln -p:EnableWindowsTargeting=true
dotnet build SanchesTV.sln -c Release -p:EnableWindowsTargeting=true --no-restore --no-incremental
dotnet test tests/SanchesTV.Tests/SanchesTV.Tests.csproj -c Release --no-build --no-restore --logger 'trx;LogFileName=validation.trx'
```

- Compilação da solução completa: **0 erros, 0 avisos**.
- Suite xUnit: **103 casos aprovados, 0 falhas, 0 ignorados**.
- Testes novos verificam descoberta determinística, diversidade, afinidade, status de fontes, deduplicação, favoritos offline, ordem dos recentes, EPG em UTC e limites dos intervalos.
- Banco real: reabertura, recuperação disponível, ordem de eventos, retenção e cascata, rejeição de referências órfãs, consulta EPG agregada, preservação de favoritos/Minha TV/histórico/preferências.
- Diagnóstico: URLs Xtream com credenciais no caminho/userinfo/query, magnet, Bearer, atribuições comuns, mensagens multilinha e redação antes de truncar.
- Cancelamento: um harness temporário com o coordinator real e motores controlados reproduziu a penalização no baseline e confirmou a correção, timeout legítimo e reprodução posterior. O harness não é parte da suíte versionada.

Neste bundle SQLite, os testes de cascata também passam no baseline; `ForeignKeys=true` torna o requisito explícito por conexão para evitar depender de defaults do runtime.

## Preservação e execução

Nenhum arquivo ou módulo preexistente foi removido. A migração usa `CREATE TABLE IF NOT EXISTS` e a descoberta é uma leitura dos dados existentes. O player dual engine, P2P, timeshift, gravação, EPG, Media Lab, controle remoto, catálogo, Command Center e workflows continuam integrados.

Comparação com a base confirma que `Catalog`, `Parsing` e `Import` não foram alterados: as listas IPTV e seu processo de importação permanecem intactos. Os 44 glifos de ícones originais em `MainWindow.xaml` continuam presentes, e nenhum arquivo anterior foi excluído.

A compilação cross-target **não executa WPF**. O [workflow Quality](https://github.com/MukaSanches/SanchesTV/actions/runs/36789985852) também compilou a solução no Windows com zero avisos/erros e passou os 103 testes em Linux e Windows. A captura demonstrativa executou a janela WPF no runner Windows, sem iniciar player, banco ou rede.

Não foram validados reprodução de streams reais, instalação, GPU/HDR, WASAPI, DLNA, Ambilight, Vulkan ou swarm real. O workflow original de Windows mantém os self-tests e instalação limpa; seus resultados devem ser consultados no PR.

## Galeria e livro

`desktop.png`, `mobile.png` e `gallery.png` são capturas Chromium da **prévia HTML com dados demonstrativos**, identificada na interface e no PDF. `cinema-hub-native.png` é uma captura RenderTargetBitmap do **WPF real no Windows**, com catálogo fictício e sem reprodução. O panorama nativo tem limite de 3000px; `capture-info.json` registra o tamanho de conteúdo de 3396px e a truncagem. O PNG de apresentação é um mockup promocional gerado, com aviso demonstrativo.

As artes de observatório, documentários e apresentação são originais, geradas com image_gen e incorporadas localmente ao repositório.

O livro usa ReportLab/Pillow e possui fonte Markdown e script em `scripts/build-evolution-book.py`. Não existe dependência de geração de imagem, navegador ou Python para usar o aplicativo Windows.

O arquivo `docs/book/build-facts.json` registra os resultados usados na renderização do livro. Consulte `docs/showcase/README.md` para abrir a prévia e `docs/book/README.md` para gerar o PDF.
