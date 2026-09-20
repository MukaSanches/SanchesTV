# Testes

Testes automatizados cobrem parser M3U, normalização de texto e parser XMLTV.

O modo --self-test valida inicialização do SQLite, persistência/importação de canais, parsing M3U/XMLTV, carregamento das bibliotecas nativas do LibVLC e criação de um MediaPlayer.

O pipeline Windows rejeita a entrega se falharem build, testes, publish, self-test, criação do instalador, instalação limpa, self-test instalado ou desinstalação.
