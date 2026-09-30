# Cinema OS — galeria visual interativa

Esta prévia visual do SanchesTV usa **dados demonstrativos** e duas artes originais geradas para o projeto. As imagens capturadas da galeria representam esta interface HTML; **não representam o aplicativo WPF em execução**.

Além da prévia HTML, `screenshots/cinema-hub-native.png` foi renderizada pelo WPF real no Windows com um catálogo fictício, sem reprodução. `cinema-hub-native-full.png` é um panorama limitado a 3000px; a altura total é 3396px e o recorte está registrado em `screenshots/capture-info.json`. Essas imagens foram geradas pelo [CI Quality validado](https://github.com/MukaSanches/SanchesTV/actions/runs/36789985852).

Abra `index.html` no navegador, ou sirva a pasta a partir da raiz do repositório:

```bash
python3 -m http.server 4173 --bind 127.0.0.1 --directory docs/showcase
```

Acesse `http://127.0.0.1:4173`. Não há bibliotecas externas, fontes remotas ou etapa de build. Navegação, filtros, busca sem distinção de acentos, favoritos locais, fichas, guia de três dias e Command Center (`Ctrl+K`) funcionam no navegador. `Esc` fecha os diálogos. A grade e os canais são fictícios; nenhum stream é reproduzido e nenhuma gravação é agendada.

## Telas

- `#inicio`: apresentação editorial e coleções demonstrativas.
- `#aovivo`: catálogo, categorias e fichas de canais.
- `#minhatv`: favoritos persistidos apenas neste navegador.
- `#medialab`: apresentação de tecnologias documentadas no projeto.
- `#guia`: grade demonstrativa navegável e fichas de programas.

O layout responde a monitores grandes e telas pequenas, incluindo 1440 × 900 e 390 × 844. A preferência do sistema por redução de movimento é respeitada; navegação por teclado e rótulos de acessibilidade foram incluídos.

## Arte original

`assets/cinema-horizon.png`: observatório na serra brasileira, céu violeta e ciano, arte panorâmica original gerada com a ferramenta de imagens. Uma cópia da mesma arte integra `src/SanchesTV.Desktop/Assets/cinema-horizon.png`.

`assets/documentary-worlds.png`: quatro paisagens originais geradas — oceano, cidade brasileira, floresta e lançamento espacial. Os cartões exibem cada quadrante por posicionamento CSS, sem imagens remotas.

As tecnologias mencionadas no Media Lab descrevem integrações existentes no repositório. Disponibilidade de HDR, Vulkan, renderers, modelos e ferramentas externas depende do ambiente real; a galeria não simula resultados de execução ou desempenho.

## Foto promocional conceitual

`screenshots/SanchesTV-Apresentacao.png` é um mockup promocional original gerado com a ferramenta de imagens a partir de `screenshots/desktop.png` e `screenshots/mobile.png`, após inspeção das duas capturas. A cena apresenta as interfaces demonstrativas em monitor e smartphone, com iluminação violeta e ciano e um cartão identificado como **“SanchesTV • prévia demonstrativa”**.

Os dispositivos e o estúdio fazem parte da composição gerada. A imagem apresenta a prévia HTML e não documenta o aplicativo WPF executando nesses dispositivos ou uma versão nativa para smartphone.
