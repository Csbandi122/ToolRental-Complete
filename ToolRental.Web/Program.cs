using Anthropic.SDK;
using Anthropic.SDK.Messaging;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;
using ToolRental.Core;
using ToolRental.Core.Models;
using ToolRental.Data;

// Utolsó kérdések memóriában (szerver újraindításig megmaradnak)
var recentQuestions = new List<string>();
var recentQuestionsLock = new object();
const int MaxRecentQuestions = 5;

var builder = WebApplication.CreateBuilder(args);

// SQL Server kapcsolat az appsettings.json-ból
builder.Services.AddDbContext<ToolRentalDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlOptions => sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 3,
            maxRetryDelay: TimeSpan.FromSeconds(5),
            errorNumbersToAdd: null)));

var app = builder.Build();

// Statikus fájlok kiszolgálása (CSS, JS, stb.)
app.UseStaticFiles();

// === REPORTING API ===
app.MapGet("/api/reporting", async (ToolRentalDbContext db) =>
{
    var today = DateTime.Today;
    var weekStart = today.AddDays(-(int)today.DayOfWeek + (today.DayOfWeek == DayOfWeek.Sunday ? -6 : 1)); // Hétfő
    var monthStart = new DateTime(today.Year, today.Month, 1);

    // Mai nap
    var todayRevenue = await db.Financials
        .Where(f => f.Date.Date == today && f.EntryType == EntryTypes.Bevetel)
        .SumAsync(f => (decimal?)f.Amount) ?? 0;
    var todayExpense = await db.Financials
        .Where(f => f.Date.Date == today && f.EntryType == EntryTypes.Koltseg)
        .SumAsync(f => (decimal?)f.Amount) ?? 0;

    // Aktuális hét (hétfőtől)
    var weekRevenue = await db.Financials
        .Where(f => f.Date.Date >= weekStart && f.Date.Date <= today && f.EntryType == EntryTypes.Bevetel)
        .SumAsync(f => (decimal?)f.Amount) ?? 0;
    var weekExpense = await db.Financials
        .Where(f => f.Date.Date >= weekStart && f.Date.Date <= today && f.EntryType == EntryTypes.Koltseg)
        .SumAsync(f => (decimal?)f.Amount) ?? 0;

    // Aktuális hónap
    var monthRevenue = await db.Financials
        .Where(f => f.Date >= monthStart && f.EntryType == EntryTypes.Bevetel)
        .SumAsync(f => (decimal?)f.Amount) ?? 0;
    var monthExpense = await db.Financials
        .Where(f => f.Date >= monthStart && f.EntryType == EntryTypes.Koltseg)
        .SumAsync(f => (decimal?)f.Amount) ?? 0;

    // Aktuális év
    var yearStart = new DateTime(today.Year, 1, 1);
    var yearRevenue = await db.Financials
        .Where(f => f.Date >= yearStart && f.EntryType == EntryTypes.Bevetel)
        .SumAsync(f => (decimal?)f.Amount) ?? 0;
    var yearExpense = await db.Financials
        .Where(f => f.Date >= yearStart && f.EntryType == EntryTypes.Koltseg)
        .SumAsync(f => (decimal?)f.Amount) ?? 0;

    // Számlázott / számlázatlan bontás (Rentals.TotalAmount alapján)
    var todayInvoiced = await db.Rentals
        .Where(r => r.RentStart.Date == today && r.Invoice != null && r.Invoice != "" && r.Invoice != "nincs számla")
        .SumAsync(r => (decimal?)r.TotalAmount) ?? 0;
    var todayUninvoiced = await db.Rentals
        .Where(r => r.RentStart.Date == today && (r.Invoice == null || r.Invoice == "" || r.Invoice == "nincs számla"))
        .SumAsync(r => (decimal?)r.TotalAmount) ?? 0;

    var weekInvoiced = await db.Rentals
        .Where(r => r.RentStart.Date >= weekStart && r.RentStart.Date <= today && r.Invoice != null && r.Invoice != "" && r.Invoice != "nincs számla")
        .SumAsync(r => (decimal?)r.TotalAmount) ?? 0;
    var weekUninvoiced = await db.Rentals
        .Where(r => r.RentStart.Date >= weekStart && r.RentStart.Date <= today && (r.Invoice == null || r.Invoice == "" || r.Invoice == "nincs számla"))
        .SumAsync(r => (decimal?)r.TotalAmount) ?? 0;

    var monthInvoiced = await db.Rentals
        .Where(r => r.RentStart >= monthStart && r.Invoice != null && r.Invoice != "" && r.Invoice != "nincs számla")
        .SumAsync(r => (decimal?)r.TotalAmount) ?? 0;
    var monthUninvoiced = await db.Rentals
        .Where(r => r.RentStart >= monthStart && (r.Invoice == null || r.Invoice == "" || r.Invoice == "nincs számla"))
        .SumAsync(r => (decimal?)r.TotalAmount) ?? 0;

    var yearInvoiced = await db.Rentals
        .Where(r => r.RentStart >= yearStart && r.Invoice != null && r.Invoice != "" && r.Invoice != "nincs számla")
        .SumAsync(r => (decimal?)r.TotalAmount) ?? 0;
    var yearUninvoiced = await db.Rentals
        .Where(r => r.RentStart >= yearStart && (r.Invoice == null || r.Invoice == "" || r.Invoice == "nincs számla"))
        .SumAsync(r => (decimal?)r.TotalAmount) ?? 0;

    return Results.Json(new
    {
        generatedAt = DateTime.Now.ToString("yyyy.MM.dd HH:mm:ss"),
        today = new { revenue = todayRevenue, expense = todayExpense, profit = todayRevenue - todayExpense, invoiced = todayInvoiced, uninvoiced = todayUninvoiced },
        week = new { revenue = weekRevenue, expense = weekExpense, profit = weekRevenue - weekExpense, startDate = weekStart.ToString("yyyy.MM.dd"), invoiced = weekInvoiced, uninvoiced = weekUninvoiced },
        month = new { revenue = monthRevenue, expense = monthExpense, profit = monthRevenue - monthExpense, startDate = monthStart.ToString("yyyy.MM.dd"), invoiced = monthInvoiced, uninvoiced = monthUninvoiced },
        year = new { revenue = yearRevenue, expense = yearExpense, profit = yearRevenue - yearExpense, startDate = yearStart.ToString("yyyy.MM.dd"), invoiced = yearInvoiced, uninvoiced = yearUninvoiced }
    });
});

