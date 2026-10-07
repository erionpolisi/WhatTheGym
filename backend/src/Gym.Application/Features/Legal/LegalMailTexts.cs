namespace Gym.Application.Features.Legal;

/// <summary>
/// German notification texts. All legal copy is marked as draft:
/// ENTWURF - anwaltlich prüfen lassen.
/// </summary>
public static class LegalMailTexts
{
    public const string DraftMarker = "ENTWURF - anwaltlich prüfen lassen";

    public static (string Subject, string Body) ReportReceived(string caseNumber, string statusUrl) => (
        $"[WhatTheGym] Meldung eingegangen - Fall {caseNumber}",
        $"""
        Guten Tag,

        Ihre Meldung wurde unter der Fallnummer {caseNumber} registriert.

        Die gemeldete Bewertung bleibt während der Prüfung grundsätzlich online,
        sofern kein offensichtlich rechtswidriger Inhalt vorliegt. Sie werden über
        die Entscheidung per E-Mail informiert.

        Den aktuellen Status Ihres Falls können Sie hier abrufen:
        {statusUrl}

        Bitte bewahren Sie diesen Link vertraulich auf.

        Mit freundlichen Grüßen
        WhatTheGym

        ({DraftMarker})
        """);

    public static (string Subject, string Body) ContentHiddenFastTrack(string caseNumber) => (
        $"[WhatTheGym] Ihre Bewertung wurde vorübergehend ausgeblendet - Fall {caseNumber}",
        $"""
        Guten Tag,

        Ihre Bewertung wurde aufgrund einer Meldung als offensichtlich rechtswidrig
        eingestuft und bis zur endgültigen Entscheidung vorübergehend ausgeblendet
        (Fallnummer {caseNumber}).

        Sie erhalten eine weitere Nachricht, sobald eine Entscheidung getroffen wurde.

        Mit freundlichen Grüßen
        WhatTheGym

        ({DraftMarker})
        """);

    public static (string Subject, string Body) DecisionToReporter(string caseNumber, bool removed, string statusUrl, string? appealUrl) => (
        $"[WhatTheGym] Entscheidung zu Ihrer Meldung - Fall {caseNumber}",
        $"""
        Guten Tag,

        zu Ihrer Meldung (Fallnummer {caseNumber}) wurde eine Entscheidung getroffen:

        {(removed
            ? "Die gemeldete Bewertung wurde vollständig entfernt."
            : "Die gemeldete Bewertung bleibt nach Prüfung online.")}

        Details zum Fallstatus: {statusUrl}
        {(appealUrl is null ? string.Empty : $"\nWenn Sie mit der Entscheidung nicht einverstanden sind, können Sie hier Einspruch erheben (mindestens 6 Monate möglich):\n{appealUrl}\n")}
        Mit freundlichen Grüßen
        WhatTheGym

        ({DraftMarker})
        """);

    public static (string Subject, string Body) DecisionToAuthor(string caseNumber, bool removed, string? appealUrl) => (
        $"[WhatTheGym] Entscheidung zu Ihrer Bewertung - Fall {caseNumber}",
        $"""
        Guten Tag,

        zu einer Meldung über Ihre Bewertung (Fallnummer {caseNumber}) wurde entschieden:

        {(removed
            ? "Ihre Bewertung wurde nach rechtlicher Prüfung vollständig entfernt."
            : "Ihre Bewertung bleibt nach Prüfung online.")}
        {(appealUrl is null ? string.Empty : $"\nWenn Sie mit der Entscheidung nicht einverstanden sind, können Sie hier Einspruch erheben (mindestens 6 Monate möglich):\n{appealUrl}\n")}
        Mit freundlichen Grüßen
        WhatTheGym

        ({DraftMarker})
        """);

    public static (string Subject, string Body) AppealReceived(string caseNumber) => (
        $"[WhatTheGym] Einspruch eingegangen - Fall {caseNumber}",
        $"""
        Guten Tag,

        Ihr Einspruch zum Fall {caseNumber} ist eingegangen und wird geprüft.
        Sie werden über das Ergebnis per E-Mail informiert.

        Mit freundlichen Grüßen
        WhatTheGym

        ({DraftMarker})
        """);

    public static (string Subject, string Body) AppealDecided(string caseNumber, bool reversed) => (
        $"[WhatTheGym] Entscheidung zu Ihrem Einspruch - Fall {caseNumber}",
        $"""
        Guten Tag,

        Über Ihren Einspruch zum Fall {caseNumber} wurde entschieden:

        {(reversed
            ? "Der ursprünglichen Entscheidung wurde nicht gefolgt; sie wurde aufgehoben."
            : "Die ursprüngliche Entscheidung wurde bestätigt.")}

        Mit freundlichen Grüßen
        WhatTheGym

        ({DraftMarker})
        """);

    public static (string Subject, string Body) ContactConfirmation(string name) => (
        "[WhatTheGym] Ihre Anfrage ist eingegangen",
        $"""
        Guten Tag {name},

        vielen Dank für Ihre Nachricht. Wir haben Ihre Anfrage erhalten und melden
        uns so bald wie möglich.

        Mit freundlichen Grüßen
        WhatTheGym

        ({DraftMarker})
        """);
}
