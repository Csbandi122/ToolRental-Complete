using System.Diagnostics;
using System.IO;
using System.Text;
using ToolRental.Core.Models;
using SystemIO = System.IO;

namespace berles2.Services
{
    /// <summary>
    /// Számlázz.hu XML számla generálás és CURL-alapú beküldés.
    /// Nincs UI függősége, önállóan tesztelhető.
    /// </summary>
    internal class InvoiceService
    {
        private readonly Setting _setting;
        private readonly string _exeDirectory;

        public InvoiceService(Setting setting)
        {
            _setting = setting ?? throw new ArgumentNullException(nameof(setting));
            _exeDirectory = AppDomain.CurrentDomain.BaseDirectory;
        }

        // ===========================================
        // PUBLIKUS BELÉPÉSI PONT
        // ===========================================

        /// <summary>
        /// XML számla generálása és beküldése Számlázz.hu-ra CURL-lal.
        /// </summary>
        /// <returns>A letöltött PDF számla elérési útja, vagy üres string ha hiba volt.</returns>
        public async Task<string> GenerateAndSendAsync(InvoiceData data)
        {
            if (string.IsNullOrWhiteSpace(_setting.InvoiceXml))
                throw new InvalidOperationException(
                    "Nincs beállítva számla XML template! Kérlek állítsd be a beállításokban.");

            if (!SystemIO.File.Exists(_setting.InvoiceXml))
                throw new FileNotFoundException(
                    $"A számla XML template nem található: {_setting.InvoiceXml}");

            // XML kimeneti mappa
            string invoiceFolder = SystemIO.Path.Combine(_exeDirectory, "files", "Invoice_xml");
            SystemIO.Directory.CreateDirectory(invoiceFolder);

            // Fájlnév
            string cleanName  = DocumentService.GetCleanFileName(data.CustomerName);
            string timestamp  = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            string xmlPath    = SystemIO.Path.Combine(invoiceFolder, $"szamla_{cleanName}_{timestamp}.xml");

            // Template kitöltése és mentése
            string xmlContent = SystemIO.File.ReadAllText(_setting.InvoiceXml);
            xmlContent = FillTemplate(xmlContent, data);
            SystemIO.File.WriteAllText(xmlPath, xmlContent, Encoding.UTF8);

            // CURL küldés → PDF visszakapás
            return await SendViaCurlAsync(xmlPath, cleanName, timestamp);
        }

        // ===========================================
        // XML TEMPLATE KITÖLTÉS
        // ===========================================

        private static string FillTemplate(string xmlContent, InvoiceData data)
        {
            return xmlContent
                .Replace("{{CUSTOMER_NAME}}",           data.CustomerName)
                .Replace("{{CUSTOMER_ZIP}}",            data.CustomerZip)
                .Replace("{{CUSTOMER_CITY}}",           data.CustomerCity)
                .Replace("{{CUSTOMER_ADDRESS}}",        data.CustomerAddress)
                .Replace("{{CUSTOMER_EMAIL}}",          data.CustomerEmail)
                .Replace("{{RENTAL_DATE}}",             DateTime.Now.ToString("yyyy-MM-dd"))
                .Replace("{{PAYMENT_DUE_DATE}}",        DateTime.Now.ToString("yyyy-MM-dd"))
                .Replace("{{PAYMENT_MODE}}",            data.PaymentMode)
                .Replace("{{NET_PRICE}}",               data.NetPrice.ToString("0"))
                .Replace("{{SELECTED_DEVICES_LIST}}",   data.DevicesList);
        }

        // ===========================================
        // CURL KÜLDÉS
        // ===========================================