// === NEGYEDÉVES ADÓBECSLÉS API ===
app.MapGet("/api/tax-estimate", async (ToolRentalDbContext db) =>
{
    var year = DateTime.Today.Year;
    var yearStart = new DateTime(year, 1, 1);

    // Negyedév végdátumai
    var quarterEnds = new DateTime[] {
        new(year, 3, 31), new(year, 6, 30), new(year, 9, 30), new(year, 12, 31)
    };

    // Általányadózó EV, 45% költséghányad, heti 36 órát meghaladó munkaviszony
    const decimal threshold = 3_520_000m;
    const decimal incomeRatio = 0.55m; // 100% - 45% költséghányad
    const decimal szjaRate = 0.15m;
    const decimal tbRate = 0.13m;
    const decimal szochoRate = 0.185m;

    var quarters = new object[4];
    decimal prevCumTaxable = 0;

    for (int q = 0; q < 4; q++)
    {
        // Kumulált számlázott bevétel az év elejétől a negyedév végéig
        var cumInvoiced = await db.Rentals
            .Where(r => r.RentStart >= yearStart && r.RentStart.Date <= quarterEnds[q]
                && r.Invoice != null && r.Invoice != "" && r.Invoice != "nincs számla")
            .SumAsync(r => (decimal?)r.TotalAmount) ?? 0;

        // Kumulált adóköteles rész (küszöb feletti bevétel)
        var cumTaxable = Math.Max(0, cumInvoiced - threshold);

        // Erre a negyedévre jutó adóköteles bevétel
        var qTaxable = cumTaxable - prevCumTaxable;

        // Adóalap = adóköteles bevétel × 55% (költséghányad levonása után)
        var qTaxBase = qTaxable * incomeRatio;

        var szja = Math.Round(qTaxBase * szjaRate);
        var tb = Math.Round(qTaxBase * tbRate);
        var szocho = Math.Round(qTaxBase * szochoRate);

        quarters[q] = new
        {
            quarter = q + 1,
            taxableRevenue = Math.Round(qTaxable),
            szja,
            tb,
            szocho,
            total = szja + tb + szocho
        };

        prevCumTaxable = cumTaxable;
    }

    // Összes számlázott bevétel az évben (a küszöbtől való távolsághoz)
    var totalInvoiced = await db.Rentals
        .Where(r => r.RentStart >= yearStart
            && r.Invoice != null && r.Invoice != "" && r.Invoice != "nincs számla")
        .SumAsync(r => (decimal?)r.TotalAmount) ?? 0;

    return Results.Json(new { year, threshold, cumInvoiced = totalInvoiced, quarters });
});

