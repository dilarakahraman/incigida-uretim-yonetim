using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.RateLimiting;
using SusamUretim.Web.Data;
using SusamUretim.Web.Services;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Services.AddRazorPages();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.Name = ".SusamUretim.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.IdleTimeout = TimeSpan.FromHours(12);
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("login", limiter =>
    {
        limiter.PermitLimit = 10;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
        limiter.AutoReplenishment = true;
    });
});
builder.Services.AddSingleton<SusamRepository>();
builder.Services.Configure<ExcelExportOptions>(builder.Configuration.GetSection("ExcelExport"));
builder.Services.AddScoped<ExcelExportService>();
builder.Services.AddDataProtection()
    .SetApplicationName("SusamUretim")
    .PersistKeysToFileSystem(new DirectoryInfo(
        Path.Combine(builder.Environment.ContentRootPath, "App_Data", "DataProtectionKeys")));

var app = builder.Build();

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Error");

app.UseRouting();
app.UseRateLimiter();
app.UseSession();
app.UseMiddleware<AccessControlMiddleware>();
app.UseAuthorization();
app.MapStaticAssets();
app.MapRazorPages().WithStaticAssets();

try
{
    using var scope=app.Services.CreateScope();
    var repository=scope.ServiceProvider.GetRequiredService<SusamRepository>();
    await repository.EnsureAccessSchemaAsync();
    var oldErrorPath=Path.Combine(app.Environment.ContentRootPath,"App_Data","schema-error.txt");
    if(File.Exists(oldErrorPath))File.Delete(oldErrorPath);
}
catch(Exception ex)
{
    var errorPath=Path.Combine(app.Environment.ContentRootPath,"App_Data","schema-error.txt");
    Directory.CreateDirectory(Path.GetDirectoryName(errorPath)!);
    await File.WriteAllTextAsync(errorPath,ex.ToString());
    app.Logger.LogError(ex,"Rol ve görev şeması başlangıçta hazırlanamadı.");
}

app.Run();
