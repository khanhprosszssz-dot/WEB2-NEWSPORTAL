'use strict';
const app = document.querySelector('#app');
const csrf = document.querySelector('meta[name="csrf-token"]').content;
let me = null, categories = [], tags = [];
const esc = value => String(value ?? '').replace(/[&<>"']/g, c => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
const date = value => value ? new Date(value).toLocaleDateString('vi-VN', {day:'2-digit',month:'2-digit',year:'numeric'}) : 'Chưa xuất bản';
const url = a => '/tin-tuc/' + encodeURIComponent(a.slug);
const isStaff = () => me?.roles.some(r => ['Administrator','Editor','Journalist','Moderator'].includes(r));
const isEditor = () => me?.roles.some(r => ['Administrator','Editor'].includes(r));
const toast = message => { const el = document.querySelector('#toast'); el.textContent = message; el.hidden = false; setTimeout(() => el.hidden = true, 4500); };
async function api(path, method = 'GET', body) {
    const options = {method, headers:{'X-CSRF-TOKEN':csrf}};
    if (body instanceof FormData) options.body = body;
    else if (body !== undefined) { options.body = JSON.stringify(body); options.headers['Content-Type'] = 'application/json'; }
    const response = await fetch('/gateway/' + path, options);
    const raw = await response.text(); let data;
    try { data = raw ? JSON.parse(raw) : null; } catch { data = null; }
    if (!response.ok) {
        const errors = data?.errors ? Object.values(data.errors).flat().join('\n') : null;
        throw new Error(errors || data?.title || data?.message || (response.status === 401 ? 'Bạn cần đăng nhập.' : response.status === 403 ? 'Bạn không có quyền thực hiện thao tác này.' : 'Không thể xử lý yêu cầu (' + response.status + ').'));
    }
    return data;
}
function mediaImage(a, extra = '') {
    if (a.thumbnailUrl) return `<img class="art-image ${extra}" loading="lazy" src="/media/${esc(a.thumbnailUrl.split('/').pop())}" alt="${esc(a.title)}">`;
    return `<div class="art-image art-placeholder tone${a.categoryId % 4} ${extra}" aria-hidden="true">${esc(a.category)}</div>`;
}
function card(a) { return `<article class="card-news"><a href="${url(a)}">${mediaImage(a)}</a><a class="eyebrow" href="/chuyen-muc/${esc(a.categorySlug)}">${esc(a.category)}</a><h3 class="mt-2"><a href="${url(a)}">${esc(a.title)}</a></h3><p>${esc(a.summary)}</p><div class="meta">${esc(a.author)} · ${date(a.publishedAt)} · ${a.viewCount} lượt xem</div></article>`; }
function cards(list) { return list.length ? `<div class="cards">${list.map(card).join('')}</div>` : '<div class="empty">Chưa có bài viết phù hợp.</div>'; }
function field(label, name, value = '', type = 'text', required = false) { return `<label class="field"><span>${label}</span><input type="${type}" name="${name}" value="${esc(value)}" ${required ? 'required' : ''}></label>`; }
function pager(result, callback) {
    const div = document.createElement('div'); div.className = 'pager';
    div.innerHTML = `<button class="button" ${result.pageIndex <= 1 ? 'disabled' : ''}>← Trước</button><span>Trang ${result.pageIndex} / ${Math.max(1,result.totalPages || Math.ceil(result.totalCount/result.pageSize))} · ${result.totalCount} kết quả</span><button class="button" ${result.pageIndex * result.pageSize >= result.totalCount ? 'disabled' : ''}>Sau →</button>`;
    const navigate = async page => {
        const buttons = [div.children[0], div.children[2]];
        const disabled = buttons.map(button => button.disabled);
        buttons.forEach(button => button.disabled = true);
        try { await callback(page); }
        catch (error) { toast(error.message); }
        finally { buttons.forEach((button, i) => button.disabled = disabled[i]); }
    };
    div.children[0].onclick = () => navigate(result.pageIndex - 1); div.children[2].onclick = () => navigate(result.pageIndex + 1);
    return div;
}
function bindForm(selector, action) {
    const form = document.querySelector(selector);
    form.onsubmit = async e => {
        e.preventDefault(); const button = form.querySelector('[type=submit]'); const error = form.querySelector('.form-error');
        if (button) button.disabled = true; if (error) error.textContent = '';
        try { await action(Object.fromEntries(new FormData(form)), form); }
        catch (err) { if (error) error.textContent = err.message; else toast(err.message); }
        finally { if (button) button.disabled = false; }
    };
}
async function home() {
    const [latest, featured, popular, breaking, choices] = await Promise.all([
        api('articles?pageSize=9'),api('articles/featured?pageSize=3'),api('articles/popular?pageSize=5'),api('articles/breaking?pageSize=1'),api('articles?editorsChoice=true&pageSize=3')]);
    const hero = featured.data[0] || latest.data[0];
    app.innerHTML = `${breaking.data[0] ? `<div class="breaking"><strong>ĐÁNG CHÚ Ý</strong><a href="${url(breaking.data[0])}">${esc(breaking.data[0].title)} →</a></div>` : ''}
        ${hero ? `<section class="hero-grid"><div class="hero"><a href="${url(hero)}">${mediaImage(hero)}</a><span class="eyebrow">${esc(hero.category)} · Tiêu điểm</span><h2 class="mt-2"><a href="${url(hero)}">${esc(hero.title)}</a></h2><p>${esc(hero.summary)}</p><div class="meta">${esc(hero.author)} · ${date(hero.publishedAt)}</div></div><div>${latest.data.filter(a => a.id !== hero.id).slice(0,3).map((a,i) => `<article class="mini">${i===0 ? `<a href="${url(a)}">${mediaImage(a)}</a>`:''}<span class="eyebrow">${esc(a.category)}</span><h3><a href="${url(a)}">${esc(a.title)}</a></h3><div class="meta">${date(a.publishedAt)}</div></article>`).join('')}</div><div><span class="eyebrow">Được quan tâm</span><h3 class="mt-2">Đọc nhiều</h3>${popular.data.map((a,i)=>`<div class="rank"><b>0${i+1}</b><div><h3><a href="${url(a)}">${esc(a.title)}</a></h3><div class="meta">${a.viewCount} lượt xem</div></div></div>`).join('')}</div></section>` : '<div class="empty">Chưa có tin tức.</div>'}
        <div class="section-head"><h2>Dòng tin mới</h2><a href="/tim-kiem">Xem tất cả →</a></div>${cards(latest.data.slice(0,6))}
        <div class="quote-band"><div><span class="eyebrow">Góc nhìn mỗi ngày</span><h2>Câu chuyện nhỏ. Giá trị lớn.</h2></div><a class="button mt-3" href="/chuyen-muc/doi-song">Khám phá đời sống →</a></div>
        <div class="section-head"><h2>Biên tập viên lựa chọn</h2></div>${cards(choices.data)}
        <div class="section-head"><h2>Theo chuyên mục</h2></div><div class="actions">${categories.map(c=>`<a class="pill" href="/chuyen-muc/${esc(c.slug)}">${esc(c.name)} →</a>`).join('')}</div><div id="category-sections"></div>`;
    const sections = await Promise.all(categories.slice(0,6).map(async c => ({category:c, result:await api('articles/category/'+encodeURIComponent(c.slug)+'?pageSize=3')})));
    document.querySelector('#category-sections').innerHTML = sections.filter(s=>s.result.data.length).map(s=>`<section><div class="section-head"><h2>${esc(s.category.name)}</h2><a href="/chuyen-muc/${esc(s.category.slug)}">Xem tất cả →</a></div>${cards(s.result.data)}</section>`).join('');
}
async function listing(page = 1) {
    const params = new URLSearchParams(location.search); params.set('pageIndex', page); params.set('pageSize', 9);
    let title = 'Tìm kiếm & khám phá'; let path = 'articles';
    if (location.pathname.startsWith('/chuyen-muc/')) { const slug = decodeURIComponent(location.pathname.split('/')[2]); params.set('category',slug); title = categories.find(c=>c.slug===slug)?.name || 'Chuyên mục'; }
    if (location.pathname.startsWith('/tag/')) { params.set('tag',decodeURIComponent(location.pathname.split('/')[2])); title = 'Tin theo chủ đề'; }
    if (location.pathname === '/da-luu') { path = 'bookmarks'; title = 'Bài viết đã lưu'; }
    if (location.pathname === '/lich-su') { path = 'history'; title = 'Lịch sử đọc'; }
    const result = await api(path+'?'+params);
    app.innerHTML = `<div class="section-head"><h1>${esc(title)}</h1><span class="eyebrow">${result.totalCount} bài viết</span></div>${path==='articles'?`<form id="search-form" class="filters"><input name="search" placeholder="Từ khóa tìm kiếm" value="${esc(params.get('search')||'')}"><select name="category"><option value="">Tất cả chuyên mục</option>${categories.map(c=>`<option value="${esc(c.slug)}" ${c.slug===params.get('category')?'selected':''}>${esc(c.name)}</option>`).join('')}</select><select name="sortColumn"><option value="publishedAt">Mới nhất</option><option value="viewCount" ${params.get('sortColumn')==='viewCount'?'selected':''}>Đọc nhiều</option><option value="title" ${params.get('sortColumn')==='title'?'selected':''}>Tiêu đề</option></select><button class="button primary">Tìm kiếm</button></form>`:''}<div id="results">${cards(result.data)}</div>`;
    if (location.pathname.startsWith('/chuyen-muc/') && page === 1) {
        const featured = await api('articles/featured?category='+encodeURIComponent(params.get('category'))+'&pageSize=1');
        const lead = featured.data[0] || result.data[0];
        if (lead) document.querySelector('#results').insertAdjacentHTML('beforebegin', `<section class="category-lead"><span class="eyebrow">Tiêu điểm chuyên mục</span>${card(lead)}</section>`);
    }
    app.append(pager(result, listing));
    if (path === 'articles') document.querySelector('#search-form').onsubmit = e => { e.preventDefault(); location.href = '/tim-kiem?' + new URLSearchParams(new FormData(e.target)); };
    if (path === 'bookmarks') document.querySelectorAll('.card-news').forEach((el,i)=> { const b = document.createElement('button'); b.className='button'; b.textContent='Bỏ lưu'; b.onclick=async()=>{try{await api('bookmarks/'+result.data[i].id,'DELETE');await listing(page);}catch(e){toast(e.message);}}; el.append(b); });
}
async function detail() {
    const d = await api('articles/slug/'+encodeURIComponent(decodeURIComponent(location.pathname.split('/')[2]))); const a = d.article;
    app.innerHTML = `<article class="article-wrap"><div class="meta"><a href="/">Trang chủ</a> / <a href="/chuyen-muc/${esc(a.categorySlug)}">${esc(a.category)}</a></div><span class="eyebrow">${esc(a.category)}</span><h1>${esc(a.title)}</h1>${d.subtitle?`<h2>${esc(d.subtitle)}</h2>`:''}<p class="article-summary">${esc(a.summary)}</p><div class="meta">${esc(a.author)} · ${date(a.publishedAt)} · ${a.viewCount} lượt xem</div>${mediaImage(a)}<div class="article-body">${esc(d.content)}</div><div class="actions">${d.tags.map(t=>`<a class="pill" href="/tag/${esc(t.slug)}"># ${esc(t.name)}</a>`).join('')}</div><div class="actions"><button class="button" id="save">＋ Lưu bài</button><button class="button" id="share">↗ Chia sẻ</button>${['Like','Love','Useful','Wow'].map((r,i)=>`<button class="button react" data-type="${r}">${['Thích','Yêu thích','Hữu ích','Ấn tượng'][i]}</button>`).join('')}<button class="button" id="unreact">Bỏ cảm xúc</button></div><div id="reaction-count" class="meta"></div><div class="section-head"><h2>Ý kiến bạn đọc</h2></div>${d.allowComments ? me ? `<form id="comment-form"><label class="field"><span>Bình luận (chờ kiểm duyệt trước khi hiển thị)</span><textarea name="content" required minlength="2" maxlength="2000"></textarea></label><input name="parentCommentId" type="hidden"><p id="reply-info" class="meta"></p><p class="form-error"></p><button type="submit" class="button primary">Gửi bình luận</button><button type="button" id="cancel-reply" class="button">Hủy trả lời</button></form>` : '<a href="/dang-nhap" class="button">Đăng nhập để bình luận</a>' : '<p>Bài viết đã đóng bình luận.</p>'}<div id="comments"></div><div class="section-head"><h2>Đọc tiếp</h2></div><div id="related"></div></article>`;
    const loadReactions = async()=>{const counts=await api(`articles/${a.id}/reactions`);document.querySelector('#reaction-count').textContent=counts.map(r=>r.type+': '+r.count).join(' · ')||'Hãy là người đầu tiên bày tỏ cảm xúc.';};
    document.querySelector('#save').onclick = async()=>{try{await api('bookmarks/'+a.id,'POST');toast('Đã lưu bài viết.');}catch(e){toast(e.message);}};
    document.querySelector('#share').onclick=async()=>{try{await navigator.clipboard.writeText(location.href);toast('Đã sao chép liên kết.');}catch{toast(location.href);}};
    document.querySelectorAll('.react').forEach(b=>b.onclick=async()=>{try{await api(`articles/${a.id}/reaction`,'PUT',{reactionType:b.dataset.type});await loadReactions();}catch(e){toast(e.message);}});
    document.querySelector('#unreact').onclick=async()=>{try{await api(`articles/${a.id}/reaction`,'DELETE');await loadReactions();}catch(e){toast(e.message);}};
    async function comments(page=1) {
        const result=await api(`articles/${a.id}/comments?pageIndex=${page}&pageSize=10`); const target=document.querySelector('#comments');
        target.innerHTML=result.data.map(c=>`<div class="comment"><strong>${esc(c.user)}</strong><span class="meta"> · ${date(c.createdAt)} · ${esc(c.status)}${c.parentCommentId?' · Trả lời #'+c.parentCommentId:''}</span><p>${esc(c.content)}</p><div class="actions">${me&&d.allowComments&&c.status==='Approved'?`<button class="button reply" data-id="${c.id}">Trả lời</button>`:''}${me?.id===c.userId?`<button class="button edit-comment" data-id="${c.id}">Sửa</button><button class="button delete-comment" data-id="${c.id}">Xóa</button>`:''}</div></div>`).join('')||'<p class="empty">Chưa có bình luận.</p>';
        target.append(pager(result,comments));
        target.querySelectorAll('.reply').forEach(b=>b.onclick=()=>{document.querySelector('[name=parentCommentId]').value=b.dataset.id;document.querySelector('#reply-info').textContent='Đang trả lời bình luận #'+b.dataset.id;document.querySelector('[name=content]').focus();});
        target.querySelectorAll('.edit-comment').forEach(b=>b.onclick=async()=>{const c=result.data.find(c=>c.id===Number(b.dataset.id));const value=prompt('Sửa bình luận (sẽ được kiểm duyệt lại)',c.content);if(!value)return;try{await api('comments/'+c.id,'PUT',{content:value});await comments(page);}catch(e){toast(e.message);}});
        target.querySelectorAll('.delete-comment').forEach(b=>b.onclick=async()=>{if(!confirm('Xóa bình luận này?'))return;try{await api('comments/'+b.dataset.id,'DELETE');await comments(page);}catch(e){toast(e.message);}});
    }
    if (me && d.allowComments) {
        document.querySelector('#cancel-reply').onclick=()=>{document.querySelector('[name=parentCommentId]').value='';document.querySelector('#reply-info').textContent='';};
        bindForm('#comment-form',async(data,form)=>{await api(`articles/${a.id}/comments`,'POST',{content:data.content,parentCommentId:data.parentCommentId?Number(data.parentCommentId):null});form.reset();document.querySelector('#reply-info').textContent='';toast('Đã gửi bình luận chờ duyệt.');await comments();});
    }
    const related=await api('articles?category='+encodeURIComponent(a.categorySlug)+'&pageSize=4');document.querySelector('#related').innerHTML=cards(related.data.filter(x=>x.id!==a.id).slice(0,3));
    await Promise.all([comments(),loadReactions(),api(`articles/${a.id}/view`,'POST')]);
}
function auth(register=false) {
    app.innerHTML=`<div class="form-panel"><span class="eyebrow">Chào mừng đến Nhịp Sống</span><h1 class="mt-3">${register?'Tạo tài khoản':'Đăng nhập'}</h1><p class="muted">Lưu câu chuyện yêu thích và chia sẻ góc nhìn của bạn.</p><form id="auth-form">${register?field('Họ và tên','fullName','','text',true):''}${field('Email','email','','email',true)}${field('Mật khẩu'+(register?' (ít nhất 10 ký tự, chữ hoa, chữ thường, số và ký tự đặc biệt)':''),'password','','password',true)}<p class="form-error"></p><button type="submit" class="button primary w-100">${register?'Đăng ký':'Đăng nhập'} →</button></form><p class="mt-4"><a href="/${register?'dang-nhap':'dang-ky'}">${register?'Đã có tài khoản? Đăng nhập':'Chưa có tài khoản? Đăng ký'}</a></p></div>`;
    bindForm('#auth-form',async data=>{await api('auth/'+(register?'register':'login'),'POST',data);location.href='/';});
}
async function profile() {
    if(!me)throw new Error('Vui lòng đăng nhập để xem tài khoản.');
    app.innerHTML=`<div class="form-panel"><h1>Tài khoản</h1><p>${esc(me.email)}</p><p class="meta">${me.roles.map(esc).join(' · ')}</p><form id="profile-form">${field('Họ và tên','fullName',me.fullName,'text',true)}<label class="field"><span>Giới thiệu</span><textarea name="bio" maxlength="1000">${esc(me.bio)}</textarea></label><p class="form-error"></p><button class="button primary" type="submit">Lưu thông tin</button></form><div class="actions"><a href="/da-luu" class="button">Bài đã lưu</a><a href="/lich-su" class="button">Lịch sử đọc</a><a href="/thong-bao" class="button">Thông báo</a></div></div>`;
    bindForm('#profile-form',async data=>{me=await api('auth/me','PUT',data);toast('Đã cập nhật thông tin.');});
}
async function notifications(page=1) {
    const r=await api(`notifications?pageIndex=${page}&pageSize=12`);
    app.innerHTML='<div class="section-head"><h1>Thông báo</h1></div>'+r.data.map(n=>`<div class="comment"><h3>${esc(n.title)}</h3><p>${esc(n.message)}</p><div class="actions">${n.url&&n.url.startsWith('/')&&!n.url.startsWith('//')?`<a class="button" href="${esc(n.url)}">Xem</a>`:''}${n.isRead?'Đã đọc':`<button class="button read-notification" data-id="${n.id}">Đánh dấu đã đọc</button>`}</div></div>`).join('');
    app.append(pager(r,notifications));document.querySelectorAll('.read-notification').forEach(b=>b.onclick=async()=>{try{await api('notifications/'+b.dataset.id+'/read','PATCH');await notifications(page);}catch(e){toast(e.message);}});
}
async function boot() {
    document.querySelector('#today').textContent=new Date().toLocaleDateString('vi-VN',{weekday:'long',day:'numeric',month:'long',year:'numeric'});
    try {
        const results=await Promise.allSettled([api('auth/me'),api('categories?pageSize=100'),api('tags?pageSize=100')]);
        me=results[0].status==='fulfilled'?results[0].value:null;
        if(results[1].status==='rejected')throw results[1].reason;
        categories=results[1].value.data;tags=results[2].status==='fulfilled'?results[2].value.data:[];
        document.querySelector('#categories-nav').innerHTML='<a href="/">Trang chủ</a>'+categories.map(c=>`<a href="/chuyen-muc/${esc(c.slug)}">${esc(c.name)}</a>`).join('');
        if(me){document.querySelector('#account-nav').innerHTML=`<a href="/tai-khoan">${esc(me.fullName)}</a>${isStaff()?' · <a href="/cms">CMS</a>':''} · <button id="logout" class="button">Thoát</button>`;document.querySelector('#logout').onclick=async()=>{try{await api('auth/logout','POST');location.href='/';}catch(e){toast(e.message);}};}
        const settings=await api('settings');
        const footer=settings.find(s=>s.key==='FooterText');if(footer)document.querySelector('#footer-copy').textContent=footer.value;
        const siteName=settings.find(s=>s.key==='SiteName');if(siteName)document.querySelectorAll('.brand').forEach(el=>el.textContent=siteName.value);
        document.querySelector('#categories-nav').insertAdjacentHTML('beforeend','<a href="/cong-dong">Cộng đồng</a>');
        const p=location.pathname;
        if(p==='/')await home();else if(p.startsWith('/tin-tuc/'))await detail();else if(p==='/dang-nhap')auth();else if(p==='/dang-ky')auth(true);else if(p==='/tai-khoan')await profile();else if(p==='/thong-bao')await notifications();else if(p==='/cong-dong')await community();else if(p==='/cms')await cms();else await listing();
    } catch(e) { app.innerHTML=`<div class="empty"><h2>Không thể tải nội dung</h2><p>${esc(e.message)}</p><a class="button" href="/">Trang chủ</a> <a class="button" href="/dang-nhap">Đăng nhập</a><p class="meta">Nếu API chưa chạy, khởi động bằng scripts/Start-Development.ps1.</p></div>`; }
}
document.addEventListener('DOMContentLoaded',boot);
