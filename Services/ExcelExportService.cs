using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using SusamUretim.Web.Models;

namespace SusamUretim.Web.Services;

public sealed class ExcelExportService
{
    private static readonly SemaphoreSlim ExportLock = new(1, 1);
    private static readonly object CatalogLock=new();
    private static ExcelDataCatalog? CachedCatalog;
    private static string? CachedCatalogPath;
    private static DateTime CachedCatalogWriteTime;
    private static readonly object WeeklySummaryLock=new();
    private static readonly Dictionary<string,ExcelWeeklySummary?> WeeklySummaryCache=[];
    private static string? WeeklySummaryCachePath;
    private static DateTime WeeklySummaryCacheWriteTime;
    private readonly string _connectionString;
    private readonly string _templateWorkbookPath;
    private readonly string _workbookPathPattern;
    private readonly string _backupDirectory;
    private readonly string _templateSheet;

    public ExcelExportService(IConfiguration configuration, IOptions<ExcelExportOptions> options, IWebHostEnvironment environment)
    {
        _connectionString = configuration.GetConnectionString("SusamUretim")
            ?? throw new InvalidOperationException("SusamUretim bağlantı bilgisi bulunamadı.");
        _templateWorkbookPath = ResolvePath(environment.ContentRootPath, options.Value.WorkbookPath);
        _workbookPathPattern = string.IsNullOrWhiteSpace(options.Value.WorkbookPathPattern)
            ? _templateWorkbookPath
            : ResolvePath(environment.ContentRootPath, options.Value.WorkbookPathPattern);
        _backupDirectory = ResolvePath(environment.ContentRootPath, options.Value.BackupDirectory);
        _templateSheet = options.Value.TemplateSheet;
    }

    public string WorkbookPath => WorkbookPathForYear(ExcelExportRouting.Year(DateTime.Today));

    public List<LookupItem> FilterOrigins(IEnumerable<LookupItem> values) =>
        FilterLookups(values,ReadDataCatalog().Origins);

    public List<LookupItem> FilterProducts(IEnumerable<LookupItem> values) =>
        FilterLookups(values,ReadDataCatalog().Products);

    private static List<LookupItem> FilterLookups(IEnumerable<LookupItem> values,IReadOnlyList<string> catalog)
    {
        var source=values.ToList();var result=new List<LookupItem>();
        foreach(var canonical in catalog)
        {
            var match=source.Where(x=>CatalogKey(x.Name)==CatalogKey(canonical))
                .OrderByDescending(x=>string.Equals(x.Name.Trim(),canonical.Trim(),StringComparison.Ordinal))
                .ThenBy(x=>x.Id).FirstOrDefault();
            if(match is not null)result.Add(match with{Name=canonical});
        }
        return result;
    }

    private static string CatalogKey(string value)
    {
        var normalized=value.Trim().ToLower(new CultureInfo("tr-TR")).Replace('ı','i').Normalize(NormalizationForm.FormD);
        return new string(normalized.Where(x=>CharUnicodeInfo.GetUnicodeCategory(x)!=UnicodeCategory.NonSpacingMark&&char.IsLetterOrDigit(x)).ToArray())
            .Replace("maiduguri","maidiguri",StringComparison.Ordinal);
    }

