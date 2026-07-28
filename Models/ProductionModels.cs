using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace SusamUretim.Web.Models;

public sealed record LookupItem(int Id, string Name, decimal? Value = null, string? Extra = null);
public sealed record ExcelDataCatalog(
    IReadOnlyList<string> Origins, IReadOnlyList<string> Products,
    IReadOnlyList<string> RoastingPersonnel, IReadOnlyList<decimal> PackagingWeights,
    IReadOnlyList<string> PackagingPersonnel, IReadOnlyList<string> FillingPackages);
public sealed record ExcelWeeklySummary(
    decimal InputKg, decimal ProducedKg, decimal AddedSortexKg,
    decimal OutputSortexKg, int PanCount, decimal YieldPercent);

public sealed record PersonnelTask(int TaskNumber, string TaskName, string Page);
public sealed record PersonnelAssignment(int PersonelId, string Name, IReadOnlyList<PersonnelTask> Tasks);

public static class ProductionTasks
{
    public static readonly IReadOnlyDictionary<int, (string Code, string Name, string Page)> All =
        new Dictionary<int, (string, string, string)>
        {
            [1] = ("Islama", "Islama · Soyma", "/Islama"),
            [2] = ("Kavurma", "Kavurma", "/Kavurma"),
            [3] = ("Paketleme", "Paketleme", "/Paketleme"),
            [4] = ("Dolum", "Dolum", "/Dolum"),
            [6] = ("Degirmen", "Değirmen", "/Degirmen"),
            [7] = ("Kurutma", "Kurutma", "/Kurutma"),
            [8] = ("Cop", "Çöp", "/Cop")
        };
}

public static class ProductionBatch
{
    public static string WeekPrefix(DateTime date) => $"{ISOWeek.GetYear(date)%100:00}{ISOWeek.GetWeekOfYear(date):00}";
    public static string Format(DateTime date, int sequence) => $"{WeekPrefix(date)}{sequence:00}";
}

public sealed record DashboardTrend(DateTime Date, decimal InputKg, decimal OutputKg);
public sealed record ProductYield(
    string Product, decimal InputKg, decimal RoastedKg, decimal AddedSortexKg,
    decimal PackagedKg, decimal PackagingSortexKg, decimal RoastingYield, decimal PackagingYield, int InputCount);
public sealed record OriginSummary(string Origin, decimal Kg, int Count);
public sealed record ProductOriginSummary(string Product, string Origin, decimal Kg, int Count);
public sealed record PackagingWeightSummary(decimal WeightKg, int Units, decimal TotalKg);
public sealed record PersonnelPerformance(string Personnel, decimal RoastedKg, int PanCount, decimal KgPerPan);
public sealed record ProcessShiftSummary(
    string Nobet, decimal PureKg, decimal InceltilenKg,
    int YikamaSayisi, decimal KirecKg, int MakineSayisi);
public sealed record ProcessDashboardStats(
    int DegirmenNobet, int DegirmenSatir, decimal PureKg, decimal InceltilenKg,
    int KurutmaNobet, int KurutmaSatir, int YikamaSayisi, decimal KirecKg, int MakineSayisi,
    IReadOnlyList<ProcessShiftSummary> Shifts);
public sealed record DashboardStats(
    int Islama, int Kavurma, int Paketleme, int Dolum, int Kepek,
    decimal IslamaKg, decimal KavurmaKg, decimal PaketlemeKg,
    decimal FireKg, decimal Randiman, decimal OrtalamaKavurmaVerimi,
    int TavaSayisi, int ArizaliTavaSayisi, IReadOnlyList<DashboardTrend> Trend,
    IReadOnlyList<ProductYield> ProductYields,
    decimal IslamaKgPerHour, decimal AddedSortexKg, decimal PackagingSortexKg, decimal PackagingYield,
    decimal FillingKg, int FillingUnits, decimal FillingKgPerPerson,
    decimal BranKg, decimal BranRatio, decimal WasteKg, decimal WasteRatio, decimal MassBalance,
    IReadOnlyList<OriginSummary> Origins, IReadOnlyList<ProductOriginSummary> ProductOrigins,
    IReadOnlyList<PackagingWeightSummary> PackagingWeights, IReadOnlyList<PersonnelPerformance> Personnel);

public sealed class RecordFilter
{
    [StringLength(100)] public string? Search { get; set; }
    [DataType(DataType.Date)] public DateTime? From { get; set; }
    [DataType(DataType.Date)] public DateTime? To { get; set; }
    public bool IsActive => !string.IsNullOrWhiteSpace(Search) || From.HasValue || To.HasValue;
}

public sealed record IslamaListItem(
    long Id, string PartiNo, DateTime SoymaBitisi, int SoymaSuresiDakika,
    decimal CekilenTonajKg, decimal? CopKg, string Mensei, string Urun, string? Silo);

