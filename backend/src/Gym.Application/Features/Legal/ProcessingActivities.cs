namespace Gym.Application.Features.Legal;

public sealed record ProcessingActivityDto(
    string Name,
    string Purpose,
    string LegalBasis,
    IReadOnlyList<string> DataCategories,
    IReadOnlyList<string> Entities,
    string Retention,
    IReadOnlyList<string> Recipients);

/// <summary>
/// Record of processing activities (GDPR Art. 30). Every entity that stores values logically
/// linkable to a person must be listed here; a test enforces the coverage.
/// Marked as draft: ENTWURF - anwaltlich pruefen lassen.
/// </summary>
public static class ProcessingActivitiesRecord
{
    public const string Version = "1.0";

    public static readonly IReadOnlyList<ProcessingActivityDto> Activities =
    [
        new(
            "Kontoverwaltung",
            "Registrierung und Anmeldung ueber Google, Verwaltung von Rollen und Profil.",
            "Art. 6 Abs. 1 lit. b DSGVO (Vertrag/Nutzungsverhaeltnis)",
            ["Google-Subject", "E-Mail-Adresse", "Anzeigename", "Verifizierungsstatus", "Login-Zeitpunkte"],
            ["User", "RefreshToken"],
            "Bis zur Kontoloeschung; danach Anonymisierung. Refresh-Tokens rotieren und verfallen nach 30 Tagen.",
            ["Google LLC (Authentifizierung)"]),
        new(
            "Bewertungen",
            "Veroeffentlichung nicht-anonymer Studio-Bewertungen inklusive Bearbeitungshistorie.",
            "Art. 6 Abs. 1 lit. b und f DSGVO",
            ["Nutzer-ID", "Bewertungsinhalte", "Kategoriebewertungen", "Zeitstempel", "Bearbeitungsverlauf"],
            ["Review", "ReviewRevision"],
            "Bewertungen bis zur Loeschung durch Nutzer/Moderation; Revisionsdaten 3 Jahre nach Entfernung der Bewertung, sofern kein Legal Hold besteht.",
            []),
        new(
            "Rechtsfaelle und Meldungen",
            "Bearbeitung von Meldungen zu Bewertungen, Entscheidungen, Einsprueche und revisionssichere Falldokumentation.",
            "Art. 6 Abs. 1 lit. c und f DSGVO (rechtliche Verpflichtungen, Rechtsverteidigung)",
            ["Name und E-Mail der meldenden Person", "Fallbegruendung", "Entscheidungen", "Einspruchstexte", "Audit-Ereignisse", "Benachrichtigungstexte"],
            ["LegalCase", "LegalCaseAppeal", "LegalCaseEvent", "LegalHold"],
            "Fall-Audit-Ereignisse 7 Jahre nach Fallabschluss; Einsprueche mindestens 6 Monate nach Entscheidung; Legal Holds pausieren Loeschung.",
            []),
        new(
            "Kontaktanfragen",
            "Bearbeitung allgemeiner Anfragen, Studio-Vorschlaege und Datenkorrekturen.",
            "Art. 6 Abs. 1 lit. b und f DSGVO",
            ["Name", "E-Mail-Adresse", "Nachrichteninhalt"],
            ["ContactRequest"],
            "Bis zur Erledigung zuzueglich angemessener Nachweisfrist.",
            []),
        new(
            "Transaktionale E-Mails",
            "Versand rechtlich erforderlicher Benachrichtigungen ueber einen persistenten Postausgang.",
            "Art. 6 Abs. 1 lit. c und f DSGVO",
            ["Empfaenger-E-Mail", "Betreff", "Nachrichtentext"],
            ["OutboxEmail"],
            "Versandte/fehlgeschlagene Mails werden nach 90 Tagen geloescht; rechtlich relevante Texte bleiben im Fall-Audit.",
            ["Resend Inc. (E-Mail-Versand)"]),
        new(
            "Reichweitenmessung (PII-frei)",
            "Anonyme Nutzungsstatistik ohne IP-Speicherung und ohne Fingerprinting.",
            "Art. 6 Abs. 1 lit. f DSGVO",
            ["Ereignistyp (Allowlist)", "Pfad", "kurzlebiger rotierender Session-Hash"],
            ["AnalyticsEvent"],
            "Rohereignisse maximal 400 Tage.",
            []),
    ];

    /// <summary>Entities that persist personal data; used by tests to enforce record coverage.</summary>
    public static readonly IReadOnlyList<string> PersonalDataEntities =
    [
        "User",
        "RefreshToken",
        "Review",
        "ReviewRevision",
        "LegalCase",
        "LegalCaseAppeal",
        "LegalCaseEvent",
        "LegalHold",
        "ContactRequest",
        "OutboxEmail",
        "AnalyticsEvent",
    ];
}
