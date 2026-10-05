'use strict';
const admin = () => me?.roles.includes('Administrator');
const guarded = action => async () => { try { await action(); } catch(e) { toast(e.message); } };
let cmsTarget;
async function cms() {
    if (!isStaff()) throw new Error('Bạn cần tài khoản tòa soạn để truy cập CMS.');
    const sections = [];
    if (admin()) sections.push(['dashboard','Tổng quan']);
    if (isEditor() || me.roles.includes('Journalist')) sections.push(['articles','Bài viết'],['media','Thư viện ảnh']);
    if (isEditor()) sections.push(['categories','Chuyên mục'],['tags','Thẻ chủ đề']);
    if (admin() || me.roles.includes('Moderator')) sections.push(['comments','Kiểm duyệt bình luận']);
    if (admin()) sections.push(['users','Người dùng'],['settings','Cài đặt'],['newsletter','Đăng ký bản tin'],['polls','Khảo sát']);
    const section = new URLSearchParams(location.search).get('section') || sections[0][0];
    if (!sections.some(s => s[0] === section)) throw new Error('Bạn không có quyền vào mục này.');
    app.innerHTML = `<div class="cms"><aside class="sidebar"><h3>Tòa soạn</h3>${sections.map(s=>`<a class="${s[0]===section?'active':''}" href="/cms?section=${s[0]}">${s[1]}</a>`).join('')}</aside><section id="cms-content"></section></div>`;
    cmsTarget = document.querySelector('#cms-content');
    await ({dashboard:cmsDashboard,articles:cmsArticles,media:cmsMedia,categories:()=>cmsTaxonomy('categories'),tags:()=>cmsTaxonomy('tags'),comments:cmsComments,users:cmsUsers,settings:cmsSettings,newsletter:cmsNewsletter,polls:cmsPolls}[section])();
}
async function cmsDashboard() {
    const d = await api('admin/dashboard');
    cmsTarget.innerHTML = `<h1>Tổng quan tòa soạn</h1><div class="stats">${[['articles','Bài viết'],['published','Đã xuất bản'],['draft','Bản nháp'],['pending','Chờ duyệt'],['users','Người dùng'],['comments','Bình luận'],['views','Lượt xem']].map(([k,l])=>`<div class="stat">${l}<strong>${d[k]}</strong></div>`).join('')}</div><h2>Bài viết theo chuyên mục</h2>${d.byCategory.map(c=>`<p>${esc(c.name)} · ${c.count}</p><div class="bar" style="width:${Math.max(1,100*c.count/Math.max(1,d.articles))}%"></div>`).join('')}`;
}
let articleFilters = '';
async function cmsArticles(page=1) {
    const r = await api(`articles/manage?pageIndex=${page}&pageSize=10${articleFilters}`);
    cmsTarget.innerHTML = `<div class="section-head"><h1>Bài viết</h1><button id="new-article" class="button primary">Viết bài mới</button></div><form id="cms-filter" class="filters"><input name="search" placeholder="Tìm bài viết"><select name="status"><option value="">Mọi trạng thái</option>${['Draft','PendingReview','Approved','Published','Rejected','Archived'].map(s=>`<option>${s}</option>`).join('')}</select><select name="category"><option value="">Mọi chuyên mục</option>${categories.map(c=>`<option value="${esc(c.slug)}">${esc(c.name)}</option>`).join('')}</select><input name="author" placeholder="ID tác giả"><select name="sortColumn"><option value="createdAt">Ngày tạo</option><option value="publishedAt">Ngày xuất bản</option><option value="title">Tiêu đề</option><option value="viewCount">Lượt xem</option></select><select name="sortOrder"><option value="desc">Giảm dần</option><option value="asc">Tăng dần</option></select><button class="button">Lọc</button></form><div class="table-wrap"><table><thead><tr><th>Bài viết</th><th>Tác giả / Chuyên mục</th><th>Trạng thái</th><th>Thao tác</th></tr></thead><tbody>${r.data.map(a=>`<tr><td>${a.thumbnailUrl?`<img width="70" alt="" src="/media/${esc(a.thumbnailUrl.split('/').pop())}">`:''}<strong>${esc(a.title)}</strong><p class="meta">Tạo: ${date(a.createdAt)}<br>Xuất bản: ${date(a.publishedAt)}</p></td><td>${esc(a.author)}<br>${esc(a.category)}</td><td>${esc(a.status)}</td><td><button class="button edit" data-id="${a.id}">Mở</button><button class="button delete" data-id="${a.id}">Xóa</button></td></tr>`).join('')}</tbody></table></div>`;
    cmsTarget.append(pager(r,p=>cmsArticles(p).catch(e=>toast(e.message))));
    const filter = document.querySelector('#cms-filter');
    new URLSearchParams(articleFilters).forEach((v,k)=>{if(filter.elements[k])filter.elements[k].value=v;});
    filter.onsubmit=e=>{e.preventDefault();articleFilters='&'+new URLSearchParams([...new FormData(filter)].filter(([,v])=>v));guarded(()=>cmsArticles())();};
    document.querySelector('#new-article').onclick=guarded(()=>cmsEdit());
    cmsTarget.querySelectorAll('.edit').forEach(b=>b.onclick=guarded(()=>cmsEdit(Number(b.dataset.id))));
    cmsTarget.querySelectorAll('.delete').forEach(b=>b.onclick=guarded(async()=>{if(confirm('Xóa bài viết và dữ liệu tương tác liên quan?')){await api('articles/'+b.dataset.id,'DELETE');await cmsArticles(page);}}));
}
async function cmsEdit(id) {
    const d=id?await api('articles/manage/'+id):null, a=d?.article||{};
    cmsTarget.innerHTML=`<h1>${id?'Biên tập bài viết':'Viết bài mới'}</h1>${d?.reviewNote?`<div class="notice">${esc(d.reviewNote)}</div>`:''}<form id="article-form"><div class="editor-grid"><div>${field('Tiêu đề','title',a.title,'text',true)}${field('Slug','slug',a.slug,'text',true)}${field('Tiêu đề phụ','subtitle',d?.subtitle)}<label class="field"><span>Tóm tắt</span><textarea name="summary" required maxlength="1000">${esc(a.summary)}</textarea></label><label class="field"><span>Nội dung (văn bản thuần)</span><textarea name="content" required minlength="20" maxlength="100000">${esc(d?.content)}</textarea></label></div><div><label class="field"><span>Chuyên mục</span><select name="categoryId">${categories.map(c=>`<option value="${c.id}" ${c.id===a.categoryId?'selected':''}>${esc(c.name)}</option>`).join('')}</select></label><label class="field"><span>Chủ đề (giữ Ctrl để chọn nhiều)</span><select name="tagIds" multiple>${tags.map(t=>`<option value="${t.id}" ${d?.tags.some(x=>x.id===t.id)?'selected':''}>${esc(t.name)}</option>`).join('')}</select></label>${field('Đường dẫn ảnh từ thư viện','thumbnailUrl',a.thumbnailUrl)}${field('SEO title','seoTitle',d?.seoTitle)}${field('SEO description','seoDescription',d?.seoDescription)}${[['allowComments','Cho phép bình luận',d?.allowComments??true],...(isEditor()?[['isFeatured','Nổi bật',a.isFeatured],['isBreakingNews','Tin nóng',a.isBreakingNews],['isEditorsChoice','Biên tập viên chọn',a.isEditorsChoice]]:[])].map(([n,l,v])=>`<label class="check"><input type="checkbox" name="${n}" ${v?'checked':''}> ${l}</label>`).join('')}<p>Trạng thái: ${esc(a.status||'Draft')}</p></div></div><p class="form-error"></p><button type="submit" class="button primary">Lưu bản thảo</button> <button type="button" id="preview" class="button">Xem trước</button> <button type="button" id="back" class="button">Về danh sách</button></form><div id="preview-content" class="article-wrap" hidden></div>${id?`<div class="actions">${['Draft','Rejected'].includes(a.status)?'<button class="button review" data-action="submit">Gửi duyệt</button>':''}${isEditor()&&a.status==='PendingReview'?'<button class="button review" data-action="approve">Duyệt bài</button><button class="button review" data-action="reject">Từ chối</button>':''}${isEditor()&&a.status==='Approved'?'<button class="button review" data-action="publish">Xuất bản ngay</button>':''}${isEditor()&&a.status==='Published'?'<button class="button review" data-action="archive">Lưu trữ</button>':''}<button class="button" id="revisions">Lịch sử sửa</button></div>${isEditor()&&a.status==='PendingReview'?field('Lịch xuất bản sau khi duyệt (tùy chọn)','schedule','','datetime-local'):''}<div id="revision-list"></div>`:''}`;
    bindForm('#article-form',async(data,form)=>{
        data.categoryId=Number(data.categoryId);data.tagIds=[...form.elements.tagIds.selectedOptions].map(o=>Number(o.value));
        ['allowComments','isFeatured','isBreakingNews','isEditorsChoice'].forEach(n=>data[n]=!!form.elements[n]?.checked);
        data.thumbnailUrl=data.thumbnailUrl||null;data.rowVersion=d?.rowVersion;
        const result=await api('articles'+(id?'/'+id:''),id?'PUT':'POST',data);toast('Đã lưu bài viết.');await cmsEdit(id||result.id);
    });
    document.querySelector('#back').onclick=guarded(()=>cmsArticles());
    document.querySelector('#preview').onclick=()=>{const f=document.querySelector('#article-form');const p=document.querySelector('#preview-content');p.hidden=false;p.innerHTML=`<h1>${esc(f.elements.title.value)}</h1><p>${esc(f.elements.summary.value)}</p><div class="article-body">${esc(f.elements.content.value)}</div>`;};
    cmsTarget.querySelectorAll('.review').forEach(b=>b.onclick=guarded(async()=>{
        const action=b.dataset.action,note=action==='reject'?prompt('Lý do từ chối'):null;if(action==='reject'&&!note)return;
        const schedule=document.querySelector('[name=schedule]')?.value;
        await api(`articles/${id}/review`,'POST',{action,note,scheduledAt:action==='approve'&&schedule?new Date(schedule).toISOString():null});await cmsEdit(id);
    }));
    if(id)document.querySelector('#revisions').onclick=guarded(async()=>{const r=await api(`articles/${id}/revisions?pageSize=100`);document.querySelector('#revision-list').innerHTML=r.data.map(v=>`<details class="comment"><summary>${date(v.createdAt)} · ${esc(v.title)}</summary><p>${esc(v.summary)}</p><div class="article-body">${esc(v.content)}</div></details>`).join('')||'Chưa có phiên bản trước.';});
}
async function cmsTaxonomy(kind,page=1) {
    const r=await api(`${kind}?all=true&pageIndex=${page}&pageSize=20`);
    cmsTarget.innerHTML=`<h1>${kind==='categories'?'Chuyên mục':'Thẻ chủ đề'}</h1><div id="taxonomy-form"></div><div class="table-wrap"><table><tbody>${r.data.map(c=>`<tr><td>${esc(c.name)}</td><td>${esc(c.slug)}</td><td><button class="button edit" data-id="${c.id}">Sửa</button><button class="button delete" data-id="${c.id}">Xóa</button></td></tr>`).join('')}</tbody></table></div>`;
    function form(c={}) {
        document.querySelector('#taxonomy-form').innerHTML=`<form id="tax-form">${field('Tên','name',c.name,'text',true)}${field('Slug','slug',c.slug,'text',true)}${kind==='categories'?`${field('Mô tả','description',c.description)}${field('ID chuyên mục cha (tùy chọn)','parentId',c.parentId,'number')}${field('Thứ tự','displayOrder',c.displayOrder||0,'number')}<label class="check"><input name="isActive" type="checkbox" ${c.isActive!==false?'checked':''}> Hoạt động</label>`:''}<p class="form-error"></p><button type="submit" class="button primary">${c.id?'Lưu thay đổi':'Thêm mới'}</button> <button id="clear-tax" type="button" class="button">Làm mới</button></form>`;
        document.querySelector('#clear-tax').onclick=()=>form();
        bindForm('#tax-form',async(data,f)=>{if(kind==='categories'){data.parentId=data.parentId?Number(data.parentId):null;data.displayOrder=Number(data.displayOrder);data.isActive=f.elements.isActive.checked;}await api(kind+(c.id?'/'+c.id:''),c.id?'PUT':'POST',data);categories=(await api('categories?pageSize=100')).data;tags=(await api('tags?pageSize=100')).data;await cmsTaxonomy(kind,page);});
    }
    form();cmsTarget.append(pager(r,p=>cmsTaxonomy(kind,p).catch(e=>toast(e.message))));
    cmsTarget.querySelectorAll('.edit').forEach(b=>b.onclick=()=>form(r.data.find(c=>c.id===Number(b.dataset.id))));
    cmsTarget.querySelectorAll('.delete').forEach(b=>b.onclick=guarded(async()=>{if(confirm('Xóa mục này?')){await api(kind+'/'+b.dataset.id,'DELETE');await cmsTaxonomy(kind,page);}}));
}
async function cmsComments(page=1) {
    const r=await api(`comments/manage?pageIndex=${page}&pageSize=15`);
    cmsTarget.innerHTML='<h1>Kiểm duyệt bình luận</h1>'+r.data.map(c=>`<div class="comment"><strong>${esc(c.user)}</strong> · Bài #${c.articleId} · ${esc(c.status)}<p>${esc(c.content)}</p>${['Approved','Rejected','Hidden'].map((s,i)=>`<button class="button moderate" data-id="${c.id}" data-status="${s}">${['Duyệt','Từ chối','Ẩn'][i]}</button>`).join('')} <button class="button delete" data-id="${c.id}">Xóa</button></div>`).join('');
    cmsTarget.append(pager(r,p=>cmsComments(p).catch(e=>toast(e.message))));
    cmsTarget.querySelectorAll('.moderate').forEach(b=>b.onclick=guarded(async()=>{await api(`comments/${b.dataset.id}/status`,'PATCH',{status:b.dataset.status});await cmsComments(page);}));
    cmsTarget.querySelectorAll('.delete').forEach(b=>b.onclick=guarded(async()=>{if(confirm('Xóa bình luận?')){await api('comments/'+b.dataset.id,'DELETE');await cmsComments(page);}}));
}
async function cmsMedia(page=1,search='') {
    const r=await api(`media?pageIndex=${page}&pageSize=12&search=${encodeURIComponent(search)}`);
    cmsTarget.innerHTML=`<h1>Thư viện ảnh</h1><form id="media-search" class="filters"><input name="search" placeholder="Tìm tên ảnh" value="${esc(search)}"><button class="button">Tìm</button></form><form id="upload-form"><label class="field"><span>JPG, PNG, WebP · tối đa 5 MB</span><input name="file" type="file" accept="image/jpeg,image/png,image/webp" required></label><p class="form-error"></p><button type="submit" class="button primary">Tải ảnh lên</button></form><div class="media-grid mt-4">${r.data.map(m=>`<div><img src="/media/${esc(m.url.split('/').pop())}" alt="${esc(m.originalFileName)}"><p>${esc(m.originalFileName)}</p><input class="form-control" aria-label="Đường dẫn ảnh" readonly value="${esc(m.url)}"><button class="button delete" data-id="${m.id}">Xóa</button></div>`).join('')}</div>`;
    document.querySelector('#media-search').onsubmit=e=>{e.preventDefault();guarded(()=>cmsMedia(1,new FormData(e.target).get('search')))();};
    bindForm('#upload-form',async(_,f)=>{await api('media','POST',new FormData(f));await cmsMedia();});
    cmsTarget.append(pager(r,p=>cmsMedia(p,search).catch(e=>toast(e.message))));
    cmsTarget.querySelectorAll('.delete').forEach(b=>b.onclick=guarded(async()=>{if(confirm('Xóa ảnh này?')){await api('media/'+b.dataset.id,'DELETE');await cmsMedia(page,search);}}));
}
async function cmsUsers(page=1,search='') {
    const r=await api(`admin/users?pageIndex=${page}&pageSize=15&search=${encodeURIComponent(search)}`);
    cmsTarget.innerHTML=`<h1>Người dùng</h1><form id="user-search" class="filters"><input name="search" value="${esc(search)}" placeholder="Tên hoặc email"><button class="button">Tìm</button></form><div class="table-wrap"><table><tbody>${r.data.map(u=>`<tr data-id="${esc(u.id)}"><td>${esc(u.fullName)}<br>${esc(u.email)}<p class="meta">${esc(u.id)}</p></td><td><select aria-label="Vai trò">${['Administrator','Editor','Journalist','Moderator','User'].map(role=>`<option ${u.roles.includes(role)?'selected':''}>${role}</option>`).join('')}</select><label><input type="checkbox" ${u.isActive?'checked':''}> Hoạt động</label></td><td><button class="button save-user" ${u.id===me.id?'disabled':''}>Lưu</button></td></tr>`).join('')}</tbody></table></div>`;
    document.querySelector('#user-search').onsubmit=e=>{e.preventDefault();guarded(()=>cmsUsers(1,new FormData(e.target).get('search')))();};
    cmsTarget.append(pager(r,p=>cmsUsers(p,search).catch(e=>toast(e.message))));
    const detail = document.createElement('section'); detail.id='user-detail'; detail.hidden=true; cmsTarget.append(detail);
    cmsTarget.querySelectorAll('tr[data-id]').forEach(tr=>{
        const button=document.createElement('button'); button.className='button user-detail'; button.textContent='Xem hồ sơ';
        button.onclick=guarded(async()=>{
            const u=await api('admin/users/'+encodeURIComponent(tr.dataset.id));
            detail.hidden=false;detail.innerHTML=`<h2>Hồ sơ người dùng</h2><dl><dt>Họ tên</dt><dd>${esc(u.fullName)}</dd><dt>Email</dt><dd>${esc(u.email)}</dd><dt>Giới thiệu</dt><dd>${esc(u.bio||'Chưa có giới thiệu')}</dd><dt>Vai trò</dt><dd>${u.roles.map(esc).join(', ')}</dd><dt>Trạng thái</dt><dd>${u.isActive?'Hoạt động':'Đã khóa'}</dd></dl><button class="button" type="button">Đóng hồ sơ</button>`;
            detail.querySelector('button').onclick=()=>detail.hidden=true;detail.scrollIntoView({block:'nearest'});
        });tr.lastElementChild.append(button);
    });
    cmsTarget.querySelectorAll('.save-user').forEach(b=>b.onclick=guarded(async()=>{const tr=b.closest('tr');await api('admin/users/'+tr.dataset.id,'PUT',{role:tr.querySelector('select').value,isActive:tr.querySelector('input').checked});toast('Đã cập nhật; phiên đăng nhập cũ đã thu hồi.');}));
}
async function cmsSettings() {
    const r=await api('admin/settings');
    cmsTarget.innerHTML=`<h1>Cài đặt</h1><form id="settings-form">${field('Tên website','SiteName',r.find(x=>x.key==='SiteName')?.value||'Nhịp Sống','text',true)}${field('Nội dung chân trang','FooterText',r.find(x=>x.key==='FooterText')?.value||'Góc nhìn mỗi ngày','text',true)}<p class="form-error"></p><button type="submit" class="button primary">Lưu</button></form>`;
    bindForm('#settings-form',async data=>{for(const [key,value] of Object.entries(data))await api('admin/settings/'+key,'PUT',{value});toast('Đã lưu cài đặt.');});
}