public sealed record KavurmaListItem(
    long Id, DateTime? Tarih, string? PartiNo, decimal NetTonajKg,
    string? Personel, decimal? KepekKg, int? TavaSayisi, string? Urun);

public sealed record PaketlemeListItem(
    long Id, DateTime? Tarih, string? PartiNo, decimal MiktarKg,
    decimal AmbalajAgirligiKg, int Adet, decimal? FireKg, string? Urun, string? Personel);

public sealed record DolumListItem(
    long Id, DateTime? Tarih, string? PartiNo, string Ambalaj,
    decimal MiktarKg, int Adet, decimal? FireKg, string? Urun);

public sealed record KepekListItem(
    long Id, DateTime? Tarih, string? PartiNo, decimal MiktarKg,
    string? UrunCinsi, decimal? HamSusamaOrani);

public sealed record DegirmenNobetListItem(
    long Id, DateTime Tarih, string Nobet, string Personel, string? Aciklama,
    int SatirSayisi, decimal PureMiktariKg, decimal InceltilenMiktarKg);

public sealed record KurutmaNobetListItem(
    long Id, DateTime Tarih, string Nobet, string Personel, string? Aciklama,
    int SatirSayisi, int YikamaSayisi, decimal KirecKg, int MakineSayisi);

public sealed record CopListItem(
    long Id, DateTime Tarih, string Mensei, decimal CopKg);

public sealed class IslamaInput
{
    [Required, StringLength(50)] public string PartiNo { get; set; } = "";
    [Required, StringLength(50)] public string? BarkodSeri { get; set; }
    [Required, DataType(DataType.Date)] public DateTime? HamSusamGelisTarihi { get; set; }
    [Required, Range(0, 999999)] public decimal? CopKg { get; set; }
    [Required, DataType(DataType.Date)] public DateTime? NobetTarihi { get; set; }
    [Required] public DateTime? IslamaBaslangici { get; set; }
    [Required] public DateTime? IslamaBitisi { get; set; }
    [Required] public DateTime SoymaBaslangici { get; set; }
    [Required] public DateTime SoymaBitisi { get; set; }
    [Required, Range(0, 999999999)] public decimal? EkranTonajiKg { get; set; }
    [Range(0.001, 999999999, ErrorMessage="Çekilen tonaj 0'dan büyük olmalıdır.")]
    public decimal CekilenTonajKg { get; set; }
    [Required, StringLength(2), RegularExpression("(?i)^[ABC][1-4]$", ErrorMessage="Uygun formatta girin: A1-A4, B1-B4 veya C1-C4.")]
    public string? Silo1 { get; set; }
    [Required, StringLength(2), RegularExpression("(?i)^[ABC][1-4]$", ErrorMessage="Uygun formatta girin: A1-A4, B1-B4 veya C1-C4.")]
    public string? Silo2 { get; set; }
    [Range(1, int.MaxValue)] public int MenseiId { get; set; }
    [Range(1, int.MaxValue)] public int UrunId { get; set; }
    [StringLength(500)] public string? Aciklama { get; set; }
    public int? PersonelId { get; set; }
}

public sealed class KavurmaInput
{
    [Required, DataType(DataType.Date)] public DateTime? Tarih { get; set; } = DateTime.Today;
    [Required, StringLength(50)] public string? PartiNo { get; set; }
    [Required, Range(0, 999999999)] public decimal? EkranTonajiKg { get; set; }
    [Range(0.001, 999999999)] public decimal NetTonajKg { get; set; }
    public int? PersonelId { get; set; }
    [Required, Range(0,999999999)] public decimal? KepekKg { get; set; }
    [Required, Range(0, 100000)] public int? TavaSayisi { get; set; }
    [Required, Range(0, 100000)] public int? ArizaliTavaSayisi { get; set; }
    [Required, Range(0, 999999999)] public decimal? CikanSorteksAltiKg { get; set; }
    [Required, Range(0, 999999999)] public decimal? EklenenSorteksAltiKg { get; set; }
    [Required, Range(1,int.MaxValue)] public int? MenseiId { get; set; }
    [Required, Range(1,int.MaxValue)] public int? UrunId { get; set; }
    [Range(0, 999)] public decimal? OrtalamaVerimOrani { get; set; }
    [Required, Range(0, 999)] public decimal? VerimOrani { get; set; }
    [StringLength(500)] public string? Aciklama { get; set; }
}

