using System.Reflection;
using System.Diagnostics;
using System.IO;
using AnyDeck.Lib;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using NLog.Extensions.Logging;
using Microsoft.OpenApi.Models;

namespace AnyDeck
{
    public class Program
    {
        public static frmPrincipal? FrmPrincipal { get; private set; }

        [STAThread]
        public static void Main(string[] args)
        {
            // Modo headless: sem janela/bandeja, roda apenas o servidor web.
            // Útil para autostart silencioso, execução sem desktop ou depuração.
            // Ctrl+C encerra graciosamente via ConsoleLifetime do host.
            // Diagnóstico/headless de pareamento: imprime a URI e salva o QR em PNG, depois sai.
            if (args.Contains("--print-pairing"))
            {
                var uri = Lib.PairingInfo.BuildUri();
                Console.WriteLine(uri);
                var pngPath = Path.Combine(Path.GetTempPath(), "anydeck-pair.png");
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

        public static IHostBuilder CreateHostBuilder(string[] args)
        {
            return Host.CreateDefaultBuilder(args)
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
                                .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, AnyDeck.Auth.ApiKeyAuthenticationHandler>("ApiKey", null);
                        services.AddAuthorization(options =>
                        {
                            // Exige a API key em TODOS os endpoints, exceto os marcados [AllowAnonymous].
                            options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
                                .RequireAuthenticatedUser()
                                .Build();
                        });

                        services.AddSingleton<AnyDeck.Audio.IAudioDeviceEnumerator, AnyDeck.Audio.NAudioDeviceEnumerator>();
                        services.AddSingleton<AnyDeck.Services.MixerService>();

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
                                services.AddHostedService<AnyDeck.Lib.DiscoveryServer>();
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
                                policy.SetIsOriginAllowed(origin => true) // Allow any origin
                                      .AllowAnyHeader()
                                      .AllowAnyMethod()
                                      .AllowCredentials(); // Allow credentials (cookies/auth)
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
                            services.AddSingleton<AnyDeck.Services.IAppActivator, AnyDeck.Services.AppActivator>();
                            services.AddSingleton<Services.MediaControlService>();
                            services.AddSingleton<Services.StreamDeckConfigService>();
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

                            endpoints.MapControllers();
                            endpoints.MapHub<AnyDeck.Hubs.DeckHub>("/deckHub");
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
