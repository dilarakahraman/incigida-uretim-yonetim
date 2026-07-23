using System.Data;
using System.Globalization;
using System.Text;
using Microsoft.Data.SqlClient;
using SusamUretim.Web.Models;

namespace SusamUretim.Web.Data;

public sealed class SusamRepository(IConfiguration configuration)
{
    private readonly string _connectionString = configuration.GetConnectionString("SusamUretim")
        ?? throw new InvalidOperationException("SusamUretim connection string bulunamadı.");

    private SqlConnection CreateConnection() => new(_connectionString);

    public async Task EnsureAccessSchemaAsync()
    {
        await ExecuteAsync("""
            IF COL_LENGTH(N'uretim.IslamaSoymaKaydi',N'Silo1') IS NULL
                EXEC(N'ALTER TABLE uretim.IslamaSoymaKaydi ADD Silo1 varchar(2) NULL;');
            IF COL_LENGTH(N'uretim.IslamaSoymaKaydi',N'Silo2') IS NULL
                EXEC(N'ALTER TABLE uretim.IslamaSoymaKaydi ADD Silo2 varchar(2) NULL;');
            """, _ => { });

        await ExecuteAsync("""
            IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name=N'IX_IslamaSoymaKaydi_SoymaBitisi' AND object_id=OBJECT_ID(N'uretim.IslamaSoymaKaydi'))
                CREATE INDEX IX_IslamaSoymaKaydi_SoymaBitisi ON uretim.IslamaSoymaKaydi(SoymaBitisi) INCLUDE(UrunId,MenseiId,CekilenTonajKg,CopKg);
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
                (CONVERT(tinyint,1),'Islama',N'Islama · Soyma','/Islama'),
                (CONVERT(tinyint,2),'Kavurma',N'Kavurma','/Kavurma'),
                (CONVERT(tinyint,3),'Paketleme',N'Paketleme','/Paketleme'),
                (CONVERT(tinyint,4),'Dolum',N'Dolum','/Dolum'),
                (CONVERT(tinyint,5),'Kepek',N'Kepek','/Kepek')) AS source(GorevNo,Kod,Ad,Sayfa)
            ON target.GorevNo=source.GorevNo
            WHEN MATCHED THEN UPDATE SET Kod=source.Kod,Ad=source.Ad,Sayfa=source.Sayfa,Aktif=1
            WHEN NOT MATCHED THEN INSERT(GorevNo,Kod,Ad,Sayfa) VALUES(source.GorevNo,source.Kod,source.Ad,source.Sayfa);
            """, _ => { });

        await ExecuteAsync("""
            IF COL_LENGTH('tanim.Personel','GorevNo') IS NULL ALTER TABLE tanim.Personel ADD GorevNo tinyint NULL;
            IF COL_LENGTH('uretim.IslamaSoymaKaydi','PersonelId') IS NULL ALTER TABLE uretim.IslamaSoymaKaydi ADD PersonelId int NULL;
            IF COL_LENGTH('uretim.DolumKaydi','PersonelId') IS NULL ALTER TABLE uretim.DolumKaydi ADD PersonelId int NULL;
            IF COL_LENGTH('uretim.KepekKaydi','PersonelId') IS NULL ALTER TABLE uretim.KepekKaydi ADD PersonelId int NULL;
            IF COL_LENGTH('uretim.PaketlemeKaydi','FireKg') IS NULL ALTER TABLE uretim.PaketlemeKaydi ADD FireKg decimal(18,3) NULL;
            IF COL_LENGTH('uretim.DolumKaydi','FireKg') IS NULL ALTER TABLE uretim.DolumKaydi ADD FireKg decimal(18,3) NULL;
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
            IF NOT EXISTS(SELECT 1 FROM sys.foreign_keys WHERE name='FK_DolumKaydi_Personel')
                ALTER TABLE uretim.DolumKaydi WITH CHECK ADD CONSTRAINT FK_DolumKaydi_Personel FOREIGN KEY(PersonelId) REFERENCES tanim.Personel(PersonelId);
            IF NOT EXISTS(SELECT 1 FROM sys.foreign_keys WHERE name='FK_KepekKaydi_Personel')
                ALTER TABLE uretim.KepekKaydi WITH CHECK ADD CONSTRAINT FK_KepekKaydi_Personel FOREIGN KEY(PersonelId) REFERENCES tanim.Personel(PersonelId);
            """, _ => { });
    }

