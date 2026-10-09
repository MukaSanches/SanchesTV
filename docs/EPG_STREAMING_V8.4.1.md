# EPG XMLTV em streaming (proposta 8.4.1)

- Importações de arquivos e HTTP deixam de usar File.ReadAllTextAsync/GetStringAsync, evitando uma segunda cópia integral do EPG em memória.
- O parser XMLTV aceita Stream e respeita o encoding declarado no XML.
- Aceita .xml.gz, .xmltv.gz e .gz em disco e respostas HTTP com URL .gz ou Content-Type application/gzip.
- A rede usa ResponseHeadersRead e processa o XMLTV fora da thread de UI.
- DTD e entidades externas permanecem desabilitadas; os limites de tamanho e o armazenamento final no SQLite continuam inalterados.
- Testes novos cobrem Latin-1, GZipStream e segurança de DTD.

**Pendente:** validação de integração Windows e de grandes XMLTV reais; esta branch não altera o release estável 8.4.0.
