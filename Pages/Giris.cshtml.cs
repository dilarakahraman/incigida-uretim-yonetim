using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SusamUretim.Web.Data;
using SusamUretim.Web.Services;

namespace SusamUretim.Web.Pages;

public sealed class GirisModel(SusamRepository repository) : PageModel
{
    [BindProperty,DataType(DataType.Password)] public string AdminPassword {get;set;}="";
    [BindProperty,DataType(DataType.Password)] public string PasswordConfirmation {get;set;}="";
    public bool RequiresSetup {get;private set;}
    public string? ErrorMessage {get;private set;}
    public async Task<IActionResult> OnGetAsync()
    {
        if(HttpContext.IsAdmin())return RedirectToPage("/Index");if(HttpContext.IsPersonnel())return Redirect(HttpContext.TaskPage()??"/PersonelSec");
        RequiresSetup=string.IsNullOrWhiteSpace(await repository.GetAdminPasswordHashAsync());return Page();
    }
    public async Task<IActionResult> OnPostAdminAsync()
    {
        var stored=await repository.GetAdminPasswordHashAsync();RequiresSetup=string.IsNullOrWhiteSpace(stored);
        if(RequiresSetup){ErrorMessage="Önce yönetici şifresini oluşturun.";return Page();}
        if(!PasswordSecurity.Verify(AdminPassword,stored!)){ErrorMessage="Yönetici şifresi yanlış.";return Page();}
        HttpContext.StartAdmin();return RedirectToPage("/Index");
    }
    public async Task<IActionResult> OnPostSetupAsync()
    {
        if(!string.IsNullOrWhiteSpace(await repository.GetAdminPasswordHashAsync())){ErrorMessage="Yönetici şifresi daha önce oluşturulmuş.";return Page();}
        RequiresSetup=true;if(AdminPassword.Length<8){ErrorMessage="Şifre en az 8 karakter olmalıdır.";return Page();}
        if(AdminPassword!=PasswordConfirmation){ErrorMessage="Şifreler eşleşmiyor.";return Page();}
        await repository.SetAdminPasswordHashAsync(PasswordSecurity.Hash(AdminPassword));HttpContext.StartAdmin();return RedirectToPage("/Index");
    }
    public IActionResult OnPostPersonnel(){HttpContext.Session.Clear();return RedirectToPage("/PersonelSec");}
    public IActionResult OnPostExit(){HttpContext.Session.Clear();return RedirectToPage();}
}
