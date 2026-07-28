using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SusamUretim.Web.Data;
using SusamUretim.Web.Models;
using SusamUretim.Web.Services;

namespace SusamUretim.Web.Pages;

public sealed class CopModel(SusamRepository repository):PageModel
{
    [BindProperty] public CopInput Input{get;set;}=new();
    public bool IsAdmin=>HttpContext.IsAdmin();
    public List<CopListItem> Records{get;private set;}=[];
    public List<LookupItem> Menseiler{get;private set;}=[];
    public string? ErrorMessage{get;private set;}

    public async Task OnGetAsync()=>await LoadAsync();

    public async Task<IActionResult> OnPostAsync()
    {
        if(!IsAdmin&&HttpContext.TaskNumber()!=8)return Forbid();
        if(!ModelState.IsValid){await LoadAsync();return Page();}
        try
        {
            await repository.InsertCopAsync(Input);
            TempData["Success"]="Çöp kaydı SQL'e eklendi.";
            return RedirectToPage();
        }
        catch(Exception ex){ErrorMessage=ex.Message;await LoadAsync();return Page();}
    }

    public async Task<IActionResult> OnPostDeleteAsync(long id)
    {
        if(!IsAdmin)return Forbid();
        try{await repository.DeleteCopAsync(id);TempData["Success"]="Çöp kaydı silindi.";}
        catch(Exception ex){TempData["Error"]=ex.Message;}
        return RedirectToPage();
    }

    private async Task LoadAsync()
    {
        try
        {
            Menseiler=await repository.GetMenseilerAsync();
            if(IsAdmin)Records=await repository.GetCopKayitlariAsync();
        }
        catch(Exception ex){ErrorMessage=ex.Message;}
    }
}
