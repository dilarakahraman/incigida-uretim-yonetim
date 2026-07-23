namespace SusamUretim.Web.Services;

public static class AccessSession
{
    public const string RoleKey = "Access.Role";
    public const string PersonnelIdKey = "Access.PersonnelId";
    public const string PersonnelNameKey = "Access.PersonnelName";
    public const string TaskNumberKey = "Access.TaskNumber";
    public const string TaskNameKey = "Access.TaskName";
    public const string TaskPageKey = "Access.TaskPage";

    public static bool IsAdmin(this HttpContext context) =>
        string.Equals(context.Session.GetString(RoleKey), "Admin", StringComparison.Ordinal);

    public static bool IsPersonnel(this HttpContext context) =>
        string.Equals(context.Session.GetString(RoleKey), "Personnel", StringComparison.Ordinal);

    public static int? PersonnelId(this HttpContext context) => context.Session.GetInt32(PersonnelIdKey);
    public static string? PersonnelName(this HttpContext context) => context.Session.GetString(PersonnelNameKey);
    public static string? TaskPage(this HttpContext context) => context.Session.GetString(TaskPageKey);

    public static void StartAdmin(this HttpContext context)
    {
        context.Session.Clear();
        context.Session.SetString(RoleKey, "Admin");
    }

    public static void StartPersonnel(this HttpContext context, int id, string name, int taskNumber, string taskName, string page)
    {
        context.Session.Clear();
        context.Session.SetString(RoleKey, "Personnel");
        context.Session.SetInt32(PersonnelIdKey, id);
        context.Session.SetString(PersonnelNameKey, name);
        context.Session.SetInt32(TaskNumberKey, taskNumber);
        context.Session.SetString(TaskNameKey, taskName);
        context.Session.SetString(TaskPageKey, page);
    }
}
