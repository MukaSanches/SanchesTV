# SanchesTV 8.4 — Escopo e validação

## Modificações verificáveis

- M3U: duplicação de atributos sem exceções, vírgulas em títulos, BOM, URLs locais bloqueadas, opções de User-Agent isoladas.
- XMLTV: programa-a-programa em vez de XDocument completo; DTD rejeitada; programas com fim anterior ao início ignorados; português priorizado.
- Health check: requisições HTTP com User-Agent/Referer/Origin da fonte; protocolos não HTTP preservados; cancelamentos propagados.
- Remoto: segredo de 256 bits, QR com fragmento, POST com X-SanchesTV-Token, comparação em tempo constante e limites anti-cliente-lento.
- Release: projeto, telemetria, Inno Setup e Velopack sincronizados em 8.4.0.

## Verificação automatizada

Quality: dotnet test para Core no Linux, dotnet build/test e captura da interface no Windows.
Windows Release: publish win-x64 self-contained, self-test, instaladores Inno e Velopack, hashes e release na branch principal.
Consultar o status dos workflows antes de anunciar sucesso de compilação. A reprodução com GPU e a comunicação LAN exigem validação em equipamento real.

## Privacidade

O segredo do controle remoto não é enviado como parâmetro do GET da página, mas os comandos circulam via HTTP na LAN. Não redirecionar a porta para a internet ou redes não confiáveis.
