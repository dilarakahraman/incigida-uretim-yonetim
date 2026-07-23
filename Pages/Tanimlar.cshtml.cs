using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SusamUretim.Web.Data;
using SusamUretim.Web.Models;
using SusamUretim.Web.Services;

namespace SusamUretim.Web.Pages;

public sealed class TanimlarModel(SusamRepository repository,ExcelExportService excel):PageModel
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
 public async Task<IActionResult> OnPostAsync(){if(Type is not ("personel" or "ambalaj"))ModelState.AddModelError(nameof(Type),"Ürün ve menşei listeleri Excel DATA sayfasından alınır.");if(Type=="ambalaj"&&Weight is null)ModelState.AddModelError(nameof(Weight),"Ambalaj ağırlığı zorunludur.");if(!ModelState.IsValid){await LoadAsync();return Page();}try{await repository.AddDefinitionAsync(Type,Name.Trim(),Weight,TaskNumber);TempData["Success"]="Tanım eklendi.";return RedirectToPage();}catch(Exception ex){ErrorMessage=ex.Message;await LoadAsync();return Page();}}
 public async Task<IActionResult> OnPostTaskAsync(int personnelId,int[] taskNumbers){try{await repository.SetPersonnelTasksAsync(personnelId,taskNumbers);TempData["Success"]="Personel görevleri güncellendi.";}catch(Exception ex){TempData["Error"]=ex.Message;}return RedirectToPage();}
 public async Task<IActionResult> OnPostActivatePersonnelAsync(int personnelId){try{await repository.SetPersonnelActiveAsync(personnelId,true);TempData["Success"]="Personel aktifleştirildi.";}catch(Exception ex){TempData["Error"]=ex.Message;}return RedirectToPage();}
 public async Task<IActionResult> OnPostDeactivatePersonnelAsync(int personnelId){try{await repository.SetPersonnelActiveAsync(personnelId,false);TempData["Success"]="Personel pasife alındı; geçmiş kayıtları korundu.";}catch(Exception ex){TempData["Error"]=ex.Message;}return RedirectToPage();}
 private async Task LoadAsync(){try{var groups=repository.GetDefinitionsAsync();var personnel=repository.GetPersonnelAssignmentsAsync();var statuses=repository.GetPersonnelStatusesAsync();await Task.WhenAll(groups,personnel,statuses);Groups=groups.Result;Groups.Remove("Silolar");var catalog=excel.ReadDataCatalog();Groups["Ürünler"]=catalog.Products.Select((name,index)=>new LookupItem(index+1,name)).ToList();Groups["Menşeiler"]=catalog.Origins.Select((name,index)=>new LookupItem(index+1,name)).ToList();Personnel=personnel.Result;PersonnelStatuses=statuses.Result;}catch(Exception ex){ErrorMessage=ex.Message;}}
}