// === ÉVES STATISZTIKA API ===
app.MapGet("/api/stats", async (ToolRentalDbContext db) =>
{
    var yearStart = new DateTime(DateTime.Today.Year, 1, 1);
    var yearEnd = new DateTime(DateTime.Today.Year + 1, 1, 1);

    var bikeTypes = new[] { "Férfi kerékpár", "Női kerékpár", "Férfi e-bike", "Női e-bike", "Utánfutó", "Gyerekbicikli" };

    var totalRentals = await db.Rentals
        .Where(r => r.RentStart >= yearStart && r.RentStart < yearEnd)
        .CountAsync();

    var bikesRented = await db.RentalDevices
        .Where(rd => rd.Rental.RentStart >= yearStart && rd.Rental.RentStart < yearEnd
                  && bikeTypes.Contains(rd.Device.DeviceTypeNavigation!.TypeName))
        .CountAsync();

    var defektCount = await db.Services
        .Where(s => s.ServiceDate >= yearStart && s.ServiceDate < yearEnd && s.ServiceType == "defekt")
        .CountAsync();

    var repairMinutes = await db.Services
        .Where(s => s.ServiceDate >= yearStart && s.ServiceDate < yearEnd)
        .SumAsync(s => s.WorkHours * 60 + s.WorkMinutes);

    return Results.Json(new
    {
        year = DateTime.Today.Year,
        totalRentals,
        bikesRented,
        kmEstimate = bikesRented * 30,
        defektCount,
        repairHours = repairMinutes / 60,
        repairMinutes = repairMinutes % 60
    });
});