    public async Task SynchronizeExcelCatalogAsync(ExcelDataCatalog catalog)
    {
        await using var connection=CreateConnection();await connection.OpenAsync();
        await using var transaction=(SqlTransaction)await connection.BeginTransactionAsync();
        try
        {
            await SyncDefinitionsAsync(connection,transaction,"tanim.Mensei","MenseiId","Ad",catalog.Origins,
                [("uretim.IslamaSoymaKaydi","MenseiId"),("uretim.KavurmaKaydi","MenseiId"),("uretim.PaketlemeKaydi","MenseiId")]);
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
            SELECT
              (SELECT COUNT(*) FROM uretim.IslamaSoymaKaydi WHERE SoymaBitisi>=@Start AND SoymaBitisi<@End AND (@Product IS NULL OR UrunId=@Product) AND (@Origin IS NULL OR MenseiId=@Origin)),
              (SELECT COUNT(*) FROM uretim.KavurmaKaydi WHERE Tarih>=@Start AND Tarih<@End AND (@Product IS NULL OR UrunId=@Product) AND (@Origin IS NULL OR MenseiId=@Origin)),
              (SELECT COUNT(*) FROM uretim.PaketlemeKaydi WHERE Tarih>=@Start AND Tarih<@End AND (@Product IS NULL OR UrunId=@Product) AND (@Origin IS NULL OR MenseiId=@Origin)),
              (SELECT COUNT(*) FROM uretim.DolumKaydi WHERE Tarih>=@Start AND Tarih<@End AND (@Product IS NULL OR UrunId=@Product) AND @Origin IS NULL),
              (SELECT COUNT(*) FROM uretim.KepekKaydi WHERE Tarih>=@Start AND Tarih<@End AND @Product IS NULL AND @Origin IS NULL),
              (SELECT COALESCE(SUM(CekilenTonajKg),0) FROM uretim.IslamaSoymaKaydi WHERE SoymaBitisi>=@Start AND SoymaBitisi<@End AND (@Product IS NULL OR UrunId=@Product) AND (@Origin IS NULL OR MenseiId=@Origin)),
              (SELECT COALESCE(SUM(NetTonajKg),0) FROM uretim.KavurmaKaydi WHERE Tarih>=@Start AND Tarih<@End AND NetTonajKg>0 AND (@Product IS NULL OR UrunId=@Product) AND (@Origin IS NULL OR MenseiId=@Origin)),
              (SELECT COALESCE(SUM(AmbalajAgirligiKg*Adet),0) FROM uretim.PaketlemeKaydi WHERE Tarih>=@Start AND Tarih<@End AND (@Product IS NULL OR UrunId=@Product) AND (@Origin IS NULL OR MenseiId=@Origin)),
              (SELECT COALESCE(AVG(NULLIF(VerimOrani,0)),AVG(NULLIF(OrtalamaVerimOrani,0)),0) FROM uretim.KavurmaKaydi WHERE Tarih>=@Start AND Tarih<@End AND (@Product IS NULL OR UrunId=@Product) AND (@Origin IS NULL OR MenseiId=@Origin)),
              (SELECT COALESCE(SUM(TavaSayisi),0) FROM uretim.KavurmaKaydi WHERE Tarih>=@Start AND Tarih<@End AND (@Product IS NULL OR UrunId=@Product) AND (@Origin IS NULL OR MenseiId=@Origin)),
              (SELECT COALESCE(SUM(ArizaliTavaSayisi),0) FROM uretim.KavurmaKaydi WHERE Tarih>=@Start AND Tarih<@End AND (@Product IS NULL OR UrunId=@Product) AND (@Origin IS NULL OR MenseiId=@Origin)),
              (SELECT COALESCE(SUM(EklenenSorteksAltiKg),0) FROM uretim.KavurmaKaydi WHERE Tarih>=@Start AND Tarih<@End AND (@Product IS NULL OR UrunId=@Product) AND (@Origin IS NULL OR MenseiId=@Origin)),
              (SELECT COALESCE(SUM(CikanSorteksAltiKg),0) FROM uretim.PaketlemeKaydi WHERE Tarih>=@Start AND Tarih<@End AND (@Product IS NULL OR UrunId=@Product) AND (@Origin IS NULL OR MenseiId=@Origin)),
              (SELECT COALESCE(SUM(CopKg),0) FROM uretim.IslamaSoymaKaydi WHERE SoymaBitisi>=@Start AND SoymaBitisi<@End AND (@Product IS NULL OR UrunId=@Product) AND (@Origin IS NULL OR MenseiId=@Origin)),
              (SELECT COALESCE(SUM(PaketlemeMiktariKg),0) FROM uretim.KepekKaydi WHERE Tarih>=@Start AND Tarih<@End AND @Product IS NULL AND @Origin IS NULL),
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

            SELECT COALESCE(U.Ad,N'Tanımsız'),
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
              FROM uretim.KavurmaKaydi WHERE Tarih>=@Start AND Tarih<@End AND (@Product IS NULL OR UrunId=@Product) AND (@Origin IS NULL OR MenseiId=@Origin)
              UNION ALL
              SELECT UrunId,0,0,0,AmbalajAgirligiKg*Adet,COALESCE(CikanSorteksAltiKg,0),0
              FROM uretim.PaketlemeKaydi WHERE Tarih>=@Start AND Tarih<@End AND (@Product IS NULL OR UrunId=@Product) AND (@Origin IS NULL OR MenseiId=@Origin)
            ) X
            LEFT JOIN tanim.Urun U ON U.UrunId=X.UrunId
            GROUP BY COALESCE(U.Ad,N'Tanımsız')
            HAVING SUM(X.GirdiKg)<>0 OR SUM(X.KavrulmusKg)<>0 OR SUM(X.PaketlenenKg)<>0
            ORDER BY COALESCE(U.Ad,N'Tanımsız');

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

            SELECT COALESCE(P.AdSoyad,N'Tanımsız'),SUM(CASE WHEN K.NetTonajKg>0 THEN K.NetTonajKg ELSE 0 END),
                   COALESCE(SUM(K.TavaSayisi),0)
            FROM uretim.KavurmaKaydi K LEFT JOIN tanim.Personel P ON P.PersonelId=K.PersonelId
            WHERE K.Tarih>=@Start AND K.Tarih<@End AND (@Product IS NULL OR K.UrunId=@Product) AND (@Origin IS NULL OR K.MenseiId=@Origin)
            GROUP BY COALESCE(P.AdSoyad,N'Tanımsız')
            ORDER BY SUM(CASE WHEN K.NetTonajKg>0 THEN K.NetTonajKg ELSE 0 END) DESC;
            """;
        await using var connection = CreateConnection();
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection); Add(command.Parameters,"@From",from?.Date);Add(command.Parameters,"@To",to?.Date);Add(command.Parameters,"@Product",productId);Add(command.Parameters,"@Origin",originId);
        await using var reader = await command.ExecuteReaderAsync();
        await reader.ReadAsync();
        var counts=new[]{reader.GetInt32(0),reader.GetInt32(1),reader.GetInt32(2),reader.GetInt32(3),reader.GetInt32(4)};
        var input=reader.GetDecimal(5);var roast=reader.GetDecimal(6);var output=reader.GetDecimal(7);var storedExcelYield=reader.GetDecimal(8);var pans=reader.GetInt32(9);var broken=reader.GetInt32(10);
        var addedSortex=reader.GetDecimal(11);var packagingSortex=reader.GetDecimal(12);
        var waste=reader.GetDecimal(13);var bran=reader.GetDecimal(14);var peelingMinutes=reader.GetInt32(15);
        var fillingKg=reader.GetDecimal(17);var fillingUnits=reader.GetInt32(18);var fillingPersonnel=reader.GetInt32(19);
        var excelRoastOutput=roast-addedSortex;
        var netInput=Math.Max(0,input-waste);
        var grossYield=netInput>0?excelRoastOutput/netInput*100:(storedExcelYield<=2?storedExcelYield*100:storedExcelYield);
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

    public async Task<List<IslamaListItem>> GetIslamaAsync(int take = 100, RecordFilter? filter = null)
    {
        const string sql = """
            SELECT TOP (@Take) I.IslamaSoymaKaydiId,I.PartiNo,I.SoymaBaslangici,I.SoymaSuresiDakika,
                   I.CekilenTonajKg,I.CopKg,M.Ad,U.Ad,COALESCE(NULLIF(CONCAT(I.Silo1,' ',I.Silo2),' '),S.Kod)
            FROM uretim.IslamaSoymaKaydi I
            JOIN tanim.Mensei M ON M.MenseiId=I.MenseiId
            JOIN tanim.Urun U ON U.UrunId=I.UrunId
            LEFT JOIN tanim.Silo S ON S.SiloId=I.SiloId
            WHERE (@Like IS NULL OR I.PartiNo LIKE @Like OR I.BarkodSeri LIKE @Like OR M.Ad LIKE @Like OR U.Ad LIKE @Like OR S.Kod LIKE @Like OR I.Silo1 LIKE @Like OR I.Silo2 LIKE @Like)
              AND (@From IS NULL OR I.SoymaBaslangici >= @From)
              AND (@To IS NULL OR I.SoymaBaslangici < DATEADD(DAY,1,@To))
            ORDER BY I.SoymaBaslangici DESC,I.IslamaSoymaKaydiId DESC;
            """;
        var result = new List<IslamaListItem>();
        await using var connection = CreateConnection(); await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection); command.Parameters.AddWithValue("@Take", take); AddFilterParameters(command, filter);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            result.Add(new(reader.GetInt64(0),reader.GetString(1),reader.GetDateTime(2),reader.GetInt32(3),reader.GetDecimal(4),
                reader.IsDBNull(5)?null:reader.GetDecimal(5),reader.GetString(6),reader.GetString(7),reader.IsDBNull(8)?null:reader.GetString(8)));
        return result;
    }

    public async Task InsertIslamaAsync(IslamaInput x)
    {
        const string sql = """
            INSERT uretim.IslamaSoymaKaydi
              (BarkodSeri,HamSusamGelisTarihi,CopKg,PartiNo,NobetTarihi,IslamaBaslangici,IslamaBitisi,
               SoymaBaslangici,SoymaBitisi,EkranTonajiKg,CekilenTonajKg,Silo1,Silo2,MenseiId,UrunId,Aciklama,PersonelId,Olusturan)
            VALUES
              (@Barkod,@Gelis,@Cop,@Parti,@Nobet,@IslamaBas,@IslamaBit,@SoymaBas,@SoymaBit,@Ekran,@Cekilen,
               @Silo1,@Silo2,@Mensei,@Urun,@Aciklama,@Personel,SUSER_SNAME());
            """;
        await ExecuteAsync(sql, p =>
        {
            Add(p,"@Barkod",x.BarkodSeri); Add(p,"@Gelis",x.HamSusamGelisTarihi); Add(p,"@Cop",x.CopKg);
            Add(p,"@Parti",x.PartiNo); Add(p,"@Nobet",x.NobetTarihi); Add(p,"@IslamaBas",x.IslamaBaslangici);
            Add(p,"@IslamaBit",x.IslamaBitisi); Add(p,"@SoymaBas",x.SoymaBaslangici); Add(p,"@SoymaBit",x.SoymaBitisi);
            Add(p,"@Ekran",x.EkranTonajiKg); Add(p,"@Cekilen",x.CekilenTonajKg); Add(p,"@Silo1",x.Silo1); Add(p,"@Silo2",x.Silo2);
            Add(p,"@Mensei",x.MenseiId); Add(p,"@Urun",x.UrunId); Add(p,"@Aciklama",x.Aciklama); Add(p,"@Personel",x.PersonelId);
        });
    }

    public async Task<string> GetNextBatchNumberAsync(DateTime date)
    {
        const string sql = """
            SELECT COALESCE(MAX(TRY_CONVERT(int,RIGHT(PartiNo,2))),0)+1
            FROM uretim.IslamaSoymaKaydi
            WHERE LEN(PartiNo)=6 AND PartiNo LIKE @Prefix+'%';
            """;
        await using var connection=CreateConnection();await connection.OpenAsync();
        await using var command=new SqlCommand(sql,connection);command.Parameters.AddWithValue("@Prefix",ProductionBatch.WeekPrefix(date));
        var sequence=Convert.ToInt32(await command.ExecuteScalarAsync());
        return ProductionBatch.Format(date,sequence);
    }

    public async Task<List<KavurmaListItem>> GetKavurmaAsync(int take = 100, RecordFilter? filter = null)
    {
        const string sql = """
            SELECT TOP (@Take) K.KavurmaKaydiId,K.Tarih,K.PartiNo,K.NetTonajKg,P.AdSoyad,K.TavaSayisi,U.Ad
            FROM uretim.KavurmaKaydi K LEFT JOIN tanim.Personel P ON P.PersonelId=K.PersonelId
            LEFT JOIN tanim.Urun U ON U.UrunId=K.UrunId
            WHERE (@Like IS NULL OR K.PartiNo LIKE @Like OR P.AdSoyad LIKE @Like OR U.Ad LIKE @Like)
              AND (@From IS NULL OR K.Tarih >= @From) AND (@To IS NULL OR K.Tarih <= @To)
            ORDER BY K.KavurmaKaydiId DESC;
            """;
        var result = new List<KavurmaListItem>();
        await using var c=CreateConnection(); await c.OpenAsync(); await using var cmd=new SqlCommand(sql,c); cmd.Parameters.AddWithValue("@Take",take); AddFilterParameters(cmd,filter);
        await using var r=await cmd.ExecuteReaderAsync(); while(await r.ReadAsync()) result.Add(new(r.GetInt64(0),D<DateTime>(r,1),S(r,2),r.GetDecimal(3),S(r,4),D<int>(r,5),S(r,6)));
        return result;
    }

    public Task InsertKavurmaAsync(KavurmaInput x) => ExecuteAsync("""
        INSERT uretim.KavurmaKaydi
          (Tarih,PartiNo,EkranTonajiKg,NetTonajKg,PersonelId,TavaSayisi,ArizaliTavaSayisi,CikanSorteksAltiKg,
           EklenenSorteksAltiKg,MenseiId,UrunId,OrtalamaVerimOrani,VerimOrani,Aciklama,Olusturan)
        VALUES(@Tarih,@Parti,@Ekran,@Net,@Personel,@Tava,@Arizali,@Cikan,@Eklenen,@Mensei,@Urun,@OrtVerim,@Verim,@Aciklama,SUSER_SNAME());
        """, p => { Add(p,"@Tarih",x.Tarih); Add(p,"@Parti",x.PartiNo); Add(p,"@Ekran",x.EkranTonajiKg); Add(p,"@Net",x.NetTonajKg); Add(p,"@Personel",x.PersonelId); Add(p,"@Tava",x.TavaSayisi); Add(p,"@Arizali",x.ArizaliTavaSayisi); Add(p,"@Cikan",x.CikanSorteksAltiKg); Add(p,"@Eklenen",x.EklenenSorteksAltiKg); Add(p,"@Mensei",x.MenseiId); Add(p,"@Urun",x.UrunId); Add(p,"@OrtVerim",x.OrtalamaVerimOrani); Add(p,"@Verim",x.VerimOrani); Add(p,"@Aciklama",x.Aciklama); });

    public async Task<List<PaketlemeListItem>> GetPaketlemeAsync(int take=100, RecordFilter? filter=null)
    {
        const string sql="""SELECT TOP (@Take) P.PaketlemeKaydiId,P.Tarih,P.PartiNo,P.AmbalajAgirligiKg*P.Adet,P.AmbalajAgirligiKg,P.Adet,P.FireKg,U.Ad,PE.AdSoyad FROM uretim.PaketlemeKaydi P LEFT JOIN tanim.Urun U ON U.UrunId=P.UrunId LEFT JOIN tanim.Personel PE ON PE.PersonelId=P.PersonelId WHERE (@Like IS NULL OR P.PartiNo LIKE @Like OR U.Ad LIKE @Like OR PE.AdSoyad LIKE @Like) AND (@From IS NULL OR P.Tarih>=@From) AND (@To IS NULL OR P.Tarih<=@To) ORDER BY P.PaketlemeKaydiId DESC;""";
        var list=new List<PaketlemeListItem>(); await using var c=CreateConnection(); await c.OpenAsync(); await using var cmd=new SqlCommand(sql,c);cmd.Parameters.AddWithValue("@Take",take);AddFilterParameters(cmd,filter);await using var r=await cmd.ExecuteReaderAsync();while(await r.ReadAsync())list.Add(new(r.GetInt64(0),D<DateTime>(r,1),S(r,2),r.GetDecimal(3),r.GetDecimal(4),r.GetInt32(5),D<decimal>(r,6),S(r,7),S(r,8)));return list;
    }

    public Task InsertPaketlemeAsync(PaketlemeInput x)=>ExecuteAsync("""INSERT uretim.PaketlemeKaydi(Tarih,PartiNo,AmbalajAgirligiKg,Adet,CikanSorteksAltiKg,FireKg,MenseiId,UrunId,SorteksAltiOrani,PersonelId,Aciklama,VerimOrani,Olusturan) VALUES(@Tarih,@Parti,@Agirlik,@Adet,@SorteksKg,@Fire,@Mensei,@Urun,@SorteksOran,@Personel,@Aciklama,@Verim,SUSER_SNAME());""",p=>{Add(p,"@Tarih",x.Tarih);Add(p,"@Parti",x.PartiNo);Add(p,"@Agirlik",x.AmbalajAgirligiKg);Add(p,"@Adet",x.Adet);Add(p,"@SorteksKg",x.CikanSorteksAltiKg);Add(p,"@Fire",x.FireKg);Add(p,"@Mensei",x.MenseiId);Add(p,"@Urun",x.UrunId);Add(p,"@SorteksOran",x.SorteksAltiOrani);Add(p,"@Personel",x.PersonelId);Add(p,"@Aciklama",x.Aciklama);Add(p,"@Verim",x.VerimOrani);});

    public async Task<List<DolumListItem>> GetDolumAsync(int take=100, RecordFilter? filter=null)
    {
        const string sql="""SELECT TOP (@Take) D.DolumKaydiId,D.Tarih,NULL,CONCAT(D.AmbalajCinsi,' ',FORMAT(D.AmbalajKg,'0.###'),' kg'),D.PaketlemeMiktariKg,D.PaketlemeAdedi,D.FireKg,U.Ad FROM uretim.DolumKaydi D LEFT JOIN tanim.Urun U ON U.UrunId=D.UrunId WHERE (@Like IS NULL OR D.AmbalajCinsi LIKE @Like OR D.Personel LIKE @Like OR U.Ad LIKE @Like) AND (@From IS NULL OR D.Tarih>=@From) AND (@To IS NULL OR D.Tarih<=@To) ORDER BY D.DolumKaydiId DESC;""";
        var list=new List<DolumListItem>();await using var c=CreateConnection();await c.OpenAsync();await using var cmd=new SqlCommand(sql,c);cmd.Parameters.AddWithValue("@Take",take);AddFilterParameters(cmd,filter);await using var r=await cmd.ExecuteReaderAsync();while(await r.ReadAsync())list.Add(new(r.GetInt64(0),D<DateTime>(r,1),S(r,2),r.GetString(3),r.GetDecimal(4),r.GetInt32(5),D<decimal>(r,6),S(r,7)));return list;
    }

    public Task InsertDolumAsync(DolumInput x)=>ExecuteAsync("""
        INSERT uretim.DolumKaydi(Tarih,PartiNo,AmbalajId,AmbalajCinsi,AmbalajKg,PaketlemeAdedi,FireKg,UrunId,Personel,PersonelSayisi,Aciklama,PersonelId,Olusturan)
        SELECT @Tarih,NULL,A.AmbalajId,A.Cins,A.AgirlikKg,@Adet,@Fire,@Urun,COALESCE(P.AdSoyad,@Personel),@PersonelSayisi,@Aciklama,@PersonelId,SUSER_SNAME()
        FROM tanim.Ambalaj A LEFT JOIN tanim.Personel P ON P.PersonelId=@PersonelId WHERE A.AmbalajId=@AmbalajId;
        """,p=>{Add(p,"@Tarih",x.Tarih);Add(p,"@AmbalajId",x.AmbalajId);Add(p,"@Adet",x.PaketlemeAdedi);Add(p,"@Fire",x.FireKg);Add(p,"@Urun",x.UrunId);Add(p,"@Personel",x.Personel);Add(p,"@PersonelId",x.PersonelId);Add(p,"@PersonelSayisi",x.PersonelSayisi);Add(p,"@Aciklama",x.Aciklama);});

    public async Task<List<KepekListItem>> GetKepekAsync(int take=100, RecordFilter? filter=null)
    {
        const string sql="""SELECT TOP (@Take) KepekKaydiId,Tarih,PartiNo,PaketlemeMiktariKg,UrunCinsi,HamSusamaOrani FROM uretim.KepekKaydi WHERE (@Like IS NULL OR PartiNo LIKE @Like OR UrunCinsi LIKE @Like OR Aciklama LIKE @Like) AND (@From IS NULL OR Tarih>=@From) AND (@To IS NULL OR Tarih<=@To) ORDER BY KepekKaydiId DESC;""";
        var list=new List<KepekListItem>();await using var c=CreateConnection();await c.OpenAsync();await using var cmd=new SqlCommand(sql,c);cmd.Parameters.AddWithValue("@Take",take);AddFilterParameters(cmd,filter);await using var r=await cmd.ExecuteReaderAsync();while(await r.ReadAsync())list.Add(new(r.GetInt64(0),D<DateTime>(r,1),S(r,2),r.GetDecimal(3),S(r,4),D<decimal>(r,5)));return list;
    }

    public Task InsertKepekAsync(KepekInput x)=>ExecuteAsync("""INSERT uretim.KepekKaydi(Tarih,PartiNo,PaketlemeMiktariKg,UrunCinsi,PersonelSayisi,HamSusamaOrani,Aciklama,PersonelId,Olusturan) VALUES(@Tarih,@Parti,@Miktar,@Urun,@PersonelSayisi,@Oran,@Aciklama,@Personel,SUSER_SNAME());""",p=>{Add(p,"@Tarih",x.Tarih);Add(p,"@Parti",x.PartiNo);Add(p,"@Miktar",x.PaketlemeMiktariKg);Add(p,"@Urun",x.UrunCinsi);Add(p,"@PersonelSayisi",x.PersonelSayisi);Add(p,"@Oran",x.HamSusamaOrani);Add(p,"@Aciklama",x.Aciklama);Add(p,"@Personel",x.PersonelId);});

    public async Task<IslamaInput?> GetIslamaInputAsync(long id)
    {
        const string sql="""SELECT PartiNo,BarkodSeri,HamSusamGelisTarihi,CopKg,NobetTarihi,IslamaBaslangici,IslamaBitisi,SoymaBaslangici,SoymaBitisi,EkranTonajiKg,CekilenTonajKg,Silo1,Silo2,MenseiId,UrunId,Aciklama,PersonelId FROM uretim.IslamaSoymaKaydi WHERE IslamaSoymaKaydiId=@Id;""";
        await using var c=CreateConnection();await c.OpenAsync();await using var cmd=new SqlCommand(sql,c);cmd.Parameters.AddWithValue("@Id",id);await using var r=await cmd.ExecuteReaderAsync();if(!await r.ReadAsync())return null;
        return new(){PartiNo=r.GetString(0),BarkodSeri=S(r,1),HamSusamGelisTarihi=D<DateTime>(r,2),CopKg=D<decimal>(r,3),NobetTarihi=D<DateTime>(r,4),IslamaBaslangici=D<DateTime>(r,5),IslamaBitisi=D<DateTime>(r,6),SoymaBaslangici=r.GetDateTime(7),SoymaBitisi=r.GetDateTime(8),EkranTonajiKg=D<decimal>(r,9),CekilenTonajKg=r.GetDecimal(10),Silo1=S(r,11),Silo2=S(r,12),MenseiId=r.GetInt32(13),UrunId=r.GetInt32(14),Aciklama=S(r,15),PersonelId=D<int>(r,16)};
    }

    public Task UpdateIslamaAsync(long id,IslamaInput x)=>ExecuteAsync("""
        UPDATE uretim.IslamaSoymaKaydi SET BarkodSeri=@Barkod,HamSusamGelisTarihi=@Gelis,CopKg=@Cop,PartiNo=@Parti,NobetTarihi=@Nobet,IslamaBaslangici=@IslamaBas,IslamaBitisi=@IslamaBit,SoymaBaslangici=@SoymaBas,SoymaBitisi=@SoymaBit,EkranTonajiKg=@Ekran,CekilenTonajKg=@Cekilen,Silo1=@Silo1,Silo2=@Silo2,SiloId=NULL,MenseiId=@Mensei,UrunId=@Urun,Aciklama=@Aciklama,PersonelId=@Personel WHERE IslamaSoymaKaydiId=@Id;
        """,p=>{Add(p,"@Id",id);Add(p,"@Barkod",x.BarkodSeri);Add(p,"@Gelis",x.HamSusamGelisTarihi);Add(p,"@Cop",x.CopKg);Add(p,"@Parti",x.PartiNo);Add(p,"@Nobet",x.NobetTarihi);Add(p,"@IslamaBas",x.IslamaBaslangici);Add(p,"@IslamaBit",x.IslamaBitisi);Add(p,"@SoymaBas",x.SoymaBaslangici);Add(p,"@SoymaBit",x.SoymaBitisi);Add(p,"@Ekran",x.EkranTonajiKg);Add(p,"@Cekilen",x.CekilenTonajKg);Add(p,"@Silo1",x.Silo1);Add(p,"@Silo2",x.Silo2);Add(p,"@Mensei",x.MenseiId);Add(p,"@Urun",x.UrunId);Add(p,"@Aciklama",x.Aciklama);Add(p,"@Personel",x.PersonelId);});

    public async Task<KavurmaInput?> GetKavurmaInputAsync(long id)
    {
        const string sql="""SELECT Tarih,PartiNo,EkranTonajiKg,NetTonajKg,PersonelId,TavaSayisi,ArizaliTavaSayisi,CikanSorteksAltiKg,EklenenSorteksAltiKg,MenseiId,UrunId,OrtalamaVerimOrani,VerimOrani,Aciklama FROM uretim.KavurmaKaydi WHERE KavurmaKaydiId=@Id;""";
        await using var c=CreateConnection();await c.OpenAsync();await using var cmd=new SqlCommand(sql,c);cmd.Parameters.AddWithValue("@Id",id);await using var r=await cmd.ExecuteReaderAsync();if(!await r.ReadAsync())return null;
        return new(){Tarih=D<DateTime>(r,0),PartiNo=S(r,1),EkranTonajiKg=D<decimal>(r,2),NetTonajKg=r.GetDecimal(3),PersonelId=D<int>(r,4),TavaSayisi=D<int>(r,5),ArizaliTavaSayisi=D<int>(r,6),CikanSorteksAltiKg=D<decimal>(r,7),EklenenSorteksAltiKg=D<decimal>(r,8),MenseiId=D<int>(r,9),UrunId=D<int>(r,10),OrtalamaVerimOrani=D<decimal>(r,11),VerimOrani=D<decimal>(r,12),Aciklama=S(r,13)};
    }

    public Task UpdateKavurmaAsync(long id,KavurmaInput x)=>ExecuteAsync("""UPDATE uretim.KavurmaKaydi SET Tarih=@Tarih,PartiNo=@Parti,EkranTonajiKg=@Ekran,NetTonajKg=@Net,PersonelId=@Personel,TavaSayisi=@Tava,ArizaliTavaSayisi=@Arizali,CikanSorteksAltiKg=@Cikan,EklenenSorteksAltiKg=@Eklenen,MenseiId=@Mensei,UrunId=@Urun,OrtalamaVerimOrani=@OrtVerim,VerimOrani=@Verim,Aciklama=@Aciklama WHERE KavurmaKaydiId=@Id;""",p=>{Add(p,"@Id",id);Add(p,"@Tarih",x.Tarih);Add(p,"@Parti",x.PartiNo);Add(p,"@Ekran",x.EkranTonajiKg);Add(p,"@Net",x.NetTonajKg);Add(p,"@Personel",x.PersonelId);Add(p,"@Tava",x.TavaSayisi);Add(p,"@Arizali",x.ArizaliTavaSayisi);Add(p,"@Cikan",x.CikanSorteksAltiKg);Add(p,"@Eklenen",x.EklenenSorteksAltiKg);Add(p,"@Mensei",x.MenseiId);Add(p,"@Urun",x.UrunId);Add(p,"@OrtVerim",x.OrtalamaVerimOrani);Add(p,"@Verim",x.VerimOrani);Add(p,"@Aciklama",x.Aciklama);});

    public async Task<PaketlemeInput?> GetPaketlemeInputAsync(long id)
    {
        const string sql="""SELECT Tarih,PartiNo,AmbalajAgirligiKg,Adet,CikanSorteksAltiKg,FireKg,MenseiId,UrunId,SorteksAltiOrani,PersonelId,Aciklama,VerimOrani FROM uretim.PaketlemeKaydi WHERE PaketlemeKaydiId=@Id;""";
        await using var c=CreateConnection();await c.OpenAsync();await using var cmd=new SqlCommand(sql,c);cmd.Parameters.AddWithValue("@Id",id);await using var r=await cmd.ExecuteReaderAsync();if(!await r.ReadAsync())return null;
        return new(){Tarih=D<DateTime>(r,0),PartiNo=S(r,1),AmbalajAgirligiKg=r.GetDecimal(2),Adet=r.GetInt32(3),CikanSorteksAltiKg=D<decimal>(r,4),FireKg=D<decimal>(r,5),MenseiId=D<int>(r,6),UrunId=D<int>(r,7),SorteksAltiOrani=D<decimal>(r,8),PersonelId=D<int>(r,9),Aciklama=S(r,10),VerimOrani=D<decimal>(r,11)};
    }

    public Task UpdatePaketlemeAsync(long id,PaketlemeInput x)=>ExecuteAsync("""UPDATE uretim.PaketlemeKaydi SET Tarih=@Tarih,PartiNo=@Parti,AmbalajAgirligiKg=@Agirlik,Adet=@Adet,CikanSorteksAltiKg=@SorteksKg,FireKg=@Fire,MenseiId=@Mensei,UrunId=@Urun,SorteksAltiOrani=@SorteksOran,PersonelId=@Personel,Aciklama=@Aciklama,VerimOrani=@Verim WHERE PaketlemeKaydiId=@Id;""",p=>{Add(p,"@Id",id);Add(p,"@Tarih",x.Tarih);Add(p,"@Parti",x.PartiNo);Add(p,"@Agirlik",x.AmbalajAgirligiKg);Add(p,"@Adet",x.Adet);Add(p,"@SorteksKg",x.CikanSorteksAltiKg);Add(p,"@Fire",x.FireKg);Add(p,"@Mensei",x.MenseiId);Add(p,"@Urun",x.UrunId);Add(p,"@SorteksOran",x.SorteksAltiOrani);Add(p,"@Personel",x.PersonelId);Add(p,"@Aciklama",x.Aciklama);Add(p,"@Verim",x.VerimOrani);});

    public async Task<DolumInput?> GetDolumInputAsync(long id)
    {
        const string sql="""SELECT Tarih,AmbalajId,PaketlemeAdedi,FireKg,UrunId,Personel,PersonelSayisi,Aciklama,PersonelId FROM uretim.DolumKaydi WHERE DolumKaydiId=@Id;""";
        await using var c=CreateConnection();await c.OpenAsync();await using var cmd=new SqlCommand(sql,c);cmd.Parameters.AddWithValue("@Id",id);await using var r=await cmd.ExecuteReaderAsync();if(!await r.ReadAsync())return null;
        return new(){Tarih=D<DateTime>(r,0),AmbalajId=r.GetInt32(1),PaketlemeAdedi=r.GetInt32(2),FireKg=D<decimal>(r,3),UrunId=D<int>(r,4),Personel=S(r,5),PersonelSayisi=D<int>(r,6),Aciklama=S(r,7),PersonelId=D<int>(r,8)};
    }

    public Task UpdateDolumAsync(long id,DolumInput x)=>ExecuteAsync("""
        UPDATE D SET D.Tarih=@Tarih,D.PartiNo=NULL,D.AmbalajId=A.AmbalajId,D.AmbalajCinsi=A.Cins,D.AmbalajKg=A.AgirlikKg,D.PaketlemeAdedi=@Adet,D.FireKg=@Fire,D.UrunId=@Urun,D.Personel=COALESCE(P.AdSoyad,@Personel),D.PersonelSayisi=@PersonelSayisi,D.Aciklama=@Aciklama,D.PersonelId=@PersonelId
        FROM uretim.DolumKaydi D JOIN tanim.Ambalaj A ON A.AmbalajId=@AmbalajId LEFT JOIN tanim.Personel P ON P.PersonelId=@PersonelId WHERE D.DolumKaydiId=@Id;
        """,p=>{Add(p,"@Id",id);Add(p,"@Tarih",x.Tarih);Add(p,"@AmbalajId",x.AmbalajId);Add(p,"@Adet",x.PaketlemeAdedi);Add(p,"@Fire",x.FireKg);Add(p,"@Urun",x.UrunId);Add(p,"@Personel",x.Personel);Add(p,"@PersonelId",x.PersonelId);Add(p,"@PersonelSayisi",x.PersonelSayisi);Add(p,"@Aciklama",x.Aciklama);});

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
                UPDATE uretim.ExcelAktarimDetayi SET BekleyenIslem='Sil' WHERE TabloAdi=@Type AND KayitId=@Id;
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
            "urun" => ("IF NOT EXISTS(SELECT 1 FROM tanim.Urun WHERE Ad=@Name) INSERT tanim.Urun(Ad) VALUES(@Name);","@Name"),
            "mensei" => ("IF NOT EXISTS(SELECT 1 FROM tanim.Mensei WHERE Ad=@Name) INSERT tanim.Mensei(Ad) VALUES(@Name);","@Name"),
            "personel" => ("IF NOT EXISTS(SELECT 1 FROM tanim.Personel WHERE AdSoyad=@Name) INSERT tanim.Personel(AdSoyad,GorevNo) VALUES(@Name,@Task);","@Name"),
            "silo" => ("IF NOT EXISTS(SELECT 1 FROM tanim.Silo WHERE Kod=@Name) INSERT tanim.Silo(Kod) VALUES(@Name);","@Name"),
            "ambalaj" => ("IF NOT EXISTS(SELECT 1 FROM tanim.Ambalaj WHERE Cins=@Name AND AgirlikKg=@Weight) INSERT tanim.Ambalaj(Cins,AgirlikKg) VALUES(@Name,@Weight);","@Name"),
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };
        return ExecuteAsync(sql,p=>{Add(p,nameParam,name);Add(p,"@Weight",weight);Add(p,"@Task",taskNumber);});
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

    private static T? D<T>(SqlDataReader reader,int ordinal) where T:struct => reader.IsDBNull(ordinal)?null:reader.GetFieldValue<T>(ordinal);
    private static string? S(SqlDataReader reader,int ordinal) => reader.IsDBNull(ordinal)?null:reader.GetString(ordinal);
}
