using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NewsPortal.Api.DTOs;
using NewsPortal.Api.Services;
namespace NewsPortal.Api.Controllers;

[ApiController, Route("api/v1/articles")]
public class ArticlesController(ArticleService service) : ControllerBase
{
    [HttpGet] public Task<PageDto<ArticleListDto>> List([FromQuery] ArticleQuery query) => service.ListAsync(query, User);
    [HttpGet("manage"), Authorize(Roles = "Administrator,Editor,Journalist")]
    public Task<PageDto<ArticleListDto>> Manage([FromQuery] ArticleQuery query) => service.ListAsync(query, User, true);
    [HttpGet("manage/{id:int}"), Authorize(Roles = "Administrator,Editor,Journalist")]
    public Task<ArticleDetailDto> Edit(int id) => service.DetailAsync(id, null, User, true);
    [HttpGet("{id:int}")] public Task<ArticleDetailDto> Get(int id) => service.DetailAsync(id, null, User);
    [HttpGet("slug/{slug}")] public Task<ArticleDetailDto> Slug(string slug) => service.DetailAsync(null, slug, User);
    [HttpGet("featured")] public Task<PageDto<ArticleListDto>> Featured([FromQuery] ArticleQuery q) { q.Featured = true; return service.ListAsync(q, User); }
    [HttpGet("breaking")] public Task<PageDto<ArticleListDto>> Breaking([FromQuery] ArticleQuery q) { q.Breaking = true; return service.ListAsync(q, User); }
    [HttpGet("latest")] public Task<PageDto<ArticleListDto>> Latest([FromQuery] ArticleQuery q) { q.SortColumn = "publishedAt"; return service.ListAsync(q, User); }
    [HttpGet("popular")] public Task<PageDto<ArticleListDto>> Popular([FromQuery] ArticleQuery q) { q.SortColumn = "viewCount"; return service.ListAsync(q, User); }
    [HttpGet("category/{slug}")] public Task<PageDto<ArticleListDto>> Category(string slug, [FromQuery] ArticleQuery q) { q.Category = slug; return service.ListAsync(q, User); }
    [HttpGet("tag/{slug}")] public Task<PageDto<ArticleListDto>> Tag(string slug, [FromQuery] ArticleQuery q) { q.Tag = slug; return service.ListAsync(q, User); }
    /// <summary>Create a draft owned by the authenticated author. The status cannot be set by the client.</summary>
    [ProducesResponseType(typeof(CreatedResourceDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [HttpPost, Authorize(Roles = "Administrator,Editor,Journalist")]
    public async Task<IActionResult> Create(ArticleInput input) { var id = await service.SaveAsync(null, input, User); return CreatedAtAction(nameof(Edit), new { id }, new { id }); }
    /// <summary>Update an article using its current RowVersion. Stale versions return 409.</summary>
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [HttpPut("{id:int}"), Authorize(Roles = "Administrator,Editor,Journalist")]
    public async Task<IActionResult> Update(int id, ArticleInput input) { await service.SaveAsync(id, input, User); return NoContent(); }
    /// <summary>Delete an article and its interactions. Journalists may only delete their own drafts.</summary>
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [HttpDelete("{id:int}"), Authorize(Roles = "Administrator,Editor,Journalist")]
    public async Task<IActionResult> Delete(int id) { await service.DeleteAsync(id, User); return NoContent(); }
}
