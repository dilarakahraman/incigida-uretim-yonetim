namespace SusamUretim.Web.Services;

public sealed class AccessControlMiddleware(RequestDelegate next)
{
    private static readonly string[] PublicPrefixes = ["/Giris", "/PersonelSec", "/Error", "/css", "/js", "/lib", "/favicon"];

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "/";
        if (PublicPrefixes.Any(prefix => path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
        {
            await next(context);
            return;
        }

        if (context.IsAdmin())
        {
            await next(context);
            return;
        }

        if (context.IsPersonnel())
        {
            var taskPage = context.TaskPage();
            if (!string.IsNullOrWhiteSpace(taskPage) && path.Equals(taskPage, StringComparison.OrdinalIgnoreCase))
            {
                await next(context);
                return;
            }

            context.Response.Redirect(taskPage ?? "/PersonelSec");
            return;
        }

        context.Response.Redirect("/Giris");
    }
}
