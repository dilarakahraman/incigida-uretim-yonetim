using Microsoft.AspNetCore.DataProtection;
using System.Globalization;
using SusamUretim.Web.Data;
using SusamUretim.Web.Services;

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
builder.Services.AddSingleton<SusamRepository>();
builder.Services.Configure<ExcelExportOptions>(builder.Configuration.GetSection("ExcelExport"));
builder.Services.AddScoped<ExcelExportService>();
builder.Services.AddDataProtection()
    .SetApplicationName("SusamUretim")
    .PersistKeysToFileSystem(new DirectoryInfo(
        Path.Combine(builder.Environment.ContentRootPath, "App_Data", "DataProtectionKeys")));

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseRouting();
app.UseSession();
app.UseMiddleware<AccessControlMiddleware>();
app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

try
{
    using var scope=app.Services.CreateScope();
    var repository=scope.ServiceProvider.GetRequiredService<SusamRepository>();
    await repository.EnsureAccessSchemaAsync();
    var today=DateTime.Today;
    scope.ServiceProvider.GetRequiredService<ExcelExportService>()
        .ReadWeeklySummary(ISOWeek.GetYear(today),ISOWeek.GetWeekOfYear(today));
}
catch (Exception ex)
{
    app.Logger.LogError(ex, "Rol ve görev şeması başlangıçta hazırlanamadı.");
}

app.Run();
