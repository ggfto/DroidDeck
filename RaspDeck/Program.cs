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
            // Cancellation token to coordinate shutdown between WinForms and the web server
            var cts = new CancellationTokenSource();

            // Inicializa e roda o servidor web em paralelo, observando o token
            var webTask = RunWebAppAsync(args, cts.Token);

            // Inicializa WinForms
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
                        services.AddAuthorization();

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
                            Program.FileLog($"[Discovery] Config Check: EnableDiscovery='{enableDiscovery}'");
                            if (!string.IsNullOrEmpty(enableDiscovery) && enableDiscovery.ToLowerInvariant() == "true")
                            {
                                services.AddHostedService<AnyDeck.Lib.DiscoveryServer>();
                                Program.FileLog("[Discovery] Service REGISTERED.");
                            }
                            else
                            {
                                Program.FileLog("[Discovery] Service SKIPPED (Disabled in config).");
                            }
                        }
                        catch (Exception ex)
                        {
                            Program.FileLog($"[Discovery] Error reading config: {ex.Message}");
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
                            app.UseCors("_myAllowSpecificOrigins");
                            app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", $"{appName} {version}"));
                        }

                        // Enable static files for Web Editor
                        app.UseDefaultFiles();
                        app.UseStaticFiles();

                        app.UseRouting();
                        app.UseAuthentication();
                        app.UseAuthorization();
                        app.UseAuthorization();
                        app.UseEndpoints(endpoints =>
                        {
                            endpoints.MapControllers();
                            endpoints.MapHub<AnyDeck.Hubs.DeckHub>("/deckHub");
                        });
                    });
                });
        }

        public static void FileLog(string message)
        {
            try
            {
                File.AppendAllText("discovery_debug.txt", $"{DateTime.Now}: {message}\n");
            }
            catch { }
        }

        private static async Task RunWebAppAsync(string[] args, CancellationToken token)
        {
            var host = CreateHostBuilder(args).Build();
            await host.RunAsync(token);
        }
    }
}
