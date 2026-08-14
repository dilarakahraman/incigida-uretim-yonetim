using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SusamUretim.Web.Data;
using SusamUretim.Web.Models;
using SusamUretim.Web.Services;

namespace SusamUretim.Web.Pages;

public sealed class TankTransferModel(SusamRepository repository):PageModel
{
    [BindProperty] public TankTransferInput Input { get; set; }=new();
    public bool IsAdmin=>HttpContext.IsAdmin();
    public string? PersonnelName=>HttpContext.PersonnelName();
    public List<TankTransferListItem> Transferler { get; private set; }=[];
    public List<LookupItem> Personeller { get; private set; }=[];
    public List<LookupItem> Menseiler { get; private set; }=[];
    public string? ErrorMessage { get; private set; }

    public async Task OnGetAsync()=>await LoadAsync();

    public async Task<IActionResult> OnPostAsync()
    {
        if(!IsAdmin)Input.PersonelId=HttpContext.PersonnelId();
        if(Input.PersonelId is null or <=0)
            ModelState.AddModelError("Input.PersonelId","Personel seçimi zorunludur.");
        if(!string.IsNullOrWhiteSpace(Input.KaynakTank) &&
           !string.IsNullOrWhiteSpace(Input.HedefTank) &&
           string.Equals(Input.KaynakTank.Trim(),Input.HedefTank.Trim(),StringComparison.OrdinalIgnoreCase))
            ModelState.AddModelError("Input.HedefTank","Kaynak ve hedef tank farklı olmalıdır.");
        if(!ModelState.IsValid){await LoadAsync();return Page();}

        try
        {
            await repository.InsertTankTransferiAsync(Input);
            TempData["Success"]="Tank transferi kaydedildi.";
            return RedirectToPage();
        }
        catch(Exception ex){ErrorMessage=ex.Message;await LoadAsync();return Page();}
    }

    public async Task<IActionResult> OnPostDeleteAsync(long id)
    {
        try
        {
            await repository.DeleteTankTransferiAsync(id,IsAdmin?null:HttpContext.PersonnelId());
            TempData["Success"]="Tank transferi silindi.";
        }
        catch(Exception ex){TempData["Error"]=ex.Message;}
        return RedirectToPage();
    }

    private async Task LoadAsync()
    {
        try
        {
            var origins=repository.GetMenseilerAsync();
            var personnel=repository.GetPersonellerAsync();
            var transfers=repository.GetTankTransferleriAsync(100,IsAdmin?null:HttpContext.PersonnelId());
            await Task.WhenAll(origins,personnel,transfers);
            Menseiler=origins.Result;
            Personeller=personnel.Result;
            Transferler=transfers.Result;
        }
        catch(Exception ex){ErrorMessage=ex.Message;}
    }
}
