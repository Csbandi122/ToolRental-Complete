using System.IO;
using System.Reflection;

namespace berles2.Services
{
    /// <summary>
    /// Magyar irányítószám → településnév keresés.
    /// Az IrszHnk.csv fájlból tölt be, memóriában keres — offline, azonnali.
    /// </summary>
    internal static class ZipCodeService
    {
        // irányítószám → első találat településnév
        private static Dictionary<string, string>? _lookup;

        private static Dictionary<string, string> Lookup
        {
            get
            {
                if (_lookup == null)
                    _lookup = LoadCsv();
                return _lookup;
            }
        }

        /// <summary>
        /// Visszaadja az irányítószámhoz tartozó településnevet, vagy null-t ha nem találja.
        /// </summary>
        public static string? GetCity(string zipCode)
        {
            if (string.IsNullOrWhiteSpace(zipCode)) return null;
            return Lookup.TryGetValue(zipCode.Trim(), out string? city) ? city : null;
        }

        private static Dictionary<string, string> LoadCsv()
        {
            var result = new Dictionary<string, string>();

            try
            {
                string exeDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";
                string csvPath = Path.Combine(exeDir, "IrszHnk.csv");

                if (!File.Exists(csvPath))
                {
                    AppLogger.Logger.Warning("IrszHnk.csv nem található: {Path}", csvPath);
                    return result;
                }

                // Fejléc: Helység.megnevezése;IRSZ;...
                // Oszlop indexek: 0 = városnév, 1 = IRSZ
                foreach (string line in File.ReadLines(csvPath, System.Text.Encoding.UTF8).Skip(1))
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    string[] parts = line.Split(';');
                    if (parts.Length < 2) continue;

                    string city = parts[0].Trim();
                    string zip  = parts[1].Trim();

                    if (string.IsNullOrEmpty(zip) || string.IsNullOrEmpty(city)) continue;

                    // Csak az első találatot tartjuk meg (egy IRSZ-hez több sor is lehet)
                    result.TryAdd(zip, city);
                }

                AppLogger.Logger.Information("IrszHnk.csv betöltve: {Count} irányítószám", result.Count);
            }
            catch (Exception ex)
            {
                AppLogger.Logger.Warning(ex, "IrszHnk.csv betöltési hiba — automatikus kitöltés nem elérhető");
            }

            return result;
        }
    }
}
