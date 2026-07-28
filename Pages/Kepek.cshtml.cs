using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SusamUretim.Web.Data;
using SusamUretim.Web.Models;
using SusamUretim.Web.Services;
namespace SusamUretim.Web.Pages;
public sealed class KepekModel(SusamRepository repository):PageModel
{
    [BindProperty]public KepekInput Input{get;set;}=new();[BindProperty(SupportsGet=true)]public RecordFilter Filter{get;set;}=new();[BindProperty(SupportsGet=true)]public long? EditId{get;set;}public bool IsAdmin=>HttpContext.IsAdmin();public string? PersonnelName=>HttpContext.PersonnelName();public List<KepekListItem>Records{get;private set;}=[];public List<LookupItem>Personeller{get;private set;}=[];public string?ErrorMessage{get;private set;}
    public IActionResult OnGet()=>RedirectToPage("/Kavurma",EditId is>0?new{kepekEditId=EditId}:null);
    public async Task<IActionResult>OnPostAsync(){if(!IsAdmin){if(EditId is>0)return Forbid();Input.PersonelId=HttpContext.PersonnelId();}if(!ModelState.IsValid){await LoadAsync();return Page();}try{if(EditId is>0){await repository.UpdateKepekAsync(EditId.Value,Input);await repository.MarkExcelUpdateAsync("Kepek",EditId.Value);TempData["Success"]="Kepek kaydı güncellendi.";}else{await repository.InsertKepekAsync(Input);TempData["Success"]="Kepek kaydı eklendi.";}return RedirectToPage();}catch(Exception ex){ErrorMessage=ex.Message;await LoadAsync();return Page();}}
    public async Task<IActionResult>OnPostDeleteAsync(long id){if(!IsAdmin)return Forbid();try{await repository.DeleteProductionRecordAsync("Kepek",id);TempData["Success"]="Kepek kaydı silindi.";}catch(Exception ex){TempData["Error"]=ex.Message;}return RedirectToPage();}
    private async Task LoadAsync(){try{if(IsAdmin)Records=await repository.GetKepekAsync(filter:Filter);Personeller=await repository.GetPersonellerAsync();}catch(Exception ex){ErrorMessage=ex.Message;}}
}
