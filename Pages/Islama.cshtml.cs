using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SusamUretim.Web.Data;
using SusamUretim.Web.Models;
using SusamUretim.Web.Services;

namespace SusamUretim.Web.Pages;

public sealed class IslamaModel(SusamRepository repository) : PageModel
{
    [BindProperty] public IslamaHazirlikInput Hazirlik { get; set; } = new();
    [BindProperty] public IslamaSurecInput Islama { get; set; } = new();
    [BindProperty] public SoymaTamamlamaInput Soyma { get; set; } = new();
    [BindProperty(SupportsGet=true)] public RecordFilter Filter { get; set; } = new();
    [BindProperty(SupportsGet=true)] public long? WorkId { get; set; }
    [BindProperty(SupportsGet=true)] public string? Stage { get; set; }
    [BindProperty(SupportsGet=true)] public long? EditId { get; set; }
    public bool IsAdmin => HttpContext.IsAdmin();
    public string? PersonnelName => HttpContext.PersonnelName();
    public List<IslamaWorkflowItem> Workflow { get; private set; }=[];
    public IslamaWorkflowItem? SelectedWork { get; private set; }
    public List<IslamaListItem> Records { get; private set; }=[];
    public List<LookupItem> Menseiler { get; private set; }=[];
    public List<LookupItem> Urunler { get; private set; }=[];
    public string? ErrorMessage { get; private set; }

    public async Task OnGetAsync()
    {
        if(WorkId is > 0)
        {
            SelectedWork=await repository.GetIslamaWorkflowItemAsync(WorkId.Value);
            if(SelectedWork?.Asama==2)Soyma.HavuzNo=SelectedWork.HavuzNo;
        }
        await LoadAsync();
    }

    public async Task<IActionResult> OnPostHazirlikAsync()
    {
        KeepOnly(nameof(Hazirlik));
        if(!ModelState.IsValid){await LoadAsync();return Page();}
        try
        {
            var parti=await repository.InsertIslamaHazirlikAsync(Hazirlik,HttpContext.PersonnelId());
            TempData["Success"]=$"Nöbet kaydı oluşturuldu: {parti}. Islama bilgisi bekleniyor.";
            return RedirectToPage();
        }
        catch(Exception ex){ErrorMessage=ex.Message;await LoadAsync();return Page();}
    }

    public async Task<IActionResult> OnPostIslamaAsync(long workId)
    {
        KeepOnly(nameof(Islama));
        SelectedWork=await repository.GetIslamaWorkflowItemAsync(workId);
        if(SelectedWork is null||SelectedWork.Asama!=1)return NotFound();
        if(Islama.IslamaBitisi<Islama.IslamaBaslangici)
            ModelState.AddModelError("Islama.IslamaBitisi","Islama bitişi başlangıçtan önce olamaz.");
        if(!ModelState.IsValid){WorkId=workId;await LoadAsync();return Page();}
        try
        {
            await repository.CompleteIslamaStageAsync(workId,Islama,HttpContext.PersonnelId());
            TempData["Success"]=$"{SelectedWork.PartiNo} partisinin ıslama bilgileri kaydedildi. Soyma bekleniyor.";
            return RedirectToPage();
        }
        catch(Exception ex){ErrorMessage=ex.Message;WorkId=workId;await LoadAsync();return Page();}
    }

    public async Task<IActionResult> OnPostSoymaAsync(long workId)
    {
        KeepOnly(nameof(Soyma));
        Soyma.Silo1=Soyma.Silo1?.Trim().ToUpperInvariant();
        Soyma.Silo2=Soyma.Silo2?.Trim().ToUpperInvariant();
        SelectedWork=await repository.GetIslamaWorkflowItemAsync(workId);
        if(SelectedWork is null||SelectedWork.Asama!=2)return NotFound();
        if(!SelectedWork.HamSusamGelisTarihi.HasValue&&!Soyma.HamSusamGelisTarihi.HasValue)
            ModelState.AddModelError("Soyma.HamSusamGelisTarihi","Ham susam geliş tarihi zorunludur.");
        if(!SelectedWork.MenseiId.HasValue&&!Soyma.MenseiId.HasValue)
            ModelState.AddModelError("Soyma.MenseiId","Ürün menşei zorunludur.");
        if(!SelectedWork.EkranTonajiKg.HasValue&&!Soyma.EkranTonajiKg.HasValue)
            ModelState.AddModelError("Soyma.EkranTonajiKg","Eski kayıt için ekran tonajı zorunludur.");
        if(!SelectedWork.CekilenTonajKg.HasValue&&!Soyma.CekilenTonajKg.HasValue)
            ModelState.AddModelError("Soyma.CekilenTonajKg","Eski kayıt için çekilen tonaj zorunludur.");
        if(Soyma.SoymaBitisi<Soyma.SoymaBaslangici)
            ModelState.AddModelError("Soyma.SoymaBitisi","Soyma bitişi başlangıçtan önce olamaz.");
        if(!ModelState.IsValid){WorkId=workId;await LoadAsync();return Page();}
        try
        {
            await repository.CompleteSoymaStageAsync(workId,Soyma,HttpContext.PersonnelId());
            TempData["Success"]=$"{SelectedWork.PartiNo} partisi tamamlandı ve üretim kayıtlarına aktarıldı.";
            return RedirectToPage();
        }
        catch(Exception ex){ErrorMessage=ex.Message;WorkId=workId;await LoadAsync();return Page();}
    }

    public async Task<IActionResult> OnPostDeleteAsync(long id)
    {
        if(!IsAdmin)return Forbid();
        try{await repository.DeleteProductionRecordAsync("Islama",id);TempData["Success"]="Islama-soyma kaydı silindi.";}
        catch(Exception ex){TempData["Error"]=ex.Message;}
        return RedirectToPage();
    }

    private void KeepOnly(string prefix)
    {
        foreach(var key in ModelState.Keys.Where(x=>!x.StartsWith(prefix+".",StringComparison.Ordinal)).ToArray())
            ModelState.Remove(key);
    }

    private async Task LoadAsync()
    {
        try
        {
            Workflow=await repository.GetIslamaWorkflowAsync();
            Records=await repository.GetIslamaAsync(filter:Filter,personnelId:null,onlyCreatedToday:true);
            Menseiler=await repository.GetMenseilerAsync();
            Urunler=await repository.GetUrunlerAsync();
            if(WorkId is > 0)SelectedWork??=await repository.GetIslamaWorkflowItemAsync(WorkId.Value);
        }
        catch(Exception ex){ErrorMessage=ex.Message;}
    }
}
