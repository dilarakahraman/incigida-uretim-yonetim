using System.ComponentModel.DataAnnotations;
using SusamUretim.Web.Models;
using SusamUretim.Web.Services;
using Xunit;

namespace SusamUretim.Web.Tests;

public sealed class CoreBehaviorTests
{
    [Fact]
    public void PasswordHash_VerifiesOnlyOriginalPassword()
    {
        var stored = PasswordSecurity.Hash("Guvenli-Sifre-123");

        Assert.True(PasswordSecurity.Verify("Guvenli-Sifre-123", stored));
        Assert.False(PasswordSecurity.Verify("yanlis-sifre", stored));
    }

    [Theory]
    [InlineData("bozuk")]
    [InlineData("120000.gecersiz.gecersiz")]
    [InlineData("")]
    public void PasswordVerify_RejectsMalformedHashes(string stored)
    {
        Assert.False(PasswordSecurity.Verify("herhangi", stored));
    }

    [Theory]
    [InlineData(2026, 1, 1, "2601")]
    [InlineData(2026, 12, 31, "2653")]
    public void ProductionBatch_UsesIsoWeek(int year, int month, int day, string expected)
    {
        Assert.Equal(expected, ProductionBatch.WeekPrefix(new DateTime(year, month, day)));
    }

    [Fact]
    public void TankTransfer_RequiresOriginButNotProduct()
    {
        var input = new TankTransferInput
        {
            TransferZamani = DateTime.Now,
            KaynakTank = "Tank 1",
            HedefTank = "Tank 2",
            MiktarKg = 100,
            MenseiId = null,
            PersonelId = 1
        };

        var results = new List<ValidationResult>();
        var valid = Validator.TryValidateObject(input, new ValidationContext(input), results, true);

        Assert.False(valid);
        Assert.Contains(results, result => result.MemberNames.Contains(nameof(TankTransferInput.MenseiId)));
        Assert.DoesNotContain(results, result => result.MemberNames.Any(name => name.Contains("Urun", StringComparison.OrdinalIgnoreCase)));
    }

    [Theory]
    [InlineData(2026, 12, 31, 2026)]
    [InlineData(2027, 1, 1, 2027)]
    [InlineData(2027, 1, 4, 2027)]
    public void ExcelExportRouting_UsesCalendarYear(int year, int month, int day, int expected)
    {
        Assert.Equal(expected, ExcelExportRouting.Year(new DateTime(year, month, day)));
    }

    [Fact]
    public void ExcelExportRouting_CreatesSeparateYearlyPaths()
    {
        const string pattern = @"C:\Data\Uretim-{year}.xlsx";

        Assert.EndsWith("Uretim-2026.xlsx", ExcelExportRouting.ResolveYearPath(pattern, new DateTime(2026, 7, 31)));
        Assert.EndsWith("Uretim-2027.xlsx", ExcelExportRouting.ResolveYearPath(pattern, new DateTime(2027, 7, 31)));
    }

    [Fact]
    public void ExcelExportRouting_RejectsRangeAcrossCalendarYears()
    {
        Assert.False(ExcelExportRouting.IsSingleYear(new DateTime(2026, 12, 31), new DateTime(2027, 1, 1)));
    }

    [Fact]
    public void IslamaHazirlik_RequiresShiftTonnagesButNotPool()
    {
        var input = new IslamaHazirlikInput
        {
            NobetTarihi = DateTime.Today,
            HamSusamGelisTarihi = DateTime.Today,
            IslamaTarihi = DateTime.Today,
            MenseiId = 1
        };

        var results = new List<ValidationResult>();
        Assert.False(Validator.TryValidateObject(input, new ValidationContext(input), results, true));
        Assert.Contains(results, x => x.MemberNames.Contains(nameof(IslamaHazirlikInput.EkranTonajiKg)));
        Assert.Contains(results, x => x.MemberNames.Contains(nameof(IslamaHazirlikInput.CekilenTonajKg)));
        Assert.DoesNotContain(results, x => x.MemberNames.Contains("HavuzNo"));
    }

    [Fact]
    public void Kavurma_RequiresTemperatureAndStarch()
    {
        var input = new KavurmaInput();
        var results = new List<ValidationResult>();

        Assert.False(Validator.TryValidateObject(input, new ValidationContext(input), results, true));
        Assert.Contains(results, x => x.MemberNames.Contains(nameof(KavurmaInput.KavurmaSicakligi)));
        Assert.Contains(results, x => x.MemberNames.Contains(nameof(KavurmaInput.NisastaKg)));
    }
}
