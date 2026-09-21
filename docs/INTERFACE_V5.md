# Interface V5 — Fluent Cinema

## Direção

A V5 usa uma interface inspirada nas diretrizes atuais do Windows 11 sem reescrever o motor estável de mídia. A camada visual WPF usa recursos nativos de DWM para Mica e cantos arredondados quando disponíveis.

## Estrutura

- Title bar integrada com identidade SanchesTV.
- Navegação lateral fixa.
- Home com hero e descoberta de conteúdo.
- Listas horizontais de destaques com miniaturas.
- Biblioteca virtualizada para catálogos grandes.
- Player integrado à tela de navegação.
- Overlay de canal, EPG e origem da fonte.
- Controles compactos na base do player.

## Performance

A biblioteca principal permanece em lista virtualizada para evitar criar centenas de cards pesados de uma vez. A home limita cada trilho visual a um pequeno conjunto de itens, preservando fluidez mesmo quando o catálogo agregado ultrapassa centenas de canais.

## Miniaturas

O app usa `tvg-logo` como origem de imagem. Quando o logo não existe ou falha, o card mantém um placeholder com iniciais do canal.

## Windows 11

O helper `Windows11Backdrop` tenta habilitar:

- modo escuro imersivo;
- cantos arredondados;
- backdrop Mica;

Se a API DWM não estiver disponível, a janela usa fundo sólido escuro sem impedir a inicialização.
