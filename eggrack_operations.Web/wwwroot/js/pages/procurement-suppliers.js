(() => {
 const root=document.querySelector('[data-supplier-directory]');
 if(!root)return;
 const {request}=window.Eggrack.http;
 const {setBusy,notify}=window.Eggrack.ui;
 root.querySelector('[data-supplier-create-form]')?.addEventListener('submit',async event=>{
  event.preventDefault();
  const button=event.submitter;
  setBusy(button,true);
  try{
   await request('/wholesale/procurement/suppliers',{method:'POST',body:new FormData(event.target)});
   notify('供应商已保存。','success');
   window.location.reload();
  }catch(error){notify(error.message);}
  finally{setBusy(button,false);}
 });
})();
