# Fontes pré-carregadas — SanchesTV 4.0

## IPTV-org

- Brasil: https://iptv-org.github.io/iptv/countries/br.m3u
- Português: https://iptv-org.github.io/iptv/languages/por.m3u
- Portugal: https://iptv-org.github.io/iptv/countries/pt.m3u

Essas playlists públicas são geradas pelo projeto iptv-org. A lista de idioma Português é segmentada pelo próprio projeto e, no snapshot verificado durante a implementação, possuía 464 entradas.

## M3UPT

Página do projeto: https://m3upt.com/iptv

O aplicativo usa o arquivo oficial mantido no GitHub para download previsível:

https://raw.githubusercontent.com/LITUATUI/M3UPT/main/M3U/M3UPT.m3u

O projeto declara trabalhar com streams públicos e oficiais em português.

## Fontes preservadas

- joaoguidugli/FTA-IPTV-Brasil
- Free-TV/IPTV

## Deduplicação e failover

Quando o mesmo canal aparece em mais de uma fonte, o SanchesTV tenta consolidar a identidade e preserva URLs alternativas para failover em vez de simplesmente duplicar o canal.
