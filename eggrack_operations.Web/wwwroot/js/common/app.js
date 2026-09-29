(() => {
 const app=window.Eggrack=window.Eggrack||{};
 const busyStates=new WeakMap();
 const escapeHtml=value=>String(value??'').replace(/[&<>"']/g,ch=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[ch]));
 const readResponse=async response=>{const type=response.headers.get('content-type')||'';if(type.includes('application/json'))return response.json().catch(()=>({}));const text=await response.text();return text?{message:text}:{};};
 const errorMessage=payload=>{if(!payload||typeof payload!=='object')return'';const validation=payload.errors&&Object.values(payload.errors).flat().find(Boolean);return payload.message||validation||payload.detail||payload.title||'';};
 const request=async(url,options={})=>{const headers=new Headers(options.headers||{});if(!headers.has('Accept'))headers.set('Accept','application/json');const response=await fetch(url,{...options,headers});const payload=await readResponse(response);if(!response.ok)throw new Error(errorMessage(payload)||`请求失败（HTTP ${response.status}）`);return payload;};
 const setBusy=(element,busy)=>{if(!element)return;const active=Boolean(busy);if(active&&!busyStates.has(element))busyStates.set(element,Boolean(element.disabled));element.disabled=active||Boolean(busyStates.get(element));element.dataset.busy=String(active);element.setAttribute('aria-busy',String(active));if(!active)busyStates.delete(element);};
 const setMessage=(element,message,tone='danger')=>{if(!element)return;element.textContent=message||'';element.classList.toggle('d-none',!message);element.classList.remove('alert-success','alert-danger','alert-info','alert-warning');if(message&&element.classList.contains('alert'))element.classList.add(`alert-${tone}`);};
 const filterRows=(container,keyword,selector='[data-row]')=>{const value=String(keyword||'').trim().toLocaleLowerCase();container.querySelectorAll(selector).forEach(row=>row.hidden=!row.textContent.toLocaleLowerCase().includes(value));};
 app.http={request,readResponse};app.ui={escapeHtml,setBusy,setMessage,filterRows};
})();