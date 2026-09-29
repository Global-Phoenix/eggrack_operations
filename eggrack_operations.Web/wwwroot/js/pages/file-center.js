(() => {
  const root=document.querySelector('[data-file-center]'); if(!root)return;
  const {escapeHtml:esc,notify}=window.Eggrack.ui;
  const source=document.getElementById('fileCenterData');
  const data={all:JSON.parse(source?.textContent||'[]')};
  data.customer=data.all.filter(file=>file[8]==='request');
  data.internal=data.all.filter(file=>file[8]==='plan');
  const groups={
    customer:['全部客户',...new Set(data.customer.map(file=>file[3]))],
    internal:['全部部门',...new Set(data.internal.map(file=>file[3]))]
  };
  const icons={PDF:'bi-file-earmark-pdf',JPG:'bi-file-earmark-image',JPEG:'bi-file-earmark-image',PNG:'bi-file-earmark-image',WEBP:'bi-file-earmark-image',MP4:'bi-file-earmark-play',TXT:'bi-file-earmark-text',CSV:'bi-file-earmark-spreadsheet',XLS:'bi-file-earmark-spreadsheet',XLSX:'bi-file-earmark-spreadsheet',DOCX:'bi-file-earmark-word',ZIP:'bi-file-earmark-zip'};
  let mode='customer',group='全部客户'; const q=s=>root.querySelector(s);

  function categories(){
    const box=q('[data-categories]');box.innerHTML='';
    groups[mode].forEach((name,index)=>{
      const button=document.createElement('button');button.type='button';button.className='fc-category '+(name===group?'active':'');
      const icon=index===0?(mode==='customer'?'bi-person-vcard':'bi-shield-lock'):'bi-building';
      button.innerHTML='<span class="fc-category-name"><i class="bi '+icon+'"></i>'+esc(name)+'</span><small>'+(index?data[mode].filter(x=>x[3]===name).length:data[mode].length)+'</small>';
      button.onclick=()=>{group=name;categories();render()};box.appendChild(button);
    });
  }

  function render(){
    const keyword=q('[data-search]').value.toLowerCase(),type=q('[data-type]').value,range=q('[data-range]').value;
    const cutoffDate=new Date(); cutoffDate.setDate(cutoffDate.getDate()-(range==='week'?7:range==='month'?30:0));
    const cutoff=range?cutoffDate.toISOString().slice(0,10):'';
    const items=data[mode].filter(x=>(group.startsWith('全部')||x[3]===group)&&(!type||x[1]===type)&&(!cutoff||x[6]>=cutoff)&&(!keyword||(x[0]+x[3]+x[4]).toLowerCase().includes(keyword)));
    const body=q('[data-rows]');body.innerHTML='';
    items.forEach(x=>{
      const row=document.createElement('tr');const typeKey=String(x[1]||'').toUpperCase();const fileClass=icons[typeKey]?typeKey.toLowerCase():'other';
      row.innerHTML='<td><div class="fc-file"><span class="fc-file-icon '+fileClass+'"><i class="bi '+(icons[typeKey]||'bi-file-earmark')+'"></i></span><span class="fc-file-copy"><strong title="'+esc(x[0])+'">'+esc(x[0])+'</strong><small>'+esc(x[2])+'</small></span></div></td><td><strong class="small">'+esc(x[4])+'</strong><small>'+esc(x[3])+'</small></td><td><span>'+esc(x[6])+'</span><small>'+esc(x[5])+'</small></td><td class="text-end text-secondary"><i class="bi bi-chevron-right"></i></td>';
      row.onclick=()=>detail(x,row);body.appendChild(row);
    });
    q('[data-empty]').style.display=items.length?'none':'block';
    q('[data-result-count]').textContent='共 '+items.length+' 个文件';
  }

  function detail(x,row){
    root.querySelectorAll('tbody tr').forEach(item=>item.classList.toggle('active',item===row));
    q('[data-detail-label]').textContent='文件详情';q('[data-detail-name]').textContent=x[0];q('[data-detail-sub]').textContent=x[1]+' · '+x[2];
    const contentUrl='/files/content/'+encodeURIComponent(x[8])+'/'+x[9];
    const preview=x[10]==='image'?'<img src="'+contentUrl+'" alt="'+esc(x[0])+'">':x[10]==='video'?'<video src="'+contentUrl+'" controls preload="metadata"></video>':(x[10]==='pdf'||x[10]==='text'||x[10]==='spreadsheet')?'<iframe src="'+contentUrl+'" title="'+esc(x[0])+'"></iframe>':'<div><i class="bi '+(icons[x[1]]||'bi-file-earmark')+'"></i>此格式暂不支持在线预览<br><small>请下载后查看</small></div>';
    q('[data-preview]').innerHTML=preview;
    q('[data-primary]').onclick=()=>{window.location.href=contentUrl+'?download=true'};
    q('[data-secondary]').disabled=false;q('[data-primary]').disabled=false;
    q('[data-secondary]').onclick=async()=>{try{await navigator.clipboard.writeText(location.origin+contentUrl);notify('链接已复制。','success')}catch{notify('复制链接失败。')}};
    q('[data-detail-meta]').innerHTML='<div class="fc-meta"><span>'+(mode==='customer'?'采购申请':'采购计划')+'</span><strong>'+esc(x[4])+'</strong></div><div class="fc-meta"><span>'+(mode==='customer'?'客户':'计划部门')+'</span><strong>'+esc(x[3])+'</strong></div><div class="fc-meta"><span>上传信息</span><strong>'+esc(x[5])+' · '+esc(x[6])+'</strong></div><div class="fc-meta"><span>文件状态</span><strong>'+esc(x[7])+'</strong></div>';

  }

  function setMode(next){
    mode=next;group=groups[next][0];
    root.querySelectorAll('[data-mode]').forEach(x=>x.classList.toggle('active',x.dataset.mode===next));
    q('[data-tree-title]').textContent=next==='customer'?'客户':'部门';
    q('[data-group-head]').textContent=next==='customer'?'采购申请':'采购计划';
    q('[data-search]').placeholder=next==='customer'?'搜索客户文件或申请编号':'搜索内部文件或计划编号';
    q('[data-policy]').style.display='none';categories();render();
  }

  root.querySelectorAll('[data-mode]').forEach(x=>x.onclick=()=>setMode(x.dataset.mode));
  q('[data-search]').oninput=render;q('[data-type]').onchange=render;q('[data-range]').onchange=render;
  q('[data-policy]').onclick=()=>{
    q('[data-detail-label]').textContent='自动清理';q('[data-detail-name]').textContent='会员文件清理策略';q('[data-detail-sub]').textContent='仅清理已失效且不再被业务引用的文件';
    q('[data-preview]').innerHTML='<div><i class="bi bi-clock-history"></i><strong class="d-block fs-4 text-primary">90 天</strong>业务结束后的保留期</div>';
    q('[data-detail-meta]').innerHTML='<div class="fc-meta"><span>执行频率</span><strong>每日 02:00</strong></div><div class="fc-meta"><span>提前提醒</span><strong>清理前 7 天</strong></div><div class="fc-meta"><span>最近执行</span><strong>2026-09-28 · 已完成</strong></div>';
  };
  setMode('customer');
})();
