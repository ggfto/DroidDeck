using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using System.Reflection;

namespace AnyDeck
{
    class Startup
    {
        readonly string MyAllowSpecificOrigins = "_myAllowSpecificOrigins";
        public string AppName { get; }
        public string Version { get; }

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
            AppName = Assembly.GetExecutingAssembly().GetName().Name ?? "AnyDeck";
            Version = "v" + (Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0");
        }

        public IConfiguration Configuration { get; }
        public void ConfigureServices(IServiceCollection services)
        {

            services.AddControllers();
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = AppName, Version = Version });
            });
            services.AddCors(options =>
            {
                options.AddPolicy(name: MyAllowSpecificOrigins,
                            builder =>
                            {
                                builder.WithOrigins("http://localhost:5000");
                            });
            });
        }
        public void Configure(IApplicationBuilder app, IHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseCors(MyAllowSpecificOrigins);
                app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", AppName + " " + Version));
            }
            app.UseRouting();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}
