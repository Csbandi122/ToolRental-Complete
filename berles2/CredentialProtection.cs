using System.Security.Cryptography;
using System.Text;

namespace berles2
{
    /// <summary>
    /// Windows DPAPI-alapú jelszótitkosítás az appsettings.json és az adatbázis
    /// által tárolt jelszavakhoz. A titkosítás az aktuális Windows felhasználóhoz
    /// kötött – más gépen vagy más felhasználóval nem fejthető vissza.
    /// </summary>
    internal static class CredentialProtection
    {
        private const string EncryptedPrefix = "ENC:";

        // Opcionális entropy – alkalmazásspecifikus "só", megnehezíti a brute force-t
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("ToolRental.berles2.v1");

        /// <summary>
        /// Titkosítja a jelszót Windows DPAPI-val (CurrentUser scope).
        /// Visszaadott string: "ENC:" + Base64(titkosított adatok)
        /// </summary>
        public static string Protect(string plainText)
        {
            if (string.IsNullOrEmpty(plainText)) return plainText;

            var bytes = Encoding.UTF8.GetBytes(plainText);
            var encrypted = ProtectedData.Protect(bytes, Entropy, DataProtectionScope.CurrentUser);
            return EncryptedPrefix + Convert.ToBase64String(encrypted);
        }

        /// <summary>
        /// Visszafejti a jelszót. Ha nem titkosított (régi adat), változatlanul adja vissza.
        /// </summary>
        public static string Unprotect(string value)
        {
            if (string.IsNullOrEmpty(value)) return value;
            if (!value.StartsWith(EncryptedPrefix)) return value; // visszamenőleges kompatibilitás

            try
            {
                var encrypted = Convert.FromBase64String(value[EncryptedPrefix.Length..]);
                var decrypted = ProtectedData.Unprotect(encrypted, Entropy, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(decrypted);
            }
            catch
            {
                // Ha a kulcs megváltozott (pl. Windows újratelepítés), inkább üres string,
                // mint crash alkalmazás indításkor
                return string.Empty;
            }
        }

        public static bool IsProtected(string value) =>
            !string.IsNullOrEmpty(value) && value.StartsWith(EncryptedPrefix);
    }
}
