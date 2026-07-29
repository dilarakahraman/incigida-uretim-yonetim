using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SusamUretim.Web.Data;
using SusamUretim.Web.Models;
using SusamUretim.Web.Services;

namespace SusamUretim.Web.Pages;

public sealed class DegirmenModel(SusamRepository repository):PageModel
{
    [BindProperty]public DegirmenNobetInput Input{get;set;}=new();
    [BindProperty(SupportsGet=true)]public long? EditId{get;set;}
    public bool IsAdmin=>HttpContext.IsAdmin();
    public string? PersonnelName=>HttpContext.PersonnelName();
    public List<DegirmenNobetListItem> Records{get;private set;}=[];
    public List<LookupItem> Personeller{get;private set;}=[];
    public List<LookupItem> Menseiler{get;private set;}=[];
    public string? ErrorMessage{get;private set;}

    public async Task OnGetAsync()
    {
        if(EditId is>0)
        {
            var editInput=await repository.GetDegirmenNobetInputAsync(EditId.Value,IsAdmin?null:HttpContext.PersonnelId());
            if(editInput is null)EditId=null;else Input=editInput;
        }
        await LoadAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if(!IsAdmin)Input.PersonelId=HttpContext.PersonnelId();
        if(Input.PersonelId is null or <=0)ModelState.AddModelError("Input.PersonelId","Personel seçimi zorunludur.");
        if(!ModelState.IsValid){await LoadAsync();return Page();}
        try
        {
            if(EditId is>0)
            {
                await repository.UpdateDegirmenNobetiAsync(EditId.Value,Input,IsAdmin?null:HttpContext.PersonnelId());
                TempData["Success"]="Değirmen kaydı güncellendi.";
            }
            else
            {
                await repository.InsertDegirmenNobetiAsync(Input);
                TempData["Success"]=$"Değirmen nöbeti ve {Input.Satirlar.Count} satır kaydedildi.";
            }
            return RedirectToPage();
        }
        catch(Exception ex){ErrorMessage=ex.Message;await LoadAsync();return Page();}
    }

    public async Task<IActionResult> OnPostDeleteAsync(long id)
    {
        if(!IsAdmin&&(await repository.GetDegirmenNobetleriAsync(1,HttpContext.PersonnelId())).FirstOrDefault()?.Id!=id)return Forbid();
        try{await repository.DeleteDegirmenNobetiAsync(id);TempData["Success"]="Değirmen nöbeti kaldırıldı.";}
        catch(Exception ex){TempData["Error"]=ex.Message;}
        return RedirectToPage();
    }

    private async Task LoadAsync()
    {
        try
        {
            var origins=repository.GetMenseilerAsync();
            var personnel=repository.GetPersonellerAsync();
            var records=repository.GetDegirmenNobetleriAsync(100,IsAdmin?null:HttpContext.PersonnelId());
            await Task.WhenAll(origins,personnel,records);
            Records=records.Result;
            Menseiler=origins.Result;Personeller=personnel.Result;
            if(Input.Satirlar.Count==0)Input.Satirlar.Add(new());
        }
        catch(Exception ex){ErrorMessage=ex.Message;}
    }
}
