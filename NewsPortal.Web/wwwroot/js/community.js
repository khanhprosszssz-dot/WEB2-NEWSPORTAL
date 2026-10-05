'use strict';
async function community(page=1) {
    const polls=await api(`polls?pageIndex=${page}&pageSize=6`);
    const subscription=me?await api('newsletter/me'):null;
    app.innerHTML=`<div class="section-head"><h1>Cộng đồng bạn đọc</h1></div><section class="notice"><h2>Bản tin Nhịp Sống</h2>${me?`<p>Email đăng ký: <strong>${esc(me.email)}</strong></p><p>${subscription.subscribed?'Bạn đã đăng ký nhận bản tin.':'Đăng ký để lưu email của bạn vào danh sách nhận bản tin.'}</p><button id="subscription" class="button primary">${subscription.subscribed?'Hủy đăng ký':'Đăng ký bản tin'}</button>`:'<p><a class="button" href="/dang-nhap">Đăng nhập để đăng ký bản tin</a></p>'}<p class="meta">Bản demo quản lý đăng ký; chưa gửi email tự động.</p></section><h2>Góc nhìn của bạn</h2>${polls.data.map(p=>`<section class="comment"><h3>${esc(p.question)}</h3><form class="vote-form" data-id="${p.id}">${p.options.map(o=>`<label class="check"><input type="radio" name="optionId" required value="${o.id}" ${p.myOptionId===o.id?'checked':''}> ${esc(o.text)} <span class="meta">(${o.votes} phiếu)</span></label>`).join('')}<p class="form-error"></p>${me?'<button class="button primary" type="submit">Lưu lựa chọn</button>':'<a class="button" href="/dang-nhap">Đăng nhập để bình chọn</a>'}</form></section>`).join('')||'<p class="empty">Hiện chưa có khảo sát đang mở.</p>'}`;
    app.append(pager(polls,p=>community(p).catch(e=>toast(e.message))));
    if(me)document.querySelector('#subscription').onclick=guarded(async()=>{await api(subscription.subscribed?'newsletter/me':'newsletter',subscription.subscribed?'DELETE':'POST');await community(page);toast(subscription.subscribed?'Đã hủy đăng ký.':'Đã đăng ký bản tin.');});
    document.querySelectorAll('.vote-form').forEach(f=>f.onsubmit=async e=>{e.preventDefault();const b=f.querySelector('button');if(!b)return;b.disabled=true;try{await api(`polls/${f.dataset.id}/vote`,'PUT',{optionId:Number(new FormData(f).get('optionId'))});await community(page);toast('Đã ghi nhận lựa chọn của bạn.');}catch(error){f.querySelector('.form-error').textContent=error.message;}finally{b.disabled=false;}});
}
async function cmsNewsletter(page=1,search='') {
    const r=await api(`newsletter?pageIndex=${page}&pageSize=20&search=${encodeURIComponent(search)}`);
    cmsTarget.innerHTML=`<h1>Đăng ký bản tin</h1><form class="filters" id="newsletter-search"><input name="search" type="search" aria-label="Tìm email" placeholder="Tìm email" value="${esc(search)}"><button class="button">Tìm</button></form><div class="table-wrap"><table><thead><tr><th>Email</th><th>Ngày đăng ký</th></tr></thead><tbody>${r.data.map(s=>`<tr><td>${esc(s.email)}</td><td>${date(s.subscribedAt)}</td></tr>`).join('')}</tbody></table></div>`;
    cmsTarget.append(pager(r,p=>cmsNewsletter(p,search).catch(e=>toast(e.message))));
    document.querySelector('#newsletter-search').onsubmit=e=>{e.preventDefault();guarded(()=>cmsNewsletter(1,new FormData(e.target).get('search')))();};
}
async function cmsPolls(page=1) {
    const r=await api(`polls?all=true&pageIndex=${page}&pageSize=10`);
    cmsTarget.innerHTML=`<h1>Khảo sát bạn đọc</h1><form id="poll-form">${field('Câu hỏi','question','','text',true)}<label class="field"><span>Lựa chọn (2–8 lựa chọn, mỗi dòng một lựa chọn)</span><textarea name="options" required></textarea></label><p class="form-error"></p><button class="button primary" type="submit">Tạo khảo sát</button></form>${r.data.map(p=>`<section class="comment"><h3>${esc(p.question)}</h3><p>${p.isActive?'Đang mở':'Đã đóng'} · ${p.options.reduce((n,o)=>n+o.votes,0)} phiếu</p>${p.options.map(o=>`<p>${esc(o.text)}: ${o.votes}</p>`).join('')}<button class="button poll-state" data-id="${p.id}" data-active="${!p.isActive}">${p.isActive?'Đóng khảo sát':'Mở lại'}</button> <button class="button poll-delete" data-id="${p.id}">Xóa</button></section>`).join('')}`;
    bindForm('#poll-form',async data=>{await api('polls','POST',{question:data.question,options:data.options.split(/\r?\n/).map(x=>x.trim()).filter(Boolean)});await cmsPolls();});
    cmsTarget.append(pager(r,p=>cmsPolls(p).catch(e=>toast(e.message))));
    cmsTarget.querySelectorAll('.poll-state').forEach(b=>b.onclick=guarded(async()=>{await api('polls/'+b.dataset.id,'PATCH',{isActive:b.dataset.active==='true'});await cmsPolls(page);}));
    cmsTarget.querySelectorAll('.poll-delete').forEach(b=>b.onclick=guarded(async()=>{if(confirm('Xóa khảo sát và toàn bộ phiếu của khảo sát này?')){await api('polls/'+b.dataset.id,'DELETE');await cmsPolls(page);}}));
}
