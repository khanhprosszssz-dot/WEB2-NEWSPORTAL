// Runs only inside the isolated QA Chrome profile, never served by the website.
window.qa = {
    assert(value, message) { if (!value) throw new Error(message); },
    async wait(check, message = 'UI did not reach the expected state') {
        for (let i=0;i<100;i++) { if (await check()) return; await new Promise(r=>setTimeout(r,100)); }
        throw new Error(message);
    },
    fill(selector, values) {
        const form=document.querySelector(selector); this.assert(form, 'Missing form '+selector);
        for (const [name,value] of Object.entries(values)) {
            const el=form.elements[name]; this.assert(el, 'Missing field '+name);
            if(el.type==='checkbox') el.checked=value; else el.value=value;
            el.dispatchEvent(new Event('input',{bubbles:true})); el.dispatchEvent(new Event('change',{bubbles:true}));
        }
        return form;
    },
    click(selector) { const el=document.querySelector(selector); this.assert(el&&!el.disabled,'Unavailable control '+selector); el.click(); },
    async createArticle(data) {
        this.click('#new-article'); await this.wait(()=>document.querySelector('#article-form'));
        const form=this.fill('#article-form',{title:data.title,slug:data.slug,summary:'Browser workflow summary',content:'Browser workflow content with enough text. <img src=x onerror="window.qaXss=true">',thumbnailUrl:data.image,allowComments:true,seoTitle:'QA SEO title',seoDescription:'QA SEO description'});
        this.click('#preview'); this.assert(document.querySelector('#preview-content h1').textContent===data.title,'Preview title mismatch');
        this.assert(!document.querySelector('#preview-content img')&&!window.qaXss,'Preview interpreted HTML content');
        form.requestSubmit(); await this.wait(()=>document.querySelector('.review[data-action="submit"]'),'Create article failed');
        const found=await api('articles/manage?search='+encodeURIComponent(data.title));
        const article=found.data.find(a=>a.slug===data.slug); this.assert(article,'Created article not persisted');
        const edited=this.fill('#article-form',{title:data.title+' edited'}); edited.requestSubmit();
        await this.wait(async()=> (await api('articles/manage/'+article.id)).article.title.endsWith(' edited'));
        await this.wait(()=>!document.querySelector('#article-form button[type=submit]').disabled);
        this.click('#revisions'); await this.wait(()=>document.querySelector('#revision-list details'),'Revision history missing');
        this.click('.review[data-action="submit"]'); await this.wait(()=>!document.querySelector('.review[data-action="submit"]'));
        this.assert(!document.querySelector('.review[data-action="approve"]'),'Journalist can see approve control');
        return article.id;
    },
    async openArticle(id) { this.click('.edit[data-id="'+id+'"]'); await this.wait(()=>document.querySelector('#article-form')); },
    async review(action) {
        if(action==='reject') { const original=window.prompt; window.prompt=()=> 'QA: revise before publication'; this.click('.review[data-action="reject"]'); window.prompt=original; }
        else this.click('.review[data-action="'+action+'"]');
        await this.wait(()=>!document.querySelector('.review[data-action="'+action+'"]'));
    },
    async reader(data) {
        this.click('#save'); await this.wait(async()=> (await api('bookmarks')).data.some(a=>a.id===data.id));
        this.click('.react[data-type="Useful"]'); await this.wait(()=>document.querySelector('#reaction-count').textContent.includes('Useful'));
        this.fill('#comment-form',{content:'QA browser comment'}).requestSubmit();
        await this.wait(async()=> (await api('articles/'+data.id+'/comments')).data.some(c=>c.content==='QA browser comment'));
        const result=await api('articles/'+data.id+'/comments'); return result.data.find(c=>c.content==='QA browser comment').id;
    },
    async taxonomy(kind,slug) {
        this.fill('#tax-form',{name:'QA '+kind,slug}).requestSubmit();
        await this.wait(()=>[...document.querySelectorAll('tbody tr')].some(tr=>tr.textContent.includes(slug)));
        let row=[...document.querySelectorAll('tbody tr')].find(tr=>tr.textContent.includes(slug)); const id=Number(row.querySelector('.edit').dataset.id);
        row.querySelector('.edit').click(); this.fill('#tax-form',{name:'QA edited '+kind}).requestSubmit();
        await this.wait(()=>[...document.querySelectorAll('tbody tr')].some(tr=>tr.textContent.includes('QA edited '+kind)));
        const original=window.confirm; window.confirm=()=>true; document.querySelector('.delete[data-id="'+id+'"]').click();window.confirm=original;
        await this.wait(()=>!document.querySelector('.delete[data-id="'+id+'"]')); return true;
    },
    async poll(question) {
        this.fill('#poll-form',{question,options:'First choice\nSecond choice'}).requestSubmit();
        await this.wait(()=>[...document.querySelectorAll('.comment h3')].some(h=>h.textContent===question));
        const all=await api('polls?all=true'); return all.data.find(p=>p.question===question).id;
    },
    async access(id,role,active) {
        const row=document.querySelector('tr[data-id="'+id+'"]'); this.assert(row,'QA user not found');
        row.querySelector('select').value=role; row.querySelector('input').checked=active; row.querySelector('.save-user').click();
        await this.wait(async()=>{const u=await api('admin/users/'+id);return u.isActive===active&&u.roles.includes(role);});
    },
    async pagerFailure() {
        let count=0;const p=pager({pageIndex:1,pageSize:1,totalCount:2},async()=>{count++;throw new Error('QA pagination failure');});
        document.querySelector('#app').append(p);p.children[2].click();p.children[2].click();
        await this.wait(()=>!p.children[2].disabled);this.assert(count===1,'Duplicate pagination request');
        this.assert(document.querySelector('#toast').textContent==='QA pagination failure','Pagination error was not shown');p.remove();
    }
};
