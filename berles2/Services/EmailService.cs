using MailKit.Net.Smtp;
using MimeKit;
using ToolRental.Core.Models;
using SystemIO = System.IO;

namespace berles2.Services
{
    /// <summary>
    /// Email küldési logika — SMTP-n keresztül szerződés és értékelő emailek.
    /// Nincs UI függősége, önállóan tesztelhető.
    /// </summary>
    internal class EmailService
    {
        private readonly Setting _setting;

        public EmailService(Setting setting)
        {
            _setting = setting ?? throw new ArgumentNullException(nameof(setting));
        }

        // ===========================================
        // SZERZŐDÉS EMAIL
        // ===========================================

        /// <summary>
        /// Bérlési szerződés emailt küld PDF melléklettel (+ ÁSZF ha be van állítva).
        /// </summary>
        /// <param name="recipientEmail">Ügyfél email címe</param>
        /// <param name="recipientName">Ügyfél neve (template-be kerül)</param>
        /// <param name="pdfPath">A generált PDF szerződés elérési útja</param>
        public void SendContractEmail(string recipientEmail, string recipientName, string pdfPath)
        {
            if (string.IsNullOrWhiteSpace(recipientEmail))
                throw new ArgumentException("Az ügyfél email címe nincs megadva!");

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_setting.SenderName, _setting.SenderEmail));
            message.To.Add(new MailboxAddress("", recipientEmail));

            if (!string.IsNullOrWhiteSpace(_setting.CcAddress))
                message.Cc.Add(new MailboxAddress("", _setting.CcAddress));

            message.Subject = _setting.EmailSubject;

            string emailBody = BuildContractEmailBody(recipientName);
            var bodyBuilder = new BodyBuilder { HtmlBody = emailBody };

            if (SystemIO.File.Exists(pdfPath))
                bodyBuilder.Attachments.Add(pdfPath);

            if (!string.IsNullOrWhiteSpace(_setting.AszfFile) && SystemIO.File.Exists(_setting.AszfFile))
                bodyBuilder.Attachments.Add(_setting.AszfFile);

            message.Body = bodyBuilder.ToMessageBody();

