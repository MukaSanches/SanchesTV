# Catálogo IPTV em Português — V2

## Objetivo

A V2 amplia o catálogo do SanchesTV sem transformar o aplicativo em um agregador indiscriminado de listas. O catálogo automático aceita apenas fontes selecionadas que se apresentam como gratuitas, abertas, públicas ou oficiais.

## Fontes ativadas

### joaoguidugli/FTA-IPTV-Brasil

URL usada pelo aplicativo:

`https://raw.githubusercontent.com/joaoguidugli/FTA-IPTV-Brasil/master/playlist.m3u8`

Política: todas as entradas ainda precisam possuir metadados compatíveis com Brasil/Português. O projeto declara foco em emissoras gratuitas de sinal aberto.

Snapshot da pesquisa V2: 101 entradas candidatas.

### LITUATUI/M3UPT

URL usada pelo aplicativo:

`https://raw.githubusercontent.com/LITUATUI/M3UPT/main/M3U/M3UPT.m3u`

Política: importar apenas `group-title="TV"` e canais ligados a países lusófonos por EPG ID/metadados/nome.

Snapshot da pesquisa V2: 55 entradas candidatas após filtro.

O SanchesTV não usa chaves de DRM, mecanismos de decriptação nem recomendações de bypass eventualmente existentes em documentação externa.

### Free-TV/IPTV

URL usada pelo aplicativo:

`https://raw.githubusercontent.com/Free-TV/IPTV/master/playlist.m3u8`

Política: importar somente grupos de países lusófonos, com prioridade atual para Brasil e Portugal.

Snapshot da pesquisa V2: 26 entradas candidatas após filtro.

## Resultado do snapshot

- Entradas candidatas somadas: 182.
- Identidades únicas estimadas antes da sincronização com o banco: aproximadamente 163.
- Duplicatas são úteis quando trazem outra URL para o mesmo canal: a V2 preserva essas URLs como fontes de failover em vez de exibir canais duplicados.

Os números são dinâmicos e variam a cada atualização dos repositórios.

## Fontes pesquisadas e não importadas em bloco

### iptv-org/iptv

É uma grande coleção pública e útil para pesquisa, mas os arquivos atuais de Brasil/Portugal também contêm entradas com nomes de canais tipicamente associados a assinatura/premium. Por isso a V2 não importa o catálogo inteiro automaticamente.

### iptv-com/iptv

O projeto declara foco em canais abertos, mas o snapshot pesquisado continha entradas como ESPN. Por precaução, não é uma fonte automática da V2.

## Atualização

O aplicativo tenta atualizar o catálogo automático a cada 12 horas. O usuário também pode usar **Atualizar PT/BR**.

Falha de uma fonte não apaga o catálogo existente e não impede o uso das demais fontes.
