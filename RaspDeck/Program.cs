using System.Reflection;
using System.Diagnostics;
using System.IO;
using DroidDeck.Lib;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using NLog.Extensions.Logging;
using Microsoft.OpenApi.Models;

namespace DroidDeck
{
    public class Program
    {
        public static frmPrincipal? FrmPrincipal { get; private set; }

        [STAThread]
        public static void Main(string[] args)
        {
            // Bind em todas as interfaces — senão o celular na mesma rede recebe "connection
            // refused" (Kestrel sozinho escuta só em localhost). Em dev o launchSettings já
            // define ASPNETCORE_URLS, mas o .exe publicado não lê launchSettings, então
            // garantimos aqui. O usuário ainda pode sobrescrever via variável de ambiente.
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ASPNETCORE_URLS")))
            {
                Environment.SetEnvironmentVariable("ASPNETCORE_URLS", "http://0.0.0.0:5000");
            }

            // Modo headless: sem janela/bandeja, roda apenas o servidor web.
            // Útil para autostart silencioso, execução sem desktop ou depuração.
            // Ctrl+C encerra graciosamente via ConsoleLifetime do host.
            // Diagnóstico/headless de pareamento: imprime a URI e salva o QR em PNG, depois sai.
            if (args.Contains("--print-pairing"))
            {
                var uri = Lib.PairingInfo.BuildUri();
                Console.WriteLine(uri);
                var pngPath = Path.Combine(Path.GetTempPath(), "droiddeck-pair.png");
                File.WriteAllBytes(pngPath, Lib.PairingInfo.BuildQrPng(uri));
                Console.WriteLine("QR_PNG=" + pngPath);
                return;
            }

            if (args.Contains("--headless") || args.Contains("--no-tray"))
            {
                RunWebAppAsync(args, CancellationToken.None).GetAwaiter().GetResult();
                return;
            }

            // Cancellation token to coordinate shutdown between WinForms and the web server
            var cts = new CancellationTokenSource();

            // Inicializa e roda o servidor web em paralelo, observando o token
            var webTask = RunWebAppAsync(args, cts.Token);

            // App de bandeja: a janela fica invisível e toda a interação é pelo ícone na bandeja.
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            FrmPrincipal = new frmPrincipal();

            Application.Run(FrmPrincipal);

            // Solicita encerramento gracioso do servidor web e discovery
            try
            {
                cts.Cancel();
            }
            catch { }

            // Garante que o servidor finalize junto
            webTask.Wait();
        }

