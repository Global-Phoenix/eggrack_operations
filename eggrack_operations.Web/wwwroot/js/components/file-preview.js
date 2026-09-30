(() => {
  const {escapeHtml:esc}=window.Eggrack.ui;
  const icons={PDF:'bi-file-earmark-pdf',JPG:'bi-file-earmark-image',JPEG:'bi-file-earmark-image',PNG:'bi-file-earmark-image',WEBP:'bi-file-earmark-image',MP4:'bi-file-earmark-play',TXT:'bi-file-earmark-text',CSV:'bi-file-earmark-spreadsheet',XLS:'bi-file-earmark-spreadsheet',XLSX:'bi-file-earmark-spreadsheet',DOCX:'bi-file-earmark-word',ZIP:'bi-file-earmark-zip'};
  const contentUrl=file=>'/files/content/'+encodeURIComponent(file.sourceKind)+'/'+file.fileId;
  const icon=file=>icons[String(file.type||'').toUpperCase()]||'bi-file-earmark';
  const canPreview=file=>['image','video','pdf','text','spreadsheet'].includes(file.previewKind);

  function markup(file,largeMode=false){
    const url=contentUrl(file),name=esc(file.name);
    if(file.previewKind==='image')return '<img src="'+url+'" alt="'+name+'">';
    if(file.previewKind==='video')return '<video src="'+url+'" controls preload="metadata"></video>';
    if(['pdf','text','spreadsheet'].includes(file.previewKind))return '<iframe src="'+url+'" title="'+name+'"></iframe>';
    return '<div class="fc-unsupported"><i class="bi '+icon(file)+'"></i><strong>'+name+'</strong><small>此格式暂不支持在线预览，请下载到本地查看。</small>'+(largeMode?'<button class="btn btn-primary" type="button" data-unsupported-download><i class="bi bi-download"></i>下载文件</button>':'')+'</div>';
  }

  function meta(file,includeFile=false){
    return (includeFile?'<div class="fc-meta"><span>文件名称</span><strong>'+esc(file.name)+'</strong></div><div class="fc-meta"><span>类型 / 大小</span><strong>'+esc(file.type)+' · '+esc(file.size)+'</strong></div>':'')+
      '<div class="fc-meta"><span>业务来源</span><strong>'+esc(file.sourceLabel)+' · '+esc(file.sourceNumber)+'</strong></div><div class="fc-meta"><span>客户</span><strong>'+esc(file.customer)+'</strong></div><div class="fc-meta"><span>上传信息</span><strong>'+esc(file.uploadedBy)+' · '+esc(file.uploadedDate)+'</strong></div><div class="fc-meta"><span>可见范围</span><strong>'+esc(file.visibility)+'</strong></div>';
  }

  window.Eggrack.filePreview={icons,contentUrl,icon,canPreview,markup,meta};
})();
