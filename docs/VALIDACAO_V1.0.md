# Validação V1.0

A V1 inclui um modo --self-test para verificação determinística.

Ele valida:
- inicialização do SQLite;
- persistência/importação de canais;
- parser M3U;
- parser XMLTV;
- carregamento das bibliotecas nativas do LibVLC;
- criação de MediaPlayer.

O GitHub Actions também realiza instalação silenciosa em diretório limpo, executa o self-test a partir da instalação e desinstala.

Disponibilidade de canais ao vivo depende da rede e da emissora e não é usada como critério determinístico de CI.
