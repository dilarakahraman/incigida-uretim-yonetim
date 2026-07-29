using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SusamUretim.Web.Data;
using SusamUretim.Web.Models;
using SusamUretim.Web.Services;

namespace SusamUretim.Web.Pages;

public sealed class KavurmaModel(SusamRepository repository) : PageModel
{
    [BindProperty] public KavurmaInput Input { get; set; } = new();
    [BindProperty(SupportsGet = true)] public RecordFilter Filter { get; set; } = new();
    [BindProperty(SupportsGet = true)] public long? EditId { get; set; }

    public bool IsAdmin => HttpContext.IsAdmin();
    public string? PersonnelName => HttpContext.PersonnelName();
    public List<KavurmaListItem> Records { get; private set; } = [];
    public List<LookupItem> Personeller { get; private set; } = [];
    public List<LookupItem> Menseiler { get; private set; } = [];
    public List<LookupItem> Urunler { get; private set; } = [];
    public string? ErrorMessage { get; private set; }

    public async Task OnGetAsync()
    {
        if (EditId is > 0){var x=await repository.GetKavurmaInputAsync(EditId.Value,IsAdmin?null:HttpContext.PersonnelId());if(x is null)EditId=null;else Input=x;}
        await LoadAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!IsAdmin && HttpContext.TaskNumber() != 2) return Forbid();
        if (!IsAdmin)
        {
            Input.PersonelId = HttpContext.PersonnelId();
        }
        if(!IsAdmin&&EditId is>0&&await repository.GetKavurmaInputAsync(EditId.Value,HttpContext.PersonnelId()) is null)return Forbid();

        if (!ModelState.IsValid)
        {
            await LoadAsync();
            return Page();
        }

        try
        {
            if (EditId is > 0)
            {
                await repository.UpdateKavurmaAsync(EditId.Value, Input);
                await repository.MarkExcelUpdateAsync("Kavurma", EditId.Value);
                TempData["Success"] = "Kavurma kaydı güncellendi.";
            }
            else
            {
                await repository.InsertKavurmaAsync(Input);
                TempData["Success"] = "Kavurma kaydı eklendi.";
            }
            return RedirectToPage();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            await LoadAsync();
            return Page();
        }
    }

    public async Task<IActionResult> OnPostDeleteAsync(long id)
    {
        if(!IsAdmin&&(await repository.GetKavurmaAsync(1,personnelId:HttpContext.PersonnelId())).FirstOrDefault()?.Id!=id)return Forbid();
        try
        {
            await repository.DeleteProductionRecordAsync("Kavurma", id);
            TempData["Success"] = "Kavurma kaydı silindi.";
        }
        catch (Exception ex) { TempData["Error"] = ex.Message; }
        return RedirectToPage();
    }

    private async Task LoadAsync()
    {
        try
        {
            Records = await repository.GetKavurmaAsync(filter: Filter,personnelId:IsAdmin?null:HttpContext.PersonnelId());
            Personeller = await repository.GetPersonellerAsync();
            Menseiler = await repository.GetMenseilerAsync();
            Urunler = await repository.GetUrunlerAsync();
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }
}