public sealed class PaketlemeInput
{
    [Required, DataType(DataType.Date)] public DateTime? Tarih { get; set; } = DateTime.Today;
    [Required, StringLength(50)] public string? PartiNo { get; set; }
    [Range(0.001, 999999)] public decimal AmbalajAgirligiKg { get; set; }
    [Range(1, int.MaxValue)] public int Adet { get; set; }
    [Required, Range(0, 999999999)] public decimal? CikanSorteksAltiKg { get; set; }
    [Required, Range(0, 999999999)] public decimal? FireKg { get; set; }
    [Required, Range(1,int.MaxValue)] public int? MenseiId { get; set; }
    [Required, Range(1,int.MaxValue)] public int? UrunId { get; set; }
    [Required, Range(0, 999)] public decimal? SorteksAltiOrani { get; set; }
    public int? PersonelId { get; set; }
    [StringLength(500)] public string? Aciklama { get; set; }
    [Required, Range(0, 999)] public decimal? VerimOrani { get; set; }
}

public sealed class DolumInput
{
    [Required, DataType(DataType.Date)] public DateTime? Tarih { get; set; } = DateTime.Today;
    [Range(1, int.MaxValue)] public int AmbalajId { get; set; }
    [Range(1, int.MaxValue)] public int PaketlemeAdedi { get; set; }
    [Required, Range(0, 999999999)] public decimal? FireKg { get; set; }
    [Required, Range(1,int.MaxValue)] public int? UrunId { get; set; }
    [StringLength(200)] public string? Personel { get; set; }
    [Required, Range(1, 1000)] public int? PersonelSayisi { get; set; }
    [StringLength(500)] public string? Aciklama { get; set; }
    public int? PersonelId { get; set; }
}

public sealed class KepekInput
{
    [Required, DataType(DataType.Date)] public DateTime? Tarih { get; set; } = DateTime.Today;
    [StringLength(50)] public string? PartiNo { get; set; }
    [Range(0.001, 999999999)] public decimal PaketlemeMiktariKg { get; set; }
    [StringLength(100)] public string? UrunCinsi { get; set; } = "Kepek";
    [Range(0, 1000)] public int? PersonelSayisi { get; set; }
    [Range(0, 999)] public decimal? HamSusamaOrani { get; set; }
    [StringLength(500)] public string? Aciklama { get; set; }
    public int? PersonelId { get; set; }
}

public sealed class DegirmenNobetInput
{
    [Required,DataType(DataType.Date)] public DateTime? Tarih{get;set;}=DateTime.Today;
    [Required,StringLength(30)]
    [RegularExpression("^(07:00 - 15:00|15:00 - 23:00|23:00 - 07:00|08:00 - 20:00|20:00 - 08:00)$",
        ErrorMessage="Geçerli bir nöbet aralığı seçin.")]
    public string Nobet{get;set;}="07:00 - 15:00";
    public int? PersonelId{get;set;}
    [StringLength(1000)] public string? Aciklama{get;set;}
    [MinLength(1,ErrorMessage="En az bir değirmen satırı ekleyin.")]
    public List<DegirmenSatirInput> Satirlar{get;set;}=[new()];
}

public sealed class DegirmenSatirInput
{
    [Required,StringLength(100)] public string FirinNoSergen{get;set;}="";
    [Range(1,int.MaxValue)] public int MenseiId{get;set;}
    [Range(0.001,999999999)] public decimal PureMiktariKg{get;set;}
    [Range(0.001,999999999)] public decimal InceltilenMiktarKg{get;set;}
    [Required,StringLength(100)] public string TransferEdilenTank{get;set;}="";
}

public sealed class KurutmaNobetInput
{
    [Required,DataType(DataType.Date)] public DateTime? Tarih{get;set;}=DateTime.Today;
    [Required,StringLength(30)]
    [RegularExpression("^(07:00 - 15:00|15:00 - 23:00|23:00 - 07:00|08:00 - 20:00|20:00 - 08:00)$",
        ErrorMessage="Geçerli bir nöbet aralığı seçin.")]
    public string Nobet{get;set;}="07:00 - 15:00";
    public int? PersonelId{get;set;}
    [StringLength(1000)] public string? Aciklama{get;set;}
    [MinLength(1,ErrorMessage="En az bir kurutma satırı ekleyin.")]
    public List<KurutmaSatirInput> Satirlar{get;set;}=[new()];
}

public sealed class KurutmaSatirInput
{
    [Range(1,int.MaxValue)] public int MenseiId{get;set;}
    [Range(1,int.MaxValue)] public int UrunId{get;set;}
    [Range(0,100000)] public int YikamaSayisi{get;set;}
    [Range(0,999999999)] public decimal KirecKg{get;set;}
    [Range(0,100000)] public int MakineSayisi{get;set;}
}

public sealed class CopInput
{
    [Required,DataType(DataType.Date)] public DateTime? Tarih{get;set;}=DateTime.Today;
    [Range(1,int.MaxValue,ErrorMessage="Menşei seçimi zorunludur.")] public int MenseiId{get;set;}
    [Range(0.001,999999999,ErrorMessage="Çöp miktarı 0'dan büyük olmalıdır.")] public decimal CopKg{get;set;}
}
