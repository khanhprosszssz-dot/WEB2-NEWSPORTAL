using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using NewsPortal.Web.Services;
namespace NewsPortal.Web.Controllers;
public class PortalController(PortalApiClient api) : Controller
{
    [HttpGet("/"), HttpGet("/chuyen-muc/{slug}"), HttpGet("/tag/{slug}"), HttpGet("/tim-kiem"),
     HttpGet("/dang-nhap"), HttpGet("/dang-ky"), HttpGet("/tai-khoan"), HttpGet("/da-luu"),
     HttpGet("/lich-su"), HttpGet("/thong-bao"), HttpGet("/cong-dong"), HttpGet("/cms")]
    public IActionResult Index() => View("~/Views/Portal/Index.cshtml");

    [HttpGet("/tin-tuc/{slug}")]
    public async Task<IActionResult> Article(string slug)
    {
        using var response = await api.SendAsync(HttpContext, HttpMethod.Get, "api/v1/articles/slug/" + Uri.EscapeDataString(slug));
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) { Response.StatusCode = 404; ViewData["Missing"] = true; }
        else if (response.IsSuccessStatusCode) {
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync()); var root = doc.RootElement;
            ViewData["Title"] = root.GetProperty("seoTitle").GetString() ?? root.GetProperty("article").GetProperty("title").GetString();
            ViewData["Description"] = root.GetProperty("seoDescription").GetString() ?? root.GetProperty("article").GetProperty("summary").GetString();
        }
        return View("~/Views/Portal/Index.cshtml");
    }
}
