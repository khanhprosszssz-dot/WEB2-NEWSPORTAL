using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authentication;
namespace NewsPortal.Web.Services;
public class PortalApiClient(HttpClient http)
{
    public async Task<HttpResponseMessage> SendAsync(HttpContext context, HttpMethod method, string path, HttpContent? content = null)
    {
        var request = new HttpRequestMessage(method, path) { Content = content };
        var token = await context.GetTokenAsync("access_token");
        if (token != null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, context.RequestAborted);
    }
}
