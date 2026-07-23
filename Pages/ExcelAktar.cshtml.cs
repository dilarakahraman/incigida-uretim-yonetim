using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SusamUretim.Web.Models;
using SusamUretim.Web.Services;

namespace SusamUretim.Web.Pages;

public sealed class ExcelAktarModel(ExcelExportService exportService) : PageModel
{
    [BindProperty, DataType(DataType.Date)] public DateTime Baslangic { get; set; }
    [BindProperty, DataType(DataType.Date)] public DateTime Bitis { get; set; }
    public string WorkbookPath => exportService.WorkbookPath;
    public ExcelExportResult? Result { get; private set; }
    public string? ErrorMessage { get; private set; }

    public void OnGet()
    {
        Baslangic = StartOfWeek(DateTime.Today);
        Bitis = Baslangic.AddDays(6);
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (Bitis < Baslangic) ModelState.AddModelError(nameof(Bitis), "Bitiş tarihi başlangıçtan önce olamaz.");
        if (!ModelState.IsValid) return Page();

        try
        {
            Result = await exportService.ExportAsync(Baslangic, Bitis, cancellationToken);
            TempData["Success"] = Result.RecordCount == 0
                ? "Seçilen tarihlerde Excel'e aktarılacak yeni kayıt bulunamadı."
                : $"{Result.RecordCount} kayıt, {Result.SheetCount} hafta sayfasına aktarıldı.";
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        return Page();
    }

    private static DateTime StartOfWeek(DateTime date)
    {
        var offset = (7 + (int)date.DayOfWeek - (int)DayOfWeek.Monday) % 7;
        return date.Date.AddDays(-offset);
    }
}

