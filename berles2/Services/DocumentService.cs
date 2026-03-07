using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using System.IO;
using ToolRental.Core.Models;
using SystemIO = System.IO;
using Word = Microsoft.Office.Interop.Word;

namespace berles2.Services
{
    /// <summary>
    /// Word szerződés generálás és Word→PDF konverzió.
    /// Nincs UI függősége, önállóan tesztelhető.
    /// </summary>
    internal class DocumentService
    {
        private readonly Setting _setting;
        private readonly string _exeDirectory;

        public DocumentService(Setting setting)
        {
            _setting = setting ?? throw new ArgumentNullException(nameof(setting));
            _exeDirectory = SystemIO.Path.GetDirectoryName(
                System.Reflection.Assembly.GetExecutingAssembly().Location) ?? "";
        }

        // ===========================================
        // SZERZŐDÉS GENERÁLÁS
        // ===========================================

        /// <summary>
        /// Word szerződést generál a sablonból, feltölti az adatokkal, majd PDF-be konvertálja.
        /// </summary>
        /// <returns>A generált PDF elérési útja, vagy üres string ha hiba történt.</returns>
        public string GenerateContract(ContractData data)
        {
            if (_setting.TemplateContract == null || !SystemIO.File.Exists(_setting.TemplateContract))
                throw new FileNotFoundException(
                    "A szerződés sablon nincs beállítva vagy nem található! " +
                    "Kérjük állítsa be a Beállítások menüben.");

            // Kimeneti Word mappa
            string contractsFolder = SystemIO.Path.Combine(_exeDirectory, "files", "contracts-word");
            SystemIO.Directory.CreateDirectory(contractsFolder);

            // Fájlnév
            string cleanName = GetCleanFileName(data.CustomerName);
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm");
            string wordFileName = $"szerződés_{cleanName}_{timestamp}.docx";
            string wordPath = SystemIO.Path.Combine(contractsFolder, wordFileName);

            // Template másolása és változók kitöltése
            SystemIO.File.Copy(_setting.TemplateContract, wordPath, overwrite: true);
            FillWordTemplate(wordPath, data);

            // Word → PDF
            string pdfPath = ConvertWordToPdfFromPath(wordPath);
            return pdfPath;
        }

        /// <summary>
        /// Az utolsó bérléshez generált PDF-et adja vissza (az email gomb használja).
        /// Ha még nincs PDF, legenerálja.
        /// </summary>
        public string GetOrCreateContractPdf(string customerName, string timestamp)
        {
            string cleanName = GetCleanFileName(customerName);

            string contractsPdfFolder = SystemIO.Path.Combine(_exeDirectory, "files", "contracts-pdf");
            string pdfFileName = $"szerződés_{cleanName}_{timestamp}.pdf";
            string pdfPath = SystemIO.Path.Combine(contractsPdfFolder, pdfFileName);

            if (SystemIO.File.Exists(pdfPath))
                return pdfPath;

            // Ha nincs PDF, keressük a Word fájlt és konvertáljuk
            string contractsWordFolder = SystemIO.Path.Combine(_exeDirectory, "files", "contracts-word");
            string wordFileName = $"szerződés_{cleanName}_{timestamp}.docx";
            string wordPath = SystemIO.Path.Combine(contractsWordFolder, wordFileName);

            if (!SystemIO.File.Exists(wordPath))
                throw new FileNotFoundException(
                    "A Word szerződés fájl nem található! Először generálja le a szerződést.");

            return ConvertWordToPdfFromPath(wordPath);
        }

        // ===========================================
        // WORD TEMPLATE KITÖLTÉS
        // ===========================================

        private void FillWordTemplate(string filePath, ContractData data)
        {
            using var document = WordprocessingDocument.Open(filePath, isEditable: true);
            var body = document.MainDocumentPart?.Document.Body;
            if (body == null) return;

            // Szöveges változók helyettesítése
            foreach (var text in body.Descendants<Text>())
            {
                if (!text.Text.Contains("{{")) continue;

                text.Text = text.Text
                    .Replace("{{CUSTOMER_NAME}}", data.CustomerName)
                    .Replace("{{CUSTOMER_ZIP}}", data.CustomerZip)
                    .Replace("{{CUSTOMER_CITY}}", data.CustomerCity)
                    .Replace("{{CUSTOMER_ADDRESS}}", data.CustomerAddress)
                    .Replace("{{CUSTOMER_EMAIL}}", data.CustomerEmail)
                    .Replace("{{CUSTOMER_ID_NUMBER}}", data.CustomerIdNumber)
                    .Replace("{{RENTAL_DATE}}", DateTime.Now.ToString("yyyy. MM. dd."))
                    .Replace("{{RENTAL_DAYS}}", data.RentalDays.ToString())
                    .Replace("{{DEVICE_COUNT}}", data.Devices.Count.ToString())
                    .Replace("{{TOTAL_AMOUNT}}", $"{data.TotalAmount:N0}");
            }

            // Eszköz táblázat helyettesítése
            InsertDeviceTable(body, data);

            document.Save();
        }

