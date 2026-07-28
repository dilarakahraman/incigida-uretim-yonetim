using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SusamUretim.Web.Data;
using SusamUretim.Web.Models;
using System.Globalization;

namespace SusamUretim.Web.Pages;

public sealed class IndexModel(SusamRepository repository) : PageModel
{
    [BindProperty(SupportsGet=true)] public int? Year { get; set; }
    [BindProperty(SupportsGet=true)] public int? Week { get; set; }
    public DateTime From { get; private set; }
    public DateTime To { get; private set; }
    [BindProperty(SupportsGet=true)] public int? ProductId { get; set; }
    [BindProperty(SupportsGet=true)] public int? OriginId { get; set; }
    public DashboardStats Stats { get; private set; } = new(0,0,0,0,0,0,0,0,0,0,0,0,0,[],[],0,0,0,0,0,0,0,0,0,0,0,0,[],[],[],[]);
    public ProcessDashboardStats Processes { get; private set; } = new(0,0,0,0,0,0,0,0,0,[]);
    public string? ErrorMessage { get; private set; }
    public IReadOnlyList<LookupItem> Products { get; private set; }=[];
    public IReadOnlyList<LookupItem> OriginOptions { get; private set; }=[];
    public decimal ChartMax => Math.Max(1, Stats.Trend.SelectMany(x=>new[]{x.InputKg,x.OutputKg}).DefaultIfEmpty(1).Max());
    public decimal OriginMax => Math.Max(1, Stats.Origins.Select(x=>x.Kg).DefaultIfEmpty(1).Max());
    public IReadOnlyList<int> Years => Enumerable.Range(2024,Math.Max(1,DateTime.Today.Year-2023)).Reverse().ToList();
    public IReadOnlyList<int> Weeks => Enumerable.Range(1,ISOWeek.GetWeeksInYear(Year??DateTime.Today.Year)).ToList();

    public async Task OnGetAsync()
    {
        Year ??= ISOWeek.GetYear(DateTime.Today);
        var maxWeek=ISOWeek.GetWeeksInYear(Year.Value);
        Week=Week is >=1 && Week<=maxWeek?Week:ISOWeek.GetWeekOfYear(DateTime.Today);
        Week=Math.Min(Week.Value,maxWeek);
        From=ISOWeek.ToDateTime(Year.Value,Week.Value,DayOfWeek.Monday);
        To=From.AddDays(6);
        try
        {
            var productsTask=repository.GetUrunlerAsync();
            var originsTask=repository.GetMenseilerAsync();
            var statsTask=repository.GetDashboardAsync(From,To,ProductId,OriginId);
            var processesTask=repository.GetProcessDashboardAsync(From,To,ProductId,OriginId);
            await Task.WhenAll(productsTask,originsTask,statsTask,processesTask);
            Products=productsTask.Result;
            OriginOptions=originsTask.Result;
            Stats=statsTask.Result;
            Processes=processesTask.Result;
        }
        catch(Exception ex) { ErrorMessage=ex.Message; }
    }
}
