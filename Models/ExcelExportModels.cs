namespace SusamUretim.Web.Models;

public sealed record ExcelExportResult(int RecordCount, int SheetCount, string WorkbookPath, string BackupPath);

public sealed record IslamaExportRow(
    long Id, string? BarkodSeri, DateTime? HamSusamGelisTarihi, decimal? CopKg, string PartiNo,
    DateTime? NobetTarihi, DateTime? IslamaBaslangici, DateTime? IslamaBitisi,
    DateTime SoymaBaslangici, DateTime SoymaBitisi, decimal? EkranTonajiKg, decimal CekilenTonajKg,
    string? Silo, string Mensei, string Urun, string? Aciklama);

public sealed record KavurmaExportRow(
    long Id, DateTime Tarih, string? PartiNo, decimal? EkranTonajiKg, decimal NetTonajKg,
    string? Personel, int? TavaSayisi, int? ArizaliTavaSayisi, decimal? CikanSorteksAltiKg,
    decimal? EklenenSorteksAltiKg, string? Mensei, string? Urun, decimal? OrtalamaVerimOrani,
    decimal? VerimOrani, string? Aciklama);

public sealed record PaketlemeExportRow(
    long Id, DateTime Tarih, decimal AmbalajAgirligiKg, int Adet, decimal? CikanSorteksAltiKg,
    decimal? FireKg, string? Mensei, string? Urun, decimal? SorteksAltiOrani, string? Personel,
    string? Aciklama, decimal? VerimOrani);

public sealed record DolumExportRow(
    long Id, DateTime Tarih, string AmbalajCinsi, decimal AmbalajKg, int PaketlemeAdedi,
    decimal? FireKg, string? Urun, string? Personel, int? PersonelSayisi, string? Aciklama);

public sealed record KepekExportRow(
    long Id, DateTime Tarih, decimal PaketlemeMiktariKg, string? UrunCinsi,
    int? PersonelSayisi, string? Aciklama);
