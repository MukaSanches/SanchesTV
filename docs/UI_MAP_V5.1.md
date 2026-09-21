# Mapa de Interface — SanchesTV 5.1

## Objetivo

Eliminar controles cortados, sobrepostos ou inacessíveis em resoluções comuns do Windows.

## Mapeamento dos controles

### Barra superior
- Logo SanchesTV.
- Estado global de reprodução.
- Minimizar.
- Maximizar/restaurar.
- Fechar.

### Navegação lateral
- Início.
- Ao vivo.
- Filmes.
- Brasil.
- Português.
- Premium oficial.
- Favoritos.
- Minha TV.
- Recentes.

### Fontes e dados
- M3U arquivo.
- M3U URL.
- Xtream.
- EPG.
- Atualizar catálogo.

Esses controles ficam na barra lateral e nunca mais ficam sobrepostos ao player.

### Biblioteca de canais
- Busca.
- Lista virtualizada com logo/miniatura.
- Favorito.
- Adicionar/remover de Minha TV.
- Testar fontes.
- Contagem de canais e fontes.

### Player — linha principal
- Canal anterior.
- Play/Pause.
- Próximo canal.
- Parar.
- Mudo.
- Volume.
- Tela cheia.

### Player — linha secundária responsiva
- Voltar 30 segundos.
- LIVE.
- Gravar.
- PiP.
- Multiview.
- Controle remoto.
- Diagnóstico.

A linha secundária usa WrapPanel e quebra automaticamente quando a largura diminui.

## Tela cheia

Três caminhos equivalentes:

1. Botão `Tela cheia F11` sempre visível na linha principal do player.
2. Tecla F11.
3. Duplo clique no vídeo.

Para sair:

- F11.
- Esc.
- Duplo clique no vídeo.
- Botão `Sair da tela cheia F11`.

No modo tela cheia são ocultados title bar, navegação, busca, lista de canais e comandos secundários. O player recebe toda a largura disponível.

## Regras responsivas

- Abaixo de 1220 px: navegação 184 px, biblioteca 318 px, busca 250 px.
- 1220–1459 px: navegação 202 px, biblioteca 352 px, busca 290 px.
- 1460–1749 px: navegação 216 px, biblioteca 390 px, busca 330 px.
- 1750 px ou mais: navegação 228 px, biblioteca 430 px, busca 380 px.

## Matriz mínima de validação

- 1366×768: todos os controles principais visíveis; linha secundária quebra quando necessário.
- 1536×864: layout padrão sem sobreposição.
- 1920×1080: biblioteca/player ampliados mantendo hierarquia.
- Tela cheia: sem menu lateral/lista/busca; saída por F11/Esc/duplo clique.

## Critério de regressão

Nenhuma função da V5 pode ser removida para corrigir a interface. Mudanças de layout devem preservar player, EPG, fontes, Premium, gravação, PiP, multiview, remoto, diagnóstico, favoritos e Minha TV.
