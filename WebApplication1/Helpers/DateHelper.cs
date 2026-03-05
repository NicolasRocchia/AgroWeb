namespace WebApplication1.Helpers
{
    /// <summary>
    /// Helper para convertir fechas UTC a hora Argentina (UTC-3).
    /// Usar en vistas Razor: @DateHelper.ToArg(fecha).ToString("dd/MM/yyyy HH:mm")
    /// </summary>
    public static class DateHelper
    {
        private static readonly TimeZoneInfo ArgTimeZone =
            TimeZoneInfo.FindSystemTimeZoneById("Argentina Standard Time");

        /// <summary>Convierte DateTime UTC a hora Argentina.</summary>
        public static DateTime ToArg(DateTime utcDate)
            => TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.SpecifyKind(utcDate, DateTimeKind.Utc), ArgTimeZone);

        /// <summary>Convierte DateTime? UTC a hora Argentina.</summary>
        public static DateTime? ToArg(DateTime? utcDate)
            => utcDate.HasValue ? ToArg(utcDate.Value) : null;

        /// <summary>Formatea a dd/MM/yyyy HH:mm en hora Argentina.</summary>
        public static string FormatArg(DateTime utcDate, string format = "dd/MM/yyyy HH:mm")
            => ToArg(utcDate).ToString(format);

        /// <summary>Formatea nullable a dd/MM/yyyy HH:mm en hora Argentina, o fallback.</summary>
        public static string FormatArg(DateTime? utcDate, string format = "dd/MM/yyyy HH:mm", string fallback = "—")
            => utcDate.HasValue ? ToArg(utcDate.Value).ToString(format) : fallback;
    }
}