        private async Task<string> SendViaCurlAsync(string xmlPath, string customerName, string timestamp)
        {
            string invoicesFolder   = SystemIO.Path.Combine(_exeDirectory, "files", "invoices");
            string curlAnswerFolder = SystemIO.Path.Combine(_exeDirectory, "files", "curl_answer");
            SystemIO.Directory.CreateDirectory(invoicesFolder);
            SystemIO.Directory.CreateDirectory(curlAnswerFolder);

            string pdfPath       = SystemIO.Path.Combine(invoicesFolder,   $"szamla_{customerName}_{timestamp}.pdf");
            string curlAnswerPath = SystemIO.Path.Combine(curlAnswerFolder, $"curl_valasz_{customerName}_{timestamp}.txt");
            string cookiesPath   = SystemIO.Path.Combine(_exeDirectory, "curl_cookies.txt");

            string curlArguments =
                $"-v " +
                $"-F \"action-xmlagentxmlfile=@{xmlPath}\" " +
                $"-c \"{cookiesPath}\" " +
                $"-o \"{pdfPath}\" " +
                $"\"https://www.szamlazz.hu/szamla/\"";

            var curlInfo = new ProcessStartInfo
            {
                FileName               = "curl",
                Arguments              = curlArguments,
                UseShellExecute        = false,
                CreateNoWindow         = true,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                WorkingDirectory       = _exeDirectory
            };

            using var curlProcess = Process.Start(curlInfo)
                ?? throw new InvalidOperationException("Nem sikerült elindítani a CURL parancsot.");

            string output = await curlProcess.StandardOutput.ReadToEndAsync();
            string error  = await curlProcess.StandardError.ReadToEndAsync();
            await curlProcess.WaitForExitAsync();

            // CURL válasz naplózása fájlba
            WriteCurlLog(curlAnswerPath, xmlPath, pdfPath, curlArguments,
                         customerName, curlProcess.ExitCode, output, error);

            // Eredmény ellenőrzése
            if (curlProcess.ExitCode != 0 || !SystemIO.File.Exists(pdfPath) || new FileInfo(pdfPath).Length == 0)
                throw new InvalidOperationException(
                    $"CURL hiba:\nKimeneti kód: {curlProcess.ExitCode}\n" +
                    $"PDF létrejött: {SystemIO.File.Exists(pdfPath)}\nHiba: {error}");

            // Szerver hibaüzenet a PDF helyett?
            string fileContent = SystemIO.File.ReadAllText(pdfPath, Encoding.UTF8);
            if (fileContent.Contains("[ERR]") || fileContent.Contains("Számla mentés sikertelen"))
            {
                try { SystemIO.File.Delete(pdfPath); } catch { /* silent */ }
                throw new InvalidOperationException(
                    $"Számla kibocsátási hiba:\n{fileContent[..Math.Min(300, fileContent.Length)]}");
            }

            // Cookies törlése sikeres küldés után
            try { SystemIO.File.Delete(cookiesPath); } catch { /* silent */ }

            return pdfPath;
        }

        private static void WriteCurlLog(string logPath, string xmlPath, string pdfPath,
            string curlArguments, string customerName, int exitCode, string output, string error)
        {
            try
            {
                bool pdfExists = SystemIO.File.Exists(pdfPath);
                long pdfSize   = pdfExists ? new FileInfo(pdfPath).Length : 0;

                string log =
                    $"=== CURL VÁLASZ RÉSZLETES LOG ===\n"     +
                    $"Dátum: {DateTime.Now}\n"                  +
                    $"Ügyfél: {customerName}\n"                 +
                    $"XML fájl: {xmlPath}\n"                    +
                    $"PDF cél: {pdfPath}\n"                     +
                    $"Exit kód: {exitCode}\n\n"                 +
                    $"=== CURL PARANCS ===\n"                   +
                    $"curl {curlArguments}\n\n"                 +
                    $"=== STANDARD OUTPUT ===\n{output}\n\n"    +
                    $"=== STANDARD ERROR ===\n{error}\n\n"      +
                    $"=== VÉGEREDMÉNY ===\n"                    +
                    $"PDF létrejött: {pdfExists}\n"             +
                    $"PDF mérete: {pdfSize} byte\n";

                SystemIO.File.WriteAllText(logPath, log, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                AppLogger.Logger.Warning(ex, "CURL log mentési hiba");
            }
        }
    }

    // ===========================================
    // ADATÁTVITELI OBJEKTUM (DTO)
    // ===========================================

    /// <summary>
    /// A számla generáláshoz szükséges összes adat — nincs UI függőség.
    /// </summary>
    internal class InvoiceData
    {
        public string  CustomerName    { get; init; } = "";
        public string  CustomerZip     { get; init; } = "";
        public string  CustomerCity    { get; init; } = "";
        public string  CustomerAddress { get; init; } = "";
        public string  CustomerEmail   { get; init; } = "";
        public string  PaymentMode     { get; init; } = "Készpénz";
        public decimal NetPrice        { get; init; } = 0;
        public string  DevicesList     { get; init; } = "";
    }
}
