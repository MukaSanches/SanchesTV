# SanchesTV — P2P Streaming

O módulo P2P reproduz arquivos enquanto um torrent ainda está sendo transferido.

## Escopo

O recurso aceita magnet links fornecidos pelo usuário e arquivos .torrent locais. Ele é destinado a conteúdo próprio, livre, domínio público ou qualquer conteúdo que o usuário tenha direito de acessar ou distribuir.

O SanchesTV não integra busca automática em indexadores de torrents.

## Pipeline

magnet/.torrent -> MonoTorrent embutido -> prioridade de pieces -> HTTP local 127.0.0.1 -> libmpv -> LibVLC fallback

O StreamProvider do MonoTorrent fornece um stream seekable. Quando o player busca uma área ainda não baixada, os pieces necessários passam a ser priorizados.

## Segurança e privacidade

- Endpoint de reprodução somente em 127.0.0.1.
- Local Peer Discovery desativado.
- UPnP/NAT-PMP opcional e desligado por padrão.
- Limite de upload configurável.
- Cache em %LOCALAPPDATA%/SanchesTV/P2P.
- Limpeza de sessões inativas.
- Dados baixados são removidos ao encerrar a sessão por padrão.

## UX

- colar magnet;
- abrir ou arrastar .torrent;
- aguardar metadados;
- listar arquivos;
- priorizar vídeos;
- escolher arquivo;
- iniciar antes de 100%;
- acompanhar peers, taxas, progresso e cache;
- controlar cache, upload e pré-buffer.

## Dependência

MonoTorrent 3.9.0-alpha.unstable.rev0000, fixado no projeto.
Licença MIT.

## Continuidade

Quando a opção de manter cache está ativa, cada torrent usa uma pasta estável derivada do info-hash. Ao abrir novamente o mesmo magnet ou .torrent, o motor pode reutilizar os dados já presentes e o fast-resume. Por padrão esse comportamento fica desligado e os dados da sessão são removidos ao encerrar.