    public ExcelDataCatalog ReadDataCatalog()
    {
        if (!File.Exists(_templateWorkbookPath)) throw new FileNotFoundException("Excel dosyası bulunamadı.",_templateWorkbookPath);
        var writeTime=File.GetLastWriteTimeUtc(_templateWorkbookPath);
        lock(CatalogLock)
        {
            if(CachedCatalog is not null&&CachedCatalogPath==_templateWorkbookPath&&CachedCatalogWriteTime==writeTime)return CachedCatalog;
            using var stream=new FileStream(_templateWorkbookPath,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);
            using var workbook=new XLWorkbook(stream);
            var sheet=workbook.Worksheets.FirstOrDefault(x=>string.Equals(x.Name.Trim(),"DATA",StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException("Excel dosyasında DATA sayfası bulunamadı.");
            var last=sheet.LastRowUsed()?.RowNumber()??1;
            static List<string> Texts(IXLWorksheet sheet,int column,int last)=>Enumerable.Range(2,Math.Max(0,last-1))
                .Select(row=>sheet.Cell(row,column).GetString().Trim()).Where(x=>x.Length>0)
                .Distinct(StringComparer.CurrentCultureIgnoreCase).ToList();
            var weights=Enumerable.Range(2,Math.Max(0,last-1)).Select(row=>sheet.Cell(row,11).TryGetValue<decimal>(out var value)?value:(decimal?)null)
                .Where(x=>x.HasValue).Select(x=>x!.Value).Distinct().ToList();
            var packages=Enumerable.Range(2,Math.Max(0,last-1)).Select(row=>$"{sheet.Cell(row,16).GetString().Trim()} {sheet.Cell(row,17).GetString().Trim()} kg".Trim())
                .Where(x=>x.Length>3).Distinct(StringComparer.CurrentCultureIgnoreCase).ToList();
            CachedCatalog=new(Texts(sheet,2,last),Texts(sheet,5,last),Texts(sheet,8,last),weights,Texts(sheet,14,last),packages);
            CachedCatalogPath=_templateWorkbookPath;CachedCatalogWriteTime=writeTime;
            return CachedCatalog;
        }
    }

    public ExcelWeeklySummary? ReadWeeklySummary(int year,int week,string? product=null,string? origin=null)
    {
        var workbookPath=WorkbookPathForYear(year);
        if(!File.Exists(workbookPath))return null;
        var writeTime=File.GetLastWriteTimeUtc(workbookPath);
        var key=$"{year}|{week}|{product?.Trim().ToUpperInvariant()}|{origin?.Trim().ToUpperInvariant()}";
        lock(WeeklySummaryLock)
        {
            if(WeeklySummaryCachePath!=workbookPath||WeeklySummaryCacheWriteTime!=writeTime)
            {
                WeeklySummaryCache.Clear();WeeklySummaryCachePath=workbookPath;WeeklySummaryCacheWriteTime=writeTime;
            }
            if(WeeklySummaryCache.TryGetValue(key,out var cached))return cached;
            var summary=ReadWeeklySummaryCore(workbookPath,year,week,product,origin);
            WeeklySummaryCache[key]=summary;
            return summary;
        }
    }

    private static ExcelWeeklySummary? ReadWeeklySummaryCore(string workbookPath,int year,int week,string? product,string? origin)
    {
        using var stream=new FileStream(workbookPath,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);
        using var workbook=new XLWorkbook(stream);
        var sheet=workbook.Worksheets.FirstOrDefault(x=>TryGetWeek(x.Name)==week&&x.Cell(1,1).GetString().Contains(year.ToString(),StringComparison.OrdinalIgnoreCase));
        if(sheet is null)return null;
        var islamaHeader=FindRow(sheet,1,"Barkod Seri");
        var kavurmaTitle=FindRow(sheet,1,"KAVURMA TABLOSU");
        var paketlemeTitle=FindRow(sheet,1,"PAKETLEME TABLOSU");
        var input=0m;var produced=0m;var added=0m;var outputSortex=0m;var pans=0;
        static bool Match(string actual,string? expected)=>string.IsNullOrWhiteSpace(expected)||string.Equals(actual.Trim(),expected.Trim(),StringComparison.CurrentCultureIgnoreCase);
        static decimal Number(IXLCell cell){try{return cell.GetValue<decimal>();}catch{return 0;}}
        for(var row=islamaHeader+1;row<kavurmaTitle;row++)
            if(Match(sheet.Cell(row,16).GetString(),product)&&Match(sheet.Cell(row,15).GetString(),origin))
                input+=Math.Max(0,Number(sheet.Cell(row,13))-Number(sheet.Cell(row,3)));
        for(var row=kavurmaTitle+2;row<paketlemeTitle;row++)
            if(Match(sheet.Cell(row,10).GetString(),product)&&Match(sheet.Cell(row,9).GetString(),origin))
            {
                var net=Number(sheet.Cell(row,3));if(net>0)produced+=net;
                added+=Number(sheet.Cell(row,8));pans+=(int)Number(sheet.Cell(row,5));
            }
        var kepekTitle=sheet.Column(13).CellsUsed().FirstOrDefault(x=>x.GetString().Contains("KURUTULMUŞ KEPEK TABLOSU",StringComparison.OrdinalIgnoreCase))?.Address.RowNumber??(sheet.LastRowUsed()?.RowNumber()+1??121);
        for(var row=paketlemeTitle+2;row<kepekTitle;row++)
            if(Match(sheet.Cell(row,6).GetString(),product)&&Match(sheet.Cell(row,5).GetString(),origin))outputSortex+=Number(sheet.Cell(row,4));
        if(string.IsNullOrWhiteSpace(product)&&string.IsNullOrWhiteSpace(origin))
        {
            var excelInput=Number(sheet.Cell(4,32));       // AF4
            var excelWaste=Enumerable.Range(islamaHeader+1,Math.Max(0,kavurmaTitle-islamaHeader-1)).Sum(row=>Number(sheet.Cell(row,3)));
            if(excelInput>0)input=Math.Max(0,excelInput-excelWaste);
            // C/H/E sütunlarını doğrudan satırlardan topluyoruz. Böylece C'de değer olup
            // ürün, menşei veya diğer hücreleri boş olan kayıtlar da randımana dahil olur.
            var exactNet=Math.Max(0,produced-added);
            return new(input,produced,added,outputSortex,pans,input==0?0:exactNet/input*100);
        }
        var netProduced=Math.Max(0,produced-added);
        return new(input,produced,added,outputSortex,pans,input==0?0:netProduced/input*100);
    }

    public async Task<ExcelExportResult> ExportAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        from = from.Date;
        to = to.Date;
        if (to < from) throw new ArgumentException("Bitiş tarihi başlangıç tarihinden önce olamaz.");
        var isoYears=Enumerable.Range(0,(to-from).Days+1).Select(day=>ISOWeek.GetYear(from.AddDays(day))).Distinct().ToList();
        if(isoYears.Count!=1)
            throw new InvalidOperationException("Tek aktarımda yalnızca bir ISO yılı seçilebilir. Yılları ayrı ayrı aktarın.");
        if(!await ExportLock.WaitAsync(0,cancellationToken))
            throw new InvalidOperationException("Şu anda başka bir Excel aktarımı devam ediyor. İşlem tamamlandıktan sonra tekrar deneyin.");
        try
        {
            var workbookPath=WorkbookPathForYear(isoYears[0]);
            EnsureYearWorkbook(workbookPath);
            await EnsureSchemaAsync(cancellationToken);
            var data = await LoadAsync(from, to, workbookPath, cancellationToken);
            var count = data.Islama.Count + data.Kavurma.Count + data.Paketleme.Count + data.Dolum.Count + data.Kepek.Count + data.Deletions.Count;
            if (count == 0) return new ExcelExportResult(0, 0, workbookPath, "");

            Directory.CreateDirectory(_backupDirectory);
            EnsureWorkbookIsAvailable(workbookPath);
            var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            var backupPath = Path.Combine(_backupDirectory, $"{Path.GetFileNameWithoutExtension(workbookPath)}-{stamp}.xlsx");
            var tempPath = Path.Combine(Path.GetDirectoryName(workbookPath)!, $".{Path.GetFileNameWithoutExtension(workbookPath)}-{Guid.NewGuid():N}.tmp.xlsx");
            File.Copy(workbookPath, backupPath, false);

            var marks = new List<ExportMark>(count);
            var sheetNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var replaced = false;
            try
            {
                using (var workbook = new XLWorkbook(workbookPath))
                {
                    foreach (var deletion in data.Deletions)
                    {
                        if (workbook.Worksheets.TryGetWorksheet(deletion.Sheet, out var deleteSheet))
                        {
                            ClearRecordRow(deleteSheet, deletion.Table, deletion.Row);
                            sheetNames.Add(deleteSheet.Name);
                        }
                    }
                    foreach (var item in data.Islama) WriteTracked(workbook,data,marks,sheetNames,"Islama",item.Id,item.SoymaBitisi,(s,r)=>WriteIslama(s,item,r));
                    foreach (var item in data.Kavurma) WriteTracked(workbook,data,marks,sheetNames,"Kavurma",item.Id,item.Tarih,(s,r)=>WriteKavurma(s,item,r));
                    foreach (var item in data.Paketleme) WriteTracked(workbook,data,marks,sheetNames,"Paketleme",item.Id,item.Tarih,(s,r)=>WritePaketleme(s,item,r));
                    foreach (var item in data.Dolum) WriteTracked(workbook,data,marks,sheetNames,"Dolum",item.Id,item.Tarih,(s,r)=>WriteDolum(s,item,r));
                    foreach (var item in data.Kepek) WriteTracked(workbook,data,marks,sheetNames,"KavurmaKepek",item.Id,item.Tarih,(s,r)=>WriteKepek(s,item,r));
                    workbook.SaveAs(tempPath);
                }

                File.Move(tempPath, workbookPath, true);
                replaced = true;
                await SaveMarksAsync(marks, data.Deletions, workbookPath, cancellationToken);
                return new ExcelExportResult(count, sheetNames.Count, workbookPath, backupPath);
            }
            catch (Exception ex)
            {
                if (File.Exists(tempPath)) File.Delete(tempPath);
                if (replaced) File.Copy(backupPath, workbookPath, true);
                await LogFailureAsync(ex, workbookPath, cancellationToken);
                throw;
            }
        }
        finally { ExportLock.Release(); }
    }

    private void WriteTracked(XLWorkbook workbook,ExportData data,List<ExportMark> marks,HashSet<string> sheetNames,
        string table,long id,DateTime date,Func<IXLWorksheet,int?,int> write)
    {
        var desired=GetWeekSheet(workbook,date);
        var (sheet,existingRow)=ResolveWriteTarget(workbook,desired,table,id,data);
        var row=write(sheet,existingRow);
        marks.Add(new(table,id,sheet.Name,row));
        sheetNames.Add(sheet.Name);
    }

    private IXLWorksheet GetWeekSheet(XLWorkbook workbook, DateTime date)
    {
        var isoYear = ISOWeek.GetYear(date);
        var week = ISOWeek.GetWeekOfYear(date);
        // Her dosya tek bir yıla aittir; aynı dosyada hafta numarası yeterlidir.
        var existing = workbook.Worksheets.FirstOrDefault(x => TryGetWeek(x.Name) == week);
        if (existing is not null) return existing;
        if (!workbook.Worksheets.TryGetWorksheet("ŞABLON", out var template) &&
            !workbook.Worksheets.TryGetWorksheet(_templateSheet, out template))
            throw new InvalidOperationException($"Excel şablon sayfası bulunamadı: {_templateSheet}");

        var name = $"{week}. Hafta";
        var sheet = template.CopyTo(name);
        InitializeWeekSheet(sheet, isoYear, week);
        return sheet;
    }

    private static void InitializeWeekSheet(IXLWorksheet sheet, int year, int week)
    {
        var islamaHeader = FindRow(sheet, 1, "Barkod Seri");
        var kavurmaTitle = FindRow(sheet, 1, "KAVURMA TABLOSU");
        var paketlemeTitle = FindRow(sheet, 1, "PAKETLEME TABLOSU");
        var kepekTitle = FindRow(sheet, 13, "KURUTULMUŞ KEPEK TABLOSU");
        var dolumHeader = FindRow(sheet, 13, "tarih", paketlemeTitle);
        var kepekHeader = FindRow(sheet, 13, "Tarih", kepekTitle);
        var lastRow = Math.Max(sheet.LastRowUsed()?.RowNumber() ?? 120, kepekHeader + 8);

        sheet.Range(islamaHeader + 1, 1, kavurmaTitle - 1, 21).Clear(XLClearOptions.Contents);
        for (var row = islamaHeader + 1; row < kavurmaTitle; row++)
        {
            sheet.Cell(row, 10).FormulaA1 = $"IFERROR((I{row}-H{row})*1440,\"\")";
            sheet.Cell(row, 11).FormulaA1 = $"IFERROR((M{row}/J{row})*60,\"\")";
            sheet.Cell(row, 17).FormulaA1 = $"IF(M{row}=\"\",\"\",M{row})";
        }

        sheet.Range(kavurmaTitle + 2, 1, paketlemeTitle - 1, 17).Clear(XLClearOptions.Contents);
        for (var row = kavurmaTitle + 2; row < paketlemeTitle; row++)
        {
            sheet.Cell(row, 13).FormulaA1 = $"IFERROR(C{row}/E{row},\"\")";
            sheet.Cell(row, 14).FormulaA1 = $"IF(C{row}=\"\",\"\",C{row})";
        }

        sheet.Range(paketlemeTitle + 2, 1, lastRow, 11).Clear(XLClearOptions.Contents);
        for (var row = paketlemeTitle + 2; row <= lastRow; row++)
            sheet.Cell(row, 1).FormulaA1 = $"IFERROR(B{row}*C{row},0)";

        sheet.Range(dolumHeader + 1, 13, kepekTitle - 1, 23).Clear(XLClearOptions.Contents);
        for (var row = dolumHeader + 1; row < kepekTitle; row++)
            sheet.Cell(row, 17).FormulaA1 = $"IFERROR(O{row}*P{row},0)";

        sheet.Range(kepekHeader + 1, 13, lastRow, 21).Clear(XLClearOptions.Contents);
        var monday = ISOWeek.ToDateTime(year, week, DayOfWeek.Monday);
        var sunday = monday.AddDays(6);
        sheet.Cell(1, 1).Value = $"HAFTALIK ISLAMA-SOYMA TABLOSU {week}. HAFTA ({monday:dd.MM.yyyy}-{sunday:dd.MM.yyyy})";
    }

    private static int WriteIslama(IXLWorksheet sheet, IslamaExportRow item, int? existingRow = null)
    {
        var header = FindRow(sheet, 1, "Barkod Seri");
        var boundary = FindRow(sheet, 1, "KAVURMA TABLOSU");
        var headers = new[]{"Havuz No","Islama Tarihi","Islama Başlangıç - Bitiş","Soyma Başlangıcı",
            "Soyma Bitişi","Soyma Süresi (dk)","Saatlik Tonaj","Ekran Tonajı","Çekilen Tonaj",
            "Silo","Menşei","Ürün","Net Tonaj","Açıklama","Salamura Derecesi","Yedek Derecesi"};
        for(var column=6;column<=21;column++)sheet.Cell(header,column).Value=headers[column-6];
        var row = existingRow ?? NextRowBefore(sheet, header + 1, boundary, r => HasValue(sheet.Cell(r, 4)));
        Set(sheet.Cell(row, 1), item.BarkodSeri);
        SetDate(sheet.Cell(row, 2), item.HamSusamGelisTarihi, "dd.MM.yyyy");
        Set(sheet.Cell(row, 3), item.CopKg);
        sheet.Cell(row, 4).Value = item.PartiNo;
        SetDate(sheet.Cell(row, 5), item.NobetTarihi, "dd.MM.yyyy");
        Set(sheet.Cell(row, 6), item.HavuzNo);
        SetDate(sheet.Cell(row, 7), item.IslamaBaslangici?.Date, "dd.MM.yyyy");
        Set(sheet.Cell(row, 8), TimeRange(item.IslamaBaslangici, item.IslamaBitisi));
        SetDate(sheet.Cell(row, 9), item.SoymaBaslangici, "dd.MM.yyyy HH:mm");
        SetDate(sheet.Cell(row, 10), item.SoymaBitisi, "dd.MM.yyyy HH:mm");
        sheet.Cell(row, 11).FormulaA1 = $"(J{row}-I{row})*1440";
        sheet.Cell(row, 12).FormulaA1 = $"IFERROR((N{row}/K{row})*60,\"\")";
        Set(sheet.Cell(row, 13), item.EkranTonajiKg);
        sheet.Cell(row, 14).Value = item.CekilenTonajKg;
        Set(sheet.Cell(row, 15), item.Silo);
        sheet.Cell(row, 16).Value = item.Mensei;
        sheet.Cell(row, 17).Value = item.Urun;
        sheet.Cell(row, 18).FormulaA1 = $"N{row}";
        Set(sheet.Cell(row,19),item.Aciklama);
        Set(sheet.Cell(row,20),item.SalamuraDerecesi);
        Set(sheet.Cell(row,21),item.YedekDerecesi);
        return row;
    }

    private static int WriteKavurma(IXLWorksheet sheet, KavurmaExportRow item, int? existingRow = null)
    {
        var title = FindRow(sheet, 1, "KAVURMA TABLOSU");
        var packetTitle = FindRow(sheet, 1, "PAKETLEME TABLOSU");
        sheet.Cell(title+1,15).Value="Açıklama";
        sheet.Cell(title+1,16).Value="Kavurma Sıcaklığı (°C)";
        sheet.Cell(title+1,17).Value="Nişasta (kg)";
        var boundary = FindFormulaRow(sheet, 3, "SUM(", title, packetTitle) ?? packetTitle;
        var row = existingRow ?? NextRowBefore(sheet, title + 2, boundary, r => HasValue(sheet.Cell(r, 3)));
        Set(sheet.Cell(row, 1), item.PartiNo);
        Set(sheet.Cell(row, 2), item.EkranTonajiKg);
        sheet.Cell(row, 3).Value = item.NetTonajKg;
        Set(sheet.Cell(row, 4), item.Personel);
        Set(sheet.Cell(row, 5), item.TavaSayisi);
        Set(sheet.Cell(row, 6), item.ArizaliTavaSayisi);
        Set(sheet.Cell(row, 7), item.CikanSorteksAltiKg);
        Set(sheet.Cell(row, 8), item.EklenenSorteksAltiKg);
        Set(sheet.Cell(row, 9), item.Mensei);
        Set(sheet.Cell(row, 10), item.Urun);
        Set(sheet.Cell(row, 11), item.OrtalamaVerimOrani);
        Set(sheet.Cell(row, 12), item.VerimOrani);
        sheet.Cell(row, 13).FormulaA1 = $"IFERROR(C{row}/E{row},\"\")";
        sheet.Cell(row, 14).FormulaA1 = $"C{row}";
        Set(sheet.Cell(row,15),item.Aciklama);
        Set(sheet.Cell(row,16),item.KavurmaSicakligi);
        Set(sheet.Cell(row,17),item.NisastaKg);
        return row;
    }

    private static int WritePaketleme(IXLWorksheet sheet, PaketlemeExportRow item, int? existingRow = null)
    {
        var title = FindRow(sheet, 1, "PAKETLEME TABLOSU");
        var start = title + 2;
        sheet.Cell(title + 1, 11).Value = "Fire Miktarı (kg)";
        var row = existingRow ?? NextOpenRow(sheet, start, r => HasValue(sheet.Cell(r, 2)) || HasValue(sheet.Cell(r, 3)), 1, 11);
        sheet.Cell(row, 1).FormulaA1 = $"B{row}*C{row}";
        sheet.Cell(row, 2).Value = item.AmbalajAgirligiKg;
        sheet.Cell(row, 3).Value = item.Adet;
        Set(sheet.Cell(row, 4), item.CikanSorteksAltiKg);
        Set(sheet.Cell(row, 5), item.Mensei);
        Set(sheet.Cell(row, 6), item.Urun);
        Set(sheet.Cell(row, 7), item.SorteksAltiOrani);
        Set(sheet.Cell(row, 8), item.Personel);
        Set(sheet.Cell(row, 9), item.Aciklama);
        Set(sheet.Cell(row, 10), item.VerimOrani);
        Set(sheet.Cell(row, 11), item.FireKg);
        return row;
    }

    private static int WriteDolum(IXLWorksheet sheet, DolumExportRow item, int? existingRow = null)
    {
        var packetTitle = FindRow(sheet, 1, "PAKETLEME TABLOSU");
        var header = FindRow(sheet, 13, "tarih", packetTitle);
        var boundary = FindRow(sheet, 13, "KURUTULMUŞ KEPEK TABLOSU");
        sheet.Cell(header, 18).Value = "Ürün Menşei";
        sheet.Cell(header, 22).Value = "Fire Miktarı (kg)";
        sheet.Cell(header, 23).Value = "Tank";
        var row = existingRow ?? NextRowBefore(sheet, header + 1, boundary, r => HasValue(sheet.Cell(r, 16)));
        SetDate(sheet.Cell(row, 13), item.Tarih, "dd.MM.yyyy");
        sheet.Cell(row, 14).Value = item.AmbalajCinsi;
        sheet.Cell(row, 15).Value = item.AmbalajKg;
        sheet.Cell(row, 16).Value = item.PaketlemeAdedi;
        sheet.Cell(row, 17).FormulaA1 = $"O{row}*P{row}";
        Set(sheet.Cell(row, 18), item.Mensei);
        Set(sheet.Cell(row, 19), item.Personel);
        Set(sheet.Cell(row, 20), item.PersonelSayisi);
        Set(sheet.Cell(row, 21), item.Aciklama);
        Set(sheet.Cell(row, 22), item.FireKg);
        Set(sheet.Cell(row, 23), item.Tank);
        return row;
    }

    private static int WriteKepek(IXLWorksheet sheet, KepekExportRow item, int? existingRow = null)
    {
        var title = FindRow(sheet, 13, "KURUTULMUŞ KEPEK TABLOSU");
        var header = FindRow(sheet, 13, "Tarih", title);
        var totalRow = sheet.Column(17).CellsUsed()
            .FirstOrDefault(x => x.Address.RowNumber > header && HasValue(x))?.Address.RowNumber;
        var row = existingRow ?? (totalRow.HasValue
            ? NextRowBefore(sheet, header + 1, totalRow.Value, r => HasValue(sheet.Cell(r, 16)))
            : NextOpenRow(sheet, header + 1, r => HasValue(sheet.Cell(r, 16)), 13, 21));
        SetDate(sheet.Cell(row, 13), item.Tarih, "dd.MM.yyyy");
        sheet.Cell(row, 16).Value = item.PaketlemeMiktariKg;
        Set(sheet.Cell(row, 18), item.UrunCinsi);
        Set(sheet.Cell(row, 19), item.Personel);
        Set(sheet.Cell(row, 20), item.PersonelSayisi);
        return row;
    }

    private static (IXLWorksheet Sheet, int? ExistingRow) ResolveWriteTarget(
        XLWorkbook workbook, IXLWorksheet desired, string table, long id, ExportData data)
    {
        if (!data.Locations.TryGetValue($"{table}:{id}", out var location) ||
            !workbook.Worksheets.TryGetWorksheet(location.Sheet, out var previous))
            return (desired, null);

        if (string.Equals(previous.Name, desired.Name, StringComparison.OrdinalIgnoreCase) ||
            TryGetWeek(previous.Name) == TryGetWeek(desired.Name))
            return (previous, location.Row);

        ClearRecordRow(previous, table, location.Row);
        return (desired, null);
    }

    private static void ClearRecordRow(IXLWorksheet sheet, string table, int row)
    {
        var (first, last) = table switch
        {
            "Islama" => (1,21),
            "Kavurma" => (1,17),
            "Paketleme" => (1,11),
            "Dolum" => (13,23),
            "Kepek" => (13,21),
            "KavurmaKepek" => (13,21),
            _ => throw new ArgumentOutOfRangeException(nameof(table))
        };
        sheet.Range(row, first, row, last).Clear(XLClearOptions.Contents);
    }

    private static int NextRowBefore(IXLWorksheet sheet, int start, int boundary, Func<int, bool> used)
    {
        var last = start - 1;
        for (var row = start; row < boundary; row++) if (used(row)) last = row;
        var next = last + 1;
        if (next < boundary) return next;

        sheet.Row(boundary).InsertRowsAbove(1);
        sheet.Cell(boundary, 1).CopyFrom(sheet.Range(boundary - 1, 1, boundary - 1, 36));
        sheet.Range(boundary, 1, boundary, 36).Clear(XLClearOptions.Contents);
        return boundary;
    }

    private static int NextOpenRow(IXLWorksheet sheet, int start, Func<int, bool> used, int firstColumn, int lastColumn)
    {
        var scanEnd = Math.Max(sheet.LastRowUsed()?.RowNumber() ?? start, start + 40);
        var last = start - 1;
        for (var row = start; row <= scanEnd; row++) if (used(row)) last = row;
        var next = last + 1;
        if (next <= scanEnd) return next;

        sheet.Cell(next, firstColumn).CopyFrom(sheet.Range(next - 1, firstColumn, next - 1, lastColumn));
        sheet.Range(next, firstColumn, next, lastColumn).Clear(XLClearOptions.Contents);
        return next;
    }

    private static int FindRow(IXLWorksheet sheet, int column, string text, int afterRow = 0)
    {
        var cell = sheet.Column(column).CellsUsed()
            .FirstOrDefault(x => x.Address.RowNumber > afterRow &&
                string.Equals(x.GetString().Trim(), text, StringComparison.OrdinalIgnoreCase));
        return cell?.Address.RowNumber
            ?? throw new InvalidOperationException($"'{sheet.Name}' sayfasında '{text}' başlığı bulunamadı.");
    }

    private static int? FindFormulaRow(IXLWorksheet sheet, int column, string formulaText, int afterRow, int beforeRow)
    {
        return sheet.Column(column).CellsUsed()
            .FirstOrDefault(x => x.Address.RowNumber > afterRow && x.Address.RowNumber < beforeRow &&
                x.HasFormula && x.FormulaA1.Contains(formulaText, StringComparison.OrdinalIgnoreCase))
            ?.Address.RowNumber;
    }

    private static bool HasValue(IXLCell cell) => !cell.IsEmpty() && !string.IsNullOrWhiteSpace(cell.GetString());

    private static void Set(IXLCell cell, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) cell.Clear(XLClearOptions.Contents); else cell.Value = value;
    }

