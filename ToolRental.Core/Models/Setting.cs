namespace ToolRental.Core.Models
{
    public class Setting
    {
        public int Id { get; set; }

        // CÉG ADATOK
        public string CompanyName { get; set; } = string.Empty;
        public string? CompanyLogo { get; set; }

        // EMAIL KAPCSOLAT
        public string EmailSmtp { get; set; } = string.Empty;
        public int SmtpPort { get; set; } = 587;
        public string EmailPassword { get; set; } = string.Empty;
        public string SenderName { get; set; } = string.Empty;
        public string SenderEmail { get; set; } = string.Empty;
        public string? CcAddress { get; set; }

        // EMAIL TEMPLATE-EK
        public string EmailSubject { get; set; } = string.Empty;
        public string? ContractEmailTemplate { get; set; }    // ÚJ MEZŐ!
        public string ReviewEmailSubject { get; set; } = string.Empty;
        public string? ReviewEmailTemplate { get; set; }

        // DOKUMENTUM SABLONOK
        public string? TemplateContract { get; set; }
        public string? AszfFile { get; set; }               // JAVÍTVA: Aszf → AszfFile

        // MARKETING
        public string? GoogleReview { get; set; }

        // SZÁMLA
        public string? InvoiceXml { get; set; }

        // ALAPÉRTELMEZÉSEK
        public int DefaultRentalDays { get; set; } = 1;
    }
}