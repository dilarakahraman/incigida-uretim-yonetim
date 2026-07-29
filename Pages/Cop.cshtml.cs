using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SusamUretim.Web.Data;
using SusamUretim.Web.Models;
using SusamUretim.Web.Services;

namespace SusamUretim.Web.Pages;

public sealed class CopModel(SusamRepository repository):PageModel
{
    [BindProperty] public CopInput Input{get;set;}=new();
    [BindProperty(SupportsGet=true)] public long? EditId{get;set;}
    public bool IsAdmin=>HttpContext.IsAdmin();
    public List<CopListItem> Records{get;private set;}=[];
    public List<LookupItem> Menseiler{get;private set;}=[];
    public string? ErrorMessage{get;private set;}

    public async Task OnGetAsync(){if(EditId is>0){var x=await repository.GetCopInputAsync(EditId.Value,IsAdmin?null:HttpContext.PersonnelId());if(x is null)EditId=null;else Input=x;}await LoadAsync();}

    public async Task<IActionResult> OnPostAsync()
    {
        if(!IsAdmin&&HttpContext.TaskNumber()!=8)return Forbid();
        if(!IsAdmin)Input.PersonelId=HttpContext.PersonnelId();
        if(!IsAdmin&&EditId is>0&&await repository.GetCopInputAsync(EditId.Value,HttpContext.PersonnelId()) is null)return Forbid();
        if(!ModelState.IsValid){await LoadAsync();return Page();}
        try
        {
            if(EditId is>0){await repository.UpdateCopAsync(EditId.Value,Input);TempData["Success"]="Giriş eleme çöpü kaydı güncellendi.";}
            else{await repository.InsertCopAsync(Input);TempData["Success"]="Giriş eleme çöpü kaydı eklendi.";}
            return RedirectToPage();
        }
        catch(Exception ex){ErrorMessage=ex.Message;await LoadAsync();return Page();}
    }

    public async Task<IActionResult> OnPostDeleteAsync(long id)
    {
        if(!IsAdmin&&(await repository.GetCopKayitlariAsync(1,HttpContext.PersonnelId())).FirstOrDefault()?.Id!=id)return Forbid();
        try{await repository.DeleteCopAsync(id);TempData["Success"]="Giriş eleme çöpü kaydı silindi.";}
        catch(Exception ex){TempData["Error"]=ex.Message;}
        return RedirectToPage();
    }

    private async Task LoadAsync()
    {
        try
        {
            Menseiler=await repository.GetMenseilerAsync();
            Records=await repository.GetCopKayitlariAsync(personnelId:IsAdmin?null:HttpContext.PersonnelId());
        }
        catch(Exception ex){ErrorMessage=ex.Message;}
    }
}
