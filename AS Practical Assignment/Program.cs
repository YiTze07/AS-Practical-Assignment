using AS_Practical_Assignment.Data;
using AS_Practical_Assignment.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;

namespace AS_Practical_Assignment
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // --- MVC ---
            builder.Services.AddControllersWithViews();

            // --- Database ---
            builder.Services.AddDbContext<AppDbContext>(options =>
                options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"))
            );

            // --- Session ---
            builder.Services.AddSession(options =>
            {
                options.IdleTimeout = TimeSpan.FromMinutes(1);
                options.Cookie.HttpOnly = true;
                options.Cookie.IsEssential = true;
                options.Cookie.SecurePolicy = Microsoft.AspNetCore.Http.CookieSecurePolicy.Always;
                options.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Strict;
            });

            // --- Anti-Forgery ---
            builder.Services.AddAntiforgery(options =>
            {
                options.Cookie.Name = "AceJob_CSRF";
            });

            // --- Custom Services ---
            builder.Services.AddScoped<EncryptionHelper>();
            builder.Services.AddScoped<RecaptchaService>();
            builder.Services.AddScoped<EmailService>();
            builder.Services.AddHttpClient(); // Required for RecaptchaService

            var app = builder.Build();

            // --- Middleware ---
            app.UseStaticFiles();
            app.UseSession();
            app.UseRouting();
            app.UseAntiforgery();

            // --- Custom Error Pages ---
            app.UseStatusCodePagesWithReExecute("/Home/StatusCodeError", "?code={0}");

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapDefaultControllerRoute();
            });

            app.Run();
        }
    }
}