    private static void Set(IXLCell cell, decimal? value)
    {
        if (value.HasValue) cell.Value = value.Value; else cell.Clear(XLClearOptions.Contents);
    }

    private static void Set(IXLCell cell, int? value)
    {
        if (value.HasValue) cell.Value = value.Value; else cell.Clear(XLClearOptions.Contents);
    }

    private static void SetDate(IXLCell cell, DateTime? value, string format)
    {
        if (!value.HasValue) { cell.Clear(XLClearOptions.Contents); return; }
        cell.Value = value.Value;
        cell.Style.DateFormat.Format = format;
    }

    private static string? TimeRange(DateTime? start, DateTime? end) =>
        start.HasValue && end.HasValue ? $"{start:HH:mm}-{end:HH:mm}" : null;

    private static int? TryGetWeek(string name)
    {
        var match = Regex.Match(name, @"^\s*(\d{1,2})\s*\.?\s*hafta", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return match.Success && int.TryParse(match.Groups[1].Value, out var week) ? week : null;
    }

    private static void EnsureWorkbookIsAvailable(string workbookPath)
    {
        try { using var _ = new FileStream(workbookPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None); }
        catch (IOException) { throw new InvalidOperationException("Excel dosyası açık. Dosyayı Excel'de kapatıp tekrar deneyin."); }
    }

    private static string ResolvePath(string contentRoot, string configuredPath)
    {
        if (string.IsNullOrWhiteSpace(configuredPath)) throw new InvalidOperationException("Excel dosya yolu ayarlanmamış.");
        return Path.GetFullPath(Path.IsPathRooted(configuredPath) ? configuredPath : Path.Combine(contentRoot, configuredPath));
    }

    private string WorkbookPathForYear(int year)=>_workbookPathPattern.Contains("{year}",StringComparison.OrdinalIgnoreCase)
        ?_workbookPathPattern.Replace("{year}",year.ToString(CultureInfo.InvariantCulture),StringComparison.OrdinalIgnoreCase)
        :_workbookPathPattern;

    private void EnsureYearWorkbook(string workbookPath)
    {
        if(File.Exists(workbookPath))return;
        if(!File.Exists(_templateWorkbookPath))throw new FileNotFoundException("Excel şablon dosyası bulunamadı.",_templateWorkbookPath);
        Directory.CreateDirectory(Path.GetDirectoryName(workbookPath)!);
        using var source=new XLWorkbook(_templateWorkbookPath);
        if(!source.Worksheets.TryGetWorksheet("DATA",out var dataSheet))
            throw new InvalidOperationException("Excel şablonunda DATA sayfası bulunamadı.");
        if(!source.Worksheets.TryGetWorksheet(_templateSheet,out var templateSheet))
            throw new InvalidOperationException($"Excel şablon sayfası bulunamadı: {_templateSheet}");
        using var target=new XLWorkbook();
        dataSheet.CopyTo(target,"DATA");
        templateSheet.CopyTo(target,"ŞABLON");
        target.SaveAs(workbookPath);
    }

    private async Task EnsureSchemaAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            IF OBJECT_ID(N'uretim.ExcelAktarimDetayi', N'U') IS NULL
            BEGIN
                CREATE TABLE uretim.ExcelAktarimDetayi
                (
                    ExcelAktarimDetayiId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_ExcelAktarimDetayi PRIMARY KEY,
                    TabloAdi varchar(30) NOT NULL,
                    KayitId bigint NOT NULL,
                    DosyaYolu nvarchar(500) NOT NULL,
                    SayfaAdi nvarchar(100) NOT NULL,
                    SatirNo int NOT NULL,
                    AktarimZamani datetime2(0) NOT NULL CONSTRAINT DF_ExcelAktarimDetayi_Zaman DEFAULT(SYSDATETIME()),
                    CONSTRAINT UQ_ExcelAktarimDetayi UNIQUE(TabloAdi, KayitId)
                );
            END;
            IF OBJECT_ID(N'uretim.ExcelAktarimDetayi',N'U') IS NOT NULL AND COL_LENGTH('uretim.ExcelAktarimDetayi','BekleyenIslem') IS NULL
                EXEC(N'ALTER TABLE uretim.ExcelAktarimDetayi ADD BekleyenIslem varchar(10) NULL;');
            """;
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<ExportData> LoadAsync(DateTime from, DateTime to,string workbookPath, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        var data = new ExportData();
        var end = to.AddDays(1);

        data.Islama.AddRange(await ReadIslamaAsync(connection, from, end, cancellationToken));
        data.Kavurma.AddRange(await ReadKavurmaAsync(connection, from, end, cancellationToken));
        data.Paketleme.AddRange(await ReadPaketlemeAsync(connection, from, end, cancellationToken));
        data.Dolum.AddRange(await ReadDolumAsync(connection, from, end, cancellationToken));
        data.Kepek.AddRange(await ReadKavurmaKepekAsync(connection, from, end, cancellationToken));
        await ReadPendingChangesAsync(connection, data,workbookPath,cancellationToken);
        return data;
    }

    private static async Task<List<IslamaExportRow>> ReadIslamaAsync(SqlConnection connection, DateTime from, DateTime end, CancellationToken ct)
    {
        const string sql = """
            SELECT I.IslamaSoymaKaydiId,I.BarkodSeri,I.HamSusamGelisTarihi,I.CopKg,I.PartiNo,I.NobetTarihi,I.HavuzNo,
                   I.IslamaBaslangici,I.IslamaBitisi,I.SoymaBaslangici,I.SoymaBitisi,I.EkranTonajiKg,I.CekilenTonajKg,
                   COALESCE(NULLIF(CONCAT(I.Silo1,' ',I.Silo2),' '),S.Kod),M.Ad,U.Ad,I.Aciklama,I.SalamuraDerecesi,I.YedekDerecesi
            FROM uretim.IslamaSoymaKaydi I
            JOIN tanim.Mensei M ON M.MenseiId=I.MenseiId JOIN tanim.Urun U ON U.UrunId=I.UrunId
            LEFT JOIN tanim.Silo S ON S.SiloId=I.SiloId
            WHERE I.KaynakSayfa IS NULL AND I.SoymaBitisi>=@From AND I.SoymaBitisi<@End
              AND (NOT EXISTS(SELECT 1 FROM uretim.ExcelAktarimDetayi X WHERE X.TabloAdi='Islama' AND X.KayitId=I.IslamaSoymaKaydiId)
                   OR EXISTS(SELECT 1 FROM uretim.ExcelAktarimDetayi X WHERE X.TabloAdi='Islama' AND X.KayitId=I.IslamaSoymaKaydiId AND X.BekleyenIslem='Guncelle'))
            ORDER BY I.SoymaBitisi,I.IslamaSoymaKaydiId;
            """;
        var list = new List<IslamaExportRow>();
        await using var command = DateCommand(sql, connection, from, end); await using var r = await command.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct)) list.Add(new(r.GetInt64(0),S(r,1),D<DateTime>(r,2),D<decimal>(r,3),r.GetString(4),D<DateTime>(r,5),S(r,6),D<DateTime>(r,7),D<DateTime>(r,8),r.GetDateTime(9),r.GetDateTime(10),D<decimal>(r,11),r.GetDecimal(12),S(r,13),r.GetString(14),r.GetString(15),S(r,16),D<decimal>(r,17),D<decimal>(r,18)));
        return list;
    }

    private static async Task<List<KavurmaExportRow>> ReadKavurmaAsync(SqlConnection connection, DateTime from, DateTime end, CancellationToken ct)
    {
        const string sql = """
            SELECT K.KavurmaKaydiId,COALESCE(K.Tarih,CONVERT(date,K.OlusturmaZamani)),K.PartiNo,K.EkranTonajiKg,K.NetTonajKg,
                   P.AdSoyad,K.TavaSayisi,K.ArizaliTavaSayisi,K.CikanSorteksAltiKg,K.EklenenSorteksAltiKg,M.Ad,U.Ad,K.OrtalamaVerimOrani,K.VerimOrani,K.Aciklama,K.KavurmaSicakligi,K.NisastaKg
            FROM uretim.KavurmaKaydi K LEFT JOIN tanim.Personel P ON P.PersonelId=K.PersonelId
            LEFT JOIN tanim.Mensei M ON M.MenseiId=K.MenseiId LEFT JOIN tanim.Urun U ON U.UrunId=K.UrunId
            WHERE K.KaynakSayfa IS NULL AND COALESCE(K.Tarih,CONVERT(date,K.OlusturmaZamani))>=@From AND COALESCE(K.Tarih,CONVERT(date,K.OlusturmaZamani))<@End
              AND (NOT EXISTS(SELECT 1 FROM uretim.ExcelAktarimDetayi X WHERE X.TabloAdi='Kavurma' AND X.KayitId=K.KavurmaKaydiId)
                   OR EXISTS(SELECT 1 FROM uretim.ExcelAktarimDetayi X WHERE X.TabloAdi='Kavurma' AND X.KayitId=K.KavurmaKaydiId AND X.BekleyenIslem='Guncelle'))
            ORDER BY COALESCE(K.Tarih,CONVERT(date,K.OlusturmaZamani)),K.KavurmaKaydiId;
            """;
        var list = new List<KavurmaExportRow>();
        await using var command = DateCommand(sql, connection, from, end); await using var r = await command.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct)) list.Add(new(r.GetInt64(0),r.GetDateTime(1),S(r,2),D<decimal>(r,3),r.GetDecimal(4),S(r,5),D<int>(r,6),D<int>(r,7),D<decimal>(r,8),D<decimal>(r,9),S(r,10),S(r,11),D<decimal>(r,12),D<decimal>(r,13),S(r,14),D<decimal>(r,15),D<decimal>(r,16)));
        return list;
    }

    private static async Task<List<PaketlemeExportRow>> ReadPaketlemeAsync(SqlConnection connection, DateTime from, DateTime end, CancellationToken ct)
    {
        const string sql = """
            SELECT P.PaketlemeKaydiId,COALESCE(P.Tarih,CONVERT(date,P.OlusturmaZamani)),P.AmbalajAgirligiKg,P.Adet,P.CikanSorteksAltiKg,P.FireKg,
                   M.Ad,U.Ad,P.SorteksAltiOrani,PE.AdSoyad,P.Aciklama,P.VerimOrani
            FROM uretim.PaketlemeKaydi P LEFT JOIN tanim.Mensei M ON M.MenseiId=P.MenseiId
            LEFT JOIN tanim.Urun U ON U.UrunId=P.UrunId LEFT JOIN tanim.Personel PE ON PE.PersonelId=P.PersonelId
            WHERE P.KaynakSayfa IS NULL AND COALESCE(P.Tarih,CONVERT(date,P.OlusturmaZamani))>=@From AND COALESCE(P.Tarih,CONVERT(date,P.OlusturmaZamani))<@End
              AND (NOT EXISTS(SELECT 1 FROM uretim.ExcelAktarimDetayi X WHERE X.TabloAdi='Paketleme' AND X.KayitId=P.PaketlemeKaydiId)
                   OR EXISTS(SELECT 1 FROM uretim.ExcelAktarimDetayi X WHERE X.TabloAdi='Paketleme' AND X.KayitId=P.PaketlemeKaydiId AND X.BekleyenIslem='Guncelle'))
            ORDER BY COALESCE(P.Tarih,CONVERT(date,P.OlusturmaZamani)),P.PaketlemeKaydiId;
            """;
        var list = new List<PaketlemeExportRow>();
        await using var command = DateCommand(sql, connection, from, end); await using var r = await command.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct)) list.Add(new(r.GetInt64(0),r.GetDateTime(1),r.GetDecimal(2),r.GetInt32(3),D<decimal>(r,4),D<decimal>(r,5),S(r,6),S(r,7),D<decimal>(r,8),S(r,9),S(r,10),D<decimal>(r,11)));
        return list;
    }

    private static async Task<List<DolumExportRow>> ReadDolumAsync(SqlConnection connection, DateTime from, DateTime end, CancellationToken ct)
    {
        const string sql = """
            SELECT D.DolumKaydiId,COALESCE(D.Tarih,CONVERT(date,D.OlusturmaZamani)),D.AmbalajCinsi,D.AmbalajKg,D.PaketlemeAdedi,D.FireKg,
                   M.Ad,D.Tank,D.Personel,D.PersonelSayisi,D.Aciklama
            FROM uretim.DolumKaydi D LEFT JOIN tanim.Mensei M ON M.MenseiId=D.MenseiId
            WHERE D.KaynakSayfa IS NULL AND COALESCE(D.Tarih,CONVERT(date,D.OlusturmaZamani))>=@From AND COALESCE(D.Tarih,CONVERT(date,D.OlusturmaZamani))<@End
              AND (NOT EXISTS(SELECT 1 FROM uretim.ExcelAktarimDetayi X WHERE X.TabloAdi='Dolum' AND X.KayitId=D.DolumKaydiId)
                   OR EXISTS(SELECT 1 FROM uretim.ExcelAktarimDetayi X WHERE X.TabloAdi='Dolum' AND X.KayitId=D.DolumKaydiId AND X.BekleyenIslem='Guncelle'))
            ORDER BY COALESCE(D.Tarih,CONVERT(date,D.OlusturmaZamani)),D.DolumKaydiId;
            """;
        var list = new List<DolumExportRow>();
        await using var command = DateCommand(sql, connection, from, end); await using var r = await command.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct)) list.Add(new(r.GetInt64(0),r.GetDateTime(1),r.GetString(2),r.GetDecimal(3),r.GetInt32(4),D<decimal>(r,5),S(r,6),S(r,7),S(r,8),D<int>(r,9),S(r,10)));
        return list;
    }

    private static async Task<List<KepekExportRow>> ReadKavurmaKepekAsync(SqlConnection connection, DateTime from, DateTime end, CancellationToken ct)
    {
        const string sql = """
            SELECT K.KavurmaKaydiId,COALESCE(K.Tarih,CONVERT(date,K.OlusturmaZamani)),K.KepekKg,U.Ad,P.AdSoyad
            FROM uretim.KavurmaKaydi K
            LEFT JOIN tanim.Urun U ON U.UrunId=K.UrunId
            LEFT JOIN tanim.Personel P ON P.PersonelId=K.PersonelId
            WHERE K.KaynakSayfa IS NULL AND COALESCE(K.KepekKg,0)>0
              AND COALESCE(K.Tarih,CONVERT(date,K.OlusturmaZamani))>=@From AND COALESCE(K.Tarih,CONVERT(date,K.OlusturmaZamani))<@End
              AND (NOT EXISTS(SELECT 1 FROM uretim.ExcelAktarimDetayi X WHERE X.TabloAdi='KavurmaKepek' AND X.KayitId=K.KavurmaKaydiId)
                   OR EXISTS(SELECT 1 FROM uretim.ExcelAktarimDetayi X WHERE X.TabloAdi='KavurmaKepek' AND X.KayitId=K.KavurmaKaydiId AND X.BekleyenIslem='Guncelle'))
            ORDER BY COALESCE(K.Tarih,CONVERT(date,K.OlusturmaZamani)),K.KavurmaKaydiId;
            """;
        var list = new List<KepekExportRow>();
        await using var command = DateCommand(sql, connection, from, end); await using var r = await command.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct)) list.Add(new(r.GetInt64(0),r.GetDateTime(1),r.GetDecimal(2),S(r,3),S(r,4),null));
        return list;
    }

    private static async Task ReadPendingChangesAsync(SqlConnection connection, ExportData data,string workbookPath,CancellationToken ct)
    {
        const string sql = """
            SELECT TabloAdi,KayitId,SayfaAdi,SatirNo,BekleyenIslem
            FROM uretim.ExcelAktarimDetayi
            WHERE BekleyenIslem IN ('Guncelle','Sil') AND DosyaYolu=@Path;
            """;
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Path",workbookPath);
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var location = new ExportLocation(reader.GetString(0),reader.GetInt64(1),reader.GetString(2),reader.GetInt32(3));
            if (reader.GetString(4) == "Sil") data.Deletions.Add(location);
            else data.Locations[$"{location.Table}:{location.Id}"] = location;
        }
    }

    private async Task SaveMarksAsync(List<ExportMark> marks, List<ExportLocation> deletions, string workbookPath, CancellationToken ct)
    {
        await using var connection = new SqlConnection(_connectionString); await connection.OpenAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        try
        {
            const string detailSql = """
                IF EXISTS(SELECT 1 FROM uretim.ExcelAktarimDetayi WHERE TabloAdi=@Table AND KayitId=@Id)
                    UPDATE uretim.ExcelAktarimDetayi SET DosyaYolu=@Path,SayfaAdi=@Sheet,SatirNo=@Row,BekleyenIslem=NULL,AktarimZamani=SYSDATETIME()
                    WHERE TabloAdi=@Table AND KayitId=@Id;
                ELSE
                    INSERT uretim.ExcelAktarimDetayi(TabloAdi,KayitId,DosyaYolu,SayfaAdi,SatirNo,BekleyenIslem)
                    VALUES(@Table,@Id,@Path,@Sheet,@Row,NULL);
                """;
            foreach (var mark in marks)
            {
                await using var command = new SqlCommand(detailSql, connection, (SqlTransaction)transaction);
                command.Parameters.AddWithValue("@Table", mark.Table); command.Parameters.AddWithValue("@Id", mark.Id);
                command.Parameters.AddWithValue("@Path", workbookPath); command.Parameters.AddWithValue("@Sheet", mark.Sheet);
                command.Parameters.AddWithValue("@Row", mark.Row); await command.ExecuteNonQueryAsync(ct);
            }
            const string deleteSql="UPDATE uretim.ExcelAktarimDetayi SET BekleyenIslem=NULL,AktarimZamani=SYSDATETIME() WHERE TabloAdi=@Table AND KayitId=@Id;";
            foreach(var deletion in deletions)
            {
                await using var command=new SqlCommand(deleteSql,connection,(SqlTransaction)transaction);
                command.Parameters.AddWithValue("@Table",deletion.Table);command.Parameters.AddWithValue("@Id",deletion.Id);
                await command.ExecuteNonQueryAsync(ct);
            }
            const string summarySql = "INSERT uretim.ExcelAktarimi(BaslamaZamani,BitisZamani,DosyaYolu,Durum,KayitSayisi,HataMesaji) VALUES(SYSDATETIME(),SYSDATETIME(),@Path,'Basarili',@Count,NULL);";
            await using (var command = new SqlCommand(summarySql, connection, (SqlTransaction)transaction))
            { command.Parameters.AddWithValue("@Path", workbookPath); command.Parameters.AddWithValue("@Count", marks.Count+deletions.Count); await command.ExecuteNonQueryAsync(ct); }
            await transaction.CommitAsync(ct);
        }
        catch { await transaction.RollbackAsync(CancellationToken.None); throw; }
    }

    private async Task LogFailureAsync(Exception exception, string workbookPath, CancellationToken ct)
    {
        try
        {
            await using var connection = new SqlConnection(_connectionString); await connection.OpenAsync(ct);
            const string sql = "INSERT uretim.ExcelAktarimi(BaslamaZamani,BitisZamani,DosyaYolu,Durum,KayitSayisi,HataMesaji) VALUES(SYSDATETIME(),SYSDATETIME(),@Path,'Basarisiz',0,@Error);";
            await using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@Path", workbookPath);
            command.Parameters.AddWithValue("@Error", exception.Message.Length > 2000 ? exception.Message[..2000] : exception.Message);
            await command.ExecuteNonQueryAsync(ct);
        }
        catch { }
    }

    private static SqlCommand DateCommand(string sql, SqlConnection connection, DateTime from, DateTime end)
    {
        var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@From", from); command.Parameters.AddWithValue("@End", end);
        return command;
    }

    private static T? D<T>(SqlDataReader reader, int ordinal) where T : struct => reader.IsDBNull(ordinal) ? null : reader.GetFieldValue<T>(ordinal);
    private static string? S(SqlDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);

    private sealed record ExportMark(string Table, long Id, string Sheet, int Row);
    private sealed record ExportLocation(string Table, long Id, string Sheet, int Row);
    private sealed class ExportData
    {
        public List<IslamaExportRow> Islama { get; } = [];
        public List<KavurmaExportRow> Kavurma { get; } = [];
        public List<PaketlemeExportRow> Paketleme { get; } = [];
        public List<DolumExportRow> Dolum { get; } = [];
        public List<KepekExportRow> Kepek { get; } = [];
        public Dictionary<string,ExportLocation> Locations { get; } = new(StringComparer.OrdinalIgnoreCase);
        public List<ExportLocation> Deletions { get; } = [];
    }
}
