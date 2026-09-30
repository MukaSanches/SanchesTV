# Livro da evolução do SanchesTV

O livro registra a base histórica v8.2.0 e a evolução do Cinema Hub preparada na versão 8.3.0. São 24 páginas em português com história verificável no Git, arquitetura, preservação das funções, privacidade, resultados de validação e uma galeria visual. A página 24 apresenta uma captura nativa WPF confirmada em Windows.

- PDF: [SanchesTV-Evolucao.pdf](../SanchesTV-Evolucao.pdf).
- Fonte editorial completa: [SanchesTV-Evolucao.md](SanchesTV-Evolucao.md).
- Fatos finais, caminhos de imagens e evidências: [build-facts.json](build-facts.json).
- Renderizador: [build-evolution-book.py](../../scripts/build-evolution-book.py).

As três imagens da galeria visual são prévias HTML com dados demonstrativos. A quarta imagem, na página 24, é uma captura nativa WPF do Cinema Hub real, em Windows, também com dados demonstrativos. O livro diferencia cross-compilation no Linux, build/testes e renderização no CI Windows e teste real de mídia/hardware, registra URLs Xtream persistidas no SQLite e identifica propostas futuras.

A edição registra o [workflow Quality aprovado em Linux e Windows](https://github.com/MukaSanches/SanchesTV/actions/runs/36789985852), incluindo 103 testes no Windows e execução WPF em modo demonstrativo sem player. A captura nativa 1160 × 850 foi inspecionada e confirmada. O panorama separado é limitado a 3000 pixels de uma altura medida de 3396; esse truncamento está explícito no livro e em capture-info.json.

A edição foi preparada para acompanhar a v8.3.0. O estado dos instaladores e da publicação está na [página GitHub Releases](https://github.com/MukaSanches/SanchesTV/releases). A verificação de preservação confirmou zero arquivos removidos, Catalog/Parsing/Import sem modificações e os 44 glifos originais de MainWindow preservados.

## Reproduzir o PDF

Instale Python 3, as fontes TrueType DejaVu Sans e as dependências abaixo. No Debian/Ubuntu, as fontes são fornecidas pelo pacote `fonts-dejavu-core`. Em outros sistemas, use `--font-dir` apontando para uma pasta que contenha DejaVuSans.ttf, DejaVuSans-Bold.ttf, DejaVuSans-Oblique.ttf, DejaVuSans-BoldOblique.ttf e DejaVuSansMono.ttf.

```bash
python -m pip install -r docs/book/requirements.txt
python scripts/build-evolution-book.py --strict-assets
```

O comando pode ser executado de qualquer diretório se o caminho do script for ajustado. Ele exige a arte e as três imagens registradas no JSON, rejeita fatos ainda pendentes e verifica a contagem de páginas para detectar transbordamento da diagramação. O renderizador não usa rede nem modifica arquivos do aplicativo.

```bash
python scripts/build-evolution-book.py --strict-assets --font-dir /caminho/das/fontes
python scripts/build-evolution-book.py --output /tmp/SanchesTV-Evolucao.pdf
```

Executar sem `--strict-assets` permite revisar uma versão de trabalho com espaço reservado para imagens que ainda não existem. A edição entregue deve sempre ser gerada com `--strict-assets`.

## Atualizar uma edição

Edite o Markdown, atualize o JSON com as evidências reais e gere novamente. Os delimitadores `<!-- page:... -->` definem as páginas editoriais; `eyebrow`, `diagram` e `image` são instruções de diagramação. As variáveis `{{nome}}` vêm do JSON. Não altere uma contagem de testes sem nova execução confirmada.

A página `visual_native` é opcional. Habilite `include_native: true` no JSON somente depois de confirmar a existência e a origem Windows da imagem indicada por `screenshot_native`. Nesse modo, o PDF terá 24 páginas e a compilação estrita também exigirá essa imagem. A legenda identifica a diferença entre captura WPF e prévia HTML, sem alegar teste de reprodução ou hardware.

A base histórica permanece `c76469e`, com 227 commits e 15 tags; essas contagens descrevem o recorte anterior à evolução e não o total futuro. O script incorpora fontes Unicode e usa metadados determinísticos do ReportLab. Mesmos arquivos, fatos, imagens, fontes e versões de bibliotecas produzem o mesmo PDF.

Ferramentas usadas na edição: Python, ReportLab, Pillow, DejaVu Sans e renderização externa de navegador para as imagens HTML. A arte de apresentação não representa a introdução de um novo serviço remoto de IA no aplicativo.
