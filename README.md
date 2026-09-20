# SanchesTV

Central de TV e IPTV para Windows.

## Status

V1.0 em desenvolvimento ativo.

## Objetivo

Aplicativo desktop Windows para reprodução de streams legítimos, importação de listas M3U/M3U8 e XMLTV, EPG, favoritos, histórico, health-check, failover, gravação, timeshift, PiP, multiview e controle remoto local.

## Regras do projeto

- Não incorporar retransmissões não autorizadas de canais pagos.
- Não embutir credenciais de terceiros.
- Priorizar funcionamento real, testes e build Release.
- Nenhum recurso anunciado deve ser apenas mock.

## Build local

```powershell
dotnet restore
dotnet build -c Release
dotnet test -c Release
```
