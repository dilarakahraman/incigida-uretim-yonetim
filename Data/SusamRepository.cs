using System.Data;
using System.Globalization;
using System.Text;
using Microsoft.Data.SqlClient;
using SusamUretim.Web.Models;

namespace SusamUretim.Web.Data;

public sealed class SusamRepository(IConfiguration configuration)
{
    private readonly string _connectionString = configuration.GetConnectionString("SusamUretim")
        ?? throw new InvalidOperationException("SusamUretim connection string bulunamadÄ±.");

    private SqlConnection CreateConnection() => new(_connectionString);

    public async Task EnsureAccessSchemaAsync()
    {
        await ExecuteAsync("""
            IF COL_LENGTH(N'uretim.IslamaSoymaKaydi',N'Silo1') IS NULL
                EXEC(N'ALTER TABLE uretim.IslamaSoymaKaydi ADD Silo1 varchar(2) NULL;');
            IF COL_LENGTH(N'uretim.IslamaSoymaKaydi',N'Silo2') IS NULL
                EXEC(N'ALTER TABLE uretim.IslamaSoymaKaydi ADD Silo2 varchar(2) NULL;');
            IF COL_LENGTH(N'uretim.IslamaSoymaKaydi',N'RaporTarihi') IS NULL
                EXEC(N'ALTER TABLE uretim.IslamaSoymaKaydi ADD RaporTarihi date NULL;');
            IF COL_LENGTH(N'uretim.IslamaSoymaKaydi',N'HavuzNo') IS NULL
                EXEC(N'ALTER TABLE uretim.IslamaSoymaKaydi ADD HavuzNo nvarchar(30) NULL;');
            IF COL_LENGTH(N'uretim.IslamaSoymaKaydi',N'HazirlayanPersonelId') IS NULL
                EXEC(N'ALTER TABLE uretim.IslamaSoymaKaydi ADD HazirlayanPersonelId int NULL;');
            IF COL_LENGTH(N'uretim.IslamaSoymaKaydi',N'IslamaPersonelId') IS NULL
                EXEC(N'ALTER TABLE uretim.IslamaSoymaKaydi ADD IslamaPersonelId int NULL;');
            IF COL_LENGTH(N'uretim.IslamaSoymaKaydi',N'SoymaPersonelId') IS NULL
                EXEC(N'ALTER TABLE uretim.IslamaSoymaKaydi ADD SoymaPersonelId int NULL;');
            IF COL_LENGTH(N'uretim.IslamaSoymaKaydi',N'SalamuraDerecesi') IS NULL
                EXEC(N'ALTER TABLE uretim.IslamaSoymaKaydi ADD SalamuraDerecesi decimal(10,2) NULL;');
            IF COL_LENGTH(N'uretim.IslamaSoymaKaydi',N'YedekDerecesi') IS NULL
                EXEC(N'ALTER TABLE uretim.IslamaSoymaKaydi ADD YedekDerecesi decimal(10,2) NULL;');
            IF COL_LENGTH(N'uretim.KavurmaKaydi',N'KavurmaSicakligi') IS NULL
                EXEC(N'ALTER TABLE uretim.KavurmaKaydi ADD KavurmaSicakligi decimal(10,2) NULL;');
            IF COL_LENGTH(N'uretim.KavurmaKaydi',N'NisastaKg') IS NULL
                EXEC(N'ALTER TABLE uretim.KavurmaKaydi ADD NisastaKg decimal(18,3) NULL;');
            UPDATE uretim.IslamaSoymaKaydi
            SET RaporTarihi=DATEADD(day,-(DATEDIFF(day,CONVERT(date,'19000101',112),CONVERT(date,SoymaBitisi)) % 7),CONVERT(date,SoymaBitisi))
            WHERE SoymaBitisi IS NOT NULL
              AND (RaporTarihi IS NULL OR RaporTarihi<>DATEADD(day,-(DATEDIFF(day,CONVERT(date,'19000101',112),CONVERT(date,SoymaBitisi)) % 7),CONVERT(date,SoymaBitisi)));
            """, _ => { });

        await ExecuteAsync("""
            IF OBJECT_ID(N'uretim.IslamaSoymaIsAkisi',N'U') IS NULL
            BEGIN
                CREATE TABLE uretim.IslamaSoymaIsAkisi
                (
                    IsAkisiId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_IslamaSoymaIsAkisi PRIMARY KEY,
                    PartiNo varchar(50) NOT NULL CONSTRAINT UQ_IslamaSoymaIsAkisi_Parti UNIQUE,
                    Asama tinyint NOT NULL CONSTRAINT DF_IslamaSoymaIsAkisi_Asama DEFAULT(1),
                    NobetTarihi date NOT NULL,
                    HamSusamGelisTarihi date NULL,
                    MenseiId int NULL,
                    IslamaTarihi date NOT NULL,
                    HavuzNo nvarchar(30) NULL,
                    EkranTonajiKg decimal(18,3) NULL,
                    CekilenTonajKg decimal(18,3) NULL,
                    IslamaBaslangici datetime2(0) NULL,
                    IslamaBitisi datetime2(0) NULL,
                    HazirlayanPersonelId int NULL,
                    IslamaPersonelId int NULL,
                    SoymaPersonelId int NULL,
                    OlusturmaZamani datetime2(0) NOT NULL CONSTRAINT DF_IslamaSoymaIsAkisi_Olusturma DEFAULT(SYSDATETIME()),
                    GuncellemeZamani datetime2(0) NOT NULL CONSTRAINT DF_IslamaSoymaIsAkisi_Guncelleme DEFAULT(SYSDATETIME()),
                    TamamlananKayitId bigint NULL,
                    CONSTRAINT CK_IslamaSoymaIsAkisi_Asama CHECK(Asama BETWEEN 1 AND 3),
                    CONSTRAINT FK_IsAkisi_Hazirlayan FOREIGN KEY(HazirlayanPersonelId) REFERENCES tanim.Personel(PersonelId),
                    CONSTRAINT FK_IsAkisi_IslamaPersoneli FOREIGN KEY(IslamaPersonelId) REFERENCES tanim.Personel(PersonelId),
                    CONSTRAINT FK_IsAkisi_SoymaPersoneli FOREIGN KEY(SoymaPersonelId) REFERENCES tanim.Personel(PersonelId)
                );
                CREATE INDEX IX_IslamaSoymaIsAkisi_Asama ON uretim.IslamaSoymaIsAkisi(Asama,GuncellemeZamani);
            END;
            IF COL_LENGTH(N'uretim.IslamaSoymaIsAkisi',N'HamSusamGelisTarihi') IS NULL
                ALTER TABLE uretim.IslamaSoymaIsAkisi ADD HamSusamGelisTarihi date NULL;
            IF COL_LENGTH(N'uretim.IslamaSoymaIsAkisi',N'MenseiId') IS NULL
                ALTER TABLE uretim.IslamaSoymaIsAkisi ADD MenseiId int NULL;
            IF COL_LENGTH(N'uretim.IslamaSoymaIsAkisi',N'EkranTonajiKg') IS NULL
                ALTER TABLE uretim.IslamaSoymaIsAkisi ADD EkranTonajiKg decimal(18,3) NULL;
            IF COL_LENGTH(N'uretim.IslamaSoymaIsAkisi',N'CekilenTonajKg') IS NULL
                ALTER TABLE uretim.IslamaSoymaIsAkisi ADD CekilenTonajKg decimal(18,3) NULL;
            IF EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'uretim.IslamaSoymaIsAkisi') AND name=N'HavuzNo' AND is_nullable=0)
                ALTER TABLE uretim.IslamaSoymaIsAkisi ALTER COLUMN HavuzNo nvarchar(30) NULL;
            IF NOT EXISTS(SELECT 1 FROM sys.foreign_keys WHERE name='FK_IslamaSoymaIsAkisi_Mensei')
                ALTER TABLE uretim.IslamaSoymaIsAkisi WITH CHECK ADD CONSTRAINT FK_IslamaSoymaIsAkisi_Mensei FOREIGN KEY(MenseiId) REFERENCES tanim.Mensei(MenseiId);
            """, _ => { });

        await ExecuteAsync("""
            IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name=N'IX_IslamaSoymaKaydi_SoymaBitisi' AND object_id=OBJECT_ID(N'uretim.IslamaSoymaKaydi'))
                CREATE INDEX IX_IslamaSoymaKaydi_SoymaBitisi ON uretim.IslamaSoymaKaydi(SoymaBitisi) INCLUDE(UrunId,MenseiId,CekilenTonajKg,CopKg);
            IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name=N'IX_IslamaSoymaKaydi_RaporTarihi' AND object_id=OBJECT_ID(N'uretim.IslamaSoymaKaydi'))
                CREATE INDEX IX_IslamaSoymaKaydi_RaporTarihi ON uretim.IslamaSoymaKaydi(RaporTarihi) INCLUDE(UrunId,MenseiId,CekilenTonajKg,CopKg,NobetTarihi);
            IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name=N'IX_KavurmaKaydi_Tarih' AND object_id=OBJECT_ID(N'uretim.KavurmaKaydi'))
                CREATE INDEX IX_KavurmaKaydi_Tarih ON uretim.KavurmaKaydi(Tarih) INCLUDE(UrunId,MenseiId,NetTonajKg,EklenenSorteksAltiKg,TavaSayisi);
            IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name=N'IX_PaketlemeKaydi_Tarih' AND object_id=OBJECT_ID(N'uretim.PaketlemeKaydi'))
                CREATE INDEX IX_PaketlemeKaydi_Tarih ON uretim.PaketlemeKaydi(Tarih) INCLUDE(UrunId,MenseiId,AmbalajAgirligiKg,Adet,CikanSorteksAltiKg);
            IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name=N'IX_DolumKaydi_Tarih' AND object_id=OBJECT_ID(N'uretim.DolumKaydi'))
                CREATE INDEX IX_DolumKaydi_Tarih ON uretim.DolumKaydi(Tarih);
            IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name=N'IX_KepekKaydi_Tarih' AND object_id=OBJECT_ID(N'uretim.KepekKaydi'))
                CREATE INDEX IX_KepekKaydi_Tarih ON uretim.KepekKaydi(Tarih);
            """, _ => { });

        await ExecuteAsync("""
            IF OBJECT_ID(N'tanim.Gorev', N'U') IS NULL
            BEGIN
                CREATE TABLE tanim.Gorev
                (
                    GorevNo tinyint NOT NULL CONSTRAINT PK_Gorev PRIMARY KEY,
                    Kod varchar(30) NOT NULL CONSTRAINT UQ_Gorev_Kod UNIQUE,
                    Ad nvarchar(100) NOT NULL,
                    Sayfa varchar(50) NOT NULL,
                    Aktif bit NOT NULL CONSTRAINT DF_Gorev_Aktif DEFAULT(1)
                );
            END;
            """, _ => { });

        await ExecuteAsync("""
            MERGE tanim.Gorev AS target
            USING (VALUES
                (CONVERT(tinyint,1),'Islama',N'Islama '+NCHAR(183)+N' Soyma','/Islama'),
                (CONVERT(tinyint,2),'Kavurma',N'Kavurma','/Kavurma'),
                (CONVERT(tinyint,3),'Paketleme',N'Paketleme','/Paketleme'),
                (CONVERT(tinyint,4),'Dolum',N'Dolum','/Dolum'),
                (CONVERT(tinyint,5),'Kepek',N'Kepek','/Kavurma'),
                (CONVERT(tinyint,6),'Degirmen',N'De'+NCHAR(287)+N'irmen','/Degirmen'),
                (CONVERT(tinyint,7),'Kurutma',N'Kurutma','/Kurutma'),
                (CONVERT(tinyint,8),'Cop',NCHAR(199)+NCHAR(246)+N'p','/Cop')) AS source(GorevNo,Kod,Ad,Sayfa)
            ON target.GorevNo=source.GorevNo
            WHEN MATCHED THEN UPDATE SET Kod=source.Kod,Ad=source.Ad,Sayfa=source.Sayfa,Aktif=1
            WHEN NOT MATCHED THEN INSERT(GorevNo,Kod,Ad,Sayfa) VALUES(source.GorevNo,source.Kod,source.Ad,source.Sayfa);
            """, _ => { });

        await ExecuteAsync("""
            IF COL_LENGTH('tanim.Personel','GorevNo') IS NULL ALTER TABLE tanim.Personel ADD GorevNo tinyint NULL;
            IF COL_LENGTH('uretim.IslamaSoymaKaydi','PersonelId') IS NULL ALTER TABLE uretim.IslamaSoymaKaydi ADD PersonelId int NULL;
            IF COL_LENGTH('uretim.DolumKaydi','PersonelId') IS NULL ALTER TABLE uretim.DolumKaydi ADD PersonelId int NULL;
            IF COL_LENGTH('uretim.KepekKaydi','PersonelId') IS NULL ALTER TABLE uretim.KepekKaydi ADD PersonelId int NULL;
            IF COL_LENGTH('uretim.KavurmaKaydi','KepekKg') IS NULL ALTER TABLE uretim.KavurmaKaydi ADD KepekKg decimal(18,3) NULL;
            IF COL_LENGTH('uretim.PaketlemeKaydi','FireKg') IS NULL ALTER TABLE uretim.PaketlemeKaydi ADD FireKg decimal(18,3) NULL;
            IF COL_LENGTH('uretim.DolumKaydi','FireKg') IS NULL ALTER TABLE uretim.DolumKaydi ADD FireKg decimal(18,3) NULL;
            IF COL_LENGTH('uretim.DolumKaydi','Tank') IS NULL ALTER TABLE uretim.DolumKaydi ADD Tank nvarchar(100) NULL;
            IF COL_LENGTH('uretim.DolumKaydi','MenseiId') IS NULL ALTER TABLE uretim.DolumKaydi ADD MenseiId int NULL;
            IF NOT EXISTS(SELECT 1 FROM sys.foreign_keys WHERE name='FK_DolumKaydi_Mensei')
                ALTER TABLE uretim.DolumKaydi WITH CHECK ADD CONSTRAINT FK_DolumKaydi_Mensei FOREIGN KEY(MenseiId) REFERENCES tanim.Mensei(MenseiId);
            IF OBJECT_ID(N'tanim.UygulamaAyari',N'U') IS NULL
                CREATE TABLE tanim.UygulamaAyari(AyarKodu varchar(50) NOT NULL CONSTRAINT PK_UygulamaAyari PRIMARY KEY,Deger nvarchar(1000) NULL);
            IF NOT EXISTS(SELECT 1 FROM tanim.Silo WHERE Kod=N'Silo 1') INSERT tanim.Silo(Kod,Aktif) VALUES(N'Silo 1',1);
            IF NOT EXISTS(SELECT 1 FROM tanim.Silo WHERE Kod=N'Silo 2') INSERT tanim.Silo(Kod,Aktif) VALUES(N'Silo 2',1);
            IF OBJECT_ID(N'uretim.ExcelAktarimDetayi',N'U') IS NOT NULL AND COL_LENGTH('uretim.ExcelAktarimDetayi','BekleyenIslem') IS NULL
                ALTER TABLE uretim.ExcelAktarimDetayi ADD BekleyenIslem varchar(10) NULL;

            IF OBJECT_ID(N'tanim.PersonelGorev', N'U') IS NULL
            BEGIN
                CREATE TABLE tanim.PersonelGorev
                (
                    PersonelId int NOT NULL,
                    GorevNo tinyint NOT NULL,
                    CONSTRAINT PK_PersonelGorev PRIMARY KEY(PersonelId,GorevNo),
                    CONSTRAINT FK_PersonelGorev_Personel FOREIGN KEY(PersonelId) REFERENCES tanim.Personel(PersonelId),
                    CONSTRAINT FK_PersonelGorev_Gorev FOREIGN KEY(GorevNo) REFERENCES tanim.Gorev(GorevNo)
                );
                INSERT tanim.PersonelGorev(PersonelId,GorevNo)
                SELECT PersonelId,GorevNo FROM tanim.Personel WHERE GorevNo IS NOT NULL;
            END;

            IF NOT EXISTS(SELECT 1 FROM sys.foreign_keys WHERE name='FK_Personel_Gorev')
                ALTER TABLE tanim.Personel WITH CHECK ADD CONSTRAINT FK_Personel_Gorev FOREIGN KEY(GorevNo) REFERENCES tanim.Gorev(GorevNo);
            IF NOT EXISTS(SELECT 1 FROM sys.foreign_keys WHERE name='FK_IslamaSoymaKaydi_Personel')
                ALTER TABLE uretim.IslamaSoymaKaydi WITH CHECK ADD CONSTRAINT FK_IslamaSoymaKaydi_Personel FOREIGN KEY(PersonelId) REFERENCES tanim.Personel(PersonelId);
            IF NOT EXISTS(SELECT 1 FROM sys.foreign_keys WHERE name='FK_IslamaSoymaKaydi_Hazirlayan')
                ALTER TABLE uretim.IslamaSoymaKaydi WITH CHECK ADD CONSTRAINT FK_IslamaSoymaKaydi_Hazirlayan FOREIGN KEY(HazirlayanPersonelId) REFERENCES tanim.Personel(PersonelId);
            IF NOT EXISTS(SELECT 1 FROM sys.foreign_keys WHERE name='FK_IslamaSoymaKaydi_IslamaPersoneli')
                ALTER TABLE uretim.IslamaSoymaKaydi WITH CHECK ADD CONSTRAINT FK_IslamaSoymaKaydi_IslamaPersoneli FOREIGN KEY(IslamaPersonelId) REFERENCES tanim.Personel(PersonelId);
            IF NOT EXISTS(SELECT 1 FROM sys.foreign_keys WHERE name='FK_IslamaSoymaKaydi_SoymaPersoneli')
                ALTER TABLE uretim.IslamaSoymaKaydi WITH CHECK ADD CONSTRAINT FK_IslamaSoymaKaydi_SoymaPersoneli FOREIGN KEY(SoymaPersonelId) REFERENCES tanim.Personel(PersonelId);
            IF NOT EXISTS(SELECT 1 FROM sys.foreign_keys WHERE name='FK_DolumKaydi_Personel')
                ALTER TABLE uretim.DolumKaydi WITH CHECK ADD CONSTRAINT FK_DolumKaydi_Personel FOREIGN KEY(PersonelId) REFERENCES tanim.Personel(PersonelId);
            IF NOT EXISTS(SELECT 1 FROM sys.foreign_keys WHERE name='FK_KepekKaydi_Personel')
                ALTER TABLE uretim.KepekKaydi WITH CHECK ADD CONSTRAINT FK_KepekKaydi_Personel FOREIGN KEY(PersonelId) REFERENCES tanim.Personel(PersonelId);
            """, _ => { });

        await ExecuteAsync("UPDATE tanim.Gorev SET Aktif=0 WHERE GorevNo IN (5,8);",_=>{});

        await ExecuteAsync("""
            IF OBJECT_ID(N'uretim.DegirmenNobeti',N'U') IS NULL
            BEGIN
                CREATE TABLE uretim.DegirmenNobeti
                (
                    DegirmenNobetId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_DegirmenNobeti PRIMARY KEY,
                    Tarih date NOT NULL,
                    Nobet nvarchar(30) NOT NULL,
                    PersonelId int NOT NULL,
                    Aciklama nvarchar(1000) NULL,
                    OlusturmaZamani datetime2(0) NOT NULL CONSTRAINT DF_DegirmenNobeti_Zaman DEFAULT(SYSDATETIME()),
                    Olusturan nvarchar(128) NULL,
                    CONSTRAINT FK_DegirmenNobeti_Personel FOREIGN KEY(PersonelId) REFERENCES tanim.Personel(PersonelId)
                );
                CREATE INDEX IX_DegirmenNobeti_Tarih ON uretim.DegirmenNobeti(Tarih);
            END;
            IF OBJECT_ID(N'uretim.DegirmenKaydi',N'U') IS NULL
            BEGIN
                CREATE TABLE uretim.DegirmenKaydi
                (
                    DegirmenKaydiId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_DegirmenKaydi PRIMARY KEY,
                    DegirmenNobetId bigint NOT NULL,
                    SatirNo int NOT NULL,
                    FirinNoSergen nvarchar(100) NOT NULL,
                    MenseiId int NOT NULL,
                    PureMiktariKg nvarchar(200) NOT NULL,
                    InceltilenMiktarKg decimal(18,3) NOT NULL,
                    TransferEdilenTank nvarchar(100) NOT NULL,
                    CONSTRAINT FK_DegirmenKaydi_Nobet FOREIGN KEY(DegirmenNobetId) REFERENCES uretim.DegirmenNobeti(DegirmenNobetId) ON DELETE CASCADE,
                    CONSTRAINT FK_DegirmenKaydi_Mensei FOREIGN KEY(MenseiId) REFERENCES tanim.Mensei(MenseiId),
                    CONSTRAINT UQ_DegirmenKaydi_Satir UNIQUE(DegirmenNobetId,SatirNo)
                );
            END;

            IF EXISTS
            (
                SELECT 1
                FROM sys.columns C
                JOIN sys.types T ON T.user_type_id=C.user_type_id
                WHERE C.object_id=OBJECT_ID(N'uretim.DegirmenKaydi')
                  AND C.name=N'PureMiktariKg'
                  AND T.name<>N'nvarchar'
            )
                ALTER TABLE uretim.DegirmenKaydi ALTER COLUMN PureMiktariKg nvarchar(200) NOT NULL;

            IF OBJECT_ID(N'uretim.TankTransferi',N'U') IS NULL
            BEGIN
                CREATE TABLE uretim.TankTransferi
                (
                    TankTransferId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_TankTransferi PRIMARY KEY,
                    TransferZamani datetime2(0) NOT NULL,
                    KaynakTank nvarchar(100) NOT NULL,
                    HedefTank nvarchar(100) NOT NULL,
                    MiktarKg decimal(18,3) NOT NULL,
                    MenseiId int NOT NULL,
                    UrunId int NULL,
                    PersonelId int NOT NULL,
                    Aciklama nvarchar(500) NULL,
                    OlusturmaZamani datetime2(0) NOT NULL CONSTRAINT DF_TankTransferi_Olusturma DEFAULT(SYSDATETIME()),
                    Olusturan nvarchar(128) NULL,
                    CONSTRAINT FK_TankTransferi_Mensei FOREIGN KEY(MenseiId) REFERENCES tanim.Mensei(MenseiId),
                    CONSTRAINT FK_TankTransferi_Urun FOREIGN KEY(UrunId) REFERENCES tanim.Urun(UrunId),
                    CONSTRAINT FK_TankTransferi_Personel FOREIGN KEY(PersonelId) REFERENCES tanim.Personel(PersonelId),
                    CONSTRAINT CK_TankTransferi_Miktar CHECK(MiktarKg>0),
                    CONSTRAINT CK_TankTransferi_FarkliTank CHECK(KaynakTank<>HedefTank)
                );
                CREATE INDEX IX_TankTransferi_Zaman ON uretim.TankTransferi(TransferZamani DESC);
            END;

            IF OBJECT_ID(N'uretim.TankTransferi',N'U') IS NOT NULL
               AND COLUMNPROPERTY(OBJECT_ID(N'uretim.TankTransferi'),N'UrunId','AllowsNull')=0
                ALTER TABLE uretim.TankTransferi ALTER COLUMN UrunId int NULL;

            IF OBJECT_ID(N'uretim.KurutmaNobeti',N'U') IS NULL
            BEGIN
                CREATE TABLE uretim.KurutmaNobeti
                (
                    KurutmaNobetId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_KurutmaNobeti PRIMARY KEY,
                    Tarih date NOT NULL,
                    Nobet nvarchar(30) NOT NULL,
                    PersonelId int NOT NULL,
                    Aciklama nvarchar(1000) NULL,
                    OlusturmaZamani datetime2(0) NOT NULL CONSTRAINT DF_KurutmaNobeti_Zaman DEFAULT(SYSDATETIME()),
                    Olusturan nvarchar(128) NULL,
                    CONSTRAINT FK_KurutmaNobeti_Personel FOREIGN KEY(PersonelId) REFERENCES tanim.Personel(PersonelId)
                );
                CREATE INDEX IX_KurutmaNobeti_Tarih ON uretim.KurutmaNobeti(Tarih);
            END;
            IF OBJECT_ID(N'uretim.KurutmaKaydi',N'U') IS NULL
            BEGIN
                CREATE TABLE uretim.KurutmaKaydi
                (
                    KurutmaKaydiId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_KurutmaKaydi PRIMARY KEY,
                    KurutmaNobetId bigint NOT NULL,
                    SatirNo int NOT NULL,
                    MenseiId int NOT NULL,
                    UrunId int NOT NULL,
                    YikamaSayisi int NOT NULL,
                    KirecKg decimal(18,3) NOT NULL,
                    MakineSayisi int NOT NULL,
                    CONSTRAINT FK_KurutmaKaydi_Nobet FOREIGN KEY(KurutmaNobetId) REFERENCES uretim.KurutmaNobeti(KurutmaNobetId) ON DELETE CASCADE,
                    CONSTRAINT FK_KurutmaKaydi_Mensei FOREIGN KEY(MenseiId) REFERENCES tanim.Mensei(MenseiId),
                    CONSTRAINT FK_KurutmaKaydi_Urun FOREIGN KEY(UrunId) REFERENCES tanim.Urun(UrunId),
                    CONSTRAINT UQ_KurutmaKaydi_Satir UNIQUE(KurutmaNobetId,SatirNo)
                );
            END;

            IF OBJECT_ID(N'uretim.CopKaydi',N'U') IS NULL
            BEGIN
                CREATE TABLE uretim.CopKaydi
                (
                    CopKaydiId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_CopKaydi PRIMARY KEY,
                    Tarih date NOT NULL,
                    MenseiId int NOT NULL,
                    CopKg decimal(18,3) NOT NULL,
                    PersonelId int NULL,
                    OlusturmaZamani datetime2(0) NOT NULL CONSTRAINT DF_CopKaydi_OlusturmaZamani DEFAULT(SYSDATETIME()),
                    Olusturan nvarchar(128) NULL,
                    CONSTRAINT FK_CopKaydi_Mensei FOREIGN KEY(MenseiId) REFERENCES tanim.Mensei(MenseiId),
                    CONSTRAINT FK_CopKaydi_Personel FOREIGN KEY(PersonelId) REFERENCES tanim.Personel(PersonelId),
                    CONSTRAINT CK_CopKaydi_CopKg CHECK(CopKg>=0)
                );
                CREATE INDEX IX_CopKaydi_Tarih ON uretim.CopKaydi(Tarih);
            END;
            IF COL_LENGTH(N'uretim.CopKaydi',N'PersonelId') IS NULL
            BEGIN
                ALTER TABLE uretim.CopKaydi ADD PersonelId int NULL;
                ALTER TABLE uretim.CopKaydi ADD CONSTRAINT FK_CopKaydi_Personel FOREIGN KEY(PersonelId) REFERENCES tanim.Personel(PersonelId);
            END;

            INSERT tanim.PersonelGorev(PersonelId,GorevNo)
            SELECT P.PersonelId,G.GorevNo
            FROM tanim.Personel P
            CROSS JOIN tanim.Gorev G
            WHERE UPPER(LTRIM(RTRIM(P.AdSoyad))) IN (N'ABDULLAH SOYER',N'ALÄ° Ã–ÄžDÃœ')
              AND G.GorevNo IN (6,7,8)
              AND NOT EXISTS(SELECT 1 FROM tanim.PersonelGorev PG WHERE PG.PersonelId=P.PersonelId AND PG.GorevNo=G.GorevNo);
            """, _ => { });
    }

