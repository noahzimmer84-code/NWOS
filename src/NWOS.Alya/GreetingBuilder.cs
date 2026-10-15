namespace NWOS.Alya;

/// <summary>
/// Alyas Ton/Ausführlichkeit ist konfigurierbar (Architekturdokument
/// Abschnitt 10: "Sie soll nicht einfach eine starre Persönlichkeit
/// besitzen."). Bewusst als kleine, geschlossene Aufzählung statt Freitext-
/// Persönlichkeitsprompt - macht das Verhalten vorhersagbar und testbar.
/// </summary>
public enum AlyaTone
{
    Sachlich,
    Freundlich,
}

public sealed class GreetingBuilder
{
    /// <summary>Baut eine tageszeitabhängige Begrüßung (echte Systemzeit, kein Platzhaltertext).</summary>
    public string BuildGreeting(AlyaTone tone, DateTimeOffset? now = null)
    {
        var hour = (now ?? DateTimeOffset.Now).Hour;
        var timeOfDayGreeting = hour switch
        {
            >= 5 and < 11 => "Guten Morgen",
            >= 11 and < 18 => "Guten Tag",
            >= 18 and < 23 => "Guten Abend",
            _ => "Hallo",
        };

        return tone switch
        {
            AlyaTone.Freundlich => $"{timeOfDayGreeting}! Schön, dass du wieder da bist.",
            _ => $"{timeOfDayGreeting}.",
        };
    }
}
