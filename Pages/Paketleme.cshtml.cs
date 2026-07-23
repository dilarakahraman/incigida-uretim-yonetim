using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SusamUretim.Web.Data;
using SusamUretim.Web.Models;
using SusamUretim.Web.Services;
namespace SusamUretim.Web.Pages;
public sealed class PaketlemeModel(SusamRepository repository,ExcelExportService excel):PageModel
{
    [BindProperty]public PaketlemeInput Input{get;set;}=new();[BindProperty(SupportsGet=true)]public RecordFilter Filter{get;set;}=new();[BindProperty(SupportsGet=true)]public long? EditId{get;set;}public bool IsAdmin=>HttpContext.IsAdmin();public string? PersonnelName=>HttpContext.PersonnelName();public List<PaketlemeListItem>Records{get;private set;}=[];public List<LookupItem>Personeller{get;private set;}=[];public List<LookupItem>Menseiler{get;private set;}=[];public List<LookupItem>Urunler{get;private set;}=[];public string?ErrorMessage{get;private set;}
    public async Task OnGetAsync(){if(IsAdmin&&EditId is>0)Input=await repository.GetPaketlemeInputAsync(EditId.Value)??Input;await LoadAsync();}
    public async Task<IActionResult>OnPostAsync(){if(!IsAdmin){if(EditId is>0)return Forbid();Input.PersonelId=HttpContext.PersonnelId();}if(!ModelState.IsValid){await LoadAsync();return Page();}try{if(EditId is>0){await repository.UpdatePaketlemeAsync(EditId.Value,Input);await repository.MarkExcelUpdateAsync("Paketleme",EditId.Value);TempData["Success"]="Paketleme kaydı güncellendi.";}else{await repository.InsertPaketlemeAsync(Input);TempData["Success"]="Paketleme kaydı eklendi.";}return RedirectToPage();}catch(Exception ex){ErrorMessage=ex.Message;await LoadAsync();return Page();}}
    public async Task<IActionResult>OnPostDeleteAsync(long id){if(!IsAdmin)return Forbid();try{await repository.DeleteProductionRecordAsync("Paketleme",id);TempData["Success"]="Paketleme kaydı silindi.";}catch(Exception ex){TempData["Error"]=ex.Message;}return RedirectToPage();}
    private async Task LoadAsync(){try{if(IsAdmin)Records=await repository.GetPaketlemeAsync(filter:Filter);Personeller=await repository.GetPersonellerAsync();Menseiler=excel.FilterOrigins(await repository.GetMenseilerAsync());Urunler=excel.FilterProducts(await repository.GetUrunlerAsync());}catch(Exception ex){ErrorMessage=ex.Message;}}
}
