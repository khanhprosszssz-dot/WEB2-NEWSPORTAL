using System.Text.Json;
using System.Xml.Linq;
using Microsoft.AspNetCore.Mvc;
using NewsPortal.Web.Services;
namespace NewsPortal.Web.Controllers;
public class FeedsController(PortalApiClient api) : Controller
{
    [HttpGet("/rss")]
    public async Task<IActionResult> Rss()
    {
        using var response = await api.SendAsync(HttpContext, HttpMethod.Get, "api/v1/articles/latest?pageSize=50");
        if (!response.IsSuccessStatusCode) return StatusCode(503);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var origin = $"{Request.Scheme}://{Request.Host}";
        var channel = new XElement("channel", new XElement("title", "Nhịp Sống"), new XElement("link", origin), new XElement("description", "Tin mới nhất — nội dung demo"));
        foreach (var a in doc.RootElement.GetProperty("data").EnumerateArray()) {
            var link = origin + "/tin-tuc/" + Uri.EscapeDataString(a.GetProperty("slug").GetString()!);
            channel.Add(new XElement("item", new XElement("title", a.GetProperty("title").GetString()), new XElement("link", link), new XElement("guid", link), new XElement("description", a.GetProperty("summary").GetString()), new XElement("pubDate", a.GetProperty("publishedAt").GetDateTime().ToUniversalTime().ToString("r"))));
        }
        return Content(new XDocument(new XElement("rss", new XAttribute("version", "2.0"), channel)).ToString(), "application/rss+xml; charset=utf-8");
    }
    [HttpGet("/robots.txt")]
    public IActionResult Robots() => Content($"User-agent: *\nDisallow: /cms\nDisallow: /gateway/\nSitemap: {Request.Scheme}://{Request.Host}/sitemap.xml\n", "text/plain");

    [HttpGet("/sitemap.xml")]
    public async Task<IActionResult> Sitemap()
    {
        XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
        var origin = $"{Request.Scheme}://{Request.Host}";
        var root = new XElement(ns + "urlset", new XElement(ns + "url", new XElement(ns + "loc", origin + "/")));
        for (var page = 1; ; page++) {
            using var response = await api.SendAsync(HttpContext, HttpMethod.Get, $"api/v1/articles?pageSize=100&pageIndex={page}");
            if (!response.IsSuccessStatusCode) return StatusCode(503);
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            foreach (var a in doc.RootElement.GetProperty("data").EnumerateArray())
                root.Add(new XElement(ns + "url", new XElement(ns + "loc", origin + "/tin-tuc/" + Uri.EscapeDataString(a.GetProperty("slug").GetString()!))));
            if (page >= doc.RootElement.GetProperty("totalPages").GetInt32()) break;
        }
        return Content(new XDocument(root).ToString(), "application/xml; charset=utf-8");
    }
}
