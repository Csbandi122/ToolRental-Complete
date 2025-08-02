namespace ToolRental.Core.Models
{
    public class Setting
    {
        public int Id { get; set; }
        public string CompanyName { get; set; } = string.Empty;
        public string? CompanyLogo { get; set; }
        public string EmailSmtp { get; set; } = string.Empty;
        public int SmtpPort { get; set; }
        public string EmailPassword { get; set; } = string.Empty;
        public string SenderName { get; set; } = string.Empty;
        public string SenderEmail { get; set; } = string.Empty;
        public string? CcAddress { get; set; }
        public string EmailSubject { get; set; } = string.Empty;
        public string? GoogleReview { get; set; }
        public string? TemplateContract { get; set; }
        public string? Aszf { get; set; }
        public string? InvoiceXml { get; set; }
        public string ReviewEmailSubject { get; set; } = string.Empty;
        public string? ReviewEmailTemplate { get; set; }
        public int DefaultRentalDays { get; set; } = 1;
    }
}