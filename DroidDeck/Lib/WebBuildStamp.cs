using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace DroidDeck.Lib
{
    /// <summary>
    /// Endereço do configurador web muda a cada build: o index.html recebe o carimbo do
    /// main.dart.js e carrega tudo de /v/{carimbo}/..., que aqui volta a ser o arquivo normal.
    /// Assim o navegador nunca junta o JS novo com a fonte de ícones velha que ele guardou
    /// (ícone novo saía em branco mesmo com no-cache, até limpar o cache na mão).
    /// </summary>
    public static class WebBuildStamp
    {
        public const string Prefix = "/v/";
        public const string Placeholder = "__WEB_BUILD__";

        public static IApplicationBuilder UseWebBuildStamp(this IApplicationBuilder app, IWebHostEnvironment env)
        {
            return app.Use(async (ctx, next) =>
            {
                var path = ctx.Request.Path.Value ?? "";
                if (path.StartsWith(Prefix, StringComparison.Ordinal))
                {
                    int slash = path.IndexOf('/', Prefix.Length);
                    if (slash > 0) ctx.Request.Path = path[slash..];
                }
                else if (path is "/" or "/index.html" && HttpMethods.IsGet(ctx.Request.Method))
                {
                    var index = env.WebRootFileProvider.GetFileInfo("index.html");
                    if (index.Exists && index.PhysicalPath != null)
                    {
                        var html = await File.ReadAllTextAsync(index.PhysicalPath);
                        ctx.Response.ContentType = "text/html; charset=utf-8";
                        ctx.Response.Headers.CacheControl = "no-cache";
                        await ctx.Response.WriteAsync(html.Replace(Placeholder, Stamp(env)));
                        return;
                    }
                }
                await next();
            });
        }

        /// <summary>Data do main.dart.js: muda a cada deploy, mesmo com o DroidDeck aberto.</summary>
        public static string Stamp(IWebHostEnvironment env)
        {
            var js = env.WebRootFileProvider.GetFileInfo("main.dart.js");
            return js.Exists ? js.LastModified.UtcTicks.ToString("x") : "dev";
        }
    }
}
