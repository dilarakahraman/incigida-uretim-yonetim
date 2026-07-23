namespace SusamUretim.Web.Services;

public sealed class ExcelExportOptions
{
    public string WorkbookPath { get; set; } = "";
    public string TemplateSheet { get; set; } = "27. Hafta";
    public string BackupDirectory { get; set; } = "";
}

