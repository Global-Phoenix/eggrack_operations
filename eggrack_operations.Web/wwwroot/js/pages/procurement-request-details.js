(() => {
    const root=document.querySelector('[data-request-detail]');
    if(!root)return;

    const switchTab=target=>{
        const tab=target?document.querySelector(`.request-detail-tabs [data-bs-target="${target}"]`):null;
        if(!tab||typeof bootstrap==='undefined')return;
        bootstrap.Tab.getOrCreateInstance(tab).show();
        tab.focus({preventScroll:true});
        document.querySelector('.request-detail-tabs')?.scrollIntoView({behavior:'smooth',block:'start'});
    };
    document.querySelectorAll('[data-request-tab]').forEach(button=>button.addEventListener('click',()=>switchTab(button.dataset.requestTab)));

    const fileWorkspace=document.querySelector('[data-file-preview-workspace]');
    if(fileWorkspace){
        const fileButtons=[...fileWorkspace.querySelectorAll('[data-file-preview-target]')];
        const filePanels=[...fileWorkspace.querySelectorAll('[data-file-preview-panel]')];
        const selectFile=button=>{
            const panelId=button.dataset.filePreviewTarget;
            const activePanel=panelId?document.getElementById(panelId):null;
            if(!activePanel||!fileWorkspace.contains(activePanel))return;
            fileButtons.forEach(item=>{const selected=item===button;item.classList.toggle('is-active',selected);item.setAttribute('aria-selected',selected?'true':'false');item.tabIndex=selected?0:-1});
            filePanels.forEach(panel=>{const selected=panel===activePanel;panel.classList.toggle('is-active',selected);panel.setAttribute('aria-hidden',selected?'false':'true')});
            const source=activePanel.querySelector('[data-file-preview-source][data-src]');
            if(source&&!source.getAttribute('src')){source.setAttribute('src',source.dataset.src);source.removeAttribute('data-src')}
        };
        fileButtons.forEach((button,index)=>{
            button.addEventListener('click',()=>selectFile(button));
            button.addEventListener('keydown',event=>{
                if(!['ArrowDown','ArrowUp','Home','End'].includes(event.key))return;
                event.preventDefault();
                let next=index;
                if(event.key==='ArrowDown')next=(index+1)%fileButtons.length;
                if(event.key==='ArrowUp')next=(index-1+fileButtons.length)%fileButtons.length;
                if(event.key==='Home')next=0;
                if(event.key==='End')next=fileButtons.length-1;
                fileButtons[next].focus();selectFile(fileButtons[next]);
            });
        });
    }

    const versionWorkspace=document.querySelector('[data-version-workspace]');
    if(versionWorkspace){
        const versionButtons=[...versionWorkspace.querySelectorAll('[data-version-target]')];
        const versionPanels=[...versionWorkspace.querySelectorAll('[data-version-panel]')];
        versionButtons.forEach(button=>button.addEventListener('click',()=>{
            const target=document.getElementById(button.dataset.versionTarget);
            if(!target)return;
            versionButtons.forEach(item=>{const active=item===button;item.classList.toggle('is-active',active);item.setAttribute('aria-selected',active?'true':'false')});
            versionPanels.forEach(panel=>{const active=panel===target;panel.classList.toggle('is-active',active);panel.setAttribute('aria-hidden',active?'false':'true')});
        }));
    }

    const dialog=document.querySelector('[data-large-preview]');
    const preview=window.Eggrack?.filePreview;
    if(!dialog||!preview)return;
    let activeFile=null,activeGallery=[],activeGalleryIndex=0,lastFocus=null;
    const download=file=>{window.location.href=preview.contentUrl(file)+'?download=true'};
    const fileFromTrigger=trigger=>({
        name:trigger.dataset.previewName||'文件预览',type:trigger.dataset.previewType||'FILE',size:trigger.dataset.previewSize||'',
        sourceKind:'request',fileId:trigger.dataset.previewFileId,previewKind:trigger.dataset.previewKind||'other',
        sourceLabel:'采购申请',sourceNumber:root.dataset.requestNumber,customer:root.dataset.customerName,
        uploadedBy:'客户',uploadedDate:trigger.dataset.previewUploaded||'—',visibility:'客户提交',businessUrl:location.pathname+location.search
    });
    const renderLarge=trigger=>{
        activeFile=fileFromTrigger(trigger);
        dialog.querySelector('[data-large-name]').textContent=activeFile.name;dialog.querySelector('[data-large-sub]').textContent=activeFile.type+' · '+activeFile.size;
        const icon=dialog.querySelector('[data-large-icon]');icon.className='fc-file-icon '+String(activeFile.type).toLowerCase();icon.innerHTML='<i class="bi '+preview.icon(activeFile)+'"></i>';
        dialog.querySelector('[data-large-stage]').innerHTML=preview.markup(activeFile,true);dialog.querySelector('[data-large-meta]').innerHTML=preview.meta(activeFile,true);
        dialog.querySelector('[data-unsupported-download]')?.addEventListener('click',()=>download(activeFile));
        const newWindow=dialog.querySelector('[data-large-new-window]');newWindow.disabled=!preview.canPreview(activeFile);newWindow.onclick=()=>window.open(preview.contentUrl(activeFile),'_blank','noopener');
        dialog.querySelector('[data-large-download]').onclick=()=>download(activeFile);
        dialog.querySelector('[data-large-business]').onclick=()=>{window.location.href=activeFile.businessUrl};
        const galleryTools=dialog.querySelector('[data-large-gallery-tools]');galleryTools.hidden=activeGallery.length<2;
        dialog.querySelector('[data-large-position]').textContent=activeGallery.length>1?`${activeGalleryIndex+1}/${activeGallery.length}`:'';
        dialog.querySelector('[data-large-previous]').disabled=activeGalleryIndex===0;dialog.querySelector('[data-large-next]').disabled=activeGalleryIndex>=activeGallery.length-1;
    };
    const openLarge=trigger=>{
        const galleryName=trigger.dataset.previewGallery;
        activeGallery=galleryName?[...document.querySelectorAll(`[data-request-large-preview][data-preview-gallery="${CSS.escape(galleryName)}"]`)].sort((a,b)=>Number(a.dataset.previewIndex)-Number(b.dataset.previewIndex)):[trigger];
        activeGalleryIndex=Math.max(0,activeGallery.indexOf(trigger));lastFocus=document.activeElement;
        dialog.hidden=false;document.body.classList.add('fc-large-preview-open');renderLarge(activeGallery[activeGalleryIndex]);
        dialog.querySelector('.fc-large-dialog').focus();
    };
    const closeLarge=()=>{if(dialog.hidden)return;dialog.hidden=true;dialog.querySelector('[data-large-stage]').innerHTML='';document.body.classList.remove('fc-large-preview-open');lastFocus?.focus?.()};
    document.querySelectorAll('[data-request-large-preview]').forEach(trigger=>trigger.addEventListener('click',()=>openLarge(trigger)));
    dialog.querySelectorAll('[data-close-large]').forEach(button=>button.addEventListener('click',closeLarge));
    dialog.querySelector('[data-large-previous]').addEventListener('click',()=>{if(activeGalleryIndex>0){activeGalleryIndex--;renderLarge(activeGallery[activeGalleryIndex])}});
    dialog.querySelector('[data-large-next]').addEventListener('click',()=>{if(activeGalleryIndex<activeGallery.length-1){activeGalleryIndex++;renderLarge(activeGallery[activeGalleryIndex])}});
    document.addEventListener('keydown',event=>{
        if(event.key==='Escape'&&!dialog.hidden){closeLarge();return}
        if(event.key!=='Tab'||dialog.hidden)return;
        const focusable=[...dialog.querySelector('.fc-large-dialog').querySelectorAll('button:not([disabled]),a[href],iframe,[tabindex]:not([tabindex="-1"])')];
        if(!focusable.length)return;
        const first=focusable[0],last=focusable[focusable.length-1];
        if(event.shiftKey&&document.activeElement===first){event.preventDefault();last.focus()}
        else if(!event.shiftKey&&document.activeElement===last){event.preventDefault();first.focus()}
    });
})();
