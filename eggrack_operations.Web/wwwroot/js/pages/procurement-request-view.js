(() => {
    window.Eggrack = window.Eggrack || {};
    const esc = window.Eggrack.ui.escapeHtml;
    const sizeText = bytes => {
        const value = Number(bytes || 0);
        if (value >= 1073741824) return `${(value / 1073741824).toFixed(1)} GB`;
        if (value >= 1048576) return `${(value / 1048576).toFixed(1)} MB`;
        if (value >= 1024) return `${Math.round(value / 1024)} KB`;
        return `${value} B`;
    };
    const preview = file => {
        const url = `/files/content/request/${file.id}`;
        const mime = String(file.mimeType || '').toLowerCase();
        if (mime.startsWith('image/')) return `<img src="${url}" alt="${esc(file.originalName)}" loading="lazy">`;
        if (mime.startsWith('video/')) return `<video src="${url}" controls preload="metadata"></video>`;
        if (mime === 'application/pdf' || mime.startsWith('text/')) return `<iframe src="${url}" title="${esc(file.originalName)}" loading="lazy"></iframe>`;
        return '<div class="request-file-unavailable"><i class="bi bi-file-earmark-arrow-down"></i><span>此格式请下载后查看</span></div>';
    };
    const render = (host, detail) => {
        if (!host) return;
        const items = detail?.items || [];
        const attachments = detail?.attachments || [];
        const itemHtml = items.length ? items.map((item, index) => {
            const attributes = [
                ['SKU', item.sku], ['品牌', item.brand], ['描述', item.description],
                ['规格', item.specifications], ['颜色', item.color], ['尺寸', item.size]
            ].filter(([, value]) => value).map(([label, value]) => `<span><b>${label}</b> ${esc(value)}</span>`).join('');
            const notes = [
                ['包装要求', item.packagingRequirements], ['定制要求', item.customizationRequirements],
                ['客户备注', item.customerNote]
            ].filter(([, value]) => value).map(([label, value]) => `<div class="request-item-note"><b>${label}：</b>${esc(value)}</div>`).join('');
            return `<article class="request-item"><div class="request-item-title"><strong>${index + 1}. ${esc(item.productName || '未命名产品')}</strong><span>${esc(item.quantity)} ${esc(item.unit || '')}</span></div>${attributes ? `<div class="request-item-meta">${attributes}</div>` : ''}${notes}</article>`;
        }).join('') : '<div class="text-secondary small">该版本没有产品明细。</div>';
        const fileHtml = attachments.length ? attachments.map(file => {
            const url = `/files/content/request/${file.id}`;
            return `<details class="request-file"><summary><span><i class="bi bi-paperclip"></i> ${esc(file.originalName)}</span><small>${sizeText(file.fileSize)}</small></summary><div class="request-file-preview">${preview(file)}</div><div class="request-file-actions"><a href="${url}" target="_blank" rel="noopener">新窗口预览</a><a href="${url}?download=true">下载</a></div></details>`;
        }).join('') : '<div class="text-secondary small">该版本没有附件。</div>';
        host.innerHTML = `<div class="request-detail-section"><div class="request-detail-heading"><strong>产品详细信息</strong><span>${items.length} 项</span></div>${itemHtml}</div><div class="request-detail-section"><div class="request-detail-heading"><strong>申请附件</strong><span>${attachments.length} 个</span></div><div class="request-file-list">${fileHtml}</div></div>`;
    };
    window.Eggrack.procurementRequestView = { render };
})();