// === AI LEKÉRDEZŐ API ===
app.MapPost("/api/ask", async (HttpRequest request, ToolRentalDbContext db, IConfiguration config) =>
{
    // Kérdés kiolvasása
    using var reader = new StreamReader(request.Body);
    var body = await reader.ReadToEndAsync();
    var json = JsonDocument.Parse(body);
    var question = json.RootElement.GetProperty("question").GetString();

    if (string.IsNullOrWhiteSpace(question))
        return Results.BadRequest(new { error = "Kérlek adj meg egy kérdést." });

    // Kérdés mentése az előzményekbe
    lock (recentQuestionsLock)
    {
        recentQuestions.Remove(question); // ha már volt, ne legyen dupla
        recentQuestions.Insert(0, question);
        if (recentQuestions.Count > MaxRecentQuestions)
            recentQuestions.RemoveAt(recentQuestions.Count - 1);
    }

    // API kulcs ellenőrzése
    var apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")
                 ?? config["Anthropic:ApiKey"]
                 ?? "";

    if (string.IsNullOrEmpty(apiKey))
        return Results.Json(new { error = "Nincs beállítva az ANTHROPIC_API_KEY." }, statusCode: 500);

    // Csak olvasási jogú kapcsolat az AI lekérdezésekhez (biztonság)
    var connectionString = config.GetConnectionString("ReadOnlyConnection")
                           ?? config.GetConnectionString("DefaultConnection")
                           ?? "";

    try
    {
        // 1. lépés: Claude generál egy SQL lekérdezést
        var client = new AnthropicClient(apiKey);

        var promptToday = DateTime.Today;
        var schemaPrompt = @"Te egy SQL Server adatbázis lekérdező asszisztens vagy egy KERÉKPÁR-KÖLCSÖNZŐ cég rendszeréhez.
A felhasználó természetes nyelven kérdez magyarul, te pedig EGYETLEN SQL SELECT lekérdezést generálsz.
A válaszod KIZÁRÓLAG a nyers SQL kód legyen — semmi más szöveg, semmi markdown (```), semmi magyarázat!

══════════════════════════════════════
MAI DÁTUM: ##TODAY## (##DAYNAME##), ##YEAR##. év
══════════════════════════════════════

DÁTUMKEZELÉS (nagyon fontos!):
- ""ma"" / ""mai nap"" = '##TODAY##'
- ""tegnap"" = '##YESTERDAY##'
- ""múlt héten"" = az előző hét hétfőjétől vasárnapjáig (számold ki ##TODAY## alapján)
- ""ezen a héten"" / ""héten"" = aktuális hét hétfőjétől ##TODAY##-ig
- ""ebben a hónapban"" / ""hónapban"" = aktuális hónap 1-jétől ##TODAY##-ig
- ""idén"" = '##YEAR##-01-01'-tól ##TODAY##-ig
- ""tavaly"" = '##LASTYEAR##-01-01'-tól '##LASTYEAR##-12-31'-ig
- Ha CSAK hónapot és napot mond (pl. ""május 12""): használd a ##YEAR##. évet. Ha az a dátum még nem jött el ##YEAR##-ben, akkor a ##LASTYEAR##. évet.
- Ha intervallumot mond (pl. ""május 12-14""): mindkét végpont BENNE van, >= és <= operátorokkal.
- Magyar ünnepek: értsd meg és számold ki a ##YEAR##. évi dátumot (pl. Húsvét hétfő, Pünkösd, Karácsony stb.)
- SQL-ben az aktuális dátum/idő: GETDATE()
- Dátumszűrésnél MINDIG: CAST(mező AS DATE) a pontos összehasonlításhoz

ESZKÖZ NÉV SZERINTI KERESÉS (nagyon fontos!):
Ha a felhasználó egy konkrét eszköz nevét vagy márkáját említi (pl. ""Merida"", ""Genesis"", ""Kelly""):
→ MINDIG LIKE '%kulcsszó%' keresést használj a Devices.DeviceName mezőn!
→ Több azonos márkájú eszköz is létezhet — MINDIG az ÖSSZESET add vissza!
→ Példa: ""Merida bicikli"" → WHERE d.DeviceName LIKE '%Merida%'
→ Ez a szabály MINDEN kérdéstípusnál érvényes ahol eszközre szűrünk!

══════════════════════════════════════
ADATBÁZIS SÉMA (SQL Server)
══════════════════════════════════════

Customers (ÜGYFELEK)
  Id, Name, Zipcode, City, Address, Email, IdNumber, Comment
  - Name: teljes név
  - Zipcode + City + Address = lakcím
  - Email: email cím
  - IdNumber: igazolvány szám
  - Comment: megjegyzés
  - NINCS telefon mező a rendszerben!

Devices (ESZKÖZÖK)
  Id, DeviceName, DeviceType[FK→DeviceTypes.Id], Serial, Price, RentPrice, Available, Picture, RentCount, Notes
  - DeviceName: eszköz neve/márkája (pl. ""Kelly's Cliff 90"", ""Merida Crossway 15"")
  - Price: beszerzési ár (Ft)
  - RentPrice: napi bérleti díj (Ft)
  - Available: admin által beállított státusz (NEM jelzi hogy épp ki van-e adva!)
  - RentCount: összesített eddigi kiadási szám

DeviceTypes (ESZKÖZTÍPUSOK)
  Id, TypeName
  Értékek: 'Férfi e-bike', 'Női e-bike', 'Férfi kerékpár', 'Női kerékpár', 'Gyerekbicikli', 'Kerékpár Szállító', 'Gyerekülés', 'Kiegészítő', 'Utánfutó', 'Zár'
  Ha ""bicikli""/""kerékpár""/""bringa"" → ÖSSZES kerékpár típus (Férfi/Női kerékpár, e-bike, Gyerekbicikli, Kerékpár Szállító)
  Ha ""e-bike""/""elektromos"" → csak 'Férfi e-bike' és 'Női e-bike'
  Gyerekülés, Kiegészítő, Utánfutó, Zár = NEM kerékpár!

Rentals (BÉRLÉSEK / BÉRLÉSI JEGYEK)
  Id, TicketNr, CustomerId[FK→Customers.Id], RentStart, RentalDays, PaymentMode, Comment, Contract, Invoice, TotalAmount, ReviewEmailSent, ContractEmailSent, InvoiceEmailSent
  - TicketNr: jegyszám (pl. RNT0042)
  - RentStart: bérlés kezdő dátum+idő
  - RentalDays: bérlés hossza napokban
  - TotalAmount: a bérlés teljes összege (Ft)
  - PaymentMode: fizetési mód (pl. készpénz, kártya)
  - Invoice: számla fájl útvonala VAGY 'nincs számla' szöveg VAGY NULL/üres → lásd SZÁMLÁZÁS részt
  - Bérlés aktív adott napon ha: adott_nap >= CAST(RentStart AS DATE) AND adott_nap < CAST(DATEADD(day, RentalDays, RentStart) AS DATE)

RentalDevices (BÉRLÉS ↔ ESZKÖZ kapcsolótábla, many-to-many)
  Id, RentalId[FK→Rentals.Id], DeviceId[FK→Devices.Id]
  Egy bérléshez több eszköz tartozhat!

Services (SZERVIZ / MUNKALAPOK)
  Id, TicketNr, ServiceType, Description, ServiceDate, CostAmount, WorkHours, WorkMinutes, RescueRequired
  - ServiceType: 'defekt', 'karbantartás', 'javítás', 'upgrade'
  - Description: hiba/munka leírása
  - WorkHours + WorkMinutes: szervíz időtartama (pl. 2ó 30p → WorkHours=2, WorkMinutes=30)
  - Teljes idő percben: (WorkHours * 60 + WorkMinutes). Formázáshoz: CONCAT(WorkHours, 'ó ', WorkMinutes, 'p')
  - RescueRequired: kellett-e helyszíni mentés/kiszállás (bit)
  - ""munkalap"" = Services tábla

ServiceDevices (SZERVIZ ↔ ESZKÖZ kapcsolótábla, many-to-many)
  Id, ServiceId[FK→Services.Id], DeviceId[FK→Devices.Id]

Parts (ALKATRÉSZEK törzsadat)
  Id, Name

ServiceParts (SZERVIZ ↔ ALKATRÉSZ kapcsolat)
  Id, ServiceId[FK→Services.Id], PartId[FK→Parts.Id], Quantity (default 1)
  Egy szervízhez több alkatrész, egy alkatrész több szervízben.

Financials (PÉNZÜGYI TÉTELEK — teljes könyvelés)
  Id, TicketNr, EntryType, SourceType, SourceId, Date, Comment, Amount
  - EntryType: 'bevétel' vagy 'költség'
  - SourceType: 'bérlés', 'szervíz', 'eszköz_vásárlás', 'marketing', 'egyéb', 'kézi', 'alkatrész', 'javítás'
  - Amount: MINDIG pozitív szám (az EntryType dönti el az irányt)

FinancialDevices (PÉNZÜGY ↔ ESZKÖZ kapcsolat)
  Id, FinancialId[FK→Financials.Id], DeviceId[FK→Devices.Id]

Settings → TILOS LEKÉRDEZNI!

SZÁMLÁZÁS LOGIKA:
A Rentals.Invoice mező tartalmazza a számla állapotát:
- SZÁMLÁZATLAN: Invoice IS NULL OR Invoice = '' OR Invoice = 'nincs számla'
- SZÁMLÁZOTT: Invoice IS NOT NULL AND Invoice <> '' AND Invoice <> 'nincs számla'

══════════════════════════════════════
KÉRDÉSTÍPUSOK ÉS SQL MINTÁK
══════════════════════════════════════

--- 1. ""Hány eszközt adtunk ki [napon/héten/időszakban]?"" ---
Adott időszak bérlési jegyei (Rentals.RentStart) → RentalDevices → eszközök COUNT.
MINTA:
  SELECT COUNT(rd.Id) AS KiadottEszkozokSzama
  FROM Rentals r
  JOIN RentalDevices rd ON r.Id = rd.RentalId
  WHERE CAST(r.RentStart AS DATE) = '2026-05-20'

--- 2. ""Kinél van [ma/adott napon] az XXX eszköz?"" ---
Adott napon INDÍTOTT bérlések + eszköz DeviceName LIKE keresés → ügyfél + minden találat.
MINTA:
  SELECT c.Name AS Ugyfel, d.DeviceName AS Eszkoz, r.TicketNr, CAST(r.RentStart AS DATE) AS BerlesKezdete, r.RentalDays
  FROM Rentals r
  JOIN RentalDevices rd ON r.Id = rd.RentalId
  JOIN Devices d ON rd.DeviceId = d.Id
  JOIN Customers c ON r.CustomerId = c.Id
  WHERE d.DeviceName LIKE '%Merida%'
    AND CAST(r.RentStart AS DATE) = '2026-05-20'

--- 3. ""Mi az email címe / lakóhelye / adatai XXX-nek?"" ---
Customers tábla, Name LIKE kereséssel, ÖSSZES elérhető adat visszaadása.
MINTA:
  SELECT Name, Email, Zipcode, City, Address, IdNumber, Comment
  FROM Customers
  WHERE Name LIKE '%Kiss%'

--- 4. ""Hányszor adtuk ki az XXX eszközt [időszakban]?"" ---
Rentals + RentalDevices + Devices LIKE keresés → eszközönkénti COUNT.
MINTA:
  SELECT d.DeviceName, COUNT(rd.Id) AS KiadasokSzama
  FROM Rentals r
  JOIN RentalDevices rd ON r.Id = rd.RentalId
  JOIN Devices d ON rd.DeviceId = d.Id
  WHERE d.DeviceName LIKE '%Genesis%'
    AND CAST(r.RentStart AS DATE) >= '2026-01-01'
    AND CAST(r.RentStart AS DATE) <= '2026-05-20'
  GROUP BY d.DeviceName

--- 5. ""Mi a legtöbbet kiadott / top 3 / top 10 eszköz [időszakban]?"" ---
Rentals + RentalDevices → eszközönkénti COUNT → TOP N ORDER BY DESC.
MINTA:
  SELECT TOP 10 d.DeviceName, COUNT(rd.Id) AS KiadasokSzama
  FROM Rentals r
  JOIN RentalDevices rd ON r.Id = rd.RentalId
  JOIN Devices d ON rd.DeviceId = d.Id
  WHERE CAST(r.RentStart AS DATE) >= '2026-01-01'
    AND CAST(r.RentStart AS DATE) <= '2026-05-20'
  GROUP BY d.DeviceName
  ORDER BY KiadasokSzama DESC

--- 6. ""Mennyi bevétel volt [időszakban]?"" (bérlési bevétel) ---
Bérlési jegyek az időszakban → SUM(TotalAmount).
MINTA:
  SELECT SUM(r.TotalAmount) AS OsszesBevetel
  FROM Rentals r
  WHERE CAST(r.RentStart AS DATE) >= '2026-05-01'
    AND CAST(r.RentStart AS DATE) <= '2026-05-20'

--- 7. ""Mennyi bevételt hozott az XXX eszköz [időszakban]?"" ---
Rentals + RentalDevices + Devices LIKE keresés → kiadások száma + TotalAmount összege.
FIGYELEM: TotalAmount a teljes bérlés összege (több eszközzel), nem csak az adott eszközé!
MINTA:
  SELECT d.DeviceName, COUNT(rd.Id) AS KiadasokSzama, SUM(r.TotalAmount) AS BerlesekOsszerteke
  FROM Rentals r
  JOIN RentalDevices rd ON r.Id = rd.RentalId
  JOIN Devices d ON rd.DeviceId = d.Id
  WHERE d.DeviceName LIKE '%Merida%'
    AND CAST(r.RentStart AS DATE) >= '2026-01-01'
    AND CAST(r.RentStart AS DATE) <= '2026-05-20'
  GROUP BY d.DeviceName

--- 8. ""Számlázott / számlázatlan bevétel [időszakban]?"" ---
Rentals.Invoice mező alapján szétválasztás. Ha eszközre is szűr: JOIN RentalDevices + Devices + LIKE.
MINTA:
  SELECT
    SUM(CASE WHEN Invoice IS NOT NULL AND Invoice <> '' AND Invoice <> 'nincs számla' THEN TotalAmount ELSE 0 END) AS SzamlazottBevetel,
    SUM(CASE WHEN Invoice IS NULL OR Invoice = '' OR Invoice = 'nincs számla' THEN TotalAmount ELSE 0 END) AS SzamlazatlanBevetel,
    COUNT(CASE WHEN Invoice IS NOT NULL AND Invoice <> '' AND Invoice <> 'nincs számla' THEN 1 END) AS SzamlazottDb,
    COUNT(CASE WHEN Invoice IS NULL OR Invoice = '' OR Invoice = 'nincs számla' THEN 1 END) AS SzamlazatlanDb
  FROM Rentals
  WHERE CAST(RentStart AS DATE) >= '2026-05-01'
    AND CAST(RentStart AS DATE) <= '2026-05-20'

--- 9. ""Milyen szervizek voltak az XXX eszközön?"" ---
ServiceDevices + Services + Devices LIKE keresés → szervizjegy lista dátummal, típussal, leírással.
MINTA:
  SELECT s.TicketNr, s.ServiceType, s.Description, CAST(s.ServiceDate AS DATE) AS Datum, s.CostAmount, d.DeviceName
  FROM Services s
  JOIN ServiceDevices sd ON s.Id = sd.ServiceId
  JOIN Devices d ON sd.DeviceId = d.Id
  WHERE d.DeviceName LIKE '%Merida%'
  ORDER BY s.ServiceDate DESC

--- 10. ""Honnan jönnek az ügyfelek [általában/adott napon/időszakban]?"" ---
Ügyfelek City mezője alapján csoportosítás, bérlésekhez kötve (időszak szűréssel ha kell).
BUDAPEST SZABÁLY: A ""Budapest"" különböző formáit (""Budapest"", ""Bp"", ""Bp."", és kerületes formák mint ""Budapest, XIII. kerület"", ""Budapest XIII"", ""1138 Budapest"" stb.) MIND egybe kell vonni egyetlen ""Budapest"" csoportba!
Ehhez: CASE WHEN c.City LIKE '%Budapest%' OR c.City LIKE '%Bp%' THEN 'Budapest' ELSE c.City END
MINTA (általában, összes bérlés alapján):
  SELECT
    CASE WHEN c.City LIKE '%Budapest%' OR c.City LIKE 'Bp%' THEN 'Budapest' ELSE c.City END AS Varos,
    COUNT(r.Id) AS BerlesekSzama
  FROM Rentals r
  JOIN Customers c ON r.CustomerId = c.Id
  GROUP BY CASE WHEN c.City LIKE '%Budapest%' OR c.City LIKE 'Bp%' THEN 'Budapest' ELSE c.City END
  ORDER BY BerlesekSzama DESC
MINTA (adott időszakban):
  SELECT
    CASE WHEN c.City LIKE '%Budapest%' OR c.City LIKE 'Bp%' THEN 'Budapest' ELSE c.City END AS Varos,
    COUNT(r.Id) AS BerlesekSzama
  FROM Rentals r
  JOIN Customers c ON r.CustomerId = c.Id
  WHERE CAST(r.RentStart AS DATE) >= '2026-05-01'
    AND CAST(r.RentStart AS DATE) <= '2026-05-20'
  GROUP BY CASE WHEN c.City LIKE '%Budapest%' OR c.City LIKE 'Bp%' THEN 'Budapest' ELSE c.City END
  ORDER BY BerlesekSzama DESC

--- 11. ""Hányan jöttek XXX városból [időszakban]?"" ---
Konkrét városra szűrés a Customers.City mezőn keresztül, bérlések számolásával.
BUDAPEST SZABÁLY: Ha Budapestre kérdez (""Budapest"", ""Bp"", ""budapestről"", ""pestről""), az ÖSSZES budapesti ügyfelet egybe kell számolni kerülettől függetlenül!
Ehhez: WHERE (c.City LIKE '%Budapest%' OR c.City LIKE 'Bp%')
Más városnál: WHERE c.City LIKE '%városnév%'
MINTA (Budapest, időszakban):
  SELECT COUNT(r.Id) AS BerlesekSzama
  FROM Rentals r
  JOIN Customers c ON r.CustomerId = c.Id
  WHERE (c.City LIKE '%Budapest%' OR c.City LIKE 'Bp%')
    AND CAST(r.RentStart AS DATE) >= '2026-05-01'
    AND CAST(r.RentStart AS DATE) <= '2026-05-20'
MINTA (más város, időszakban):
  SELECT COUNT(r.Id) AS BerlesekSzama
  FROM Rentals r
  JOIN Customers c ON r.CustomerId = c.Id
  WHERE c.City LIKE '%Debrecen%'
    AND CAST(r.RentStart AS DATE) >= '2026-05-01'
    AND CAST(r.RentStart AS DATE) <= '2026-05-20'

══════════════════════════════════════
EGYÉB SZERVÍZ MINTÁK
══════════════════════════════════════
- Összes munkaóra egy eszközön:
  SELECT SUM(s.WorkHours * 60 + s.WorkMinutes) AS OsszesPerc FROM Services s JOIN ServiceDevices sd ON s.Id = sd.ServiceId JOIN Devices d ON sd.DeviceId = d.Id WHERE d.DeviceName LIKE '%X%'
- Milyen alkatrészeket cseréltem X eszközön:
  SELECT p.Name, sp.Quantity, CAST(s.ServiceDate AS DATE) AS Datum FROM ServiceParts sp JOIN Parts p ON sp.PartId = p.Id JOIN Services s ON sp.ServiceId = s.Id JOIN ServiceDevices sd ON s.Id = sd.ServiceId JOIN Devices d ON sd.DeviceId = d.Id WHERE d.DeviceName LIKE '%X%'
- Melyik eszközt javítottam legtöbbször:
  SELECT TOP 1 d.DeviceName, COUNT(DISTINCT s.Id) AS SzervizDb FROM Services s JOIN ServiceDevices sd ON s.Id = sd.ServiceId JOIN Devices d ON sd.DeviceId = d.Id GROUP BY d.DeviceName ORDER BY SzervizDb DESC

══════════════════════════════════════
SQL ÍRÁSI SZABÁLYOK
══════════════════════════════════════
- CSAK SELECT! Soha INSERT/UPDATE/DELETE/DROP/ALTER/EXEC!
- EGYETLEN SELECT utasítás! Nincs pontosvessző (;), nincs több query!
- Maximum TOP 100 sor
- A válaszod KIZÁRÓLAG a nyers SQL — semmi más szöveg, semmi markdown, semmi magyarázat!
- Dátumszűrésnél MINDIG CAST(mező AS DATE)
- OR + AND kombinálásnál MINDIG zárójel: WHERE (a OR b) AND c
- Ha darabszámot ÉS listát is kérnek: COUNT(*) OVER() ablakfüggvényt használj
- Ha a kérdés nem válaszolható meg SQL-lel: HIBA: [rövid ok]
- A Settings táblát SOHA ne kérdezd le!
- Ne használd a ReviewEmailSent, ContractEmailSent, InvoiceEmailSent mezőket üzleti logikához"
            .Replace("##TODAY##", promptToday.ToString("yyyy-MM-dd"))
            .Replace("##DAYNAME##", promptToday.ToString("dddd", new System.Globalization.CultureInfo("hu-HU")))
            .Replace("##YEAR##", promptToday.Year.ToString())
            .Replace("##YESTERDAY##", promptToday.AddDays(-1).ToString("yyyy-MM-dd"))
            .Replace("##LASTYEAR##", (promptToday.Year - 1).ToString());

        var sqlResponse = await client.Messages.GetClaudeMessageAsync(new MessageParameters
        {
            Model = "claude-sonnet-4-6",
            MaxTokens = 500,
            System = new List<SystemMessage> { new SystemMessage(schemaPrompt) },
            Messages = new List<Message>
            {
                new Message(RoleType.User, question)
            }
        });

        var generatedSql = sqlResponse.Message.ToString().Trim();

        // Markdown kódblokk eltávolítása ha Claude backtick-ekkel válaszol (```sql ... ```)
        if (generatedSql.Contains("```"))
        {
            var lines = generatedSql.Split('\n')
                .Where(l => !l.TrimStart().StartsWith("```"))
                .ToArray();
            generatedSql = string.Join('\n', lines).Trim();
        }

        // Biztonsági ellenőrzés
        if (generatedSql.StartsWith("HIBA:"))
            return Results.Json(new { answer = generatedSql, sql = "" });

        var sqlUpper = generatedSql.ToUpperInvariant();
        var forbiddenKeywords = new[] { "INSERT", "UPDATE", "DELETE", "DROP", "ALTER", "EXEC", "TRUNCATE", "CREATE", "SETTINGS" };
        if (forbiddenKeywords.Any(kw => sqlUpper.Contains(kw)))
        {
            return Results.Json(new { answer = "Biztonsagi okokbol csak olvasasi muveletek engedelyezettek.", sql = generatedSql });
        }

        // 2. lépés: SQL futtatása (max 2 próbálkozás — ha hibás, Claude javítja)
        var resultRows = new List<Dictionary<string, object?>>();
        var columns = new List<string>();
        string executedSql = generatedSql;

        for (int attempt = 0; attempt < 2; attempt++)
        {
            try
            {
                resultRows.Clear();
                columns.Clear();
                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();
                using var command = new SqlCommand(executedSql, connection);
                command.CommandTimeout = 10;
                using var dataReader = await command.ExecuteReaderAsync();

                columns = Enumerable.Range(0, dataReader.FieldCount)
                    .Select(i => dataReader.GetName(i)).ToList();

                while (await dataReader.ReadAsync() && resultRows.Count < 100)
                {
                    var row = new Dictionary<string, object?>();
                    foreach (var col in columns)
                    {
                        var val = dataReader[col];
                        row[col] = val == DBNull.Value ? null : val;
                    }
                    resultRows.Add(row);
                }
                break; // Sikeres — kilépünk a ciklusból
            }
            catch (SqlException sqlEx) when (attempt == 0)
            {
                // Első hiba: visszaküldjük Claude-nak javításra
                var fixResponse = await client.Messages.GetClaudeMessageAsync(new MessageParameters
                {
                    Model = "claude-sonnet-4-6",
                    MaxTokens = 500,
                    System = new List<SystemMessage> { new SystemMessage(schemaPrompt) },
                    Messages = new List<Message>
                    {
                        new Message(RoleType.User, question),
                        new Message(RoleType.Assistant, executedSql),
                        new Message(RoleType.User, $"Ez az SQL hibát dobott: {sqlEx.Message}\n\nKérlek javítsd ki! Csak a javított SQL-t add vissza, semmi mást.")
                    }
                });

                executedSql = fixResponse.Message.ToString().Trim();

                // Markdown kódblokk eltávolítása ha van
                if (executedSql.Contains("```"))
                {
                    var lines = executedSql.Split('\n')
                        .Where(l => !l.TrimStart().StartsWith("```"))
                        .ToArray();
                    executedSql = string.Join('\n', lines).Trim();
                }
            }
        }

        // 3. lépés: Claude összefoglalja az eredményt magyarul
        var resultText = new StringBuilder();
        resultText.AppendLine($"SQL: {executedSql}");
        resultText.AppendLine($"Oszlopok: {string.Join(", ", columns)}");
        resultText.AppendLine($"Sorok szama: {resultRows.Count}");
        resultText.AppendLine("Adatok:");
        foreach (var row in resultRows.Take(50))
        {
            resultText.AppendLine(string.Join(" | ", row.Select(kv => $"{kv.Key}: {kv.Value}")));
        }

        var summaryResponse = await client.Messages.GetClaudeMessageAsync(new MessageParameters
        {
            Model = "claude-sonnet-4-6",
            MaxTokens = 1000,
            System = new List<SystemMessage> { new SystemMessage(
                @"A felhasználó feltett egy kérdést egy kerékpár-kölcsönző cég adatbázisáról. Lefuttattuk az SQL lekérdezést és megkaptuk az eredményt.

Foglald össze az eredményt magyarul, közérthetően, röviden. Szabályok:
- Számokat formázd szépen: ezres elválasztó szóközzel, pénznem Ft (pl. 1 234 567 Ft)
- Ha nincs eredmény (0 sor): mondd el hogy nincs találat, és ha időszakos kérdés volt, nevezd meg az időszakot
- Ha van Osszes oszlop, azt tekintsd teljes darabszámnak és mondd ki
- Ha több sor jön vissza: adj előbb 1 mondatos összegzést, utána legfeljebb 5-10 fontos tételt
- Ha a találat TOP 100 miatt csonkolt lehet, jelezd
- Ha a TotalAmount összeget adod meg egy konkrét eszközre szűrt lekérdezésnél, jelezd hogy ez a teljes bérlés összege (ami más eszközöket is tartalmazhat)
- Ne magyarázd az SQL-t, csak az eredményt!
- Ha telefonszámot kérdeztek de nincs az eredményben, jelezd hogy a rendszer nem tartalmaz telefonszámot") },
            Messages = new List<Message>
            {
                new Message(RoleType.User, $"Kérdés: {question}\n\nEredmény:\n{resultText}")
            }
        });

        var answer = summaryResponse.Message.ToString().Trim();

        return Results.Json(new { answer, sql = executedSql, rowCount = resultRows.Count });
    }
    catch (SqlException ex)
    {
        return Results.Json(new { answer = $"SQL hiba (javítás után is): {ex.Message}", sql = "", rowCount = 0 });
    }
    catch (Exception ex)
    {
        return Results.Json(new { answer = $"Hiba: {ex.Message}", sql = "", rowCount = 0 });
    }
});

// === KORÁBBI KÉRDÉSEK API ===
app.MapGet("/api/recent-questions", () =>
{
    lock (recentQuestionsLock)
    {
        return Results.Json(recentQuestions.ToList());
    }
});

// Főoldal → reporting.html
app.MapGet("/", () => Results.Redirect("/reporting.html"));

// Hálózaton elérhető legyen (nem csak localhost)
app.Run("http://0.0.0.0:5000");
