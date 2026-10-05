document.addEventListener('DOMContentLoaded',()=>{
 document.querySelectorAll('.sidebar nav a').forEach(a=>{if(a.pathname===location.pathname||(location.pathname==='/'&&a.pathname==='/Index')){a.classList.add('current');a.setAttribute('aria-current','page')}});
 document.querySelectorAll('input[placeholder],textarea[placeholder]').forEach((input,i)=>{if(!input.id)input.id='field-'+i;if(!document.querySelector('label[for="'+input.id+'"]')){let label=document.createElement('label');label.htmlFor=input.id;label.textContent=input.placeholder;input.before(label)}});
 document.querySelectorAll('table').forEach(table=>{
  if(table.rows.length===1){let tr=table.insertRow();let td=tr.insertCell();td.colSpan=table.rows[0].cells.length;td.className='empty-state';td.textContent='Chưa có dữ liệu phù hợp. Thử đổi bộ lọc hoặc thêm bản ghi mới.'}
  if(!table.parentElement.classList.contains('table-wrap')){let wrap=document.createElement('div');wrap.className='table-wrap';table.before(wrap);wrap.append(table)}
 });
 document.querySelectorAll('select[name]').forEach(s=>{let v=new URLSearchParams(location.search).get(s.name);if(v!==null)s.value=v});
 document.querySelectorAll('form').forEach(f=>f.addEventListener('submit',e=>{
  if(!f.checkValidity()){e.preventDefault();f.reportValidity();return;}
  let text=(e.submitter?.textContent||'').trim();
  if((text==='Xóa'||text==='Khóa')&&!f.hasAttribute('onsubmit')&&!confirm(text+' bản ghi này?'))e.preventDefault();
 }));
});
