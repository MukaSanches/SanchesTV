# Player

O mecanismo principal da V1 é LibVLC/LibVLCSharp.

Recursos:
- HLS/M3U8 e demais protocolos compatíveis com LibVLC;
- aceleração de hardware automática;
- volume e mute;
- pausa;
- seek quando a fonte informa capacidade de busca;
- failover entre URLs;
- PiP;
- multiview;
- gravação via saída do LibVLC.

Timeshift depende da capacidade real da fonte de manter uma janela seekable. O aplicativo não finge timeshift quando o stream não suporta seek.