    public async Task SynchronizeExcelCatalogAsync(ExcelDataCatalog catalog)
    {
        await using var connection=CreateConnection();await connection.OpenAsync();
        await using var transaction=(SqlTransaction)await connection.BeginTransactionAsync();
        try
        {
            await SyncDefinitionsAsync(connection,transaction,"tanim.Mensei","MenseiId","Ad",catalog.Origins,
                [("uretim.IslamaSoymaKaydi","MenseiId"),("uretim.KavurmaKaydi","MenseiId"),("uretim.PaketlemeKaydi","MenseiId"),("uretim.DolumKaydi","MenseiId")]);
            await SyncDefinitionsAsync(connection,transaction,"tanim.Urun","UrunId","Ad",catalog.Products,
                [("uretim.IslamaSoymaKaydi","UrunId"),("uretim.KavurmaKaydi","UrunId"),("uretim.PaketlemeKaydi","UrunId"),("uretim.DolumKaydi","UrunId")]);
            await transaction.CommitAsync();
        }
        catch{await transaction.RollbackAsync();throw;}
    }

    private static async Task SyncDefinitionsAsync(SqlConnection connection,SqlTransaction transaction,string table,string idColumn,string nameColumn,
        IReadOnlyList<string> catalog,IReadOnlyList<(string Table,string Column)> references)
    {
        var rows=new List<(int Id,string Name)>();
        await using(var command=new SqlCommand($"SELECT {idColumn},{nameColumn} FROM {table};",connection,transaction))
        await using(var reader=await command.ExecuteReaderAsync())while(await reader.ReadAsync())rows.Add((reader.GetInt32(0),reader.GetString(1)));
        var canonical=catalog.Where(x=>!string.IsNullOrWhiteSpace(x)).DistinctBy(CatalogKey).ToList();
        foreach(var name in canonical)
        {
            var matches=rows.Where(x=>CatalogKey(x.Name)==CatalogKey(name)).ToList();
            if(matches.Count==0)
            {
                await using var insert=new SqlCommand($"INSERT {table}({nameColumn},Aktif) OUTPUT INSERTED.{idColumn} VALUES(@Name,1);",connection,transaction);
                insert.Parameters.AddWithValue("@Name",name);var id=Convert.ToInt32(await insert.ExecuteScalarAsync());rows.Add((id,name));continue;
            }
            var keeper=matches.FirstOrDefault(x=>string.Equals(x.Name.Trim(),name.Trim(),StringComparison.CurrentCultureIgnoreCase));
            if(keeper==default)keeper=matches[0];
            foreach(var duplicate in matches.Where(x=>x.Id!=keeper.Id))
                foreach(var reference in references)
                {
                    await using var merge=new SqlCommand($"UPDATE {reference.Table} SET {reference.Column}=@Keep WHERE {reference.Column}=@Duplicate;",connection,transaction);
                    merge.Parameters.AddWithValue("@Keep",keeper.Id);merge.Parameters.AddWithValue("@Duplicate",duplicate.Id);await merge.ExecuteNonQueryAsync();
                }
            await using var activate=new SqlCommand($"UPDATE {table} SET Aktif=0 WHERE {idColumn} IN ({string.Join(',',matches.Select(x=>x.Id))}) AND {idColumn}<>@Keep; UPDATE {table} SET {nameColumn}=@Name,Aktif=1 WHERE {idColumn}=@Keep;",connection,transaction);
            activate.Parameters.AddWithValue("@Name",name);activate.Parameters.AddWithValue("@Keep",keeper.Id);await activate.ExecuteNonQueryAsync();
        }
        var allowed=canonical.Select(CatalogKey).ToHashSet();
        foreach(var row in rows.Where(x=>!allowed.Contains(CatalogKey(x.Name))))
        {
            await using var deactivate=new SqlCommand($"UPDATE {table} SET Aktif=0 WHERE {idColumn}=@Id;",connection,transaction);
            deactivate.Parameters.AddWithValue("@Id",row.Id);await deactivate.ExecuteNonQueryAsync();
        }
    }

    private static string CatalogKey(string value)
    {
        var normalized=value.Trim().ToLower(new CultureInfo("tr-TR")).Replace('ı','i').Normalize(NormalizationForm.FormD);
        return new string(normalized.Where(x=>CharUnicodeInfo.GetUnicodeCategory(x)!=UnicodeCategory.NonSpacingMark&&char.IsLetterOrDigit(x)).ToArray())
            .Replace("maiduguri","maidiguri",StringComparison.Ordinal);
    }

