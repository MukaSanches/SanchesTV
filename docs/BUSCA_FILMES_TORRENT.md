# SanchesTV — Busca de filmes via BitTorrent

## O que foi acrescentado

A partir do menu **Buscar filmes • torrent**, uma janela independente permite pesquisar filmes por título. O projeto original continua intacto: IPTV/M3U, Cinema Hub, EPG, libmpv/LibVLC, P2P, cache, timeshift, gravações e Media Lab.

1. Digite o nome do filme e clique em **Pesquisar**.
2. Selecione um item e clique em **Preparar no player P2P**.
3. A janela P2P existente carrega os arquivos e seleciona um vídeo.
4. Clique em **Assistir agora** para iniciar antes da transferência completa.

Apenas o descritor pequeno `.torrent` é obtido antecipadamente quando necessário. O vídeo é recebido progressivamente por pieces e reproduzido via HTTP local. **Streaming não significa transferência zero nem reprodução sem peers.**

## Fontes de busca

- **Internet Archive**: já incluído. A busca verifica se o item possui descritor `.torrent`. Nem todos os itens oferecem peers.
- **Torznab**: indexadores opcionais do próprio usuário. Nenhuma fonte de pirataria vem preconfigurada.

**Configurar fontes** abre `%LOCALAPPDATA%\SanchesTV\TorrentSearch\sources.json`. Exemplo (substitua pela URL e chave de uma fonte autorizada):

    {
      "Sources": [
        {
          "Name": "Minha fonte Torznab",
          "Url": "https://indexador.exemplo/api",
          "ApiKey": "MINHA_CHAVE",
          "Enabled": true
        }
      ]
    }

As alterações são lidas na próxima pesquisa. Não compartilhe o arquivo: as chaves de API são armazenadas em texto puro no perfil do usuário.

## Limites e segurança

- Pesquisar não inicia download de vídeo nem altera o player corrente.
- Abertura de um resultado reutiliza o mesmo serviço P2P com seleção de arquivo, pré-buffer, recuperação, cache e limite de upload.
- Descritores `.torrent` são limitados a 2 MB e removidos do diretório temporário após carregar.
- Consultas limitadas a 2–100 caracteres, com timeouts e limites de resultados.
- Respostas Torznab são analisadas como XML sem DTDs externos.
- Conteúdo protegido por direitos autorais exige permissão; não se garante localizar **qualquer** filme.
- Problemas de fontes de busca não desativam IPTV ou outras funções existentes.

## Validação

Testes do parser: `tests/SanchesTV.Tests/TorznabSearchParserTests.cs`. CI `Quality` compila o WPF e executa testes. Reprodução real deve ser verificada no Windows com torrent autorizado e seeders disponíveis.
