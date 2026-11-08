document.addEventListener('DOMContentLoaded',()=>{
 document.querySelectorAll('.sidebar nav a').forEach(a=>{if(a.pathname===location.pathname||(location.pathname==='/'&&a.pathname==='/Index')){a.classList.add('current');a.setAttribute('aria-current','page')}});
 document.querySelectorAll('input[placeholder],textarea[placeholder]').forEach((input,i)=>{if(input.hasAttribute('aria-label')||input.previousElementSibling?.tagName==='LABEL')return; /* [UI-03] đã có nhãn */ if(!input.id)input.id='field-'+i;if(!document.querySelector('label[for="'+input.id+'"]')){let label=document.createElement('label');label.htmlFor=input.id;label.textContent=input.placeholder;input.before(label)}});
 document.querySelectorAll('table').forEach(table=>{
  if(table.rows.length===1){let tr=table.insertRow();let td=tr.insertCell();td.colSpan=table.rows[0].cells.length;td.className='empty-state';td.textContent='Chưa có dữ liệu phù hợp. Thử đổi bộ lọc hoặc thêm bản ghi mới.'}
  if(!table.parentElement.classList.contains('table-wrap')){let wrap=document.createElement('div');wrap.className='table-wrap';table.before(wrap);wrap.append(table)}
 });
 document.querySelectorAll('select[name]').forEach(s=>{let v=new URLSearchParams(location.search).get(s.name);if(v!==null)s.value=v});
 document.querySelectorAll('form').forEach(f=>f.addEventListener('submit',e=>{
  if(!f.checkValidity()){e.preventDefault();f.reportValidity();return;}
  let text=(e.submitter?.textContent||'').trim();
  if((text==='Xóa'||text==='Khóa')&&!f.hasAttribute('onsubmit')&&!f.closest('dialog')&&!confirm(text+' bản ghi này?'))e.preventDefault(); // [UI-01] trong modal đã là bước xác nhận
 }));
});

// ===== [UI-01] Cửa sổ form (modal) =====
// Nút <button data-open="id" data-fill='{"id":5,"full_name":"Lan"}'> mở <dialog id="id">, điền input[name] và [data-text].
function mbOpenModal(id,fill){
 const d=document.getElementById(id); if(!d||typeof d.showModal!=='function') return;
 d.querySelectorAll('form').forEach(f=>f.reset()); d.querySelectorAll('.modal-error').forEach(e=>e.remove());
 if(fill) for(const [k,v] of Object.entries(fill)){
  d.querySelectorAll('[name="'+k+'"]').forEach(el=>{
   if(el.type==='checkbox'||el.type==='radio') el.checked=Array.isArray(v)?v.includes(el.value):String(v)===el.value||v===true;
   else if(!(el.type==='hidden'&&k==='modal')) el.value=Array.isArray(v)?v[0]:(v??'');
  });
  d.querySelectorAll('[data-text="'+k+'"]').forEach(el=>el.textContent=v??'');
 }
 d.querySelectorAll('[data-show-if]').forEach(el=>{const [k,val]=el.dataset.showIf.split('=');el.hidden=!(fill&&String(fill[k])===val)});
 d.showModal();
 d.querySelector('input:not([type=hidden]):not([readonly]),select,textarea')?.focus();
}
document.addEventListener('click',e=>{
 const open=e.target.closest('[data-open]');
 if(open){e.preventDefault();e.stopPropagation();mbOpenModal(open.dataset.open,open.dataset.fill?JSON.parse(open.dataset.fill):null);return;}
 if(e.target.closest('[data-close]')){e.target.closest('dialog')?.close();return;}
 if(e.target.tagName==='DIALOG'){const r=e.target.getBoundingClientRect();if(e.clientX<r.left||e.clientX>r.right||e.clientY<r.top||e.clientY>r.bottom)e.target.close();}
});
document.addEventListener('DOMContentLoaded',()=>{
 const st=document.getElementById('modal-state');
 if(st){try{const m=JSON.parse(st.textContent);mbOpenModal(m.id,m.form);
  if(m.error){const b=document.querySelector('#'+m.id+' .modal-body');if(b){const e=document.createElement('div');e.className='modal-error';e.setAttribute('role','alert');e.textContent=m.error;b.prepend(e);}}
 }catch(_){}}
});

// ===== [UI-04] Thông báo nổi: thành công tự ẩn sau 4 giây, lỗi giữ tới khi bấm × (hoặc 10 giây) =====
document.addEventListener('DOMContentLoaded',()=>{
 document.querySelectorAll('.toast').forEach(t=>{
  const hide=()=>{t.classList.add('leaving');setTimeout(()=>t.remove(),250)};
  t.querySelector('.toast-close')?.addEventListener('click',hide);
  setTimeout(hide,t.classList.contains('toast-error')?10000:4000);
 });
});

// ===== [UI-05] Ngăn chi tiết bên phải: bấm vào dòng có data-detail-url (hoặc nút data-detail) =====
async function mbOpenDrawer(url,title){
 const d=document.getElementById('drawer'); if(!d||!url) return;
 d.querySelector('#drawer-title').textContent=title||'Chi tiết';
 const body=d.querySelector('.drawer-body'); body.innerHTML='<div class="drawer-loading">Đang tải…</div>';
 d.classList.add('open');d.setAttribute('aria-hidden','false');document.querySelector('.drawer-backdrop').hidden=false;
 try{const r=await fetch(url,{headers:{'X-Requested-With':'fetch'}});body.innerHTML=r.ok?await r.text():'<div class="empty-state">Không tải được chi tiết ('+r.status+').</div>';}
 catch(_){body.innerHTML='<div class="empty-state">Không kết nối được máy chủ.</div>';}
}
function mbCloseDrawer(){const d=document.getElementById('drawer');if(!d)return;d.classList.remove('open');d.setAttribute('aria-hidden','true');document.querySelector('.drawer-backdrop').hidden=true;}
document.addEventListener('click',e=>{
 if(e.target.closest('[data-drawer-close]')){mbCloseDrawer();return;}
 const btn=e.target.closest('[data-detail]');
 if(btn){e.preventDefault();mbOpenDrawer(btn.dataset.detail,btn.dataset.detailTitle);return;}
 const row=e.target.closest('tr[data-detail-url]');
 if(row&&row.dataset.detailUrl&&!e.target.closest('a,button,input,select,textarea,form,label'))mbOpenDrawer(row.dataset.detailUrl,row.dataset.detailTitle);
});
document.addEventListener('keydown',e=>{if(e.key==='Escape'&&!document.querySelector('dialog[open]'))mbCloseDrawer();});
