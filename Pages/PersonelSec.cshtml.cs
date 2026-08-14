using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SusamUretim.Web.Data;
using SusamUretim.Web.Models;
using SusamUretim.Web.Services;

namespace SusamUretim.Web.Pages;

public sealed class PersonelSecModel(SusamRepository repository):PageModel
{
    private static readonly int[] VisibleTaskNumbers=[1,2,3,4,6,7];
    [BindProperty(SupportsGet=true)] public int? TaskNumber { get; set; }
    [BindProperty(SupportsGet=true)] public string? Stage { get; set; }
    public List<PersonnelAssignment> Personnel { get; private set; }=[];
    public List<PersonnelTask> Tasks { get; private set; }=[];
    public PersonnelTask? SelectedTask=>Tasks.FirstOrDefault(x=>x.TaskNumber==TaskNumber);
    public string? SelectedStageName=>Stage?.ToLowerInvariant() switch
    {
        "nobet"=>"Nöbet",
        "islama"=>"Islama",
        "soyma"=>"Soyma",
        _=>null
    };
    public IEnumerable<PersonnelAssignment> AssignedPersonnel=>TaskNumber is null
        ? [] : Personnel.Where(x=>x.Tasks.Any(t=>t.TaskNumber==TaskNumber));
    public string? ErrorMessage { get; private set; }

    public async Task OnGetAsync()=>await LoadAsync();

    public async Task<IActionResult> OnPostAsync(int personnelId,int taskNumber,string? stage)
    {
        var person=await repository.GetPersonnelAssignmentAsync(personnelId);
        var task=person?.Tasks.FirstOrDefault(x=>x.TaskNumber==taskNumber);
        if(person is null||task is null||!VisibleTaskNumbers.Contains(taskNumber))
        {
            ErrorMessage="Seçilen görev bu personele atanmamış.";
            TaskNumber=taskNumber;
            await LoadAsync();
            return Page();
        }
        if(taskNumber==1 && stage?.ToLowerInvariant() is not ("nobet" or "islama" or "soyma"))
        {
            ErrorMessage="Önce Nöbet, Islama veya Soyma aşamasını seçin.";
            TaskNumber=taskNumber;
            await LoadAsync();
            return Page();
        }
        HttpContext.StartPersonnel(person.PersonelId,person.Name,task.TaskNumber,task.TaskName,task.Page);
        return taskNumber==1
            ?Redirect($"{task.Page}?Stage={Uri.EscapeDataString(stage!.ToLowerInvariant())}")
            :Redirect(task.Page);
    }

    private async Task LoadAsync()
    {
        try
        {
            Personnel=await repository.GetPersonnelAssignmentsAsync();
            Tasks=Personnel.SelectMany(x=>x.Tasks).Where(x=>VisibleTaskNumbers.Contains(x.TaskNumber))
                .GroupBy(x=>x.TaskNumber).Select(x=>x.First()).OrderBy(x=>x.TaskNumber).ToList();
            if(TaskNumber.HasValue&&!Tasks.Any(x=>x.TaskNumber==TaskNumber))TaskNumber=null;
            if(TaskNumber!=1)Stage=null;
            else if(SelectedStageName is null)Stage=null;
        }
        catch(Exception ex){ErrorMessage=ex.Message;}
    }
}