        private void InsertDeviceTable(Body body, ContractData data)
        {
            foreach (var text in body.Descendants<Text>())
            {
                if (!text.Text.Contains("{{DEVICE_TABLE}}")) continue;

                var table = new Table();

                table.AppendChild(new TableProperties(
                    new TableBorders(
                        new TopBorder()              { Val = BorderValues.Single, Size = 12 },
                        new BottomBorder()           { Val = BorderValues.Single, Size = 12 },
                        new LeftBorder()             { Val = BorderValues.Single, Size = 12 },
                        new RightBorder()            { Val = BorderValues.Single, Size = 12 },
                        new InsideHorizontalBorder() { Val = BorderValues.Single, Size = 12 },
                        new InsideVerticalBorder()   { Val = BorderValues.Single, Size = 12 }
                    )
                ));

                // Fejléc sor
                var headerRow = new TableRow();
                headerRow.AppendChild(CreateCell("Eszköztípus",  bold: true));
                headerRow.AppendChild(CreateCell("Eszköz neve",  bold: true));
                headerRow.AppendChild(CreateCell("Sorozatszám",  bold: true));
                headerRow.AppendChild(CreateCell("Eszköz értéke", bold: true));
                headerRow.AppendChild(CreateCell("Bérleti díj",  bold: true));
                table.AppendChild(headerRow);

                // Eszköz sorok
                foreach (var device in data.Devices)
                {
                    decimal discountedPrice = device.RentPrice * (100 - data.DiscountPercent) / 100;

                    var row = new TableRow();
                    row.AppendChild(CreateCell(device.DeviceTypeNavigation?.TypeName ?? "N/A"));
                    row.AppendChild(CreateCell(device.DeviceName));
                    row.AppendChild(CreateCell(device.Serial));
                    row.AppendChild(CreateCell($"{device.Price:N0} Ft"));
                    row.AppendChild(CreateCell($"{discountedPrice:N0} Ft"));
                    table.AppendChild(row);
                }

                // {{DEVICE_TABLE}} szöveg törlése, táblázat beszúrása utána
                var paragraph = text.Ancestors<Paragraph>().First();
                text.Text = text.Text.Replace("{{DEVICE_TABLE}}", "");
                paragraph.Parent!.InsertAfter(table, paragraph);
                break;
            }
        }

        private static TableCell CreateCell(string content, bool bold = false)
        {
            var run = new Run();
            if (bold)
                run.AppendChild(new RunProperties(new Bold()));
            run.AppendChild(new Text(content));

            var para = new Paragraph(run);
            return new TableCell(para);
        }

        // ===========================================
        // WORD → PDF KONVERZIÓ
        // ===========================================

        /// <summary>
        /// Word fájlt PDF-be konvertál Microsoft Word Interop-pal.
        /// </summary>
        /// <returns>A generált PDF elérési útja.</returns>
        public string ConvertWordToPdfFromPath(string wordPath)
        {
            if (!SystemIO.File.Exists(wordPath))
                throw new FileNotFoundException($"A Word szerződés fájl nem található: {wordPath}");

            string contractsPdfFolder = SystemIO.Path.Combine(_exeDirectory, "files", "contracts-pdf");
            SystemIO.Directory.CreateDirectory(contractsPdfFolder);

            string pdfFileName = SystemIO.Path.GetFileNameWithoutExtension(wordPath) + ".pdf";
            string pdfPath = SystemIO.Path.Combine(contractsPdfFolder, pdfFileName);

            Word.Application? wordApp = null;
            Word.Document? doc = null;
            try
            {
                wordApp = new Word.Application { Visible = false };
                doc = wordApp.Documents.Open(wordPath);
                doc.SaveAs2(pdfPath, Word.WdSaveFormat.wdFormatPDF);
            }
            finally
            {
                if (doc != null)
                {
                    doc.Close();
                    System.Runtime.InteropServices.Marshal.ReleaseComObject(doc);
                }
                if (wordApp != null)
                {
                    wordApp.Quit();
                    System.Runtime.InteropServices.Marshal.ReleaseComObject(wordApp);
                }
            }

            return pdfPath;
        }

        // ===========================================
        // SEGÉD METÓDUS
        // ===========================================

        /// <summary>
        /// Fájlnévbe nem megengedett karaktereket eltávolítja, szóközöket aláhúzásra cseréli.
        /// </summary>
        public static string GetCleanFileName(string input)
        {
            if (string.IsNullOrEmpty(input)) return "ismeretlen";

            string clean = input;
            foreach (char c in SystemIO.Path.GetInvalidFileNameChars())
                clean = clean.Replace(c.ToString(), "");

            return clean.Replace(" ", "_");
        }
    }

    // ===========================================
    // ADATÁTVITELI OBJEKTUM (DTO)
    // ===========================================

    /// <summary>
    /// A szerződés generáláshoz szükséges összes adat egy helyen — nincs UI függőség.
    /// </summary>
    internal class ContractData
    {
        public string CustomerName      { get; init; } = "";
        public string CustomerZip       { get; init; } = "";
        public string CustomerCity      { get; init; } = "";
        public string CustomerAddress   { get; init; } = "";
        public string CustomerEmail     { get; init; } = "";
        public string CustomerIdNumber  { get; init; } = "";
        public int    RentalDays        { get; init; } = 1;
        public int    DiscountPercent   { get; init; } = 0;
        public decimal TotalAmount      { get; init; } = 0;
        public List<ToolRental.Core.Models.Device> Devices { get; init; } = new();
    }
}
