using System.Net.Http.Headers;

namespace WebApplication1.Logic;

public sealed class ApiAuthHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _http;

    public ApiAuthHandler(IHttpContextAccessor http)
    {
        _http = http;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var ctx = _http.HttpContext;

        if (ctx != null && ctx.Request.Cookies.TryGetValue("agro_token", out var token))
        {
            if (!string.IsNullOrWhiteSpace(token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
        }

        return base.SendAsync(request, cancellationToken);
    }
}
