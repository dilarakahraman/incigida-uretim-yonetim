using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SusamUretim.Web.Data;
using SusamUretim.Web.Models;
using SusamUretim.Web.Services;
using System.Globalization;

namespace SusamUretim.Web.Pages;

public sealed class IndexModel(SusamRepository repository, ExcelExportService excel) : PageModel
{
    [BindProperty(SupportsGet=true)] public int? Year { get; set; }
    [BindProperty(SupportsGet=true)] public int? Week { get; set; }
    public DateTime From { get; private set; }
    public DateTime To { get; private set; }
    [BindProperty(SupportsGet=true)] public int? ProductId { get; set; }
    [BindProperty(SupportsGet=true)] public int? OriginId { get; set; }
    public DashboardStats Stats { get; private set; } = new(0,0,0,0,0,0,0,0,0,0,0,0,0,[],[],0,0,0,0,0,0,0,0,0,0,0,0,[],[],[],[]);
    public string? ErrorMessage { get; private set; }
    public IReadOnlyList<LookupItem> Products { get; private set; }=[];
    public IReadOnlyList<LookupItem> OriginOptions { get; private set; }=[];
    public string? CatalogMessage { get; private set; }
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
        To=From.AddDays(5);
        try
        {
            var productsTask=repository.GetUrunlerAsync();var originsTask=repository.GetMenseilerAsync();
            await Task.WhenAll(productsTask,originsTask);Products=productsTask.Result;OriginOptions=originsTask.Result;
            try
            {
                var catalog=excel.ReadDataCatalog();
                Products=excel.FilterProducts(Products);
                OriginOptions=excel.FilterOrigins(OriginOptions);
                Stats=await repository.GetDashboardAsync(From,To,ProductId,OriginId);
                var selectedProduct=Products.FirstOrDefault(x=>x.Id==ProductId)?.Name;
                var selectedOrigin=OriginOptions.FirstOrDefault(x=>x.Id==OriginId)?.Name;
                var weeklyAll=excel.ReadWeeklySummary(Year.Value,Week.Value);
                if(weeklyAll is not { InputKg: >0 })weeklyAll=null;
                var weekly=string.IsNullOrWhiteSpace(selectedProduct)&&string.IsNullOrWhiteSpace(selectedOrigin)
                    ? weeklyAll
                    : excel.ReadWeeklySummary(Year.Value,Week.Value,selectedProduct,selectedOrigin);
                if(weekly is not { InputKg: >0 })weekly=null;
                var normalizedProductOrigins=Stats.ProductOrigins.Select(x=>Normalize(x,catalog))
                    .Where(x=>catalog.Products.Any(c=>Same(c,x.Product))&&catalog.Origins.Any(c=>Same(c,x.Origin)))
                    .GroupBy(x=>(Key(x.Product),Key(x.Origin)))
                    .Select(g=>new ProductOriginSummary(g.First().Product,g.First().Origin,g.Sum(x=>x.Kg),g.Sum(x=>x.Count))).ToList();
                Stats=Stats with
                {
                    IslamaKg=weekly?.InputKg??Stats.IslamaKg,
                    KavurmaKg=weekly?.ProducedKg??Stats.KavurmaKg,
                    AddedSortexKg=weekly?.AddedSortexKg??Stats.AddedSortexKg,
                    PackagingSortexKg=weekly?.OutputSortexKg??Stats.PackagingSortexKg,
                    TavaSayisi=weekly?.PanCount??Stats.TavaSayisi,
                    Randiman=weeklyAll?.YieldPercent??Stats.Randiman,
                    ProductYields=OrderByCatalog(Stats.ProductYields,x=>x.Product,catalog.Products),
                    ProductOrigins=normalizedProductOrigins.OrderBy(x=>CatalogIndex(catalog.Products,x.Product)).ThenBy(x=>CatalogIndex(catalog.Origins,x.Origin)).ToList(),
                    Origins=OrderByCatalog(Stats.Origins,x=>x.Origin,catalog.Origins)
                };
            }
            catch(Exception ex)
            {
                CatalogMessage=$"Excel DATA sayfası okunamadı; veritabanı sırası kullanıldı. {ex.Message}";
                Stats=await repository.GetDashboardAsync(From,To,ProductId,OriginId);
            }
        }
        catch(Exception ex) { ErrorMessage=ex.Message; }
    }

    private static IReadOnlyList<LookupItem> OrderByCatalog(IReadOnlyList<LookupItem> values,IReadOnlyList<string> catalog)=>
        values.Where(x=>catalog.Any(c=>Same(c,x.Name))).OrderBy(x=>CatalogIndex(catalog,x.Name)).ToList();
    private static IReadOnlyList<T> OrderByCatalog<T>(IReadOnlyList<T> values,Func<T,string> name,IReadOnlyList<string> catalog)=>
        values.OrderBy(x=>CatalogIndex(catalog,name(x))).ToList();
    private static int CatalogIndex(IReadOnlyList<string> catalog,string value)
    {
        for(var i=0;i<catalog.Count;i++)if(Same(catalog[i],value))return i;
        return int.MaxValue;
    }
    private static bool Same(string left,string right)=>Key(left)==Key(right);
    private static string Key(string value)
    {
        var normalized=value.Trim().ToLower(new CultureInfo("tr-TR")).Replace('ı','i').Normalize(System.Text.NormalizationForm.FormD);
        return new string(normalized.Where(x=>CharUnicodeInfo.GetUnicodeCategory(x)!=UnicodeCategory.NonSpacingMark&&char.IsLetterOrDigit(x)).ToArray())
            .Replace("maiduguri","maidiguri",StringComparison.Ordinal);
    }
    private static ProductOriginSummary Normalize(ProductOriginSummary value,ExcelDataCatalog catalog)
    {
        var productLooksLikeOrigin=catalog.Origins.Any(x=>Same(x,value.Product));
        var originLooksLikeProduct=catalog.Products.Any(x=>Same(x,value.Origin));
        var normalized=productLooksLikeOrigin&&originLooksLikeProduct
            ? new ProductOriginSummary(value.Origin,value.Product,value.Kg,value.Count)
            : value;
        var product=catalog.Products.FirstOrDefault(x=>Same(x,normalized.Product))??normalized.Product;
        var origin=catalog.Origins.FirstOrDefault(x=>Same(x,normalized.Origin))??normalized.Origin;
        return new(product,origin,normalized.Kg,normalized.Count);
    }
}
