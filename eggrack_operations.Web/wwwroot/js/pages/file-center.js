(() => {
  const root=document.querySelector('[data-file-center]'); if(!root)return;
  const {escapeHtml:esc,notify}=window.Eggrack.ui;
  const source=document.getElementById('fileCenterData');
  const files=JSON.parse(source?.textContent||'[]');
  const groups=['全部客户',...new Set(files.map(file=>file.customer))];
  const icons={PDF:'bi-file-earmark-pdf',JPG:'bi-file-earmark-image',JPEG:'bi-file-earmark-image',PNG:'bi-file-earmark-image',WEBP:'bi-file-earmark-image',MP4:'bi-file-earmark-play',TXT:'bi-file-earmark-text',CSV:'bi-file-earmark-spreadsheet',XLS:'bi-file-earmark-spreadsheet',XLSX:'bi-file-earmark-spreadsheet',DOCX:'bi-file-earmark-word',ZIP:'bi-file-earmark-zip'};
  let group='全部客户'; const q=s=>root.querySelector(s);

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
      row.onclick=()=>detail(x,row);body.appendChild(row);
    });
    q('[data-empty]').style.display=items.length?'none':'block';
    q('[data-result-count]').textContent='共 '+items.length+' 个文件';
  }

  function detail(x,row){
    root.querySelectorAll('tbody tr').forEach(item=>item.classList.toggle('active',item===row));
    q('[data-detail-label]').textContent='文件详情';q('[data-detail-name]').textContent=x.name;q('[data-detail-sub]').textContent=x.type+' · '+x.size;
    const contentUrl='/files/content/'+encodeURIComponent(x.sourceKind)+'/'+x.fileId;
    const preview=x.previewKind==='image'?'<img src="'+contentUrl+'" alt="'+esc(x.name)+'">':x.previewKind==='video'?'<video src="'+contentUrl+'" controls preload="metadata"></video>':(x.previewKind==='pdf'||x.previewKind==='text'||x.previewKind==='spreadsheet')?'<iframe src="'+contentUrl+'" title="'+esc(x.name)+'"></iframe>':'<div><i class="bi '+(icons[x.type]||'bi-file-earmark')+'"></i>此格式不提供在线预览<br><small>请下载到本地后查看</small></div>';
    q('[data-preview]').innerHTML=preview;
    q('[data-primary]').onclick=()=>{window.location.href=contentUrl+'?download=true'};
    q('[data-open-source]').disabled=false;q('[data-open-source]').onclick=()=>{window.location.href=x.businessUrl};
    q('[data-secondary]').disabled=false;q('[data-primary]').disabled=false;
    q('[data-secondary]').onclick=async()=>{try{await navigator.clipboard.writeText(location.origin+contentUrl);notify('链接已复制。','success')}catch{notify('复制链接失败。')}};
    q('[data-detail-meta]').innerHTML='<div class="fc-meta"><span>业务来源</span><strong>'+esc(x.sourceLabel)+' · '+esc(x.sourceNumber)+'</strong></div><div class="fc-meta"><span>客户</span><strong>'+esc(x.customer)+'</strong></div><div class="fc-meta"><span>上传信息</span><strong>'+esc(x.uploadedBy)+' · '+esc(x.uploadedDate)+'</strong></div><div class="fc-meta"><span>可见范围</span><strong>'+esc(x.visibility)+'</strong></div>';
  }
  q('[data-search]').oninput=render;q('[data-type]').onchange=render;q('[data-range]').onchange=render;
  categories();render();
})();
