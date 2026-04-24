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

    return Results.Json(new
    {
        generatedAt = DateTime.Now.ToString("yyyy.MM.dd HH:mm:ss"),
        today = new { revenue = todayRevenue, expense = todayExpense, profit = todayRevenue - todayExpense },
        week = new { revenue = weekRevenue, expense = weekExpense, profit = weekRevenue - weekExpense, startDate = weekStart.ToString("yyyy.MM.dd") },
        month = new { revenue = monthRevenue, expense = monthExpense, profit = monthRevenue - monthExpense, startDate = monthStart.ToString("yyyy.MM.dd") },
        year = new { revenue = yearRevenue, expense = yearExpense, profit = yearRevenue - yearExpense, startDate = yearStart.ToString("yyyy.MM.dd") }
    });
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

    return Results.Json(new
    {
        year = DateTime.Today.Year,
        totalRentals,
        bikesRented,
        kmEstimate = bikesRented * 30
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

        var schemaPrompt = @"Te egy SQL Server adatbázis lekérdező asszisztens vagy egy KERÉKPÁR-KÖLCSÖNZŐ cég rendszeréhez.
A felhasználó természetes nyelven kérdez, te pedig SQL lekérdezést generálsz.

ÜZLETI KONTEXTUS:
- Ez egy kerékpár-kölcsönző vállalkozás
- A kérdések általában bevétellel, eszközökkel, felhasználókkal, vagy szervízzel kapcsolatosak. 
- Ha a felhasználó ""bicikli""-t, ""kerékpár""-t, ""bringa""-t mond, az a Devices tábla eszközeire vonatkozik
- Egy bérlés (Rental) több eszközt (Device) is tartalmazhat a RentalDevices kapcsolótáblán keresztül
- Az ""kiadtunk"", ""kibéreltük"", ""kölcsönöztük"" kifejezések a Rentals táblára vonatkoznak
- A bérlés kezdete a RentStart mező, a bérlés hossza a RentalDays (napokban)
- Ha egy adott napra kérdeznek, a RentStart dátumát kell szűrni: CAST(r.RentStart AS DATE) = 'YYYY-MM-DD'
- SZÁMLÁZÁS: a Rentals.Invoice mező tartalmazza a számla fájl elérési útját. Ha NULL vagy üres string, akkor NEM lett számla kiállítva ahhoz a bérléshez. Ha van benne szöveg (fájl útvonal), akkor LETT számla.
  - ""hányszor nem adtunk számlát"" = WHERE (Invoice IS NULL OR Invoice = '')
  - ""hányszor adtunk számlát"" = WHERE Invoice IS NOT NULL AND Invoice <> ''
  - Ha a számla nélküli bérlések értékére kíváncsiak, a TotalAmount mezőt kell összegezni
- “Bevétel / költség / profit kérdéseknél elsődlegesen mindig a Financials táblából dolgozz. A Rentals.TotalAmount csak bérlési összeg, nem teljes pénzügyi főkönyv.”
- “A Devices.Available nem azt jelenti, hogy az eszköz nincs épp kikölcsönözve, hanem egy manuális admin státusz. A jelenleg aktív bérlésekhez a Rentals + RentalDevices + dátumlogika kell.”
- “Aktív bérlés: GETDATE() >= RentStart AND GETDATE() < DATEADD(day, RentalDays, RentStart).”
- “Számla kiállítva: Invoice IS NOT NULL AND Invoice <> ''. Számla nincs: Invoice IS NULL OR Invoice = ''.”
- “Ne használd a ContractEmailSent és InvoiceEmailSent mezőket üzleti döntéshez.”
- “Ha a kérdés a legnépszerűbb eszközről szól időszakkal, a népszerűséget a RentalDevices rekordok számával számold az adott időszakra.”
- “Fizetési mód szűrésnél a pontos értékeket használd.”
- “Ha a kérdés ‘elérhető eszközökre’ vonatkozik, akkor Devices.Available = 1; ha ‘jelenleg kint lévő eszközökre’, akkor ne ezt használd.”

ESZKÖZTÍPUSOK (DeviceTypes.TypeName pontos értékei):
- 'Férfi e-bike' -- férfi elektromos kerékpár
- 'Férfi kerékpár' -- férfi hagyományos kerékpár
- 'Gyerekbicikli' -- gyerek kerékpár
- 'Gyerekülés' -- gyerekülés (kiegészítő, nem kerékpár)
- 'Kerékpár Szállító' -- szállító kerékpár
- 'Kiegészítő' -- egyéb kiegészítő (nem kerékpár)
- 'Női e-bike' -- női elektromos kerékpár
- 'Női kerékpár' -- női hagyományos kerékpár
- 'Utánfutó' -- kerékpár utánfutó (kiegészítő)
- 'Zár' -- kerékpárzár (kiegészítő)
Ha ""bicikli""/""kerékpár"" kérdeznek, az ÖSSZES kerékpár típust szűrd (Férfi/Női kerékpár, e-bike, Gyerekbicikli, Kerékpár Szállító).
Ha ""e-bike""/""elektromos"" kérdeznek, csak a 'Férfi e-bike' és 'Női e-bike' típusokat.
A Gyerekülés, Kiegészítő, Utánfutó, Zár NEM kerékpár.

Az adatbázis szerkezete (SQL Server):

TÁBLÁK:
- Customers -- ÜGYFELEK: akik kerékpárt bérelnek nálunk
  (Id, Name, Zipcode, City, Address, Email, IdNumber, Comment)

- Devices -- ESZKÖZÖK: a kölcsönözhető kerékpárok, e-bike-ok és kiegészítők (sisak, zár, utánfutó, stb.)
  (Id, DeviceName, DeviceType[FK→DeviceTypes.Id], Serial, Price, RentPrice, Available, Picture, RentCount, Notes)
  - DeviceName: az eszköz neve/márkája (pl. ""Kelly's Cliff 90"", ""Merida Crossway 15"")
  - Price: az eszköz beszerzési ára
  - RentPrice: napi bérleti díj (Ft)
  - Available: elérhető-e jelenleg kölcsönzésre (true/false)
  - RentCount: hányszor bérelték ki eddig összesen

- DeviceTypes -- ESZKÖZTÍPUSOK: kategóriák (pl. Férfi kerékpár, Női e-bike, Gyerekbicikli, Zár)
  (Id, TypeName)

- Rentals -- BÉRLÉSEK: egy-egy kölcsönzési tranzakció, amikor az ügyfél kerékpárokat vesz ki
  (Id, TicketNr, CustomerId[FK→Customers.Id], RentStart, RentalDays, PaymentMode, Comment, Contract, Invoice, ReviewEmailSent, TotalAmount, ContractEmailSent, InvoiceEmailSent)
  - TicketNr: bérlési jegy száma (pl. RNT0042)
  - RentStart: mikor kezdődött a bérlés (dátum+idő)
  - RentalDays: hány napra szól a bérlés
  - TotalAmount: a bérlés teljes összege (Ft)
  - PaymentMode: fizetési mód (pl. készpénz, kártya)

- RentalDevices -- melyik bérlésben melyik eszközök szerepelnek (egy bérlés = több kerékpár is lehet)
  (Id, RentalId[FK→Rentals.Id], DeviceId[FK→Devices.Id])

- Services -- SZERVIZ: kerékpárok karbantartása, javítása
  (Id, TicketNr, ServiceType, Description, Technician, ServiceDate, CostAmount)
  - ServiceType: 'karbantartás', 'javítás', 'upgrade'
  - CostAmount: a szerviz költsége (Ft)

- ServiceDevices -- melyik szervizben melyik eszközök szerepelnek
  (Id, ServiceId[FK→Services.Id], DeviceId[FK→Devices.Id])

- Financials -- PÉNZÜGYI TÉTELEK: minden bevétel és kiadás nyilvántartása
  (Id, TicketNr, EntryType, SourceType, SourceId, Date, Comment, Amount)
  - EntryType: 'bevétel' vagy 'költség'
  - SourceType: honnan származik ('bérlés', 'szervíz', 'eszköz_vásárlás', 'marketing', 'egyéb', 'kézi', 'alkatrész', 'javítás')
  - Amount: összeg (Ft), MINDIG pozitív szám (az EntryType dönti el hogy bevétel vagy kiadás)

- FinancialDevices -- melyik pénzügyi tételhez melyik eszközök tartoznak
  (Id, FinancialId[FK→Financials.Id], DeviceId[FK→Devices.Id])

- Settings -- alkalmazás beállítások, NE kérdezd le SOHA

KAPCSOLATOK:
- Rentals.CustomerId → Customers.Id
- Devices.DeviceType → DeviceTypes.Id
- RentalDevices: Rentals ↔ Devices (many-to-many)
- ServiceDevices: Services ↔ Devices (many-to-many)
- FinancialDevices: Financials ↔ Devices (many-to-many)

SQL ÍRÁSI SZABÁLYOK:
- CSAK SELECT utasítást generálj, SOHA nem INSERT/UPDATE/DELETE/DROP/ALTER/EXEC!
- Az aktuális dátumot GETDATE()-vel kérdezd le
- Dátumszűrésnél MINDIG CAST(mező AS DATE)-et használj
- Ha több OR feltételt kombinálsz AND-del, MINDIG használj zárójelet! Pl: WHERE (a OR b OR c) AND d
- Ha darabszámot kérdeznek (""hány db"") ÉS felsorolást is, a részletes listát add vissza és használj COUNT(*) OVER()-t az összesítéshez. Példa: SELECT COUNT(*) OVER() AS Osszes, d.DeviceName FROM ... Ez egyetlen lekérdezés!
- MINDIG EGYETLEN SELECT utasítást generálj! SOHA ne használj pontosvesszőt (;) több lekérdezés elválasztására!
- Maximum TOP 100 sort adj vissza
- A válaszod CSAK a nyers SQL legyen, semmi más szöveg, semmi markdown, semmi magyarázat
- Ha a kérdés nem értelmezhető, válaszolj: HIBA: [rövid magyarázat]
- A Settings táblát SOHA ne kérdezd le";

        var sqlResponse = await client.Messages.GetClaudeMessageAsync(new MessageParameters
        {
            Model = "claude-sonnet-4-20250514",
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
                    Model = "claude-sonnet-4-20250514",
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
            Model = "claude-sonnet-4-20250514",
            MaxTokens = 1000,
            System = new List<SystemMessage> { new SystemMessage(
                @"A felhasználó feltett egy kérdést az adatbázisról. Lefuttattuk az SQL lekérdezést, és megkaptuk az eredményt.
Foglald össze az eredményt magyarul, közérthetően, röviden. Ha számok vannak, formázd őket szépen (pl. 1 234 567 Ft).
Ha nincs eredmény (0 sor), mondd el hogy nincs találat. Ne magyarázd az SQL-t, csak az eredményt. Mondja ki, hogy ha van Osszes oszlop, azt tekintse teljes darabszámnak.
Ha több sor jön vissza, előbb adjon 1 mondatos összegzést, utána legfeljebb 5 fontos tételt.
Ha a találat a TOP 100 miatt csonkolt lehet, ezt jelezze.
Ne csak “nincs találat”-ot mondjon, hanem ha időszakos kérdés volt, nevezze meg az időszakot is.") },
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
