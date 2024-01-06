using Fido2NetLib;
using Fido2NetLib.Development; // Use the appropriate namespace for your IMetadataService
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace WebAuthn3
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddDistributedMemoryCache();


            

            builder.Services.AddSession(options =>
            {
                options.IdleTimeout = TimeSpan.FromMinutes(2);
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Unspecified;
                options.Cookie.IsEssential = true;
            });



            builder.Services.AddCors(options =>
            {
                options.AddPolicy("MyCorsPolicy",
                    builder => builder.WithOrigins("http://localhost:8000")
                        .AllowAnyMethod()
                        .AllowAnyHeader());
            });

            builder.Services.AddControllers();
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            builder.Services.AddSingleton<IFido2>(sp =>
            {
                var fido2Config = new Fido2Configuration
                {
                    // Set up the configuration for Fido2
                    ServerDomain = "localhost",
                    ServerName = "Fido2 Server",
                    Origin = "http://localhost:8000"
                };
                return new Fido2(fido2Config);
            });

            var app = builder.Build();

            if (app.Environment.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();
            

            
            app.UseRouting();
            app.UseCors("MyCorsPolicy");

            app.UseAuthorization();

            app.UseSession();

            app.MapControllers();

            app.Run();
        }
    }
}
