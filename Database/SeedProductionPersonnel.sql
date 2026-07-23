/*
    Kaynak: PERSONEL İSİM LİSTESİ İNCİ.xlsx
    Kapsam: Bölümü ÜRETİM olan ve uygulamadaki beş üretim görevinden
    birine doğrudan eşleşen personeller.

    Görev eşleştirmesi:
      1 = Islama · Soyma   (Excel görevi: SOYMA)
      2 = Kavurma          (Excel görevi: KAVURMA)
      3 = Paketleme        (Excel görevi: PAKETLEME)
      4 = Dolum            (Excel görevi: DOLUM)
      5 = Kepek            (Excel görevi: KURUTMA)

    DİLARA KAHRAMAN, talep doğrultusunda Dolum grubuna ve personel
    ekleme sorgusuna dahil edilmemiştir.

    ÜRETİM ŞEFİ, ÜRETİM MÜDÜRÜ, TEMİZLİK, MEYDANCI, DEĞİRMEN ve BAKIM
    görevleri mevcut beş işlem ekranından birine karşılık gelmediği için
    sorguya dahil edilmemiştir.

    Sorgu tekrar çalıştırılabilir:
      - Personel yoksa ekler.
      - Aynı adla personel varsa görevini günceller ve aktif hale getirir.
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'tanim.Personel', N'U') IS NULL
        THROW 50001, N'tanim.Personel tablosu bulunamadı.', 1;

    IF OBJECT_ID(N'tanim.Gorev', N'U') IS NULL
        THROW 50002, N'tanim.Gorev tablosu bulunamadı. Önce uygulamayı bir kez çalıştırın.', 1;

    IF EXISTS
    (
        SELECT V.GorevNo
        FROM (VALUES (CONVERT(tinyint,1)),(2),(3),(4),(5)) V(GorevNo)
        WHERE NOT EXISTS (SELECT 1 FROM tanim.Gorev G WHERE G.GorevNo=V.GorevNo AND G.Aktif=1)
    )
        THROW 50003, N'1-5 arasındaki üretim görevlerinden biri tanim.Gorev tablosunda eksik veya pasif.', 1;

    DECLARE @Personeller TABLE
    (
        AdSoyad nvarchar(100) NOT NULL PRIMARY KEY,
        GorevNo tinyint NOT NULL
    );

    INSERT INTO @Personeller (AdSoyad, GorevNo)
    VALUES
        -- 1 · Islama / Soyma
        (N'FATİH ŞİMŞEK',       1),
        (N'HARUN SUNA',          1),
        (N'SERHAT GEZGİNCİ',     1),

        -- 2 · Kavurma
        (N'HALİD HAC AHMET',     2),
        (N'MEHMET DOĞAN',        2),
        (N'YAHYA GÜNDOĞAN',      2),

        -- 3 · Paketleme
        (N'SELİM BOYRAZ',        3),

        -- 4 · Dolum (DİLARA KAHRAMAN hariç)
        (N'BİLGEHAN YİĞİTER',    4),
        (N'ENES ALKIN',           4),
        (N'MUSTAFA CEREN',        4),
        (N'NİSA NUR ÖZDİN',       4),
        (N'NURCAN POLAT',         4),
        (N'NURSEL KARAKOÇ',       4),

        -- 5 · Kepek / Kurutma
        (N'EMİRHAN DOĞAN',        5),
        (N'LEVENT YAZGAN',        5),
        (N'SEZAİ COŞKUN',         5);

    IF EXISTS (SELECT 1 FROM @Personeller WHERE AdSoyad=N'DİLARA KAHRAMAN')
        THROW 50004, N'DİLARA KAHRAMAN bu sorguyla eklenemez.', 1;

    MERGE tanim.Personel WITH (HOLDLOCK) AS Hedef
    USING @Personeller AS Kaynak
       ON LTRIM(RTRIM(Hedef.AdSoyad))=Kaynak.AdSoyad
    WHEN MATCHED THEN
        UPDATE SET Hedef.GorevNo=Kaynak.GorevNo, Hedef.Aktif=1
    WHEN NOT MATCHED BY TARGET THEN
        INSERT (AdSoyad, GorevNo, Aktif)
        VALUES (Kaynak.AdSoyad, Kaynak.GorevNo, 1)
    OUTPUT
        $action AS Islem,
        inserted.PersonelId,
        inserted.AdSoyad,
        inserted.GorevNo;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