    public async Task<List<PersonnelAssignment>> GetPersonnelAssignmentsAsync()
    {
        const string sql = """
            SELECT P.PersonelId,P.AdSoyad,G.GorevNo,G.Ad,G.Sayfa
            FROM tanim.Personel P
            LEFT JOIN tanim.PersonelGorev PG ON PG.PersonelId=P.PersonelId
            LEFT JOIN tanim.Gorev G ON G.GorevNo=PG.GorevNo AND G.Aktif=1
            WHERE P.Aktif=1 ORDER BY P.AdSoyad,G.GorevNo;
            """;
        var rows = new List<(int Id,string Name,int? No,string? Task,string? Page)>();
        await using var connection = CreateConnection(); await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection); await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) rows.Add((reader.GetInt32(0),reader.GetString(1),D<byte>(reader,2),S(reader,3),S(reader,4)));
        return rows.GroupBy(x=>new{x.Id,x.Name}).Select(g=>new PersonnelAssignment(g.Key.Id,g.Key.Name,
            g.Where(x=>x.No.HasValue && x.Task is not null && x.Page is not null)
             .Select(x=>new PersonnelTask(x.No!.Value,x.Task!,x.Page!)).ToList())).ToList();
    }

    public async Task<PersonnelAssignment?> GetPersonnelAssignmentAsync(int personnelId)
    {
        const string sql = """
            SELECT P.PersonelId,P.AdSoyad,G.GorevNo,G.Ad,G.Sayfa
            FROM tanim.Personel P
            LEFT JOIN tanim.PersonelGorev PG ON PG.PersonelId=P.PersonelId
            LEFT JOIN tanim.Gorev G ON G.GorevNo=PG.GorevNo AND G.Aktif=1
            WHERE P.Aktif=1 AND P.PersonelId=@Id ORDER BY G.GorevNo;
            """;
        await using var connection = CreateConnection(); await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection); command.Parameters.AddWithValue("@Id", personnelId);
        await using var reader = await command.ExecuteReaderAsync();
        var tasks=new List<PersonnelTask>(); string? name=null;
        while(await reader.ReadAsync()){name=reader.GetString(1);var no=D<byte>(reader,2);if(no.HasValue&&S(reader,3) is string task&&S(reader,4) is string page)tasks.Add(new(no.Value,task,page));}
        return name is null?null:new PersonnelAssignment(personnelId,name,tasks);
    }

    public Task SetPersonnelTasksAsync(int personnelId, IEnumerable<int> taskNumbers) => ExecuteAsync("""
        DELETE tanim.PersonelGorev WHERE PersonelId=@Id;
        INSERT tanim.PersonelGorev(PersonelId,GorevNo)
        SELECT @Id,GorevNo FROM tanim.Gorev WHERE Aktif=1 AND GorevNo IN (SELECT TRY_CONVERT(tinyint,value) FROM STRING_SPLIT(@Tasks,','));
        UPDATE tanim.Personel SET GorevNo=(SELECT MIN(GorevNo) FROM tanim.PersonelGorev WHERE PersonelId=@Id) WHERE PersonelId=@Id;
        """,p=>{Add(p,"@Id",personnelId);Add(p,"@Tasks",string.Join(',',taskNumbers.Distinct()));});

    public Task SetPersonnelActiveAsync(int personnelId,bool active)=>ExecuteAsync("UPDATE tanim.Personel SET Aktif=@Active WHERE PersonelId=@Id;",p=>{Add(p,"@Id",personnelId);Add(p,"@Active",active);});

    public async Task<string?> GetAdminPasswordHashAsync()
    {
        await using var c=CreateConnection();await c.OpenAsync();await using var cmd=new SqlCommand("SELECT Deger FROM tanim.UygulamaAyari WHERE AyarKodu='AdminPasswordHash';",c);
        return await cmd.ExecuteScalarAsync() as string;
    }

    public Task SetAdminPasswordHashAsync(string hash)=>ExecuteAsync("""
        MERGE tanim.UygulamaAyari AS T USING(SELECT 'AdminPasswordHash' AyarKodu,@Hash Deger) AS S ON T.AyarKodu=S.AyarKodu
        WHEN MATCHED THEN UPDATE SET Deger=S.Deger WHEN NOT MATCHED THEN INSERT(AyarKodu,Deger) VALUES(S.AyarKodu,S.Deger);
        """,p=>Add(p,"@Hash",hash));

    public async Task<DashboardStats> GetDashboardAsync(DateTime? from=null, DateTime? to=null, int? productId=null, int? originId=null)
    {
        const string sql = """
            DECLARE @End date=DATEADD(DAY,1,COALESCE(@To,CONVERT(date,GETDATE())));
            DECLARE @Start date=COALESCE(@From,DATEADD(DAY,-29,@End));
            DECLARE @Kavurma TABLE
            (
              KavurmaKaydiId bigint,Tarih date,UrunId int,MenseiId int,NetTonajKg decimal(18,3),
              VerimOrani decimal(18,6),OrtalamaVerimOrani decimal(18,6),TavaSayisi int,
              ArizaliTavaSayisi int,EklenenSorteksAltiKg decimal(18,3),KepekKg decimal(18,3),PersonelId int
            );
            INSERT @Kavurma
            SELECT K.KavurmaKaydiId,K.Tarih,K.UrunId,K.MenseiId,
                   K.NetTonajKg,K.VerimOrani,K.OrtalamaVerimOrani,K.TavaSayisi,K.ArizaliTavaSayisi,
                   K.EklenenSorteksAltiKg,K.KepekKg,K.PersonelId
            FROM uretim.KavurmaKaydi K;
            SELECT
              (SELECT COUNT(*) FROM uretim.IslamaSoymaKaydi WHERE SoymaBitisi>=@Start AND SoymaBitisi<@End AND (@Product IS NULL OR UrunId=@Product) AND (@Origin IS NULL OR MenseiId=@Origin)),
              (SELECT COUNT(*) FROM @Kavurma WHERE Tarih>=@Start AND Tarih<@End AND (@Product IS NULL OR UrunId=@Product) AND (@Origin IS NULL OR MenseiId=@Origin)),
              (SELECT COUNT(*) FROM uretim.PaketlemeKaydi WHERE Tarih>=@Start AND Tarih<@End AND (@Product IS NULL OR UrunId=@Product) AND (@Origin IS NULL OR MenseiId=@Origin)),
              (SELECT COUNT(*) FROM uretim.DolumKaydi WHERE Tarih>=@Start AND Tarih<@End AND (@Product IS NULL OR UrunId=@Product) AND @Origin IS NULL),
              ((SELECT COUNT(*) FROM uretim.KepekKaydi WHERE Tarih>=@Start AND Tarih<@End AND @Product IS NULL AND @Origin IS NULL)
               +(SELECT COUNT(*) FROM @Kavurma WHERE Tarih>=@Start AND Tarih<@End AND COALESCE(KepekKg,0)>0 AND (@Product IS NULL OR UrunId=@Product) AND (@Origin IS NULL OR MenseiId=@Origin))),
              (SELECT COALESCE(SUM(CekilenTonajKg),0) FROM uretim.IslamaSoymaKaydi WHERE SoymaBitisi>=@Start AND SoymaBitisi<@End AND (@Product IS NULL OR UrunId=@Product) AND (@Origin IS NULL OR MenseiId=@Origin)),
              (SELECT COALESCE(SUM(NetTonajKg),0) FROM @Kavurma WHERE Tarih>=@Start AND Tarih<@End AND NetTonajKg>0 AND (@Product IS NULL OR UrunId=@Product) AND (@Origin IS NULL OR MenseiId=@Origin)),
              (SELECT COALESCE(SUM(AmbalajAgirligiKg*Adet),0) FROM uretim.PaketlemeKaydi WHERE Tarih>=@Start AND Tarih<@End AND (@Product IS NULL OR UrunId=@Product) AND (@Origin IS NULL OR MenseiId=@Origin)),
              (SELECT COALESCE(AVG(NULLIF(VerimOrani,0)),AVG(NULLIF(OrtalamaVerimOrani,0)),0) FROM @Kavurma WHERE Tarih>=@Start AND Tarih<@End AND (@Product IS NULL OR UrunId=@Product) AND (@Origin IS NULL OR MenseiId=@Origin)),
              (SELECT COALESCE(SUM(TavaSayisi),0) FROM @Kavurma WHERE Tarih>=@Start AND Tarih<@End AND (@Product IS NULL OR UrunId=@Product) AND (@Origin IS NULL OR MenseiId=@Origin)),
              (SELECT COALESCE(SUM(ArizaliTavaSayisi),0) FROM @Kavurma WHERE Tarih>=@Start AND Tarih<@End AND (@Product IS NULL OR UrunId=@Product) AND (@Origin IS NULL OR MenseiId=@Origin)),
              (SELECT COALESCE(SUM(EklenenSorteksAltiKg),0) FROM @Kavurma WHERE Tarih>=@Start AND Tarih<@End AND (@Product IS NULL OR UrunId=@Product) AND (@Origin IS NULL OR MenseiId=@Origin)),
              (SELECT COALESCE(SUM(CikanSorteksAltiKg),0) FROM uretim.PaketlemeKaydi WHERE Tarih>=@Start AND Tarih<@End AND (@Product IS NULL OR UrunId=@Product) AND (@Origin IS NULL OR MenseiId=@Origin)),
              (SELECT COALESCE(SUM(CopKg),0) FROM uretim.IslamaSoymaKaydi WHERE SoymaBitisi>=@Start AND SoymaBitisi<@End AND (@Product IS NULL OR UrunId=@Product) AND (@Origin IS NULL OR MenseiId=@Origin)),
              ((SELECT COALESCE(SUM(PaketlemeMiktariKg),0) FROM uretim.KepekKaydi WHERE Tarih>=@Start AND Tarih<@End AND @Product IS NULL AND @Origin IS NULL)
               +(SELECT COALESCE(SUM(KepekKg),0) FROM @Kavurma WHERE Tarih>=@Start AND Tarih<@End AND (@Product IS NULL OR UrunId=@Product) AND (@Origin IS NULL OR MenseiId=@Origin))),
              (SELECT COALESCE(SUM(SoymaSuresiDakika),0) FROM uretim.IslamaSoymaKaydi WHERE SoymaBitisi>=@Start AND SoymaBitisi<@End AND (@Product IS NULL OR UrunId=@Product) AND (@Origin IS NULL OR MenseiId=@Origin)),
              (SELECT COALESCE(SUM(Adet),0) FROM uretim.PaketlemeKaydi WHERE Tarih>=@Start AND Tarih<@End AND (@Product IS NULL OR UrunId=@Product) AND (@Origin IS NULL OR MenseiId=@Origin)),
              (SELECT COALESCE(SUM(PaketlemeMiktariKg),0) FROM uretim.DolumKaydi WHERE Tarih>=@Start AND Tarih<@End AND (@Product IS NULL OR UrunId=@Product) AND @Origin IS NULL),
              (SELECT COALESCE(SUM(PaketlemeAdedi),0) FROM uretim.DolumKaydi WHERE Tarih>=@Start AND Tarih<@End AND (@Product IS NULL OR UrunId=@Product) AND @Origin IS NULL),
              (SELECT COALESCE(SUM(PersonelSayisi),0) FROM uretim.DolumKaydi WHERE Tarih>=@Start AND Tarih<@End AND (@Product IS NULL OR UrunId=@Product) AND @Origin IS NULL);

            SELECT Gun,SUM(GirdiKg),SUM(CiktiKg) FROM
            (
              SELECT CONVERT(date,SoymaBitisi) Gun,CekilenTonajKg GirdiKg,CONVERT(decimal(18,3),0) CiktiKg FROM uretim.IslamaSoymaKaydi WHERE SoymaBitisi>=@Start AND SoymaBitisi<@End AND (@Product IS NULL OR UrunId=@Product) AND (@Origin IS NULL OR MenseiId=@Origin)
              UNION ALL
              SELECT Tarih,0,AmbalajAgirligiKg*Adet FROM uretim.PaketlemeKaydi WHERE Tarih>=@Start AND Tarih<@End AND (@Product IS NULL OR UrunId=@Product) AND (@Origin IS NULL OR MenseiId=@Origin)
            ) X GROUP BY Gun ORDER BY Gun;

            SELECT COALESCE(U.Ad,N'Tan'+NCHAR(305)+N'ms'+NCHAR(305)+N'z'),
                   SUM(X.GirdiKg) GirdiKg,
                   SUM(X.KavrulmusKg) KavrulmusKg,
                   SUM(X.EklenenSorteksKg) EklenenSorteksKg,
                   SUM(X.PaketlenenKg) PaketlenenKg,
                   SUM(X.PaketlemeSorteksKg) PaketlemeSorteksKg,
                   SUM(X.GirdiAdedi) GirdiAdedi
            FROM
            (
              SELECT UrunId,CekilenTonajKg GirdiKg,CONVERT(decimal(18,3),0) KavrulmusKg,CONVERT(decimal(18,3),0) EklenenSorteksKg,
                     CONVERT(decimal(18,3),0) PaketlenenKg,CONVERT(decimal(18,3),0) PaketlemeSorteksKg,1 GirdiAdedi
              FROM uretim.IslamaSoymaKaydi WHERE SoymaBitisi>=@Start AND SoymaBitisi<@End AND (@Product IS NULL OR UrunId=@Product) AND (@Origin IS NULL OR MenseiId=@Origin)
              UNION ALL
              SELECT UrunId,0,CASE WHEN NetTonajKg>0 THEN NetTonajKg ELSE 0 END,COALESCE(EklenenSorteksAltiKg,0),0,0,0
              FROM @Kavurma WHERE Tarih>=@Start AND Tarih<@End AND (@Product IS NULL OR UrunId=@Product) AND (@Origin IS NULL OR MenseiId=@Origin)
              UNION ALL
              SELECT UrunId,0,0,0,AmbalajAgirligiKg*Adet,COALESCE(CikanSorteksAltiKg,0),0
              FROM uretim.PaketlemeKaydi WHERE Tarih>=@Start AND Tarih<@End AND (@Product IS NULL OR UrunId=@Product) AND (@Origin IS NULL OR MenseiId=@Origin)
            ) X
            LEFT JOIN tanim.Urun U ON U.UrunId=X.UrunId
            GROUP BY COALESCE(U.Ad,N'Tan'+NCHAR(305)+N'ms'+NCHAR(305)+N'z')
            HAVING SUM(X.GirdiKg)<>0 OR SUM(X.KavrulmusKg)<>0 OR SUM(X.PaketlenenKg)<>0
            ORDER BY COALESCE(U.Ad,N'Tan'+NCHAR(305)+N'ms'+NCHAR(305)+N'z');

            SELECT M.Ad,SUM(I.CekilenTonajKg),COUNT(*)
            FROM uretim.IslamaSoymaKaydi I JOIN tanim.Mensei M ON M.MenseiId=I.MenseiId
            WHERE I.SoymaBitisi>=@Start AND I.SoymaBitisi<@End AND (@Product IS NULL OR I.UrunId=@Product) AND (@Origin IS NULL OR I.MenseiId=@Origin)
            GROUP BY M.Ad ORDER BY SUM(I.CekilenTonajKg) DESC;

            SELECT U.Ad,M.Ad,SUM(I.CekilenTonajKg),COUNT(*)
            FROM uretim.IslamaSoymaKaydi I
            JOIN tanim.Urun U ON U.UrunId=I.UrunId
            JOIN tanim.Mensei M ON M.MenseiId=I.MenseiId
            WHERE I.SoymaBitisi>=@Start AND I.SoymaBitisi<@End AND (@Product IS NULL OR I.UrunId=@Product) AND (@Origin IS NULL OR I.MenseiId=@Origin)
            GROUP BY U.Ad,M.Ad
            ORDER BY COUNT(*) DESC,SUM(I.CekilenTonajKg) DESC;

            SELECT AmbalajAgirligiKg,SUM(Adet),SUM(AmbalajAgirligiKg*Adet)
            FROM uretim.PaketlemeKaydi
            WHERE Tarih>=@Start AND Tarih<@End AND (@Product IS NULL OR UrunId=@Product) AND (@Origin IS NULL OR MenseiId=@Origin)
            GROUP BY AmbalajAgirligiKg ORDER BY AmbalajAgirligiKg;

            SELECT COALESCE(P.AdSoyad,N'Tan'+NCHAR(305)+N'ms'+NCHAR(305)+N'z'),SUM(CASE WHEN K.NetTonajKg>0 THEN K.NetTonajKg ELSE 0 END),
                   COALESCE(SUM(K.TavaSayisi),0)
            FROM @Kavurma K LEFT JOIN tanim.Personel P ON P.PersonelId=K.PersonelId
            WHERE K.Tarih>=@Start AND K.Tarih<@End AND (@Product IS NULL OR K.UrunId=@Product) AND (@Origin IS NULL OR K.MenseiId=@Origin)
            GROUP BY COALESCE(P.AdSoyad,N'Tan'+NCHAR(305)+N'ms'+NCHAR(305)+N'z')
            ORDER BY SUM(CASE WHEN K.NetTonajKg>0 THEN K.NetTonajKg ELSE 0 END) DESC;
            """;
        await using var connection = CreateConnection();
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection); Add(command.Parameters,"@From",from?.Date);Add(command.Parameters,"@To",to?.Date);Add(command.Parameters,"@Product",productId);Add(command.Parameters,"@Origin",originId);
        await using var reader = await command.ExecuteReaderAsync();
        await reader.ReadAsync();
        var counts=new[]{reader.GetInt32(0),reader.GetInt32(1),reader.GetInt32(2),reader.GetInt32(3),reader.GetInt32(4)};
        var input=reader.GetDecimal(5);var roast=reader.GetDecimal(6);var output=reader.GetDecimal(7);var pans=reader.GetInt32(9);var broken=reader.GetInt32(10);
        var addedSortex=reader.GetDecimal(11);var packagingSortex=reader.GetDecimal(12);
        var waste=reader.GetDecimal(13);var bran=reader.GetDecimal(14);var peelingMinutes=reader.GetInt32(15);
        var fillingKg=reader.GetDecimal(17);var fillingUnits=reader.GetInt32(18);var fillingPersonnel=reader.GetInt32(19);
        var excelRoastOutput=roast-addedSortex;
        // HaftalÄ±k randÄ±man: (net kavurma tonajÄ± - eklenen sorteks altÄ±) /
        // Islama-soyma Ã§ekilen toplam tonajÄ± x 100. Ã‡Ã¶p paydadan dÃ¼ÅŸÃ¼lmez.
        var grossYield=input>0?excelRoastOutput/input*100:0;
        var netYield=input==0?0:(excelRoastOutput-packagingSortex)/input*100;
        var trend=new List<DashboardTrend>();await reader.NextResultAsync();while(await reader.ReadAsync())trend.Add(new(reader.GetDateTime(0),reader.GetDecimal(1),reader.GetDecimal(2)));
        var productYields=new List<ProductYield>();await reader.NextResultAsync();while(await reader.ReadAsync())
        {
            var productInput=reader.GetDecimal(1);var productRoasted=reader.GetDecimal(2);var productAdded=reader.GetDecimal(3);
            var productPackaged=reader.GetDecimal(4);var productSortex=reader.GetDecimal(5);
            productYields.Add(new(reader.GetString(0),productInput,productRoasted,productAdded,productPackaged,productSortex,
                productInput==0?0:(productRoasted-productAdded)/productInput*100,
                productPackaged+productSortex==0?0:productPackaged/(productPackaged+productSortex)*100,reader.GetInt32(6)));
        }
        var origins=new List<OriginSummary>();await reader.NextResultAsync();while(await reader.ReadAsync())origins.Add(new(reader.GetString(0),reader.GetDecimal(1),reader.GetInt32(2)));
        var productOrigins=new List<ProductOriginSummary>();await reader.NextResultAsync();while(await reader.ReadAsync())productOrigins.Add(new(reader.GetString(0),reader.GetString(1),reader.GetDecimal(2),reader.GetInt32(3)));
        var packagingWeights=new List<PackagingWeightSummary>();await reader.NextResultAsync();while(await reader.ReadAsync())packagingWeights.Add(new(reader.GetDecimal(0),reader.GetInt32(1),reader.GetDecimal(2)));
        var personnel=new List<PersonnelPerformance>();await reader.NextResultAsync();while(await reader.ReadAsync())
        {var kg=reader.GetDecimal(1);var pan=reader.GetInt32(2);personnel.Add(new(reader.GetString(0),kg,pan,pan==0?0:kg/pan));}
        var packagingYield=output+packagingSortex==0?0:output/(output+packagingSortex)*100;
        var branRatio=input==0?0:bran/input*100;var wasteRatio=input==0?0:waste/input*100;
        return new DashboardStats(counts[0],counts[1],counts[2],counts[3],counts[4],input,roast,output,
            Math.Max(0,input-excelRoastOutput),grossYield,netYield,pans,broken,trend,productYields,
            peelingMinutes==0?0:input/peelingMinutes*60,addedSortex,packagingSortex,packagingYield,
            fillingKg,fillingUnits,fillingPersonnel==0?0:fillingKg/fillingPersonnel,
            bran,branRatio,waste,wasteRatio,grossYield+branRatio+wasteRatio,origins,productOrigins,packagingWeights,personnel);
    }

    public async Task<DateTime?> GetLatestProductionDateAsync()
    {
        const string sql="""
            SELECT MAX(X.Tarih)
            FROM
            (
                SELECT MAX(CONVERT(date,SoymaBitisi)) Tarih FROM uretim.IslamaSoymaKaydi
                UNION ALL SELECT MAX(Tarih) FROM uretim.KavurmaKaydi
                UNION ALL SELECT MAX(Tarih) FROM uretim.PaketlemeKaydi
                UNION ALL SELECT MAX(Tarih) FROM uretim.DolumKaydi
                UNION ALL SELECT MAX(Tarih) FROM uretim.DegirmenNobeti
                UNION ALL SELECT MAX(Tarih) FROM uretim.KurutmaNobeti
            ) X;
            """;
        await using var connection=CreateConnection();await connection.OpenAsync();
        await using var command=new SqlCommand(sql,connection);
        var value=await command.ExecuteScalarAsync();
        return value is null or DBNull?null:Convert.ToDateTime(value);
    }

    public async Task<ProcessDashboardStats> GetProcessDashboardAsync(
        DateTime from,DateTime to,int? productId=null,int? originId=null)
    {
        const string sql="""
            DECLARE @End date=DATEADD(day,1,@To);

            SELECT
              (SELECT COUNT(DISTINCT N.DegirmenNobetId)
               FROM uretim.DegirmenNobeti N JOIN uretim.DegirmenKaydi K ON K.DegirmenNobetId=N.DegirmenNobetId
               WHERE N.Tarih>=@From AND N.Tarih<@End AND @Product IS NULL AND (@Origin IS NULL OR K.MenseiId=@Origin)),
              (SELECT COUNT(*)
               FROM uretim.DegirmenNobeti N JOIN uretim.DegirmenKaydi K ON K.DegirmenNobetId=N.DegirmenNobetId
               WHERE N.Tarih>=@From AND N.Tarih<@End AND @Product IS NULL AND (@Origin IS NULL OR K.MenseiId=@Origin)),
              CAST(0 AS decimal(18,3)),
              (SELECT COALESCE(SUM(K.InceltilenMiktarKg),0)
               FROM uretim.DegirmenNobeti N JOIN uretim.DegirmenKaydi K ON K.DegirmenNobetId=N.DegirmenNobetId
               WHERE N.Tarih>=@From AND N.Tarih<@End AND @Product IS NULL AND (@Origin IS NULL OR K.MenseiId=@Origin)),
              (SELECT COUNT(DISTINCT N.KurutmaNobetId)
               FROM uretim.KurutmaNobeti N JOIN uretim.KurutmaKaydi K ON K.KurutmaNobetId=N.KurutmaNobetId
               WHERE N.Tarih>=@From AND N.Tarih<@End AND (@Product IS NULL OR K.UrunId=@Product) AND (@Origin IS NULL OR K.MenseiId=@Origin)),
              (SELECT COUNT(*)
               FROM uretim.KurutmaNobeti N JOIN uretim.KurutmaKaydi K ON K.KurutmaNobetId=N.KurutmaNobetId
               WHERE N.Tarih>=@From AND N.Tarih<@End AND (@Product IS NULL OR K.UrunId=@Product) AND (@Origin IS NULL OR K.MenseiId=@Origin)),
              (SELECT COALESCE(SUM(K.YikamaSayisi),0)
               FROM uretim.KurutmaNobeti N JOIN uretim.KurutmaKaydi K ON K.KurutmaNobetId=N.KurutmaNobetId
               WHERE N.Tarih>=@From AND N.Tarih<@End AND (@Product IS NULL OR K.UrunId=@Product) AND (@Origin IS NULL OR K.MenseiId=@Origin)),
              (SELECT COALESCE(SUM(K.KirecKg),0)
               FROM uretim.KurutmaNobeti N JOIN uretim.KurutmaKaydi K ON K.KurutmaNobetId=N.KurutmaNobetId
               WHERE N.Tarih>=@From AND N.Tarih<@End AND (@Product IS NULL OR K.UrunId=@Product) AND (@Origin IS NULL OR K.MenseiId=@Origin)),
              (SELECT COALESCE(SUM(K.MakineSayisi),0)
               FROM uretim.KurutmaNobeti N JOIN uretim.KurutmaKaydi K ON K.KurutmaNobetId=N.KurutmaNobetId
               WHERE N.Tarih>=@From AND N.Tarih<@End AND (@Product IS NULL OR K.UrunId=@Product) AND (@Origin IS NULL OR K.MenseiId=@Origin));

            WITH Shifts(Nobet,Sira) AS
            (
              SELECT N'07:00 - 15:00',1 UNION ALL SELECT N'15:00 - 23:00',2
              UNION ALL SELECT N'23:00 - 07:00',3 UNION ALL SELECT N'08:00 - 20:00',4
              UNION ALL SELECT N'20:00 - 08:00',5
            ),
            D AS
            (
              SELECT N.Nobet,CAST(0 AS decimal(18,3)) PureKg,SUM(K.InceltilenMiktarKg) InceltilenKg
              FROM uretim.DegirmenNobeti N JOIN uretim.DegirmenKaydi K ON K.DegirmenNobetId=N.DegirmenNobetId
              WHERE N.Tarih>=@From AND N.Tarih<@End AND @Product IS NULL AND (@Origin IS NULL OR K.MenseiId=@Origin)
              GROUP BY N.Nobet
            ),
            K AS
            (
              SELECT N.Nobet,SUM(K.YikamaSayisi) Yikama,SUM(K.KirecKg) KirecKg,SUM(K.MakineSayisi) Makine
              FROM uretim.KurutmaNobeti N JOIN uretim.KurutmaKaydi K ON K.KurutmaNobetId=N.KurutmaNobetId
              WHERE N.Tarih>=@From AND N.Tarih<@End AND (@Product IS NULL OR K.UrunId=@Product) AND (@Origin IS NULL OR K.MenseiId=@Origin)
              GROUP BY N.Nobet
            )
            SELECT S.Nobet,COALESCE(D.PureKg,0),COALESCE(D.InceltilenKg,0),
                   COALESCE(K.Yikama,0),COALESCE(K.KirecKg,0),COALESCE(K.Makine,0)
            FROM Shifts S LEFT JOIN D ON D.Nobet=S.Nobet LEFT JOIN K ON K.Nobet=S.Nobet
            WHERE D.Nobet IS NOT NULL OR K.Nobet IS NOT NULL
            ORDER BY S.Sira;
            """;
        await using var connection=CreateConnection();await connection.OpenAsync();
        await using var command=new SqlCommand(sql,connection);
        Add(command.Parameters,"@From",from.Date);Add(command.Parameters,"@To",to.Date);
        Add(command.Parameters,"@Product",productId);Add(command.Parameters,"@Origin",originId);
        await using var reader=await command.ExecuteReaderAsync();
        await reader.ReadAsync();
        var result=new ProcessDashboardStats(
            reader.GetInt32(0),reader.GetInt32(1),reader.GetDecimal(2),reader.GetDecimal(3),
            reader.GetInt32(4),reader.GetInt32(5),reader.GetInt32(6),reader.GetDecimal(7),reader.GetInt32(8),[]);
        var shifts=new List<ProcessShiftSummary>();
        await reader.NextResultAsync();
        while(await reader.ReadAsync())
            shifts.Add(new(reader.GetString(0),reader.GetDecimal(1),reader.GetDecimal(2),
                reader.GetInt32(3),reader.GetDecimal(4),reader.GetInt32(5)));
        return result with { Shifts=shifts };
    }

    public Task<List<LookupItem>> GetMenseilerAsync() => GetLookupAsync("SELECT MenseiId, Ad FROM tanim.Mensei WHERE Aktif=1 ORDER BY Ad");
    public Task<List<LookupItem>> GetUrunlerAsync() => GetLookupAsync("SELECT UrunId, Ad FROM tanim.Urun WHERE Aktif=1 ORDER BY Ad");
    public Task<List<LookupItem>> GetPersonellerAsync() => GetLookupAsync("SELECT PersonelId, AdSoyad FROM tanim.Personel WHERE Aktif=1 ORDER BY AdSoyad");
    public async Task<List<LookupItem>> GetPersonnelStatusesAsync()
    {
        const string sql="SELECT PersonelId,AdSoyad,Aktif FROM tanim.Personel ORDER BY Aktif DESC,AdSoyad";var result=new List<LookupItem>();
        await using var c=CreateConnection();await c.OpenAsync();await using var cmd=new SqlCommand(sql,c);await using var r=await cmd.ExecuteReaderAsync();
        while(await r.ReadAsync())result.Add(new(r.GetInt32(0),r.GetString(1),null,r.GetBoolean(2)?"Aktif":"Pasif"));return result;
    }
    public Task<List<LookupItem>> GetSilolarAsync() => GetLookupAsync("SELECT SiloId, Kod FROM tanim.Silo WHERE Aktif=1 AND Kod IN (N'Silo 1', N'Silo 2') ORDER BY Kod");

    public async Task<List<LookupItem>> GetAmbalajlarAsync()
    {
        const string sql = "SELECT AmbalajId,Cins,AgirlikKg FROM tanim.Ambalaj WHERE Aktif=1 ORDER BY Cins,AgirlikKg";
        var items = new List<LookupItem>();
        await using var connection = CreateConnection();
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            items.Add(new LookupItem(reader.GetInt32(0), reader.GetString(1), reader.GetDecimal(2)));
        return items;
    }

    public async Task<List<DegirmenNobetListItem>> GetDegirmenNobetleriAsync(int take=100,int? personnelId=null)
    {
        const string sql="""
            SELECT TOP(@Take) N.DegirmenNobetId,N.Tarih,N.Nobet,P.AdSoyad,N.Aciklama,
                   K.SatirNo,K.FirinNoSergen,M.Ad,K.PureMiktariKg,
                   K.InceltilenMiktarKg,K.TransferEdilenTank
            FROM uretim.DegirmenNobeti N
            JOIN tanim.Personel P ON P.PersonelId=N.PersonelId
            JOIN uretim.DegirmenKaydi K ON K.DegirmenNobetId=N.DegirmenNobetId
            JOIN tanim.Mensei M ON M.MenseiId=K.MenseiId
            WHERE @PersonnelId IS NULL OR N.PersonelId=@PersonnelId
            ORDER BY N.Tarih DESC,N.DegirmenNobetId DESC,K.SatirNo;
            """;
        var result=new List<DegirmenNobetListItem>();
        await using var connection=CreateConnection();await connection.OpenAsync();
        await using var command=new SqlCommand(sql,connection);command.Parameters.AddWithValue("@Take",take);
        Add(command.Parameters,"@PersonnelId",personnelId);
        await using var reader=await command.ExecuteReaderAsync();
        while(await reader.ReadAsync())result.Add(new(
            reader.GetInt64(0),reader.GetDateTime(1),reader.GetString(2),reader.GetString(3),S(reader,4),
            reader.GetInt32(5),reader.GetString(6),reader.GetString(7),reader.GetString(8),
            reader.GetDecimal(9),reader.GetString(10)));
        return result;
    }

    public async Task<DegirmenNobetInput?> GetDegirmenNobetInputAsync(long id,int? personnelId=null)
    {
        const string sql="""
            SELECT N.Tarih,N.Nobet,N.PersonelId,N.Aciklama
            FROM uretim.DegirmenNobeti N
            WHERE N.DegirmenNobetId=@Id AND (@PersonnelId IS NULL OR N.PersonelId=@PersonnelId);
            SELECT K.FirinNoSergen,K.MenseiId,K.PureMiktariKg,K.InceltilenMiktarKg,K.TransferEdilenTank
            FROM uretim.DegirmenKaydi K
            JOIN uretim.DegirmenNobeti N ON N.DegirmenNobetId=K.DegirmenNobetId
            WHERE K.DegirmenNobetId=@Id AND (@PersonnelId IS NULL OR N.PersonelId=@PersonnelId)
            ORDER BY K.SatirNo;
            """;
        await using var connection=CreateConnection();await connection.OpenAsync();
        await using var command=new SqlCommand(sql,connection);Add(command.Parameters,"@Id",id);Add(command.Parameters,"@PersonnelId",personnelId);
        await using var reader=await command.ExecuteReaderAsync();
        if(!await reader.ReadAsync())return null;
        var input=new DegirmenNobetInput
        {
            Tarih=reader.GetDateTime(0),Nobet=reader.GetString(1),PersonelId=reader.GetInt32(2),
            Aciklama=S(reader,3),Satirlar=[]
        };
        await reader.NextResultAsync();
        while(await reader.ReadAsync())input.Satirlar.Add(new DegirmenSatirInput
        {
            FirinNoSergen=reader.GetString(0),MenseiId=reader.GetInt32(1),PureMiktari=reader.GetString(2),
            InceltilenMiktarKg=reader.GetDecimal(3),TransferEdilenTank=reader.GetString(4)
        });
        return input;
    }

    public async Task<List<TankTransferListItem>> GetTankTransferleriAsync(int take=100,int? personnelId=null)
    {
        const string sql="""
            SELECT TOP(@Take) T.TankTransferId,T.TransferZamani,T.KaynakTank,T.HedefTank,
                   T.MiktarKg,M.Ad,P.AdSoyad,T.Aciklama
            FROM uretim.TankTransferi T
            JOIN tanim.Mensei M ON M.MenseiId=T.MenseiId
            JOIN tanim.Personel P ON P.PersonelId=T.PersonelId
            WHERE @PersonnelId IS NULL OR T.PersonelId=@PersonnelId
            ORDER BY T.TransferZamani DESC,T.TankTransferId DESC;
            """;
        var result=new List<TankTransferListItem>();
        await using var connection=CreateConnection();await connection.OpenAsync();
        await using var command=new SqlCommand(sql,connection);
        command.Parameters.AddWithValue("@Take",take);Add(command.Parameters,"@PersonnelId",personnelId);
        await using var reader=await command.ExecuteReaderAsync();
        while(await reader.ReadAsync())result.Add(new(
            reader.GetInt64(0),reader.GetDateTime(1),reader.GetString(2),reader.GetString(3),
            reader.GetDecimal(4),reader.GetString(5),reader.GetString(6),S(reader,7)));
        return result;
    }

    public Task InsertTankTransferiAsync(TankTransferInput input)=>ExecuteAsync("""
        INSERT uretim.TankTransferi
          (TransferZamani,KaynakTank,HedefTank,MiktarKg,MenseiId,PersonelId,Aciklama,Olusturan)
        VALUES(@Zaman,@Kaynak,@Hedef,@Miktar,@Mensei,@Personel,@Aciklama,SUSER_SNAME());
        """,parameters=>{
            Add(parameters,"@Zaman",input.TransferZamani);
            Add(parameters,"@Kaynak",input.KaynakTank.Trim());
            Add(parameters,"@Hedef",input.HedefTank.Trim());
            Add(parameters,"@Miktar",input.MiktarKg);
            Add(parameters,"@Mensei",input.MenseiId);
            Add(parameters,"@Personel",input.PersonelId);
            Add(parameters,"@Aciklama",input.Aciklama?.Trim());
        });

    public Task DeleteTankTransferiAsync(long id,int? personnelId=null)=>ExecuteAsync(
        "DELETE uretim.TankTransferi WHERE TankTransferId=@Id AND (@PersonnelId IS NULL OR PersonelId=@PersonnelId);",
        parameters=>{Add(parameters,"@Id",id);Add(parameters,"@PersonnelId",personnelId);});

    public async Task InsertDegirmenNobetiAsync(DegirmenNobetInput input)
    {
        await using var connection=CreateConnection();await connection.OpenAsync();
        await using var transaction=(SqlTransaction)await connection.BeginTransactionAsync();
        try
        {
            const string headerSql="""
                INSERT uretim.DegirmenNobeti(Tarih,Nobet,PersonelId,Aciklama,Olusturan)
                OUTPUT INSERTED.DegirmenNobetId
                VALUES(@Tarih,@Nobet,@Personel,@Aciklama,SUSER_SNAME());
                """;
            long headerId;
            await using(var header=new SqlCommand(headerSql,connection,transaction))
            {
                Add(header.Parameters,"@Tarih",input.Tarih);
                Add(header.Parameters,"@Nobet",input.Nobet.Trim());
                Add(header.Parameters,"@Personel",input.PersonelId);
                Add(header.Parameters,"@Aciklama",input.Aciklama?.Trim());
                headerId=Convert.ToInt64(await header.ExecuteScalarAsync());
            }
            const string rowSql="""
                INSERT uretim.DegirmenKaydi
                  (DegirmenNobetId,SatirNo,FirinNoSergen,MenseiId,PureMiktariKg,InceltilenMiktarKg,TransferEdilenTank)
                VALUES(@NobetId,@SatirNo,@Firin,@Mensei,@Pure,@Inceltilen,@Tank);
                """;
            for(var i=0;i<input.Satirlar.Count;i++)
            {
                var row=input.Satirlar[i];
                await using var command=new SqlCommand(rowSql,connection,transaction);
                Add(command.Parameters,"@NobetId",headerId);Add(command.Parameters,"@SatirNo",i+1);
                Add(command.Parameters,"@Firin",row.FirinNoSergen.Trim());Add(command.Parameters,"@Mensei",row.MenseiId);
                Add(command.Parameters,"@Pure",row.PureMiktari.Trim());Add(command.Parameters,"@Inceltilen",row.InceltilenMiktarKg);
                Add(command.Parameters,"@Tank",row.TransferEdilenTank.Trim());
                await command.ExecuteNonQueryAsync();
            }
            await transaction.CommitAsync();
        }
        catch{await transaction.RollbackAsync();throw;}
    }

    public Task DeleteDegirmenNobetiAsync(long id)=>ExecuteAsync(
        "DELETE uretim.DegirmenNobeti WHERE DegirmenNobetId=@Id;",p=>Add(p,"@Id",id));

    public async Task UpdateDegirmenNobetiAsync(long id,DegirmenNobetInput input,int? personnelId=null)
    {
        await using var connection=CreateConnection();await connection.OpenAsync();
        await using var transaction=(SqlTransaction)await connection.BeginTransactionAsync();
        try
        {
            const string headerSql="""
                UPDATE uretim.DegirmenNobeti
                SET Tarih=@Tarih,Nobet=@Nobet,PersonelId=@Personel,Aciklama=@Aciklama
                WHERE DegirmenNobetId=@Id AND (@PersonnelId IS NULL OR PersonelId=@PersonnelId);
                """;
            await using(var header=new SqlCommand(headerSql,connection,transaction))
            {
                Add(header.Parameters,"@Id",id);Add(header.Parameters,"@Tarih",input.Tarih);Add(header.Parameters,"@Nobet",input.Nobet.Trim());
                Add(header.Parameters,"@Personel",input.PersonelId);Add(header.Parameters,"@Aciklama",input.Aciklama?.Trim());
                Add(header.Parameters,"@PersonnelId",personnelId);
                if(await header.ExecuteNonQueryAsync()==0)throw new UnauthorizedAccessException("Bu kaydı düzenleme yetkiniz yok.");
            }
            await using(var delete=new SqlCommand("DELETE uretim.DegirmenKaydi WHERE DegirmenNobetId=@Id;",connection,transaction))
            {Add(delete.Parameters,"@Id",id);await delete.ExecuteNonQueryAsync();}
            const string rowSql="INSERT uretim.DegirmenKaydi(DegirmenNobetId,SatirNo,FirinNoSergen,MenseiId,PureMiktariKg,InceltilenMiktarKg,TransferEdilenTank) VALUES(@Id,@No,@Firin,@Mensei,@Pure,@Inceltilen,@Tank);";
            for(var i=0;i<input.Satirlar.Count;i++)
            {
                var row=input.Satirlar[i];await using var command=new SqlCommand(rowSql,connection,transaction);
                Add(command.Parameters,"@Id",id);Add(command.Parameters,"@No",i+1);Add(command.Parameters,"@Firin",row.FirinNoSergen.Trim());
                Add(command.Parameters,"@Mensei",row.MenseiId);Add(command.Parameters,"@Pure",row.PureMiktari.Trim());
                Add(command.Parameters,"@Inceltilen",row.InceltilenMiktarKg);Add(command.Parameters,"@Tank",row.TransferEdilenTank.Trim());
                await command.ExecuteNonQueryAsync();
            }
            await transaction.CommitAsync();
        }
        catch{await transaction.RollbackAsync();throw;}
    }

    public async Task<List<KurutmaNobetListItem>> GetKurutmaNobetleriAsync(int take=100,int? personnelId=null)
    {
        const string sql="""
            SELECT TOP(@Take) N.KurutmaNobetId,N.Tarih,N.Nobet,P.AdSoyad,N.Aciklama,
                   K.SatirNo,M.Ad,U.Ad,K.YikamaSayisi,K.KirecKg,K.MakineSayisi
            FROM uretim.KurutmaNobeti N
            JOIN tanim.Personel P ON P.PersonelId=N.PersonelId
            JOIN uretim.KurutmaKaydi K ON K.KurutmaNobetId=N.KurutmaNobetId
            JOIN tanim.Mensei M ON M.MenseiId=K.MenseiId
            JOIN tanim.Urun U ON U.UrunId=K.UrunId
            WHERE @PersonnelId IS NULL OR N.PersonelId=@PersonnelId
            ORDER BY N.Tarih DESC,N.KurutmaNobetId DESC,K.SatirNo;
            """;
        var result=new List<KurutmaNobetListItem>();
        await using var connection=CreateConnection();await connection.OpenAsync();
        await using var command=new SqlCommand(sql,connection);command.Parameters.AddWithValue("@Take",take);
        Add(command.Parameters,"@PersonnelId",personnelId);
        await using var reader=await command.ExecuteReaderAsync();
        while(await reader.ReadAsync())result.Add(new(
            reader.GetInt64(0),reader.GetDateTime(1),reader.GetString(2),reader.GetString(3),S(reader,4),
            reader.GetInt32(5),reader.GetString(6),reader.GetString(7),reader.GetInt32(8),
            reader.GetDecimal(9),reader.GetInt32(10)));
        return result;
    }

    public async Task<KurutmaNobetInput?> GetKurutmaNobetInputAsync(long id,int? personnelId=null)
    {
        const string sql="""
            SELECT N.Tarih,N.Nobet,N.PersonelId,N.Aciklama
            FROM uretim.KurutmaNobeti N
            WHERE N.KurutmaNobetId=@Id AND (@PersonnelId IS NULL OR N.PersonelId=@PersonnelId);
            SELECT K.MenseiId,K.UrunId,K.YikamaSayisi,K.KirecKg,K.MakineSayisi
            FROM uretim.KurutmaKaydi K
            JOIN uretim.KurutmaNobeti N ON N.KurutmaNobetId=K.KurutmaNobetId
            WHERE K.KurutmaNobetId=@Id AND (@PersonnelId IS NULL OR N.PersonelId=@PersonnelId)
            ORDER BY K.SatirNo;
            """;
        await using var connection=CreateConnection();await connection.OpenAsync();
        await using var command=new SqlCommand(sql,connection);Add(command.Parameters,"@Id",id);Add(command.Parameters,"@PersonnelId",personnelId);
        await using var reader=await command.ExecuteReaderAsync();
        if(!await reader.ReadAsync())return null;
        var input=new KurutmaNobetInput
        {
            Tarih=reader.GetDateTime(0),Nobet=reader.GetString(1),PersonelId=reader.GetInt32(2),
            Aciklama=S(reader,3),Satirlar=[]
        };
        await reader.NextResultAsync();
        while(await reader.ReadAsync())input.Satirlar.Add(new KurutmaSatirInput
        {
            MenseiId=reader.GetInt32(0),UrunId=reader.GetInt32(1),YikamaSayisi=reader.GetInt32(2),
            KirecKg=reader.GetDecimal(3),MakineSayisi=reader.GetInt32(4)
        });
        return input;
    }

    public async Task InsertKurutmaNobetiAsync(KurutmaNobetInput input)
    {
        await using var connection=CreateConnection();await connection.OpenAsync();
        await using var transaction=(SqlTransaction)await connection.BeginTransactionAsync();
        try
        {
            const string headerSql="""
                INSERT uretim.KurutmaNobeti(Tarih,Nobet,PersonelId,Aciklama,Olusturan)
                OUTPUT INSERTED.KurutmaNobetId
                VALUES(@Tarih,@Nobet,@Personel,@Aciklama,SUSER_SNAME());
                """;
            long headerId;
            await using(var header=new SqlCommand(headerSql,connection,transaction))
            {
                Add(header.Parameters,"@Tarih",input.Tarih);Add(header.Parameters,"@Nobet",input.Nobet.Trim());
                Add(header.Parameters,"@Personel",input.PersonelId);Add(header.Parameters,"@Aciklama",input.Aciklama?.Trim());
                headerId=Convert.ToInt64(await header.ExecuteScalarAsync());
            }
            const string rowSql="""
                INSERT uretim.KurutmaKaydi
                  (KurutmaNobetId,SatirNo,MenseiId,UrunId,YikamaSayisi,KirecKg,MakineSayisi)
                VALUES(@NobetId,@SatirNo,@Mensei,@Urun,@Yikama,@Kirec,@Makine);
                """;
            for(var i=0;i<input.Satirlar.Count;i++)
            {
                var row=input.Satirlar[i];
                await using var command=new SqlCommand(rowSql,connection,transaction);
                Add(command.Parameters,"@NobetId",headerId);Add(command.Parameters,"@SatirNo",i+1);
                Add(command.Parameters,"@Mensei",row.MenseiId);Add(command.Parameters,"@Urun",row.UrunId);
                Add(command.Parameters,"@Yikama",row.YikamaSayisi);Add(command.Parameters,"@Kirec",row.KirecKg);
                Add(command.Parameters,"@Makine",row.MakineSayisi);
                await command.ExecuteNonQueryAsync();
            }
            await transaction.CommitAsync();
        }
        catch{await transaction.RollbackAsync();throw;}
    }

    public Task DeleteKurutmaNobetiAsync(long id)=>ExecuteAsync(
        "DELETE uretim.KurutmaNobeti WHERE KurutmaNobetId=@Id;",p=>Add(p,"@Id",id));

    public async Task UpdateKurutmaNobetiAsync(long id,KurutmaNobetInput input,int? personnelId=null)
    {
        await using var connection=CreateConnection();await connection.OpenAsync();
        await using var transaction=(SqlTransaction)await connection.BeginTransactionAsync();
        try
        {
            const string headerSql="""
                UPDATE uretim.KurutmaNobeti
                SET Tarih=@Tarih,Nobet=@Nobet,PersonelId=@Personel,Aciklama=@Aciklama
                WHERE KurutmaNobetId=@Id AND (@PersonnelId IS NULL OR PersonelId=@PersonnelId);
                """;
            await using(var header=new SqlCommand(headerSql,connection,transaction))
            {
                Add(header.Parameters,"@Id",id);Add(header.Parameters,"@Tarih",input.Tarih);Add(header.Parameters,"@Nobet",input.Nobet.Trim());
                Add(header.Parameters,"@Personel",input.PersonelId);Add(header.Parameters,"@Aciklama",input.Aciklama?.Trim());
                Add(header.Parameters,"@PersonnelId",personnelId);
                if(await header.ExecuteNonQueryAsync()==0)throw new UnauthorizedAccessException("Bu kaydı düzenleme yetkiniz yok.");
            }
            await using(var delete=new SqlCommand("DELETE uretim.KurutmaKaydi WHERE KurutmaNobetId=@Id;",connection,transaction))
            {Add(delete.Parameters,"@Id",id);await delete.ExecuteNonQueryAsync();}
            const string rowSql="INSERT uretim.KurutmaKaydi(KurutmaNobetId,SatirNo,MenseiId,UrunId,YikamaSayisi,KirecKg,MakineSayisi) VALUES(@Id,@No,@Mensei,@Urun,@Yikama,@Kirec,@Makine);";
            for(var i=0;i<input.Satirlar.Count;i++)
            {
                var row=input.Satirlar[i];await using var command=new SqlCommand(rowSql,connection,transaction);
                Add(command.Parameters,"@Id",id);Add(command.Parameters,"@No",i+1);Add(command.Parameters,"@Mensei",row.MenseiId);
                Add(command.Parameters,"@Urun",row.UrunId);Add(command.Parameters,"@Yikama",row.YikamaSayisi);
                Add(command.Parameters,"@Kirec",row.KirecKg);Add(command.Parameters,"@Makine",row.MakineSayisi);
                await command.ExecuteNonQueryAsync();
            }
            await transaction.CommitAsync();
        }
        catch{await transaction.RollbackAsync();throw;}
    }

    public async Task<List<CopListItem>> GetCopKayitlariAsync(int take=100,int? personnelId=null)
    {
        const string sql="""
            SELECT TOP(@Take) C.CopKaydiId,C.Tarih,M.Ad,C.CopKg
            FROM uretim.CopKaydi C
            JOIN tanim.Mensei M ON M.MenseiId=C.MenseiId
            WHERE @PersonnelId IS NULL OR C.PersonelId=@PersonnelId
            ORDER BY C.Tarih DESC,C.CopKaydiId DESC;
            """;
        var result=new List<CopListItem>();
        await using var connection=CreateConnection();await connection.OpenAsync();
        await using var command=new SqlCommand(sql,connection);command.Parameters.AddWithValue("@Take",take);Add(command.Parameters,"@PersonnelId",personnelId);
        await using var reader=await command.ExecuteReaderAsync();
        while(await reader.ReadAsync())result.Add(new(reader.GetInt64(0),reader.GetDateTime(1),reader.GetString(2),reader.GetDecimal(3)));
        return result;
    }

    public Task InsertCopAsync(CopInput input)=>ExecuteAsync("""
        INSERT uretim.CopKaydi(Tarih,MenseiId,CopKg,PersonelId,Olusturan)
        VALUES(@Tarih,@Mensei,@Cop,@Personel,SUSER_SNAME());
        """,p=>{Add(p,"@Tarih",input.Tarih);Add(p,"@Mensei",input.MenseiId);Add(p,"@Cop",input.CopKg);Add(p,"@Personel",input.PersonelId);});

    public async Task<CopInput?> GetCopInputAsync(long id,int? personnelId=null)
    {
        const string sql="SELECT Tarih,MenseiId,CopKg,PersonelId FROM uretim.CopKaydi WHERE CopKaydiId=@Id AND (@PersonnelId IS NULL OR PersonelId=@PersonnelId);";
        await using var c=CreateConnection();await c.OpenAsync();await using var cmd=new SqlCommand(sql,c);Add(cmd.Parameters,"@Id",id);Add(cmd.Parameters,"@PersonnelId",personnelId);
        await using var r=await cmd.ExecuteReaderAsync();if(!await r.ReadAsync())return null;
        return new(){Tarih=r.GetDateTime(0),MenseiId=r.GetInt32(1),CopKg=r.GetDecimal(2),PersonelId=D<int>(r,3)};
    }

    public Task UpdateCopAsync(long id,CopInput input)=>ExecuteAsync(
        "UPDATE uretim.CopKaydi SET Tarih=@Tarih,MenseiId=@Mensei,CopKg=@Cop,PersonelId=@Personel WHERE CopKaydiId=@Id;",
        p=>{Add(p,"@Id",id);Add(p,"@Tarih",input.Tarih);Add(p,"@Mensei",input.MenseiId);Add(p,"@Cop",input.CopKg);Add(p,"@Personel",input.PersonelId);});

    public Task DeleteCopAsync(long id)=>ExecuteAsync(
        "DELETE uretim.CopKaydi WHERE CopKaydiId=@Id;",p=>Add(p,"@Id",id));

    private async Task<List<LookupItem>> GetLookupAsync(string sql)
    {
        var items = new List<LookupItem>();
        await using var connection = CreateConnection();
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            items.Add(new LookupItem(reader.GetInt32(0), reader.GetString(1)));
        return items;
    }

    public async Task<List<IslamaListItem>> GetIslamaAsync(int take = 100, RecordFilter? filter = null,int? personnelId=null,bool onlyCreatedToday=false)
    {
        const string sql = """
            SELECT TOP (@Take) I.IslamaSoymaKaydiId,I.PartiNo,I.SoymaBitisi,I.SoymaSuresiDakika,
                   I.CekilenTonajKg,I.CopKg,M.Ad,U.Ad,COALESCE(NULLIF(CONCAT(I.Silo1,' ',I.Silo2),' '),S.Kod),
                   I.HavuzNo,I.SalamuraDerecesi,I.YedekDerecesi,HP.AdSoyad,IP.AdSoyad,SP.AdSoyad
            FROM uretim.IslamaSoymaKaydi I
            JOIN tanim.Mensei M ON M.MenseiId=I.MenseiId
            JOIN tanim.Urun U ON U.UrunId=I.UrunId
            LEFT JOIN tanim.Silo S ON S.SiloId=I.SiloId
            LEFT JOIN tanim.Personel HP ON HP.PersonelId=I.HazirlayanPersonelId
            LEFT JOIN tanim.Personel IP ON IP.PersonelId=I.IslamaPersonelId
            LEFT JOIN tanim.Personel SP ON SP.PersonelId=I.SoymaPersonelId
            WHERE (@Like IS NULL OR I.PartiNo LIKE @Like OR I.BarkodSeri LIKE @Like OR M.Ad LIKE @Like OR U.Ad LIKE @Like OR S.Kod LIKE @Like OR I.Silo1 LIKE @Like OR I.Silo2 LIKE @Like)
              AND (@PersonnelId IS NULL OR I.PersonelId=@PersonnelId)
              AND (@OnlyCreatedToday=0 OR CONVERT(date,I.OlusturmaZamani)=CONVERT(date,GETDATE()))
              AND (@From IS NULL OR I.SoymaBitisi >= @From)
              AND (@To IS NULL OR I.SoymaBitisi < DATEADD(DAY,1,@To))
            ORDER BY I.SoymaBitisi DESC,I.IslamaSoymaKaydiId DESC;
            """;
        var result = new List<IslamaListItem>();
        await using var connection = CreateConnection(); await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection); command.Parameters.AddWithValue("@Take", take); AddFilterParameters(command, filter);Add(command.Parameters,"@PersonnelId",personnelId);command.Parameters.AddWithValue("@OnlyCreatedToday",onlyCreatedToday);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            result.Add(new(reader.GetInt64(0),reader.GetString(1),reader.GetDateTime(2),reader.GetInt32(3),reader.GetDecimal(4),
                reader.IsDBNull(5)?null:reader.GetDecimal(5),reader.GetString(6),reader.GetString(7),S(reader,8),
                S(reader,9),D<decimal>(reader,10),D<decimal>(reader,11),S(reader,12),S(reader,13),S(reader,14)));
        return result;
    }

    public async Task<List<IslamaWorkflowItem>> GetIslamaWorkflowAsync(bool includeCompleted = false)
    {
        const string sql = """
            SELECT W.IsAkisiId,W.PartiNo,W.Asama,W.NobetTarihi,W.HamSusamGelisTarihi,W.IslamaTarihi,W.HavuzNo,
                   W.EkranTonajiKg,W.CekilenTonajKg,W.MenseiId,M.Ad,W.IslamaBaslangici,W.IslamaBitisi,H.AdSoyad,I.AdSoyad,W.GuncellemeZamani
            FROM uretim.IslamaSoymaIsAkisi W
            LEFT JOIN tanim.Mensei M ON M.MenseiId=W.MenseiId
            LEFT JOIN tanim.Personel H ON H.PersonelId=W.HazirlayanPersonelId
            LEFT JOIN tanim.Personel I ON I.PersonelId=W.IslamaPersonelId
            WHERE (@IncludeCompleted=1 OR W.Asama<3)
            ORDER BY W.Asama,W.GuncellemeZamani,W.IsAkisiId;
            """;
        var result = new List<IslamaWorkflowItem>();
        await using var connection=CreateConnection();await connection.OpenAsync();
        await using var command=new SqlCommand(sql,connection);
        command.Parameters.AddWithValue("@IncludeCompleted",includeCompleted);
        await using var reader=await command.ExecuteReaderAsync();
        while(await reader.ReadAsync())
            result.Add(new(reader.GetInt64(0),reader.GetString(1),reader.GetByte(2),reader.GetDateTime(3),
                D<DateTime>(reader,4),reader.GetDateTime(5),S(reader,6),D<decimal>(reader,7),D<decimal>(reader,8),D<int>(reader,9),S(reader,10),
                D<DateTime>(reader,11),D<DateTime>(reader,12),S(reader,13),S(reader,14),reader.GetDateTime(15)));
        return result;
    }

    public async Task<IslamaWorkflowItem?> GetIslamaWorkflowItemAsync(long id)
    {
        const string sql = """
            SELECT W.IsAkisiId,W.PartiNo,W.Asama,W.NobetTarihi,W.HamSusamGelisTarihi,W.IslamaTarihi,W.HavuzNo,
                   W.EkranTonajiKg,W.CekilenTonajKg,W.MenseiId,M.Ad,W.IslamaBaslangici,W.IslamaBitisi,H.AdSoyad,I.AdSoyad,W.GuncellemeZamani
            FROM uretim.IslamaSoymaIsAkisi W
            LEFT JOIN tanim.Mensei M ON M.MenseiId=W.MenseiId
            LEFT JOIN tanim.Personel H ON H.PersonelId=W.HazirlayanPersonelId
            LEFT JOIN tanim.Personel I ON I.PersonelId=W.IslamaPersonelId
            WHERE W.IsAkisiId=@Id;
            """;
        await using var connection=CreateConnection();await connection.OpenAsync();
        await using var command=new SqlCommand(sql,connection);command.Parameters.AddWithValue("@Id",id);
        await using var reader=await command.ExecuteReaderAsync();
        if(!await reader.ReadAsync())return null;
        return new(reader.GetInt64(0),reader.GetString(1),reader.GetByte(2),reader.GetDateTime(3),
            D<DateTime>(reader,4),reader.GetDateTime(5),S(reader,6),D<decimal>(reader,7),D<decimal>(reader,8),D<int>(reader,9),S(reader,10),
            D<DateTime>(reader,11),D<DateTime>(reader,12),S(reader,13),S(reader,14),reader.GetDateTime(15));
    }

    public async Task<string> InsertIslamaHazirlikAsync(IslamaHazirlikInput x,int? personnelId)
    {
        var parti=await GetNextBatchNumberAsync(x.IslamaTarihi!.Value);
        await ExecuteAsync("""
            INSERT uretim.IslamaSoymaIsAkisi
                (PartiNo,Asama,NobetTarihi,HamSusamGelisTarihi,IslamaTarihi,MenseiId,EkranTonajiKg,CekilenTonajKg,HazirlayanPersonelId)
            VALUES(@Parti,1,@Nobet,@Gelis,@Islama,@Mensei,@Ekran,@Cekilen,@Personel);
            """,p=>{Add(p,"@Parti",parti);Add(p,"@Nobet",x.NobetTarihi);Add(p,"@Gelis",x.HamSusamGelisTarihi);Add(p,"@Islama",x.IslamaTarihi);
                Add(p,"@Mensei",x.MenseiId);Add(p,"@Ekran",x.EkranTonajiKg);Add(p,"@Cekilen",x.CekilenTonajKg);Add(p,"@Personel",personnelId);});
        return parti;
    }

    public Task CompleteIslamaStageAsync(long id,IslamaSurecInput x,int? personnelId)=>ExecuteAsync("""
        UPDATE uretim.IslamaSoymaIsAkisi
        SET IslamaBaslangici=@Baslangic,IslamaBitisi=@Bitis,IslamaPersonelId=@Personel,
            Asama=2,GuncellemeZamani=SYSDATETIME()
        WHERE IsAkisiId=@Id AND Asama=1;
        IF @@ROWCOUNT=0 THROW 50001,N'Bu kayıt artık ıslama aşamasında değil.',1;
        """,p=>{Add(p,"@Id",id);Add(p,"@Baslangic",x.IslamaBaslangici);Add(p,"@Bitis",x.IslamaBitisi);Add(p,"@Personel",personnelId);});

    public async Task CompleteSoymaStageAsync(long id,SoymaTamamlamaInput x,int? personnelId)
    {
        await using var connection=CreateConnection();await connection.OpenAsync();
        await using var transaction=await connection.BeginTransactionAsync();
        try
        {
            const string sql = """
                DECLARE @Parti varchar(50),@Nobet date,@IslamaBas datetime2(0),@IslamaBit datetime2(0),
                        @Havuz nvarchar(30),@Hazirlayan int,@IslamaPersoneli int,@Gelis date,@MenseiKaydi int,
                        @EkranKaydi decimal(18,3),@CekilenKaydi decimal(18,3);
                SELECT @Parti=PartiNo,@Nobet=NobetTarihi,@IslamaBas=IslamaBaslangici,@IslamaBit=IslamaBitisi,
                       @Havuz=HavuzNo,@Hazirlayan=HazirlayanPersonelId,@IslamaPersoneli=IslamaPersonelId,
                       @Gelis=HamSusamGelisTarihi,@MenseiKaydi=MenseiId,@EkranKaydi=EkranTonajiKg,@CekilenKaydi=CekilenTonajKg
                FROM uretim.IslamaSoymaIsAkisi WITH(UPDLOCK,HOLDLOCK) WHERE IsAkisiId=@Id AND Asama=2;
                IF @Parti IS NULL THROW 50002,N'Bu kayıt artık soyma aşamasında değil.',1;

                INSERT uretim.IslamaSoymaKaydi
                  (BarkodSeri,HamSusamGelisTarihi,CopKg,PartiNo,NobetTarihi,IslamaBaslangici,IslamaBitisi,
                   SoymaBaslangici,SoymaBitisi,EkranTonajiKg,CekilenTonajKg,Silo1,Silo2,MenseiId,UrunId,
                   Aciklama,PersonelId,RaporTarihi,HavuzNo,SalamuraDerecesi,YedekDerecesi,HazirlayanPersonelId,IslamaPersonelId,SoymaPersonelId,Olusturan)
                VALUES(@Barkod,COALESCE(@Gelis,@EskiGelis),@Cop,@Parti,@Nobet,@IslamaBas,@IslamaBit,@SoymaBas,@SoymaBit,
                   COALESCE(@EkranKaydi,@Ekran),COALESCE(@CekilenKaydi,@Cekilen),@Silo1,@Silo2,COALESCE(@MenseiKaydi,@EskiMensei),@Urun,@Aciklama,@Personel,@RaporTarihi,
                   COALESCE(NULLIF(@YeniHavuz,N''),@Havuz),@Salamura,@Yedek,@Hazirlayan,@IslamaPersoneli,@Personel,SUSER_SNAME());
                DECLARE @KayitId bigint=SCOPE_IDENTITY();
                UPDATE uretim.IslamaSoymaIsAkisi SET Asama=3,SoymaPersonelId=@Personel,
                    TamamlananKayitId=@KayitId,GuncellemeZamani=SYSDATETIME() WHERE IsAkisiId=@Id;
                """;
            await using var command=new SqlCommand(sql,connection,(SqlTransaction)transaction);
            Add(command.Parameters,"@Id",id);Add(command.Parameters,"@Barkod",x.BarkodSeri);
            Add(command.Parameters,"@EskiGelis",x.HamSusamGelisTarihi);Add(command.Parameters,"@Cop",x.CopKg);
            Add(command.Parameters,"@SoymaBas",x.SoymaBaslangici);Add(command.Parameters,"@SoymaBit",x.SoymaBitisi);
            Add(command.Parameters,"@Ekran",x.EkranTonajiKg);Add(command.Parameters,"@Cekilen",x.CekilenTonajKg);
            Add(command.Parameters,"@YeniHavuz",x.HavuzNo?.Trim());Add(command.Parameters,"@Salamura",x.SalamuraDerecesi);Add(command.Parameters,"@Yedek",x.YedekDerecesi);
            Add(command.Parameters,"@Silo1",x.Silo1);Add(command.Parameters,"@Silo2",x.Silo2);
            Add(command.Parameters,"@EskiMensei",x.MenseiId);Add(command.Parameters,"@Urun",x.UrunId);
            Add(command.Parameters,"@Aciklama",x.Aciklama);Add(command.Parameters,"@Personel",personnelId);
            Add(command.Parameters,"@RaporTarihi",ReportWeekStart(x.SoymaBitisi!.Value));
            await command.ExecuteNonQueryAsync();
            await transaction.CommitAsync();
        }
        catch{await transaction.RollbackAsync();throw;}
    }

    public async Task InsertIslamaAsync(IslamaInput x)
    {
        const string sql = """
            INSERT uretim.IslamaSoymaKaydi
              (BarkodSeri,HamSusamGelisTarihi,CopKg,PartiNo,NobetTarihi,IslamaBaslangici,IslamaBitisi,
               SoymaBaslangici,SoymaBitisi,EkranTonajiKg,CekilenTonajKg,Silo1,Silo2,MenseiId,UrunId,Aciklama,PersonelId,RaporTarihi,Olusturan)
            VALUES
              (@Barkod,@Gelis,@Cop,@Parti,@Nobet,@IslamaBas,@IslamaBit,@SoymaBas,@SoymaBit,@Ekran,@Cekilen,
               @Silo1,@Silo2,@Mensei,@Urun,@Aciklama,@Personel,@RaporTarihi,SUSER_SNAME());
            """;
        await ExecuteAsync(sql, p =>
        {
            Add(p,"@Barkod",x.BarkodSeri); Add(p,"@Gelis",x.HamSusamGelisTarihi); Add(p,"@Cop",x.CopKg);
            Add(p,"@Parti",x.PartiNo); Add(p,"@Nobet",x.NobetTarihi); Add(p,"@IslamaBas",x.IslamaBaslangici);
            Add(p,"@IslamaBit",x.IslamaBitisi); Add(p,"@SoymaBas",x.SoymaBaslangici); Add(p,"@SoymaBit",x.SoymaBitisi);
            Add(p,"@Ekran",x.EkranTonajiKg); Add(p,"@Cekilen",x.CekilenTonajKg); Add(p,"@Silo1",x.Silo1); Add(p,"@Silo2",x.Silo2);
            Add(p,"@Mensei",x.MenseiId); Add(p,"@Urun",x.UrunId); Add(p,"@Aciklama",x.Aciklama); Add(p,"@Personel",x.PersonelId);
            Add(p,"@RaporTarihi",ReportWeekStart(x.SoymaBitisi));
        });
    }

    public async Task<string> GetNextBatchNumberAsync(DateTime date)
    {
        const string sql = """
            SELECT COALESCE(MAX(TRY_CONVERT(int,RIGHT(X.PartiNo,2))),0)+1
            FROM
            (
                SELECT PartiNo FROM uretim.IslamaSoymaKaydi
                UNION ALL
                SELECT PartiNo FROM uretim.IslamaSoymaIsAkisi WHERE Asama<3
            ) X
            WHERE LEN(X.PartiNo)=6 AND X.PartiNo LIKE @Prefix+'%';
            """;
        await using var connection=CreateConnection();await connection.OpenAsync();
        await using var command=new SqlCommand(sql,connection);command.Parameters.AddWithValue("@Prefix",ProductionBatch.WeekPrefix(date));
        var sequence=Convert.ToInt32(await command.ExecuteScalarAsync());
        return ProductionBatch.Format(date,sequence);
    }

    public async Task<List<KavurmaListItem>> GetKavurmaAsync(int take = 100, RecordFilter? filter = null,int? personnelId=null)
    {
        const string sql = """
            SELECT TOP (@Take) K.KavurmaKaydiId,K.Tarih,K.PartiNo,K.NetTonajKg,P.AdSoyad,K.KepekKg,K.TavaSayisi,U.Ad,
                   K.KavurmaSicakligi,K.NisastaKg
            FROM uretim.KavurmaKaydi K LEFT JOIN tanim.Personel P ON P.PersonelId=K.PersonelId
            LEFT JOIN tanim.Urun U ON U.UrunId=K.UrunId
            WHERE (@Like IS NULL OR K.PartiNo LIKE @Like OR P.AdSoyad LIKE @Like OR U.Ad LIKE @Like)
              AND (@PersonnelId IS NULL OR K.PersonelId=@PersonnelId)
              AND (@From IS NULL OR K.Tarih >= @From) AND (@To IS NULL OR K.Tarih <= @To)
            ORDER BY K.KavurmaKaydiId DESC;
            """;
        var result = new List<KavurmaListItem>();
        await using var c=CreateConnection(); await c.OpenAsync(); await using var cmd=new SqlCommand(sql,c); cmd.Parameters.AddWithValue("@Take",take); AddFilterParameters(cmd,filter);Add(cmd.Parameters,"@PersonnelId",personnelId);
        await using var r=await cmd.ExecuteReaderAsync(); while(await r.ReadAsync()) result.Add(new(r.GetInt64(0),D<DateTime>(r,1),S(r,2),r.GetDecimal(3),S(r,4),D<decimal>(r,5),D<int>(r,6),S(r,7),D<decimal>(r,8),D<decimal>(r,9)));
        return result;
    }

    public Task InsertKavurmaAsync(KavurmaInput x) => ExecuteAsync("""
        INSERT uretim.KavurmaKaydi
          (Tarih,PartiNo,EkranTonajiKg,NetTonajKg,KavurmaSicakligi,NisastaKg,PersonelId,KepekKg,TavaSayisi,ArizaliTavaSayisi,CikanSorteksAltiKg,
           EklenenSorteksAltiKg,MenseiId,UrunId,OrtalamaVerimOrani,VerimOrani,Aciklama,Olusturan)
        VALUES(@Tarih,@Parti,@Ekran,@Net,@Sicaklik,@Nisasta,@Personel,@Kepek,@Tava,@Arizali,@Cikan,@Eklenen,@Mensei,@Urun,@OrtVerim,@Verim,@Aciklama,SUSER_SNAME());
        """, p => { Add(p,"@Tarih",x.Tarih); Add(p,"@Parti",x.PartiNo); Add(p,"@Ekran",x.EkranTonajiKg); Add(p,"@Net",x.NetTonajKg); Add(p,"@Sicaklik",x.KavurmaSicakligi); Add(p,"@Nisasta",x.NisastaKg); Add(p,"@Personel",x.PersonelId); Add(p,"@Kepek",x.KepekKg); Add(p,"@Tava",x.TavaSayisi); Add(p,"@Arizali",x.ArizaliTavaSayisi); Add(p,"@Cikan",x.CikanSorteksAltiKg); Add(p,"@Eklenen",x.EklenenSorteksAltiKg); Add(p,"@Mensei",x.MenseiId); Add(p,"@Urun",x.UrunId); Add(p,"@OrtVerim",x.OrtalamaVerimOrani); Add(p,"@Verim",x.VerimOrani); Add(p,"@Aciklama",x.Aciklama); });

    public async Task<List<PaketlemeListItem>> GetPaketlemeAsync(int take=100, RecordFilter? filter=null,int? personnelId=null)
    {
        const string sql="""SELECT TOP (@Take) P.PaketlemeKaydiId,P.Tarih,P.PartiNo,P.AmbalajAgirligiKg*P.Adet,P.AmbalajAgirligiKg,P.Adet,P.FireKg,U.Ad,PE.AdSoyad FROM uretim.PaketlemeKaydi P LEFT JOIN tanim.Urun U ON U.UrunId=P.UrunId LEFT JOIN tanim.Personel PE ON PE.PersonelId=P.PersonelId WHERE (@Like IS NULL OR P.PartiNo LIKE @Like OR U.Ad LIKE @Like OR PE.AdSoyad LIKE @Like) AND (@PersonnelId IS NULL OR P.PersonelId=@PersonnelId) AND (@From IS NULL OR P.Tarih>=@From) AND (@To IS NULL OR P.Tarih<=@To) ORDER BY P.PaketlemeKaydiId DESC;""";
        var list=new List<PaketlemeListItem>(); await using var c=CreateConnection(); await c.OpenAsync(); await using var cmd=new SqlCommand(sql,c);cmd.Parameters.AddWithValue("@Take",take);AddFilterParameters(cmd,filter);Add(cmd.Parameters,"@PersonnelId",personnelId);await using var r=await cmd.ExecuteReaderAsync();while(await r.ReadAsync())list.Add(new(r.GetInt64(0),D<DateTime>(r,1),S(r,2),r.GetDecimal(3),r.GetDecimal(4),r.GetInt32(5),D<decimal>(r,6),S(r,7),S(r,8)));return list;
    }

    public Task InsertPaketlemeAsync(PaketlemeInput x)=>ExecuteAsync("""INSERT uretim.PaketlemeKaydi(Tarih,PartiNo,AmbalajAgirligiKg,Adet,CikanSorteksAltiKg,FireKg,MenseiId,UrunId,SorteksAltiOrani,PersonelId,Aciklama,VerimOrani,Olusturan) VALUES(@Tarih,@Parti,@Agirlik,@Adet,@SorteksKg,@Fire,@Mensei,@Urun,@SorteksOran,@Personel,@Aciklama,@Verim,SUSER_SNAME());""",p=>{Add(p,"@Tarih",x.Tarih);Add(p,"@Parti",x.PartiNo);Add(p,"@Agirlik",x.AmbalajAgirligiKg);Add(p,"@Adet",x.Adet);Add(p,"@SorteksKg",x.CikanSorteksAltiKg);Add(p,"@Fire",x.FireKg);Add(p,"@Mensei",x.MenseiId);Add(p,"@Urun",x.UrunId);Add(p,"@SorteksOran",x.SorteksAltiOrani);Add(p,"@Personel",x.PersonelId);Add(p,"@Aciklama",x.Aciklama);Add(p,"@Verim",x.VerimOrani);});

    public async Task<List<DolumListItem>> GetDolumAsync(int take=100, RecordFilter? filter=null,int? personnelId=null)
    {
        const string sql="""SELECT TOP (@Take) D.DolumKaydiId,D.Tarih,NULL,CONCAT(D.AmbalajCinsi,' ',FORMAT(D.AmbalajKg,'0.###'),' kg'),D.PaketlemeMiktariKg,D.PaketlemeAdedi,D.FireKg,M.Ad,D.Tank FROM uretim.DolumKaydi D LEFT JOIN tanim.Mensei M ON M.MenseiId=D.MenseiId WHERE (@Like IS NULL OR D.AmbalajCinsi LIKE @Like OR D.Personel LIKE @Like OR D.Tank LIKE @Like OR M.Ad LIKE @Like) AND (@PersonnelId IS NULL OR D.PersonelId=@PersonnelId) AND (@From IS NULL OR D.Tarih>=@From) AND (@To IS NULL OR D.Tarih<=@To) ORDER BY D.DolumKaydiId DESC;""";
        var list=new List<DolumListItem>();await using var c=CreateConnection();await c.OpenAsync();await using var cmd=new SqlCommand(sql,c);cmd.Parameters.AddWithValue("@Take",take);AddFilterParameters(cmd,filter);Add(cmd.Parameters,"@PersonnelId",personnelId);await using var r=await cmd.ExecuteReaderAsync();while(await r.ReadAsync())list.Add(new(r.GetInt64(0),D<DateTime>(r,1),S(r,2),r.GetString(3),r.GetDecimal(4),r.GetInt32(5),D<decimal>(r,6),S(r,7),S(r,8)));return list;
    }

    public Task InsertDolumAsync(DolumInput x)=>ExecuteAsync("""
        INSERT uretim.DolumKaydi(Tarih,PartiNo,AmbalajId,AmbalajCinsi,AmbalajKg,PaketlemeAdedi,FireKg,UrunId,MenseiId,Tank,Personel,PersonelSayisi,Aciklama,PersonelId,Olusturan)
        SELECT @Tarih,NULL,A.AmbalajId,A.Cins,A.AgirlikKg,@Adet,@Fire,NULL,@Mensei,@Tank,COALESCE(P.AdSoyad,@Personel),@PersonelSayisi,@Aciklama,@PersonelId,SUSER_SNAME()
        FROM tanim.Ambalaj A LEFT JOIN tanim.Personel P ON P.PersonelId=@PersonelId WHERE A.AmbalajId=@AmbalajId;
        """,p=>{Add(p,"@Tarih",x.Tarih);Add(p,"@AmbalajId",x.AmbalajId);Add(p,"@Adet",x.PaketlemeAdedi);Add(p,"@Fire",x.FireKg);Add(p,"@Mensei",x.MenseiId);Add(p,"@Tank",x.Tank?.Trim());Add(p,"@Personel",x.Personel);Add(p,"@PersonelId",x.PersonelId);Add(p,"@PersonelSayisi",x.PersonelSayisi);Add(p,"@Aciklama",x.Aciklama);});

    public async Task<List<KepekListItem>> GetKepekAsync(int take=100, RecordFilter? filter=null)
    {
        const string sql="""SELECT TOP (@Take) KepekKaydiId,Tarih,PartiNo,PaketlemeMiktariKg,UrunCinsi,HamSusamaOrani FROM uretim.KepekKaydi WHERE (@Like IS NULL OR PartiNo LIKE @Like OR UrunCinsi LIKE @Like OR Aciklama LIKE @Like) AND (@From IS NULL OR Tarih>=@From) AND (@To IS NULL OR Tarih<=@To) ORDER BY KepekKaydiId DESC;""";
        var list=new List<KepekListItem>();await using var c=CreateConnection();await c.OpenAsync();await using var cmd=new SqlCommand(sql,c);cmd.Parameters.AddWithValue("@Take",take);AddFilterParameters(cmd,filter);await using var r=await cmd.ExecuteReaderAsync();while(await r.ReadAsync())list.Add(new(r.GetInt64(0),D<DateTime>(r,1),S(r,2),r.GetDecimal(3),S(r,4),D<decimal>(r,5)));return list;
    }

    public Task InsertKepekAsync(KepekInput x)=>ExecuteAsync("""INSERT uretim.KepekKaydi(Tarih,PartiNo,PaketlemeMiktariKg,UrunCinsi,PersonelSayisi,HamSusamaOrani,Aciklama,PersonelId,Olusturan) VALUES(@Tarih,@Parti,@Miktar,@Urun,@PersonelSayisi,@Oran,@Aciklama,@Personel,SUSER_SNAME());""",p=>{Add(p,"@Tarih",x.Tarih);Add(p,"@Parti",x.PartiNo);Add(p,"@Miktar",x.PaketlemeMiktariKg);Add(p,"@Urun",x.UrunCinsi);Add(p,"@PersonelSayisi",x.PersonelSayisi);Add(p,"@Oran",x.HamSusamaOrani);Add(p,"@Aciklama",x.Aciklama);Add(p,"@Personel",x.PersonelId);});

    public async Task<IslamaInput?> GetIslamaInputAsync(long id,int? personnelId=null)
    {
        const string sql="""SELECT PartiNo,BarkodSeri,HamSusamGelisTarihi,CopKg,NobetTarihi,IslamaBaslangici,IslamaBitisi,SoymaBaslangici,SoymaBitisi,EkranTonajiKg,CekilenTonajKg,Silo1,Silo2,MenseiId,UrunId,Aciklama,PersonelId FROM uretim.IslamaSoymaKaydi WHERE IslamaSoymaKaydiId=@Id AND (@PersonnelId IS NULL OR PersonelId=@PersonnelId);""";
        await using var c=CreateConnection();await c.OpenAsync();await using var cmd=new SqlCommand(sql,c);cmd.Parameters.AddWithValue("@Id",id);Add(cmd.Parameters,"@PersonnelId",personnelId);await using var r=await cmd.ExecuteReaderAsync();if(!await r.ReadAsync())return null;
        return new(){PartiNo=r.GetString(0),BarkodSeri=S(r,1),HamSusamGelisTarihi=D<DateTime>(r,2),CopKg=D<decimal>(r,3),NobetTarihi=D<DateTime>(r,4),IslamaBaslangici=D<DateTime>(r,5),IslamaBitisi=D<DateTime>(r,6),SoymaBaslangici=r.GetDateTime(7),SoymaBitisi=r.GetDateTime(8),EkranTonajiKg=D<decimal>(r,9),CekilenTonajKg=r.GetDecimal(10),Silo1=S(r,11),Silo2=S(r,12),MenseiId=r.GetInt32(13),UrunId=r.GetInt32(14),Aciklama=S(r,15),PersonelId=D<int>(r,16)};
    }

    public Task UpdateIslamaAsync(long id,IslamaInput x)=>ExecuteAsync("""
        UPDATE uretim.IslamaSoymaKaydi SET BarkodSeri=@Barkod,HamSusamGelisTarihi=@Gelis,CopKg=@Cop,PartiNo=@Parti,NobetTarihi=@Nobet,RaporTarihi=@RaporTarihi,IslamaBaslangici=@IslamaBas,IslamaBitisi=@IslamaBit,SoymaBaslangici=@SoymaBas,SoymaBitisi=@SoymaBit,EkranTonajiKg=@Ekran,CekilenTonajKg=@Cekilen,Silo1=@Silo1,Silo2=@Silo2,SiloId=NULL,MenseiId=@Mensei,UrunId=@Urun,Aciklama=@Aciklama,PersonelId=@Personel WHERE IslamaSoymaKaydiId=@Id;
        """,p=>{Add(p,"@Id",id);Add(p,"@Barkod",x.BarkodSeri);Add(p,"@Gelis",x.HamSusamGelisTarihi);Add(p,"@Cop",x.CopKg);Add(p,"@Parti",x.PartiNo);Add(p,"@Nobet",x.NobetTarihi);Add(p,"@RaporTarihi",ReportWeekStart(x.SoymaBitisi));Add(p,"@IslamaBas",x.IslamaBaslangici);Add(p,"@IslamaBit",x.IslamaBitisi);Add(p,"@SoymaBas",x.SoymaBaslangici);Add(p,"@SoymaBit",x.SoymaBitisi);Add(p,"@Ekran",x.EkranTonajiKg);Add(p,"@Cekilen",x.CekilenTonajKg);Add(p,"@Silo1",x.Silo1);Add(p,"@Silo2",x.Silo2);Add(p,"@Mensei",x.MenseiId);Add(p,"@Urun",x.UrunId);Add(p,"@Aciklama",x.Aciklama);Add(p,"@Personel",x.PersonelId);});

    public async Task<KavurmaInput?> GetKavurmaInputAsync(long id,int? personnelId=null)
    {
        const string sql="""SELECT Tarih,PartiNo,EkranTonajiKg,NetTonajKg,PersonelId,KepekKg,TavaSayisi,ArizaliTavaSayisi,CikanSorteksAltiKg,EklenenSorteksAltiKg,MenseiId,UrunId,OrtalamaVerimOrani,VerimOrani,Aciklama,KavurmaSicakligi,NisastaKg FROM uretim.KavurmaKaydi WHERE KavurmaKaydiId=@Id AND (@PersonnelId IS NULL OR PersonelId=@PersonnelId);""";
        await using var c=CreateConnection();await c.OpenAsync();await using var cmd=new SqlCommand(sql,c);cmd.Parameters.AddWithValue("@Id",id);Add(cmd.Parameters,"@PersonnelId",personnelId);await using var r=await cmd.ExecuteReaderAsync();if(!await r.ReadAsync())return null;
        return new(){Tarih=D<DateTime>(r,0),PartiNo=S(r,1),EkranTonajiKg=D<decimal>(r,2),NetTonajKg=r.GetDecimal(3),PersonelId=D<int>(r,4),KepekKg=D<decimal>(r,5),TavaSayisi=D<int>(r,6),ArizaliTavaSayisi=D<int>(r,7),CikanSorteksAltiKg=D<decimal>(r,8),EklenenSorteksAltiKg=D<decimal>(r,9),MenseiId=D<int>(r,10),UrunId=D<int>(r,11),OrtalamaVerimOrani=D<decimal>(r,12),VerimOrani=D<decimal>(r,13),Aciklama=S(r,14),KavurmaSicakligi=D<decimal>(r,15),NisastaKg=D<decimal>(r,16)};
    }

    public Task UpdateKavurmaAsync(long id,KavurmaInput x)=>ExecuteAsync("""
        UPDATE uretim.KavurmaKaydi SET Tarih=@Tarih,PartiNo=@Parti,EkranTonajiKg=@Ekran,NetTonajKg=@Net,KavurmaSicakligi=@Sicaklik,NisastaKg=@Nisasta,PersonelId=@Personel,KepekKg=@Kepek,TavaSayisi=@Tava,ArizaliTavaSayisi=@Arizali,CikanSorteksAltiKg=@Cikan,EklenenSorteksAltiKg=@Eklenen,MenseiId=@Mensei,UrunId=@Urun,OrtalamaVerimOrani=@OrtVerim,VerimOrani=@Verim,Aciklama=@Aciklama WHERE KavurmaKaydiId=@Id;
        IF OBJECT_ID(N'uretim.ExcelAktarimDetayi',N'U') IS NOT NULL
            UPDATE uretim.ExcelAktarimDetayi
            SET BekleyenIslem=CASE WHEN COALESCE(@Kepek,0)>0 THEN 'Guncelle' ELSE 'Sil' END
            WHERE TabloAdi='KavurmaKepek' AND KayitId=@Id;
        """,p=>{Add(p,"@Id",id);Add(p,"@Tarih",x.Tarih);Add(p,"@Parti",x.PartiNo);Add(p,"@Ekran",x.EkranTonajiKg);Add(p,"@Net",x.NetTonajKg);Add(p,"@Sicaklik",x.KavurmaSicakligi);Add(p,"@Nisasta",x.NisastaKg);Add(p,"@Personel",x.PersonelId);Add(p,"@Kepek",x.KepekKg);Add(p,"@Tava",x.TavaSayisi);Add(p,"@Arizali",x.ArizaliTavaSayisi);Add(p,"@Cikan",x.CikanSorteksAltiKg);Add(p,"@Eklenen",x.EklenenSorteksAltiKg);Add(p,"@Mensei",x.MenseiId);Add(p,"@Urun",x.UrunId);Add(p,"@OrtVerim",x.OrtalamaVerimOrani);Add(p,"@Verim",x.VerimOrani);Add(p,"@Aciklama",x.Aciklama);});

    public async Task<PaketlemeInput?> GetPaketlemeInputAsync(long id,int? personnelId=null)
    {
        const string sql="""SELECT Tarih,PartiNo,AmbalajAgirligiKg,Adet,CikanSorteksAltiKg,FireKg,MenseiId,UrunId,SorteksAltiOrani,PersonelId,Aciklama,VerimOrani FROM uretim.PaketlemeKaydi WHERE PaketlemeKaydiId=@Id AND (@PersonnelId IS NULL OR PersonelId=@PersonnelId);""";
        await using var c=CreateConnection();await c.OpenAsync();await using var cmd=new SqlCommand(sql,c);cmd.Parameters.AddWithValue("@Id",id);Add(cmd.Parameters,"@PersonnelId",personnelId);await using var r=await cmd.ExecuteReaderAsync();if(!await r.ReadAsync())return null;
        return new(){Tarih=D<DateTime>(r,0),PartiNo=S(r,1),AmbalajAgirligiKg=r.GetDecimal(2),Adet=r.GetInt32(3),CikanSorteksAltiKg=D<decimal>(r,4),FireKg=D<decimal>(r,5),MenseiId=D<int>(r,6),UrunId=D<int>(r,7),SorteksAltiOrani=D<decimal>(r,8),PersonelId=D<int>(r,9),Aciklama=S(r,10),VerimOrani=D<decimal>(r,11)};
    }

    public Task UpdatePaketlemeAsync(long id,PaketlemeInput x)=>ExecuteAsync("""UPDATE uretim.PaketlemeKaydi SET Tarih=@Tarih,PartiNo=@Parti,AmbalajAgirligiKg=@Agirlik,Adet=@Adet,CikanSorteksAltiKg=@SorteksKg,FireKg=@Fire,MenseiId=@Mensei,UrunId=@Urun,SorteksAltiOrani=@SorteksOran,PersonelId=@Personel,Aciklama=@Aciklama,VerimOrani=@Verim WHERE PaketlemeKaydiId=@Id;""",p=>{Add(p,"@Id",id);Add(p,"@Tarih",x.Tarih);Add(p,"@Parti",x.PartiNo);Add(p,"@Agirlik",x.AmbalajAgirligiKg);Add(p,"@Adet",x.Adet);Add(p,"@SorteksKg",x.CikanSorteksAltiKg);Add(p,"@Fire",x.FireKg);Add(p,"@Mensei",x.MenseiId);Add(p,"@Urun",x.UrunId);Add(p,"@SorteksOran",x.SorteksAltiOrani);Add(p,"@Personel",x.PersonelId);Add(p,"@Aciklama",x.Aciklama);Add(p,"@Verim",x.VerimOrani);});

    public async Task<DolumInput?> GetDolumInputAsync(long id,int? personnelId=null)
    {
        const string sql="""SELECT Tarih,AmbalajId,PaketlemeAdedi,FireKg,MenseiId,Tank,Personel,PersonelSayisi,Aciklama,PersonelId FROM uretim.DolumKaydi WHERE DolumKaydiId=@Id AND (@PersonnelId IS NULL OR PersonelId=@PersonnelId);""";
        await using var c=CreateConnection();await c.OpenAsync();await using var cmd=new SqlCommand(sql,c);cmd.Parameters.AddWithValue("@Id",id);Add(cmd.Parameters,"@PersonnelId",personnelId);await using var r=await cmd.ExecuteReaderAsync();if(!await r.ReadAsync())return null;
        return new(){Tarih=D<DateTime>(r,0),AmbalajId=r.GetInt32(1),PaketlemeAdedi=r.GetInt32(2),FireKg=D<decimal>(r,3),MenseiId=D<int>(r,4),Tank=S(r,5),Personel=S(r,6),PersonelSayisi=D<int>(r,7),Aciklama=S(r,8),PersonelId=D<int>(r,9)};
    }

    public Task UpdateDolumAsync(long id,DolumInput x)=>ExecuteAsync("""
        UPDATE D SET D.Tarih=@Tarih,D.PartiNo=NULL,D.AmbalajId=A.AmbalajId,D.AmbalajCinsi=A.Cins,D.AmbalajKg=A.AgirlikKg,D.PaketlemeAdedi=@Adet,D.FireKg=@Fire,D.UrunId=NULL,D.MenseiId=@Mensei,D.Tank=@Tank,D.Personel=COALESCE(P.AdSoyad,@Personel),D.PersonelSayisi=@PersonelSayisi,D.Aciklama=@Aciklama,D.PersonelId=@PersonelId
        FROM uretim.DolumKaydi D JOIN tanim.Ambalaj A ON A.AmbalajId=@AmbalajId LEFT JOIN tanim.Personel P ON P.PersonelId=@PersonelId WHERE D.DolumKaydiId=@Id;
        """,p=>{Add(p,"@Id",id);Add(p,"@Tarih",x.Tarih);Add(p,"@AmbalajId",x.AmbalajId);Add(p,"@Adet",x.PaketlemeAdedi);Add(p,"@Fire",x.FireKg);Add(p,"@Mensei",x.MenseiId);Add(p,"@Tank",x.Tank?.Trim());Add(p,"@Personel",x.Personel);Add(p,"@PersonelId",x.PersonelId);Add(p,"@PersonelSayisi",x.PersonelSayisi);Add(p,"@Aciklama",x.Aciklama);});

    public async Task<KepekInput?> GetKepekInputAsync(long id)
    {
        const string sql="""SELECT Tarih,PartiNo,PaketlemeMiktariKg,UrunCinsi,PersonelSayisi,HamSusamaOrani,Aciklama,PersonelId FROM uretim.KepekKaydi WHERE KepekKaydiId=@Id;""";
        await using var c=CreateConnection();await c.OpenAsync();await using var cmd=new SqlCommand(sql,c);cmd.Parameters.AddWithValue("@Id",id);await using var r=await cmd.ExecuteReaderAsync();if(!await r.ReadAsync())return null;
        return new(){Tarih=D<DateTime>(r,0),PartiNo=S(r,1),PaketlemeMiktariKg=r.GetDecimal(2),UrunCinsi=S(r,3),PersonelSayisi=D<int>(r,4),HamSusamaOrani=D<decimal>(r,5),Aciklama=S(r,6),PersonelId=D<int>(r,7)};
    }

    public Task UpdateKepekAsync(long id,KepekInput x)=>ExecuteAsync("""UPDATE uretim.KepekKaydi SET Tarih=@Tarih,PartiNo=@Parti,PaketlemeMiktariKg=@Miktar,UrunCinsi=@Urun,PersonelSayisi=@PersonelSayisi,HamSusamaOrani=@Oran,Aciklama=@Aciklama,PersonelId=@Personel WHERE KepekKaydiId=@Id;""",p=>{Add(p,"@Id",id);Add(p,"@Tarih",x.Tarih);Add(p,"@Parti",x.PartiNo);Add(p,"@Miktar",x.PaketlemeMiktariKg);Add(p,"@Urun",x.UrunCinsi);Add(p,"@PersonelSayisi",x.PersonelSayisi);Add(p,"@Oran",x.HamSusamaOrani);Add(p,"@Aciklama",x.Aciklama);Add(p,"@Personel",x.PersonelId);});

    public Task DeleteProductionRecordAsync(string type,long id)
    {
        var (table,key)=type switch
        {
            "Islama" => ("uretim.IslamaSoymaKaydi","IslamaSoymaKaydiId"),
            "Kavurma" => ("uretim.KavurmaKaydi","KavurmaKaydiId"),
            "Paketleme" => ("uretim.PaketlemeKaydi","PaketlemeKaydiId"),
            "Dolum" => ("uretim.DolumKaydi","DolumKaydiId"),
            "Kepek" => ("uretim.KepekKaydi","KepekKaydiId"),
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };
        return ExecuteAsync($"""
            IF OBJECT_ID(N'uretim.ExcelAktarimDetayi',N'U') IS NOT NULL
                UPDATE uretim.ExcelAktarimDetayi SET BekleyenIslem='Sil'
                WHERE KayitId=@Id AND (TabloAdi=@Type OR (@Type='Kavurma' AND TabloAdi='KavurmaKepek'));
            DELETE {table} WHERE {key}=@Id;
            """,p=>{Add(p,"@Id",id);Add(p,"@Type",type);});
    }

    public Task MarkExcelUpdateAsync(string type,long id)=>ExecuteAsync("""
        IF OBJECT_ID(N'uretim.ExcelAktarimDetayi',N'U') IS NOT NULL
            UPDATE uretim.ExcelAktarimDetayi SET BekleyenIslem='Guncelle' WHERE TabloAdi=@Type AND KayitId=@Id;
        """,p=>{Add(p,"@Type",type);Add(p,"@Id",id);});

    public async Task<Dictionary<string,List<LookupItem>>> GetDefinitionsAsync()
    {
        var products=GetUrunlerAsync();var origins=GetMenseilerAsync();var personnel=GetPersonellerAsync();var packages=GetAmbalajlarAsync();
        await Task.WhenAll(products,origins,personnel,packages);
        return new(){["Ürünler"]=products.Result,["Menşeiler"]=origins.Result,["Personeller"]=personnel.Result,["Ambalajlar"]=packages.Result};
    }

    public Task AddDefinitionAsync(string type,string name,decimal? weight,int? taskNumber=null)
    {
        var (sql,nameParam)=type switch
        {
            "urun" => ("IF EXISTS(SELECT 1 FROM tanim.Urun WHERE Ad=@Name) UPDATE tanim.Urun SET Aktif=1 WHERE Ad=@Name; ELSE INSERT tanim.Urun(Ad) VALUES(@Name);","@Name"),
            "mensei" => ("IF EXISTS(SELECT 1 FROM tanim.Mensei WHERE Ad=@Name) UPDATE tanim.Mensei SET Aktif=1 WHERE Ad=@Name; ELSE INSERT tanim.Mensei(Ad) VALUES(@Name);","@Name"),
            "personel" => ("IF EXISTS(SELECT 1 FROM tanim.Personel WHERE AdSoyad=@Name) UPDATE tanim.Personel SET Aktif=1 WHERE AdSoyad=@Name; ELSE INSERT tanim.Personel(AdSoyad,GorevNo) VALUES(@Name,@Task);","@Name"),
            "silo" => ("IF NOT EXISTS(SELECT 1 FROM tanim.Silo WHERE Kod=@Name) INSERT tanim.Silo(Kod) VALUES(@Name);","@Name"),
            "ambalaj" => ("IF EXISTS(SELECT 1 FROM tanim.Ambalaj WHERE Cins=@Name AND AgirlikKg=@Weight) UPDATE tanim.Ambalaj SET Aktif=1 WHERE Cins=@Name AND AgirlikKg=@Weight; ELSE INSERT tanim.Ambalaj(Cins,AgirlikKg) VALUES(@Name,@Weight);","@Name"),
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };
        return ExecuteAsync(sql,p=>{Add(p,nameParam,name);Add(p,"@Weight",weight);Add(p,"@Task",taskNumber);});
    }

    public Task DeactivateDefinitionAsync(string type,int id)
    {
        var (table,key)=type switch
        {
            "urun" => ("tanim.Urun","UrunId"),
            "mensei" => ("tanim.Mensei","MenseiId"),
            "ambalaj" => ("tanim.Ambalaj","AmbalajId"),
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };
        return ExecuteAsync($"UPDATE {table} SET Aktif=0 WHERE {key}=@Id;",p=>Add(p,"@Id",id));
    }

    public Task UpdateDefinitionAsync(string type,int id,string name,decimal? weight)
    {
        var sql=type switch
        {
            "urun" => "UPDATE tanim.Urun SET Ad=@Name WHERE UrunId=@Id;",
            "mensei" => "UPDATE tanim.Mensei SET Ad=@Name WHERE MenseiId=@Id;",
            "ambalaj" => "UPDATE tanim.Ambalaj SET Cins=@Name,AgirlikKg=@Weight WHERE AmbalajId=@Id;",
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };
        return ExecuteAsync(sql,p=>{Add(p,"@Id",id);Add(p,"@Name",name.Trim());Add(p,"@Weight",weight);});
    }

    private async Task ExecuteAsync(string sql,Action<SqlParameterCollection> parameters)
    {
        await using var connection=CreateConnection();await connection.OpenAsync();await using var command=new SqlCommand(sql,connection);parameters(command.Parameters);await command.ExecuteNonQueryAsync();
    }

    private static void Add(SqlParameterCollection parameters,string name,object? value)
    {
        if(parameters.Contains(name)) return;
        parameters.AddWithValue(name,value ?? DBNull.Value);
    }

    private static void AddFilterParameters(SqlCommand command, RecordFilter? filter)
    {
        var like = command.Parameters.Add("@Like", SqlDbType.NVarChar, 202);
        like.Value = string.IsNullOrWhiteSpace(filter?.Search) ? DBNull.Value : $"%{filter.Search.Trim()}%";
        var from = command.Parameters.Add("@From", SqlDbType.Date);
        from.Value = filter?.From?.Date ?? (object)DBNull.Value;
        var to = command.Parameters.Add("@To", SqlDbType.Date);
        to.Value = filter?.To?.Date ?? (object)DBNull.Value;
    }

    private static DateTime? ReportWeekStart(DateTime? date)
    {
        if (!date.HasValue) return null;
        var year = ISOWeek.GetYear(date.Value);
        var week = ISOWeek.GetWeekOfYear(date.Value);
        return ISOWeek.ToDateTime(year, week, DayOfWeek.Monday);
    }

    private static T? D<T>(SqlDataReader reader,int ordinal) where T:struct => reader.IsDBNull(ordinal)?null:reader.GetFieldValue<T>(ordinal);
    private static string? S(SqlDataReader reader,int ordinal) => reader.IsDBNull(ordinal)?null:reader.GetString(ordinal);
}
