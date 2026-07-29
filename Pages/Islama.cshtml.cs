using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SusamUretim.Web.Data;
using SusamUretim.Web.Models;
using SusamUretim.Web.Services;

namespace SusamUretim.Web.Pages;

public sealed class IslamaModel(SusamRepository repository) : PageModel
{
    [BindProperty] public IslamaInput Input { get; set; } = new() { SoymaBaslangici=DateTime.Now, SoymaBitisi=DateTime.Now.AddHours(6) };
    [BindProperty(SupportsGet=true)] public RecordFilter Filter { get; set; } = new();
    [BindProperty(SupportsGet=true)] public long? EditId { get; set; }
    public bool IsAdmin => HttpContext.IsAdmin();
    public string? PersonnelName => HttpContext.PersonnelName();
    public List<IslamaListItem> Records { get; private set; }=[];
    public List<LookupItem> Menseiler { get; private set; }=[];
    public List<LookupItem> Urunler { get; private set; }=[];
    public List<LookupItem> Silolar { get; private set; }=[];
    public List<LookupItem> Personeller { get; private set; }=[];
    public string? ErrorMessage { get; private set; }

    public async Task OnGetAsync()
    {
        if(EditId is > 0){var x=await repository.GetIslamaInputAsync(EditId.Value,IsAdmin?null:HttpContext.PersonnelId());if(x is null)EditId=null;else Input=x;}
        await LoadAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        Input.Silo1=Input.Silo1?.Trim().ToUpperInvariant();
        Input.Silo2=Input.Silo2?.Trim().ToUpperInvariant();
        if(EditId is not >0)
        {
            Input.PartiNo=await repository.GetNextBatchNumberAsync(Input.SoymaBaslangici);
            ModelState.Remove("Input.PartiNo");
        }
        if(!IsAdmin)Input.PersonelId=HttpContext.PersonnelId();
        if(!IsAdmin&&EditId is>0&&await repository.GetIslamaInputAsync(EditId.Value,HttpContext.PersonnelId()) is null)return Forbid();
        if(Input.SoymaBitisi < Input.SoymaBaslangici) ModelState.AddModelError("Input.SoymaBitisi","Bitiş başlangıçtan önce olamaz.");
        if(!ModelState.IsValid){await LoadAsync();return Page();}
        try
        {
            if(EditId is > 0){await repository.UpdateIslamaAsync(EditId.Value,Input);await repository.MarkExcelUpdateAsync("Islama",EditId.Value);TempData["Success"]=$"Islama-soyma kaydı güncellendi. Parti: {Input.PartiNo}";}
            else{await repository.InsertIslamaAsync(Input);TempData["Success"]=$"Kayıt eklendi. Parti numarası: {Input.PartiNo}";}
            return RedirectToPage();
        }
        catch(Exception ex){ErrorMessage=ex.Message;await LoadAsync();return Page();}
    }

    public async Task<IActionResult> OnPostDeleteAsync(long id)
    {
        if(!IsAdmin&&(await repository.GetIslamaAsync(1,personnelId:HttpContext.PersonnelId())).FirstOrDefault()?.Id!=id)return Forbid();
        try{await repository.DeleteProductionRecordAsync("Islama",id);TempData["Success"]="Islama-soyma kaydı silindi.";}catch(Exception ex){TempData["Error"]=ex.Message;}
        return RedirectToPage();
    }

    private async Task LoadAsync(){try{Records=await repository.GetIslamaAsync(filter:Filter,personnelId:IsAdmin?null:HttpContext.PersonnelId());Menseiler=await repository.GetMenseilerAsync();Urunler=await repository.GetUrunlerAsync();Personeller=await repository.GetPersonellerAsync();}catch(Exception ex){ErrorMessage=ex.Message;}}
}
