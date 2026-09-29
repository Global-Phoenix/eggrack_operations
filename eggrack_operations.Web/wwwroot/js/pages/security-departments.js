document.addEventListener('click',event=>{
  const button=event.target.closest('[data-department-template]');
  if(!button)return;
  const form=button.closest('form');
  if(!form)return;
  const code=form.querySelector('[name="Code"]');
  const name=form.querySelector('[name="Name"]');
  if(code)code.value=button.dataset.code||'';
  if(name)name.value=button.dataset.name||'';
});