        /// <summary>
        /// True se a origem (header Origin de uma requisição de navegador) é o próprio PC
        /// (loopback) ou um IP de LAN privada — de onde o configurador web legítimo roda.
        /// Hostnames públicos (ex.: evil.com) retornam false, barrando CORS de sites externos.
        /// </summary>
        private static bool IsLocalOrLanOrigin(string origin)
        {
            if (string.IsNullOrEmpty(origin)) return false;
            if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri)) return false;
            if (uri.IsLoopback) return true;
            if (!System.Net.IPAddress.TryParse(uri.Host, out var ip)) return false; // hostname público → barra
            var b = ip.GetAddressBytes();
            if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && b.Length == 4)
            {
                return b[0] == 10                                   // 10.0.0.0/8
                    || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)    // 172.16.0.0/12
                    || (b[0] == 192 && b[1] == 168)                 // 192.168.0.0/16
                    || (b[0] == 169 && b[1] == 254)                 // 169.254.0.0/16 (link-local)
                    || b[0] == 127;                                 // 127.0.0.0/8
            }
            if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
                return ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal
                    || (b.Length == 16 && (b[0] & 0xFE) == 0xFC);   // fc00::/7 (ULA)
            return false;
        }

        public static IHostBuilder CreateHostBuilder(string[] args)
        {
            // Em dev (dotnet run a partir de RaspDeck/) o wwwroot fonte está no diretório
            // atual; no .exe publicado, ao lado do executável. Preferir o diretório atual
            // evita servir uma cópia velha de bin/ (que o build cria ao copiar o wwwroot).
            var cwd = Directory.GetCurrentDirectory();
            var contentRoot = File.Exists(Path.Combine(cwd, "wwwroot", "index.html"))
                ? cwd
                : AppContext.BaseDirectory;
            return Host.CreateDefaultBuilder(args)
                .UseContentRoot(contentRoot)
                .ConfigureLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.AddConsole();
                    try
                    {
                        logging.AddNLog();
                    }
                    catch { }
                })
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.ConfigureServices(services =>
                    {
                        // Variáveis de app/versão
                        services.AddControllers()
                                .AddJsonOptions(options =>
                                {
                                    options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
                                    options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
                                });

                        // Authentication: API Key
                        services.AddAuthentication("ApiKey")
                                .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, DroidDeck.Auth.ApiKeyAuthenticationHandler>("ApiKey", null);
                        services.AddAuthorization(options =>
                        {
                            // Exige a API key em TODOS os endpoints, exceto os marcados [AllowAnonymous].
                            options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
                                .RequireAuthenticatedUser()
                                .Build();
                        });

                        services.AddSingleton<DroidDeck.Audio.IAudioDeviceEnumerator, DroidDeck.Audio.NAudioDeviceEnumerator>();
                        services.AddSingleton<DroidDeck.Services.MixerService>();

                        // Conditional Discovery registration (respects env/config)
                        try
                        {
                            var cfg = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
                                        .AddJsonFile("appsettings.json", optional: true)
                                        .AddEnvironmentVariables()
                                        .Build();
                            var enableDiscovery = cfg["EnableDiscovery"];
                            if (!string.IsNullOrEmpty(enableDiscovery) && enableDiscovery.ToLowerInvariant() == "true")
                            {
                                services.AddHostedService<DroidDeck.Lib.DiscoveryServer>();
                                Console.WriteLine("[Discovery] Serviço registrado.");
                            }
                            else
                            {
                                Console.WriteLine("[Discovery] Serviço desativado (config).");
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[Discovery] Erro lendo config: {ex.Message}");
                        }

                        services.AddSwaggerGen(c =>
                        {
                            string appName = Assembly.GetExecutingAssembly().GetName().Name!;
                            string version = "v" + Assembly.GetExecutingAssembly().GetName().Version!.ToString().Replace(".", "").Replace("0", "");
                            c.SwaggerDoc("v1", new OpenApiInfo { Title = appName, Version = version });
                        });
                        services.AddCors(options =>
                        {
                            options.AddPolicy(name: "_myAllowSpecificOrigins", policy =>
                            {
                                // SEGURANÇA: não refletir qualquer origem. Só o próprio host
                                // (loopback) e IPs de LAN privada — que é de onde o configurador
                                // web legítimo roda. Sites públicos (evil.com) são barrados, o que
                                // impede um site que a vítima visite de ler respostas da API.
                                // Sem AllowCredentials: a auth é por header X-API-KEY, não cookie.
                                policy.SetIsOriginAllowed(IsLocalOrLanOrigin)
                                      .AllowAnyHeader()
                                      .AllowAnyMethod();
                            });
                        });

                        // Register audio control service (Windows-only)
                        if (System.OperatingSystem.IsWindows())
                        {
                            services.AddSingleton<Services.IAudioControlService, Services.AudioControlService>();
                        }

                        // Register the activator service only on Windows (it uses Win32 APIs)
                        if (System.OperatingSystem.IsWindows())
                        {
                            services.AddSingleton<DroidDeck.Services.IAppActivator, DroidDeck.Services.AppActivator>();
                            services.AddSingleton<Services.MediaControlService>();
                            services.AddSingleton<Services.StreamDeckConfigService>();
                            services.AddSingleton<Services.DiscordRpcService>();
                            services.AddHostedService<Services.DiscordAutoConnect>();
                            services.AddSingleton<Services.ObsService>();
                            services.AddHostedService<Services.ObsAutoConnect>();
                            services.AddSingleton<Services.ActionExecutorService>();
                            services.AddHostedService<Services.SystemMonitorService>();
                        }

                        services.AddSignalR();
                    });

                    webBuilder.Configure(app =>
                    {
                        var env = app.ApplicationServices.GetRequiredService<IHostEnvironment>();
                        string appName = Assembly.GetExecutingAssembly().GetName().Name!;
                        string version = "v" + Assembly.GetExecutingAssembly().GetName().Version!.ToString().Replace(".", "").Replace("0", "");

                        if (env.IsDevelopment())
                        {
                            app.UseDeveloperExceptionPage();
                            app.UseSwagger();
                            app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", $"{appName} {version}"));
                        }

                        // Enable static files for Web Editor
                        app.UseDefaultFiles();
                        app.UseStaticFiles();

                        app.UseRouting();
                        app.UseCors("_myAllowSpecificOrigins"); // CORS em qualquer ambiente (web client)
                        app.UseAuthentication();
                        app.UseAuthorization();
                        app.UseEndpoints(endpoints =>
                        {
                            // Endpoint anônimo para o app checar conectividade (antes de ter a chave).
                            endpoints.MapGet("/api/ping", () =>
                                Microsoft.AspNetCore.Http.Results.Ok(new { ok = true, name = Environment.MachineName }))
                                .AllowAnonymous();

                            // Entrega a API key APENAS para quem acessa via loopback (o próprio PC).
                            // Deixa o configurador web (servido pelo PC) autenticar sem QR.
                            endpoints.MapGet("/api/pairing/local-key", (Microsoft.AspNetCore.Http.HttpContext ctx) =>
                            {
                                var ip = ctx.Connection.RemoteIpAddress;
                                if (ip == null || !System.Net.IPAddress.IsLoopback(ip))
                                    return Microsoft.AspNetCore.Http.Results.StatusCode(403);

                                // SEGURANÇA: bloqueia leitura por site cross-site (o navegador da
                                // vítima faz fetch a partir de 127.0.0.1, mas o browser marca a
                                // requisição). Sec-Fetch-Site é um header proibido — JS não forja.
                                // Fetch de outro site vem "cross-site"; o configurador servido aqui
                                // vem "same-origin"/"none". Sem esse gate, qualquer site leria a chave.
                                var secFetchSite = ctx.Request.Headers["Sec-Fetch-Site"].ToString();
                                if (!string.IsNullOrEmpty(secFetchSite) &&
                                    secFetchSite != "same-origin" && secFetchSite != "none")
                                    return Microsoft.AspNetCore.Http.Results.StatusCode(403);
                                var origin = ctx.Request.Headers["Origin"].ToString();
                                if (!string.IsNullOrEmpty(origin) && !IsLocalOrLanOrigin(origin))
                                    return Microsoft.AspNetCore.Http.Results.StatusCode(403);

                                return Microsoft.AspNetCore.Http.Results.Ok(new { key = DroidDeck.Auth.ApiKeyProvider.GetKey() });
                            }).AllowAnonymous();

                            endpoints.MapControllers();
                            endpoints.MapHub<DroidDeck.Hubs.DeckHub>("/deckHub");
                        });
                    });
                });
        }

        private static async Task RunWebAppAsync(string[] args, CancellationToken token)
        {
            var host = CreateHostBuilder(args).Build();
            await host.RunAsync(token);
        }
    }
}
