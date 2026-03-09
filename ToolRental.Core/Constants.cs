namespace ToolRental.Core
{
    /// <summary>
    /// Pénzügyi bejegyzés típusa (bevétel vagy költség).
    /// </summary>
    public static class EntryTypes
    {
        public const string Bevetel = "bevétel";
        public const string Koltseg = "költség";
    }

    /// <summary>
    /// Pénzügyi bejegyzés forrása (honnan származik a bevétel/költség).
    /// </summary>
    public static class SourceTypes
    {
        public const string Berles = "bérlés";
        public const string Szerviz = "szervíz";
        public const string EszkozVasarlas = "eszköz_vásárlás";
        public const string Marketing = "marketing";
        public const string Egyeb = "egyéb";
    }

    /// <summary>
    /// Szerviz munka típusa.
    /// </summary>
    public static class ServiceTypes
    {
        public const string Karbantartas = "karbantartás";
        public const string Javitas = "javítás";
        public const string Upgrade = "upgrade";
    }
}
