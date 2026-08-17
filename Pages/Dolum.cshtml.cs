using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SusamUretim.Web.Data;
using SusamUretim.Web.Models;
using SusamUretim.Web.Services;
namespace SusamUretim.Web.Pages;
public sealed class DolumModel(SusamRepository repository):PageModel
{
    [BindProperty]public DolumInput Input{get;set;}=new();[BindProperty(SupportsGet=true)]public RecordFilter Filter{get;set;}=new();[BindProperty(SupportsGet=true)]public long? EditId{get;set;}public bool IsAdmin=>HttpContext.IsAdmin();public string? PersonnelName=>HttpContext.PersonnelName();public List<DolumListItem>Records{get;private set;}=[];public List<LookupItem>Ambalajlar{get;private set;}=[];public List<LookupItem>Personeller{get;private set;}=[];public string?ErrorMessage{get;private set;}
    public async Task OnGetAsync(){if(EditId is>0){var x=await repository.GetDolumInputAsync(EditId.Value,IsAdmin?null:HttpContext.PersonnelId());if(x is null)EditId=null;else Input=x;}await LoadAsync();}
    public async Task<IActionResult>OnPostAsync(){if(!IsAdmin){Input.PersonelId=HttpContext.PersonnelId();Input.Personel=HttpContext.PersonnelName();}if(!IsAdmin&&EditId is>0&&await repository.GetDolumInputAsync(EditId.Value,HttpContext.PersonnelId()) is null)return Forbid();if(!ModelState.IsValid){await LoadAsync();return Page();}try{if(EditId is>0){await repository.UpdateDolumAsync(EditId.Value,Input);await repository.MarkExcelUpdateAsync("Dolum",EditId.Value);TempData["Success"]="Dolum kaydı güncellendi.";}else{await repository.InsertDolumAsync(Input);TempData["Success"]="Dolum kaydı eklendi.";}return RedirectToPage();}catch(Exception ex){ErrorMessage=ex.Message;await LoadAsync();return Page();}}
    public async Task<IActionResult>OnPostDeleteAsync(long id){if(!IsAdmin&&(await repository.GetDolumAsync(1,personnelId:HttpContext.PersonnelId())).FirstOrDefault()?.Id!=id)return Forbid();try{await repository.DeleteProductionRecordAsync("Dolum",id);TempData["Success"]="Dolum kaydı silindi.";}catch(Exception ex){TempData["Error"]=ex.Message;}return RedirectToPage();}
    private async Task LoadAsync(){try{Records=await repository.GetDolumAsync(filter:Filter,personnelId:IsAdmin?null:HttpContext.PersonnelId());Ambalajlar=await repository.GetAmbalajlarAsync();Personeller=await repository.GetPersonellerAsync();}catch(Exception ex){ErrorMessage=ex.Message;}}
}
