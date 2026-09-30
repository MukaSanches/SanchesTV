# Privacidade

- Banco SQLite e preferências ficam localmente em %APPDATA%\SanchesTV.
- O servidor de controle remoto é iniciado apenas quando solicitado.
- O controle remoto usa token temporário e existe somente enquanto o aplicativo está aberto.
- Há diagnóstico e telemetria **locais**: Serilog grava logs em `%LOCALAPPDATA%\SanchesTV\Logs` com retenção de até 14 arquivos diários. OpenTelemetry coleta métricas/traces internos; o código atual não configura exportador remoto. O aplicativo não inclui publicidade.
- URLs importadas são persistidas no SQLite. Em fontes Xtream, essas URLs podem conter usuário e senha no caminho. O banco não é criptografado pelo aplicativo; considere banco, backups e listas como dados sensíveis.
- A V8.3 redige URLs, magnet links e padrões comuns de credenciais nas mensagens de erro e deixa de registrar objetos `Exception` integrais no diagnóstico. Isso não criptografa o banco nem garante que qualquer objeto arbitrário passado aos logs seja livre de dados sensíveis.
- Cinema Hub e recomendações usam apenas catálogo, favoritos, recentes e EPG locais. Nenhuma mídia ou perfil precisa ser enviado para gerar essas recomendações.
- Whisper, ONNX e ferramentas de processamento offline operam localmente. Sincronização de catálogo, fontes de vídeo, atualização e protocolos P2P fazem conexões de rede quando usados.
