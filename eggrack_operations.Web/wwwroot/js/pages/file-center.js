(() => {
  const root=document.querySelector('[data-file-center]'); if(!root)return;
  const {escapeHtml:esc,notify}=window.Eggrack.ui;
  const source=document.getElementById('fileCenterData');
  const files=JSON.parse(source?.textContent||'[]');
  const groups=['全部客户',...new Set(files.map(file=>file.customer))];
  const preview=window.Eggrack.filePreview;
  const {icons,contentUrl,icon:fileIcon,canPreview,markup:previewMarkup,meta:metaMarkup}=preview;
  let group='全部客户',selectedFile=null,lastFocus=null; const q=s=>root.querySelector(s);
  const large=document.querySelector('[data-large-preview]');

  const download=file=>{window.location.href=contentUrl(file)+'?download=true'};

  function categories(){
    const box=q('[data-categories]');box.innerHTML='';
    groups.forEach((name,index)=>{
      const button=document.createElement('button');button.type='button';button.className='fc-category '+(name===group?'active':'');
      const icon=index===0?'bi-people':'bi-person';
      button.innerHTML='<span class="fc-category-name"><i class="bi '+icon+'"></i>'+esc(name)+'</span><small>'+(index?files.filter(x=>x.customer===name).length:files.length)+'</small>';
      button.onclick=()=>{group=name;categories();render()};box.appendChild(button);
    });
  }

  function render(){
    const keyword=q('[data-search]').value.toLowerCase(),type=q('[data-type]').value,range=q('[data-range]').value;
    const cutoffDate=new Date(); cutoffDate.setDate(cutoffDate.getDate()-(range==='week'?7:range==='month'?30:0));
    const cutoff=range?cutoffDate.toISOString().slice(0,16).replace('T',' '):'';
    const items=files.filter(x=>(group==='全部客户'||x.customer===group)&&(!type||x.type===type)&&(!cutoff||x.uploadedDate>=cutoff)&&(!keyword||(x.name+x.customer+x.sourceNumber).toLowerCase().includes(keyword)));
    const body=q('[data-rows]');body.innerHTML='';
    items.forEach(x=>{
      const row=document.createElement('tr');const typeKey=String(x.type||'').toUpperCase();const fileClass=icons[typeKey]?typeKey.toLowerCase():'other';
      row.innerHTML='<td><div class="fc-file"><span class="fc-file-icon '+fileClass+'"><i class="bi '+(icons[typeKey]||'bi-file-earmark')+'"></i></span><span class="fc-file-copy"><strong title="'+esc(x.name)+'">'+esc(x.name)+'</strong><small>'+esc(x.type)+' · '+esc(x.size)+'</small></span></div></td><td><strong class="small">'+esc(x.sourceNumber)+'</strong><small>'+esc(x.sourceLabel)+' · '+esc(x.visibility)+'</small></td><td><span>'+esc(x.uploadedDate)+'</span><small>'+esc(x.uploadedBy)+'</small></td><td class="text-end text-secondary"><i class="bi bi-chevron-right"></i></td>';
      row.tabIndex=0;row.setAttribute('role','button');row.setAttribute('aria-label','查看 '+x.name);
      row.onclick=()=>detail(x,row);row.ondblclick=()=>{detail(x,row);openLargePreview()};
      row.onkeydown=event=>{if(event.key==='Enter'||event.key===' '){event.preventDefault();detail(x,row)}};body.appendChild(row);
    });
    q('[data-empty]').style.display=items.length?'none':'block';
    q('[data-result-count]').textContent='共 '+items.length+' 个文件';
    if(selectedFile&&!items.some(item=>item.fileId===selectedFile.fileId&&item.sourceKind===selectedFile.sourceKind))closeInspector();
  }

  function detail(x,row){
    selectedFile=x;root.classList.add('has-inspector');q('[data-detail-panel]').setAttribute('aria-hidden','false');
    root.querySelectorAll('tbody tr').forEach(item=>{const active=item===row;item.classList.toggle('active',active);item.setAttribute('aria-selected',active?'true':'false')});
    q('[data-detail-label]').textContent='文件详情';q('[data-detail-name]').textContent=x.name;q('[data-detail-sub]').textContent=x.type+' · '+x.size;
    q('[data-preview]').innerHTML=previewMarkup(x);
    q('[data-primary]').onclick=()=>download(x);
    q('[data-open-source]').disabled=false;q('[data-open-source]').onclick=()=>{window.location.href=x.businessUrl};
    q('[data-secondary]').disabled=false;q('[data-primary]').disabled=false;
    q('[data-secondary]').onclick=async()=>{try{await navigator.clipboard.writeText(location.origin+contentUrl(x));notify('链接已复制。','success')}catch{notify('复制链接失败。')}};
    q('[data-detail-meta]').innerHTML=metaMarkup(x,true);
    root.querySelectorAll('[data-expand-preview]').forEach(button=>{button.disabled=false;button.onclick=openLargePreview});
  }

  function closeInspector(){
    root.classList.remove('has-inspector');q('[data-detail-panel]').setAttribute('aria-hidden','true');
    root.querySelectorAll('tbody tr').forEach(item=>{item.classList.remove('active');item.setAttribute('aria-selected','false')});
    selectedFile=null;q('[data-preview]').innerHTML='<div><i class="bi bi-file-earmark"></i>暂无预览</div>';
  }

  function openLargePreview(){
    if(!selectedFile)return;lastFocus=document.activeElement;
    large.hidden=false;document.body.classList.add('fc-large-preview-open');
    large.querySelector('[data-large-name]').textContent=selectedFile.name;large.querySelector('[data-large-sub]').textContent=selectedFile.type+' · '+selectedFile.size;
    large.querySelector('[data-large-icon]').className='fc-file-icon '+String(selectedFile.type||'').toLowerCase();large.querySelector('[data-large-icon]').innerHTML='<i class="bi '+fileIcon(selectedFile)+'"></i>';
    large.querySelector('[data-large-stage]').innerHTML=previewMarkup(selectedFile,true);large.querySelector('[data-large-meta]').innerHTML=metaMarkup(selectedFile,true);
    const unsupported=large.querySelector('[data-unsupported-download]');if(unsupported)unsupported.onclick=()=>download(selectedFile);
    large.querySelector('[data-large-new-window]').disabled=!canPreview(selectedFile);large.querySelector('[data-large-new-window]').onclick=()=>window.open(contentUrl(selectedFile),'_blank','noopener');
    large.querySelector('[data-large-download]').onclick=()=>download(selectedFile);large.querySelector('[data-large-business]').onclick=()=>{window.location.href=selectedFile.businessUrl};
    large.querySelector('.fc-large-dialog').focus();
  }

  function closeLargePreview(){
    if(large.hidden)return;large.hidden=true;large.querySelector('[data-large-stage]').innerHTML='';document.body.classList.remove('fc-large-preview-open');lastFocus?.focus?.();
  }
  root.querySelectorAll('[data-close-inspector]').forEach(button=>button.onclick=closeInspector);
  large.querySelectorAll('[data-close-large]').forEach(button=>button.onclick=closeLargePreview);
  document.addEventListener('keydown',event=>{
    if(event.key==='Escape'){if(!large.hidden)closeLargePreview();else if(root.classList.contains('has-inspector'))closeInspector();return}
    if(event.key!=='Tab'||large.hidden)return;
    const focusable=[...large.querySelector('.fc-large-dialog').querySelectorAll('button:not([disabled]),a[href],iframe,[tabindex]:not([tabindex="-1"])')];
    if(!focusable.length)return;
    const first=focusable[0],last=focusable[focusable.length-1];
    if(event.shiftKey&&document.activeElement===first){event.preventDefault();last.focus()}
    else if(!event.shiftKey&&document.activeElement===last){event.preventDefault();first.focus()}
  });
  q('[data-search]').oninput=render;q('[data-type]').onchange=render;q('[data-range]').onchange=render;
  categories();render();
})();
