(() => {
  'use strict';
  const svg = name => `<svg aria-hidden="true"><use href="#i-${name}"/></svg>`;
  const escape = value => String(value).replace(/[&<>"']/g, ch => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[ch]));
  const views = {
    inicio: { label: 'Início', icon: 'home', description: 'Uma nova perspectiva para sua TV' },
    aovivo: { label: 'Ao vivo', icon: 'live', description: 'Explore a grade demonstrativa' },
    minhatv: { label: 'Minha TV', icon: 'heart', description: 'Sua seleção de canais favoritos' },
    medialab: { label: 'Media Lab', icon: 'lab', description: 'Conheça as ferramentas do aplicativo' },
    guia: { label: 'Guia', icon: 'guide', description: 'Descubra a programação demonstrativa' }
  };
  const channels = [
    { id:'oceano', channel:'Canal Oceano', title:'Um mundo sob as ondas', art:'ocean', poster:'OCEANO', overline:'UM PLANETA PARA DESCOBRIR', category:'Natureza', duration:'Documentário', description:'A imensidão azul vista por uma nova perspectiva. Uma coleção visual demonstrativa sobre a beleza e os detalhes de nosso planeta.', tech:['M3U / M3U8','EPG XMLTV','Cinema Engine'], programs:['Um mundo sob as ondas','Os segredos das ilhas','Marés do amanhã'] },
    { id:'horizonte', channel:'Horizonte Brasil', title:'Quando a cidade desperta', art:'city', poster:'horizonte', overline:'OLHARES DO BRASIL', category:'Brasil', duration:'Série documental', description:'Luz, arquitetura e paisagem em uma jornada imaginária por cidades brasileiras. Arte original criada para esta prévia do SanchesTV.', tech:['Catálogo em português','Busca tolerante a erros','Favoritos locais'], programs:['Quando a cidade desperta','Novos caminhos','Histórias da serra'] },
    { id:'verde', channel:'Verde', title:'O coração da floresta', art:'forest', poster:'verde.', overline:'A NATUREZA EM OUTRA ESCALA', category:'Natureza', duration:'Documentário', description:'Texturas, sombras e raios de luz dão vida a um universo verde. Uma experiência editorial demonstrativa que celebra a natureza brasileira.', tech:['libmpv','Tone mapping HDR','FFmpeg'], programs:['O coração da floresta','A vida entre as folhas','Raízes e rios'] },
    { id:'orbita', channel:'Órbita', title:'O próximo grande salto', art:'space', poster:'ÓRBITA', overline:'CIÊNCIA SEM FRONTEIRAS', category:'Ciência', duration:'Especial', description:'Uma perspectiva cinematográfica sobre exploração espacial, curiosidade e ciência. O conteúdo desta ficha é inteiramente demonstrativo.', tech:['libplacebo / D3D11','LibVLC fallback','Preferências de áudio'], programs:['O próximo grande salto','Além do céu','A ciência do futuro'] },
    { id:'cosmos', channel:'Cosmos', title:'Além do horizonte', art:'horizon', poster:'COSMOS', overline:'OLHE MAIS LONGE', category:'Ciência', duration:'Coleção editorial', description:'Um observatório na serra brasileira, sob um céu violeta e ciano. A imagem é arte original gerada para o SanchesTV; a programação é demonstrativa.', tech:['Cinema Hub','Coleções locais','Command Center'], programs:['Além do horizonte','Luzes da galáxia','Serra das estrelas'] },
    { id:'brasil', channel:'Brasil em Foco', title:'Uma viagem pela luz', art:'city', poster:'BRASIL', overline:'PAISAGENS & HISTÓRIAS', category:'Brasil', duration:'Série documental', description:'Uma coleção de paisagens e histórias imaginárias para explorar o catálogo, a busca e os favoritos desta prévia. Nenhum stream é reproduzido aqui.', tech:['Busca em português','Minha TV','Guia de programação'], programs:['Uma viagem pela luz','Cidades em movimento','Terras e caminhos'] }
  ];
  const labs = [
    { id:'cinema', icon:'play', label:'Cinema Engine', tag:'Reprodução', summary:'Uma imagem bem cuidada. Áudio e legendas do seu jeito.', description:'O projeto integra libmpv como motor preferencial e LibVLC como fallback. Seus perfis de imagem, tone mapping HDR, deinterlace, áudio e legendas dependem dos recursos da fonte e do hardware.', tech:['libmpv','LibVLC','FFmpeg','libplacebo','D3D11'] },
    { id:'legendas', icon:'spark', label:'Vozes que viram texto', tag:'Legendas', summary:'Transcrição local e ferramentas para closed captions.', description:'O Media Lab reúne whisper.cpp para transcrição offline, CCExtractor para closed captions e libass na reprodução. A qualidade da transcrição depende do áudio, modelo e idioma.', tech:['whisper.cpp','CCExtractor','libass','SRT PT-BR'] },
    { id:'router', icon:'live', label:'Sua mídia, mais longe', tag:'Media Router', summary:'Rotas locais e conexão com renderers compatíveis.', description:'MediaMTX oferece rotas RTSP, HLS e WebRTC conforme a compatibilidade da fonte. A descoberta DLNA usa SSDP e envio para renderers compatíveis. O acesso LAN é uma opção do aplicativo.', tech:['MediaMTX','RTSP','HLS','WebRTC','DLNA / UPnP'] },
    { id:'processamento', icon:'settings', label:'Cada detalhe importa', tag:'Processamento', summary:'Ferramentas de vídeo e áudio para processamento local.', description:'O aplicativo detecta os recursos disponíveis no FFmpeg. O Media Lab também integra Real-ESRGAN e RIFE NCNN Vulkan para processamento offline, conforme runtimes e hardware disponíveis.', tech:['FFmpeg','FFprobe','Real-ESRGAN','RIFE','ONNX Runtime'] },
    { id:'orquestracao', icon:'stack', label:'Tudo em seu lugar', tag:'Orquestração', summary:'Workflows duráveis, histórico e recuperação local.', description:'O motor C#/.NET de orquestração persiste estado em SQLite/WAL. Inclui dependências, retries, pausa, retomada e histórico por execução. O fluxo real de boot coordena banco, seed, canais e home.', tech:['C# / .NET','SQLite / WAL','DAG','Retry & recovery'] },
    { id:'diagnostico', icon:'wave', label:'Entenda sua experiência', tag:'Diagnóstico', summary:'Informações técnicas e observabilidade local.', description:'O projeto utiliza Serilog e OpenTelemetry para logs, métricas e traces locais. LibreHardwareMonitor oferece informações de hardware disponíveis. A prévia apresenta o recurso sem simular métricas de execução.', tech:['Serilog','OpenTelemetry','LibreHardwareMonitor','Playback Intelligence'] }
  ];
  const main = document.querySelector('#content');
  const search = document.querySelector('#global-search');
  const detail = document.querySelector('#detail-dialog');
  const command = document.querySelector('#command-dialog');
  const state = { view: 'inicio', query: '', filter: 'Todos', day: 0, favorites: new Set(['oceano','verde']) };
  try {
    const stored = JSON.parse(localStorage.getItem('sanchestv-showcase-favorites'));
    if (Array.isArray(stored)) state.favorites = new Set(stored.filter(id => channels.some(c => c.id === id)));
  } catch (_) { /* A prévia funciona mesmo quando o navegador bloqueia storage. */ }

  function toast(message) {
    const node = document.querySelector('#toast');
    node.textContent = message;
    node.classList.add('visible');
    clearTimeout(toast.timer);
    toast.timer = setTimeout(() => node.classList.remove('visible'), 2500);
  }

  function card(channel) {
    const selected = state.favorites.has(channel.id);
    return `<article class="channel-card"><button class="card-open" data-open="${channel.id}" aria-label="Conhecer ${channel.channel}: ${channel.title}"></button><div class="card-art ${channel.art}"><div class="art-title ${channel.art}"><small>${channel.overline}</small>${channel.poster}</div></div><button class="favorite-button ${selected?'selected':''}" data-favorite="${channel.id}" aria-label="${selected?'Remover':'Adicionar'} ${channel.channel} ${selected?'dos':'aos'} favoritos" aria-pressed="${selected}">${svg('heart')}</button><div class="card-body"><div class="card-meta"><span class="card-dot"></span><span>${channel.channel.toUpperCase()}</span><span>·</span><span>GRADE DEMO</span></div><h3>${channel.title}</h3><div class="card-bottom"><span>${channel.category} · ${channel.duration}</span>${svg('arrow')}</div></div></article>`;
  }

  function home() {
    return `<section class="welcome"><div><h1>Boa noite, Sanches.</h1><p>Seu próximo grande momento começa aqui.</p><small class="mobile-preview">Prévia visual • dados demonstrativos</small></div><span class="edition-tag">${svg('spark')} CINEMA OS EXPERIENCE</span></section><section class="hero" aria-labelledby="hero-title"><div class="hero-image" role="img" aria-label="Arte original de um observatório na serra brasileira sob a Via Láctea"></div><div class="hero-content"><div class="eyebrow">${svg('spark')} UMA NOVA PERSPECTIVA</div><h2 id="hero-title">Além do<br><span>horizonte.</span></h2><p class="hero-description">Um novo olhar para o universo.<br>Sua TV, com possibilidades infinitas.</p><div class="hero-actions"><button class="primary-button" data-nav="aovivo">Explorar canais ${svg('arrow')}</button><button class="ghost-button" data-about="cosmos">${svg('plus')} Sobre a coleção</button></div></div><div class="hero-caption"><span>✦</span><p>ARTE ORIGINAL<small>Serra brasileira · céu sem limites</small></p></div></section><section aria-labelledby="discovery-title"><div class="section-heading"><h2 id="discovery-title">Vale a descoberta <small>Um convite à curiosidade</small></h2><button class="text-link" data-nav="aovivo">Ver coleção ${svg('arrow')}</button></div><div class="channel-grid">${channels.slice(0,4).map(card).join('')}</div></section><section class="feature-strip" aria-label="Recursos do projeto"><div class="feature-item"><span class="feature-icon">${svg('play')}</span><div><strong>Uma experiência de cinema</strong><p>libmpv · LibVLC · FFmpeg</p></div></div><div class="feature-item"><span class="feature-icon">${svg('lab')}</span><div><strong>Seu laboratório de mídia</strong><p>Whisper local · MediaMTX · ONNX</p></div></div><div class="feature-item"><span class="feature-icon">${svg('stack')}</span><div><strong>Inteligência nos bastidores</strong><p>Orquestração durável · SQLite / WAL</p></div></div></section>`;
  }

  function pageHeading(eyebrow,title,description,count='') {
    return `<div class="page-heading"><div><div class="eyebrow">${svg(views[state.view].icon)} ${eyebrow}</div><h1>${title}</h1><p>${description}</p></div>${count?`<span class="page-count">${count}</span>`:''}</div>`;
  }

  function catalog() {
    const filtered = state.filter === 'Todos' ? channels : channels.filter(c => c.category === state.filter);
    return `${pageHeading('SEMPRE HÁ ALGO PARA DESCOBRIR','Sua janela para o mundo.','Explore canais e programação com dados demonstrativos. No aplicativo Windows, suas fontes M3U, Xtream e XMLTV compõem a experiência.',`${filtered.length} canais demo`)}<div class="filter-row" role="group" aria-label="Categorias">${['Todos','Natureza','Brasil','Ciência'].map(label=>`<button class="filter ${state.filter===label?'active':''}" data-filter="${label}" aria-pressed="${state.filter===label}">${label}</button>`).join('')}</div><div class="channel-grid catalog-grid">${filtered.map(card).join('')}</div>`;
  }

  function myTv() {
    const favorites = channels.filter(c => state.favorites.has(c.id));
    return `${pageHeading('VOCÊ FAZ A CURADORIA','Sua TV. Sua seleção.','Guarde o que desperta sua curiosidade. Os favoritos desta prévia ficam neste navegador.',`${favorites.length} favoritos`)}<section class="mytv-summary"><div><h2>Um universo com a sua assinatura.</h2><p>Toque no coração de um canal para criar sua coleção.<br>Os conteúdos abaixo são demonstrativos.</p></div>${svg('heart')}</section>${favorites.length?`<div class="channel-grid catalog-grid">${favorites.map(card).join('')}</div>`:`<div class="empty-state">${svg('heart')}<h2>Sua coleção começa com uma descoberta.</h2><p>Explore os canais demonstrativos e marque seus favoritos para encontrá-los aqui.</p><button class="primary-button" data-nav="aovivo">Explorar canais ${svg('arrow')}</button></div>`}`;
  }

  function mediaLab() {
    return `${pageHeading('CRIADO PARA QUEM QUER IR ALÉM','Bem-vindo ao Media Lab.','Conheça as tecnologias que já compõem o projeto SanchesTV. Esta prévia apresenta as ferramentas; a execução acontece no aplicativo Windows.')}<section class="lab-intro"><div class="lab-orb">${svg('lab')}</div><div><h2>Curiosidade encontra possibilidade.</h2><p>Do detalhe de uma legenda a novos caminhos para sua mídia: um espaço para explorar, compreender e transformar, com ferramentas locais.</p></div></section><div class="lab-grid">${labs.map(lab=>`<button class="lab-card" data-lab="${lab.id}"><div class="lab-card-top">${svg(lab.icon)}<span class="tech-tag">${lab.tag}</span></div><h2>${lab.label}</h2><p>${lab.summary}</p><span class="lab-footer">Conheça a tecnologia ${svg('arrow')}</span></button>`).join('')}</div>`;
  }

  function guide() {
    const days=['Hoje','Amanhã','Depois'];
    const offset=state.day;
    return `${pageHeading('SEU PRÓXIMO MOMENTO','A programação, em perspectiva.','Uma grade demonstrativa para explorar a experiência. O app usa XMLTV para guia, timeshift e gravações quando a fonte permite.')}<div class="guide-days" role="group" aria-label="Dia da programação">${days.map((label,index)=>`<button class="day-button ${state.day===index?'active':''}" data-day="${index}" aria-pressed="${state.day===index}">${label}<small>${index===0?'DEMO':`D+${index}`}</small></button>`).join('')}</div><div class="guide-table" aria-label="Grade demonstrativa"><div class="guide-row header"><span>CANAL</span><span>20:00 — 21:00</span><span>21:00 — 22:00</span><span>22:00 — 23:00</span></div>${channels.slice(0,5).map((c,index)=>`<div class="guide-row"><div class="guide-channel"><span class="guide-badge">${c.channel.replace('Canal ','').charAt(0)}</span>${c.channel}</div>${[0,1,2].map((time)=>`<button class="guide-program ${time===1?'current':''}" data-program="${c.id}" data-title="${escape(c.programs[(time+offset)%3])}" data-time="${20+time}:00"><span>${c.programs[(time+offset)%3]}</span><small>${20+time}:00 · ${c.duration} · DEMO</small></button>`).join('')}</div>`).join('')}</div><p class="guide-note">${svg('guide')}Escolha um programa para conhecer a ficha demonstrativa. Nenhuma gravação é agendada nesta prévia.</p>`;
  }

  function searchResults() {
    const query=normalize(state.query);
    const found=channels.filter(c=>normalize(`${c.channel} ${c.title} ${c.category} ${c.description}`).includes(query));
    return `${pageHeading('SIGA SUA CURIOSIDADE','Encontre seu próximo momento.','Busca nos canais e programas demonstrativos desta prévia.',`${found.length} resultados`)}<p class="search-summary">Resultados para “${escape(state.query)}”</p>${found.length?`<div class="channel-grid catalog-grid">${found.map(card).join('')}</div>`:`<div class="empty-state">${svg('search')}<h2>Ainda não encontramos esse momento.</h2><p>Tente “natureza”, “Brasil”, “ciência”, “oceano” ou “horizonte”.</p><button class="ghost-button" data-clear-search>Limpar busca</button></div>`}`;
  }
  function normalize(value) {return value.normalize('NFD').replace(/[\u0300-\u036f]/g,'').toLowerCase();}

  function render() {
    document.querySelectorAll('[data-view]').forEach(button => {
      const active = button.dataset.view===state.view;
      button.classList.toggle('active',active);
      if(active) button.setAttribute('aria-current','page'); else button.removeAttribute('aria-current');
    });
    document.querySelector('#view-label').textContent=state.query?'Busca':views[state.view].label;
    document.querySelector('#favorite-count').textContent=state.favorites.size;
    main.innerHTML=state.query?searchResults():({inicio:home,aovivo:catalog,minhatv:myTv,medialab:mediaLab,guia:guide}[state.view])();
    document.title=`${state.query?'Busca':views[state.view].label} · SanchesTV Cinema OS — Prévia visual`;
  }

  function navigate(view) {
    if(!views[view]) return;
    state.view=view;
    state.query='';
    search.value='';
    history.replaceState(null,'',`#${view}`);
    render();
    window.scrollTo({top:0,behavior:window.matchMedia('(prefers-reduced-motion: reduce)').matches?'instant':'smooth'});
  }

  function toggleFavorite(id) {
    const channel=channels.find(c=>c.id===id);
    if(!channel) return;
    if(state.favorites.has(id))state.favorites.delete(id);else state.favorites.add(id);
    try{localStorage.setItem('sanchestv-showcase-favorites',JSON.stringify([...state.favorites]));}catch(_){}
    render();
    toast(`${channel.channel} ${state.favorites.has(id)?'adicionado aos':'removido dos'} favoritos.`);
    if(detail.open && detail.dataset.channel===id)showChannel(id,detail.dataset.program||'',detail.dataset.time||'');
  }

  function showDialog(html) {
    document.querySelector('#detail-content').innerHTML=html;
    if(!detail.open)detail.showModal();
  }

  function showChannel(id,program='',time='') {
    const c=channels.find(channel=>channel.id===id);
    if(!c)return;
    detail.dataset.channel=id;
    detail.dataset.program=program;
    detail.dataset.time=time;
    showDialog(`<div class="detail-art card-art ${c.art}" role="img" aria-label="Arte original da coleção ${c.channel}"></div><div class="detail-body"><div class="eyebrow">${c.category.toUpperCase()} · COLEÇÃO DEMONSTRATIVA</div><h2 id="detail-title">${escape(program||c.title)}</h2><p>${c.description}</p>${program?`<p>${escape(time)} · ${c.channel} · Grade demonstrativa</p>`:''}<div class="detail-techs">${c.tech.map(t=>`<span>${t}</span>`).join('')}</div><div class="detail-actions"><button class="primary-button" data-favorite="${id}">${svg(state.favorites.has(id)?'check':'heart')}${state.favorites.has(id)?'Nos meus favoritos':'Adicionar aos favoritos'}</button><button class="ghost-button" data-dialog-nav="guia">${svg('guide')}Explorar guia</button></div><p class="detail-note">Arte original e dados demonstrativos. Esta prévia não reproduz streams. No aplicativo Windows, a reprodução usa as fontes configuradas pelo usuário.</p></div>`);
  }

  function showLab(id) {
    const lab=labs.find(l=>l.id===id);
    if(!lab)return;
    detail.dataset.channel='';
    showDialog(`<div class="detail-body"><div class="eyebrow">${svg(lab.icon)} MEDIA LAB · ${lab.tag.toUpperCase()}</div><h2 id="detail-title">${lab.label}</h2><p>${lab.description}</p><div class="detail-techs">${lab.tech.map(t=>`<span>${t}</span>`).join('')}</div><div class="detail-actions"><button class="primary-button" data-dialog-nav="medialab">Explorar Media Lab ${svg('arrow')}</button></div><p class="detail-note">Tecnologias documentadas no projeto. Nesta prévia, nenhum processo externo é iniciado e nenhuma métrica de execução é simulada.</p></div>`);
  }

  function showAbout() {
    detail.dataset.channel='';
    showDialog(`<div class="detail-art" role="img" aria-label="Observatório na serra brasileira sob a Via Láctea"></div><div class="detail-body"><div class="eyebrow">${svg('spark')} SANCHESTV · CINEMA OS</div><h2 id="detail-title">TV do seu jeito.</h2><p>O SanchesTV é uma central multimídia para Windows em português. Cinema Engine, fontes configuráveis, P2P, guia, gravações e Media Lab compõem um projeto que continua evoluindo.</p><p>Esta galeria dá forma a uma nova perspectiva visual com arte original. Início, busca, favoritos, guia e ferramentas podem ser explorados com dados demonstrativos.</p><div class="detail-techs"><span>C# / .NET</span><span>WPF</span><span>SQLite</span><span>libmpv / LibVLC</span><span>MonoTorrent</span></div><div class="detail-actions"><button class="primary-button" data-dialog-nav="medialab">Conhecer Media Lab ${svg('arrow')}</button></div><p class="detail-note">Prévia visual independente em HTML. As capturas desta galeria representam esta prévia, não o aplicativo WPF em execução. Arte gerada originalmente para o projeto.</p></div>`);
  }

  function renderCommands() {
    const query=normalize(document.querySelector('#command-search').value.trim());
    const found=Object.entries(views).filter(([key,v])=>normalize(`${v.label} ${v.description}`).includes(query));
    document.querySelector('#command-results').innerHTML=found.length?found.map(([key,view])=>`<button class="command-item" data-command="${key}">${svg(view.icon)}<span><strong>${view.label}</strong><small>${view.description}</small></span>${svg('chevron')}</button>`).join(''):`<div class="empty-state"><p>Nenhum destino encontrado. Tente “Guia”, “TV” ou “Lab”.</p></div>`;
  }
  function openCommands() {
    if(detail.open)detail.close();
    document.querySelector('#command-search').value='';
    renderCommands();
    if(!command.open)command.showModal();
    document.querySelector('#command-search').focus();
  }

  document.addEventListener('click',event=>{
    const target=event.target.closest('button,a');
    if(!target)return;
    if(target.dataset.view)navigate(target.dataset.view);
    if(target.dataset.nav)navigate(target.dataset.nav);
    if(target.dataset.favorite)toggleFavorite(target.dataset.favorite);
    if(target.dataset.open)showChannel(target.dataset.open);
    if(target.dataset.about)showChannel(target.dataset.about);
    if(target.dataset.lab)showLab(target.dataset.lab);
    if(target.dataset.filter){state.filter=target.dataset.filter;render();}
    if(target.dataset.day){state.day=Number(target.dataset.day);render();}
    if(target.dataset.program)showChannel(target.dataset.program,target.dataset.title,target.dataset.time);
    if(target.hasAttribute('data-clear-search')){state.query='';search.value='';render();search.focus();}
    if(target.dataset.dialogNav){detail.close();navigate(target.dataset.dialogNav);}
    if(target.dataset.command){command.close();navigate(target.dataset.command);}
    if(target.classList.contains('dialog-close'))detail.close();
    if(target.classList.contains('command-close'))command.close();
    if(target.classList.contains('command-trigger'))openCommands();
    if(target.id==='collection-info')showAbout();
    if(target.classList.contains('brand')){event.preventDefault();navigate('inicio');}
  });
  search.addEventListener('input',()=>{state.query=search.value.trim();render();});
  document.querySelector('#command-search').addEventListener('input',renderCommands);
  document.querySelector('#command-search').addEventListener('keydown',event=>{if(event.key==='Enter'){const first=document.querySelector('[data-command]');if(first){command.close();navigate(first.dataset.command);}}});
  document.addEventListener('keydown',event=>{if((event.ctrlKey||event.metaKey)&&event.key.toLowerCase()==='k'){event.preventDefault();openCommands();}});
  [detail,command].forEach(dialog=>dialog.addEventListener('click',event=>{const rect=dialog.getBoundingClientRect();if(event.target===dialog && (event.clientX<rect.left||event.clientX>rect.right||event.clientY<rect.top||event.clientY>rect.bottom))dialog.close();}));
  window.addEventListener('hashchange',()=>{const view=location.hash.slice(1);if(views[view]){state.view=view;state.query='';search.value='';render();}});
  const initial=location.hash.slice(1);
  if(views[initial])state.view=initial;
  render();
})();
