namespace NWOS.ConfigSetup;

/// <summary>
/// Abstraktion für die Frage/Antwort-Interaktion des Konfigurationsformulars
/// (Abschnitt 4). Bewusst getrennt von einer konkreten UI-Technologie: für
/// den kleinsten stabilen Kern gibt es eine Konsolen-Implementierung
/// (<see cref="ConsoleUserPrompt"/>), die spätere Desktop-Shell (Phase 2)
/// implementiert dieselbe Schnittstelle mit echten Dialogfeldern, ohne dass
/// der Wizard selbst angepasst werden muss.
/// </summary>
public interface IUserPrompt
{
    Task<bool> AskYesNoAsync(string question, bool defaultValue, CancellationToken cancellationToken);
}

/// <summary>
/// Einfache, textbasierte Implementierung für Entwicklung/Server-Betrieb ohne
/// grafische Oberfläche. Kein Ersatz für das eigentliche UI-Formular aus
/// Phase 2 - dient hier ausschließlich dazu, den kleinsten stabilen Kern
/// end-to-end testbar zu machen (Abschnitt 39: Tests vor dem nächsten Modul).
/// </summary>
public sealed class ConsoleUserPrompt : IUserPrompt
{
    public Task<bool> AskYesNoAsync(string question, bool defaultValue, CancellationToken cancellationToken)
    {
        var suffix = defaultValue ? "[J/n]" : "[j/N]";
        Console.Write($"{question} {suffix}: ");
        var input = Console.ReadLine();

        if (string.IsNullOrWhiteSpace(input))
        {
            return Task.FromResult(defaultValue);
        }

        return Task.FromResult(input.Trim().Equals("j", StringComparison.OrdinalIgnoreCase)
            || input.Trim().Equals("y", StringComparison.OrdinalIgnoreCase));
    }
}

/// <summary>
/// Führt das minimale Konfigurationsformular für den kleinsten stabilen Kern
/// aus. Fragt nur nach Modulen, die in Phase 1 tatsächlich existieren
/// (Desktop-Shell-Flag als Vorbereitung für Phase 2, Alice/Marc/Alya-Flags
/// als Vorbereitung für Phase 3) - keine Fragen zu Funktionen, die noch nicht
/// implementiert sind (Abschnitt 40).
/// </summary>
public sealed class FirstRunWizard
{
    private readonly IUserPrompt _prompt;

    public FirstRunWizard(IUserPrompt prompt)
    {
        _prompt = prompt;
    }

    public async Task<NwosConfiguration> RunAsync(CancellationToken cancellationToken)
    {
        var config = new NwosConfiguration
        {
            EnableAlice = await _prompt.AskYesNoAsync(
                "Sicherheits-KI Alice aktivieren? (empfohlen)", defaultValue: true, cancellationToken),

            EnableMarc = await _prompt.AskYesNoAsync(
                "Arbeits-KI Marc aktivieren?", defaultValue: false, cancellationToken),

            EnableAlya = await _prompt.AskYesNoAsync(
                "Benutzerkontakt-KI Alya aktivieren?", defaultValue: false, cancellationToken),

            EnableMultiDevice = await _prompt.AskYesNoAsync(
                "Multi-Device-Funktion aktivieren?", defaultValue: false, cancellationToken),

            EnableMaintenance = await _prompt.AskYesNoAsync(
                "Automatische Hintergrundwartung aktivieren?", defaultValue: false, cancellationToken),
        };

        return config;
    }
}
