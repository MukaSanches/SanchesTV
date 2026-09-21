# Validação SanchesTV 3.0

## Escopo

- Mantém todos os recursos da V2.
- Adiciona Hub Premium PT-BR.
- Provedores oficiais: Claro tv+, Zapping, SKY+, Vivo TV e Globoplay.
- Diretório pesquisável de canais premium.
- Acesso ao provedor oficial para autenticação e reprodução protegida.
- Sem inclusão de credenciais de terceiros, chaves DRM ou URLs clandestinas de canais pagos.

## Pipeline obrigatório

1. Restore.
2. Build Release.
3. Testes automatizados.
4. Publish Windows x64 self-contained.
5. Self-test do executável publicado.
6. Geração do instalador Inno Setup.
7. Instalação limpa.
8. Self-test após instalação.
9. Desinstalação.
10. SHA-256.
11. Publicação da release.

Este commit força uma execução final isolada sobre o estado completo da V3 para evitar que builds concorrentes anteriores substituam os assets da release.