            Send(message);
        }

        /// <summary>
        /// Felépíti a szerződés email törzsét — template fájlból vagy alapértelmezett HTML-ből.
        /// </summary>
        private string BuildContractEmailBody(string customerName)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(_setting.ContractEmailTemplate) &&
                    SystemIO.File.Exists(_setting.ContractEmailTemplate))
                {
                    string template = SystemIO.File.ReadAllText(_setting.ContractEmailTemplate);
                    template = template.Replace("{{CUSTOMER_NAME}}", customerName);
                    template = template.Replace("{{COMPANY_NAME}}", _setting.CompanyName);
                    template = template.Replace("{{RENTAL_DATE}}", DateTime.Now.ToString("yyyy. MM. dd."));
                    template = template.Replace("{{GOOGLE_REVIEW_LINK}}", _setting.GoogleReview ?? "");
                    return template;
                }
            }
            catch
            {
                // Ha a template fájl olvasása sikertelen, az alapértelmezett tartalomra esünk vissza
            }

            return $@"
            <html>
            <body>
                <h2>Kedves {customerName}!</h2>
                <p>Köszönjük, hogy választotta a {_setting.CompanyName} szolgáltatásait!</p>
                <p>Mellékletben megtalálja:</p>
                <ul>
                    <li>A bérlési szerződést PDF formátumban</li>
                    <li>Az Általános Szerződési Feltételeket</li>
                </ul>
                <p>Kérjük, olvassa át figyelmesen a dokumentumokat.</p>
                <p>Köszönjük a bizalmát!</p>
                <br>
                <p>Üdvözlettel,<br>{_setting.CompanyName}</p>
            </body>
            </html>";
        }

        // ===========================================
        // ÉRTÉKELŐ EMAIL
        // ===========================================

        /// <summary>
        /// Értékelő emailt küld egy lezárt bérléshez.
        /// </summary>
        public void SendReviewEmail(Rental rental)
        {
            if (rental.Customer == null)
                throw new ArgumentException("A bérléshez nincs ügyfél betöltve!");

            string emailBody = BuildReviewEmailBody(rental);

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_setting.SenderName, _setting.SenderEmail));
            message.To.Add(new MailboxAddress("", rental.Customer.Email));

            if (!string.IsNullOrWhiteSpace(_setting.CcAddress))
                message.Cc.Add(new MailboxAddress("", _setting.CcAddress));

            message.Subject = _setting.ReviewEmailSubject;
            message.Body = new TextPart("html") { Text = emailBody };

            Send(message);
        }

        /// <summary>
        /// Felépíti az értékelő email törzsét — template fájlból vagy alapértelmezett HTML-ből.
        /// </summary>
        private string BuildReviewEmailBody(Rental rental)
        {
            if (!string.IsNullOrWhiteSpace(_setting.ReviewEmailTemplate) &&
                SystemIO.File.Exists(_setting.ReviewEmailTemplate))
            {
                string template = SystemIO.File.ReadAllText(_setting.ReviewEmailTemplate);
                template = template.Replace("{{CUSTOMER_NAME}}", rental.Customer.Name);
                template = template.Replace("{{COMPANY_NAME}}", _setting.CompanyName);
                template = template.Replace("{{RENTAL_DATE}}", rental.RentStart.ToString("yyyy. MM. dd."));
                template = template.Replace("{{GOOGLE_REVIEW_LINK}}", _setting.GoogleReview ?? "");
                return template;
            }

            return $@"
            <html>
            <body>
                <h2>Kedves {rental.Customer.Name}!</h2>
                <p>Köszönjük, hogy választotta a {_setting.CompanyName} szolgáltatásait!</p>
                <p>Kérjük, segítsen nekünk a szolgáltatásunk fejlesztésében egy rövid értékeléssel!</p>
                {(!string.IsNullOrWhiteSpace(_setting.GoogleReview)
                    ? $"<p><a href='{_setting.GoogleReview}'>Értékelés írása itt</a></p>"
                    : "")}
                <p>Köszönjük!</p>
                <br>
                <p>Üdvözlettel,<br>{_setting.CompanyName}</p>
            </body>
            </html>";
        }

        // ===========================================
        // BELSŐ SMTP KÜLDÉS
        // ===========================================

        /// <summary>
        /// SMTP kapcsolódás, autentikáció és küldés — minden email típus ezt használja.
        /// </summary>
        private void Send(MimeMessage message)
        {
            var socketOptions = _setting.SmtpPort == 465
                ? MailKit.Security.SecureSocketOptions.SslOnConnect
                : MailKit.Security.SecureSocketOptions.StartTls;

            AppLogger.Logger.Debug("SMTP kapcsolódás: {Server}:{Port}", _setting.EmailSmtp, _setting.SmtpPort);

            using var client = new SmtpClient();

            try
            {
                client.Connect(_setting.EmailSmtp, _setting.SmtpPort, socketOptions);

                AppLogger.Logger.Debug("SMTP autentikáció: {Email}", _setting.SenderEmail);
                string emailPassword;
                try
                {
                    emailPassword = CredentialProtection.Unprotect(_setting.EmailPassword);
                }
                catch (System.Security.Cryptography.CryptographicException ex)
                {
                    AppLogger.Logger.Error(ex, "DPAPI email jelszó visszafejtés sikertelen az email küldéskor");
                    throw new Exception("Az email jelszó visszafejtése sikertelen (DPAPI hiba). Kérlek add meg újra a jelszót a Beállításokban.");
                }
                client.Authenticate(_setting.SenderEmail, emailPassword);

                AppLogger.Logger.Debug("Email küldése...");
                client.Send(message);
                client.Disconnect(true);

                AppLogger.Logger.Information("Email sikeresen elküldve: {Recipient}", message.To.ToString());
            }
            catch (Exception ex)
            {
                AppLogger.Logger.Error(ex, "SMTP küldési hiba - szerver: {Server}:{Port}, feladó: {Email}",
                    _setting.EmailSmtp, _setting.SmtpPort, _setting.SenderEmail);

                throw new Exception(
                    $"SMTP hiba részletesen:\n" +
                    $"Szerver: {_setting.EmailSmtp}:{_setting.SmtpPort}\n" +
                    $"Email: {_setting.SenderEmail}\n" +
                    $"Hiba: {ex.Message}\n" +
                    $"Típus: {ex.GetType().Name}");
            }
        }
    }
}
