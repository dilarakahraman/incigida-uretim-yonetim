using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SusamUretim.Web.Data;
using SusamUretim.Web.Models;

namespace SusamUretim.Web.Pages;

public sealed class TanimlarModel(SusamRepository repository):PageModel
{
    [BindProperty,Required] public string Type{get;set;}="urun";
    [BindProperty,Required,StringLength(100)] public string Name{get;set;}="";
    [BindProperty,Range(0.001,999999)] public decimal? Weight{get;set;}
    [BindProperty] public int? TaskNumber{get;set;}
    public Dictionary<string,List<LookupItem>> Groups{get;private set;}=[];
    public List<PersonnelAssignment> Personnel{get;private set;}=[];
    public List<LookupItem> PersonnelStatuses{get;private set;}=[];
    public string? ErrorMessage{get;private set;}

    public async Task OnGetAsync()=>await LoadAsync();

    public async Task<IActionResult> OnPostAsync()
    {
        if(Type is not ("urun" or "mensei" or "personel" or "ambalaj"))
            ModelState.AddModelError(nameof(Type),"Geçersiz tanım türü.");
        if(Type=="ambalaj"&&Weight is null)
            ModelState.AddModelError(nameof(Weight),"Ambalaj ağırlığı zorunludur.");
        if(!ModelState.IsValid){await LoadAsync();return Page();}
        try
        {
            await repository.AddDefinitionAsync(Type,Name.Trim(),Weight,TaskNumber);
            TempData["Success"]="Tanım eklendi.";
            return RedirectToPage();
        }
        catch(Exception ex){ErrorMessage=ex.Message;await LoadAsync();return Page();}
    }

    public async Task<IActionResult> OnPostTaskAsync(int personnelId,int[] taskNumbers)
    {
        try{await repository.SetPersonnelTasksAsync(personnelId,taskNumbers);TempData["Success"]="Personel görevleri güncellendi.";}
        catch(Exception ex){TempData["Error"]=ex.Message;}
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostActivatePersonnelAsync(int personnelId)
    {
        try{await repository.SetPersonnelActiveAsync(personnelId,true);TempData["Success"]="Personel aktifleştirildi.";}
        catch(Exception ex){TempData["Error"]=ex.Message;}
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeactivatePersonnelAsync(int personnelId)
    {
        try{await repository.SetPersonnelActiveAsync(personnelId,false);TempData["Success"]="Personel pasife alındı; geçmiş kayıtları korundu.";}
        catch(Exception ex){TempData["Error"]=ex.Message;}
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteDefinitionAsync(string type,int id)
    {
        if(type is not ("urun" or "mensei" or "ambalaj")||id<=0)
        {
            TempData["Error"]="Geçersiz tanım seçildi.";
            return RedirectToPage();
        }
        try
        {
            await repository.DeactivateDefinitionAsync(type,id);
            TempData["Success"]="Tanım kaldırıldı; geçmiş üretim kayıtları korundu.";
        }
        catch(Exception ex){TempData["Error"]=ex.Message;}
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostUpdateDefinitionAsync(string type,int id,string name,decimal? weight)
    {
        if(type is not ("urun" or "mensei" or "ambalaj")||id<=0||string.IsNullOrWhiteSpace(name))
        {
            TempData["Error"]="Geçerli bir tanım adı girin.";
            return RedirectToPage();
        }
        if(type=="ambalaj"&&weight is null or <=0)
        {
            TempData["Error"]="Ambalaj ağırlığı zorunludur.";
            return RedirectToPage();
        }
        try
        {
            await repository.UpdateDefinitionAsync(type,id,name,weight);
            TempData["Success"]="Tanım değişiklikleri kaydedildi.";
        }
        catch(Exception ex){TempData["Error"]=ex.Message;}
        return RedirectToPage();
    }

    private async Task LoadAsync()
    {
        try
        {
            var groups=repository.GetDefinitionsAsync();
            var personnel=repository.GetPersonnelAssignmentsAsync();
            var statuses=repository.GetPersonnelStatusesAsync();
            await Task.WhenAll(groups,personnel,statuses);
            Groups=groups.Result;
            Groups.Remove("Silolar");
            Personnel=personnel.Result;
            PersonnelStatuses=statuses.Result;
        }
        catch(Exception ex){ErrorMessage=ex.Message;}
    }
}
