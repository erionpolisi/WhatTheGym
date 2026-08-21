namespace Gym.Application.Features.Legal;

/// <summary>
/// German notification texts. All legal copy is marked as draft:
/// ENTWURF - anwaltlich pruefen lassen.
/// </summary>
public static class LegalMailTexts
{
    public const string DraftMarker = "ENTWURF - anwaltlich pruefen lassen";

    public static (string Subject, string Body) ReportReceived(string caseNumber, string statusUrl) => (
        $"[WhatTheGym] Meldung eingegangen - Fall {caseNumber}",
        $"""
        Guten Tag,

        Ihre Meldung wurde unter der Fallnummer {caseNumber} registriert.

        Die gemeldete Bewertung bleibt waehrend der Pruefung grundsaetzlich online,
        sofern kein offensichtlich rechtswidriger Inhalt vorliegt. Sie werden ueber
        die Entscheidung per E-Mail informiert.

        Den aktuellen Status Ihres Falls koennen Sie hier abrufen:
        {statusUrl}

        Bitte bewahren Sie diesen Link vertraulich auf.

        Mit freundlichen Gruessen
        WhatTheGym

        ({DraftMarker})
        """);

    public static (string Subject, string Body) ContentHiddenFastTrack(string caseNumber) => (
        $"[WhatTheGym] Ihre Bewertung wurde voruebergehend ausgeblendet - Fall {caseNumber}",
        $"""
        Guten Tag,

        Ihre Bewertung wurde aufgrund einer Meldung als offensichtlich rechtswidrig
        eingestuft und bis zur endgueltigen Entscheidung voruebergehend ausgeblendet
        (Fallnummer {caseNumber}).

        Sie erhalten eine weitere Nachricht, sobald eine Entscheidung getroffen wurde.

        Mit freundlichen Gruessen
        WhatTheGym

        ({DraftMarker})
        """);

    public static (string Subject, string Body) DecisionToReporter(string caseNumber, bool removed, string statusUrl, string? appealUrl) => (
        $"[WhatTheGym] Entscheidung zu Ihrer Meldung - Fall {caseNumber}",
        $"""
        Guten Tag,

        zu Ihrer Meldung (Fallnummer {caseNumber}) wurde eine Entscheidung getroffen:

        {(removed
            ? "Die gemeldete Bewertung wurde vollstaendig entfernt."
            : "Die gemeldete Bewertung bleibt nach Pruefung online.")}

        Details zum Fallstatus: {statusUrl}
        {(appealUrl is null ? string.Empty : $"\nWenn Sie mit der Entscheidung nicht einverstanden sind, koennen Sie hier Einspruch erheben (mindestens 6 Monate moeglich):\n{appealUrl}\n")}
        Mit freundlichen Gruessen
        WhatTheGym

        ({DraftMarker})
        """);

    public static (string Subject, string Body) DecisionToAuthor(string caseNumber, bool removed, string? appealUrl) => (
        $"[WhatTheGym] Entscheidung zu Ihrer Bewertung - Fall {caseNumber}",
        $"""
        Guten Tag,

        zu einer Meldung ueber Ihre Bewertung (Fallnummer {caseNumber}) wurde entschieden:

        {(removed
            ? "Ihre Bewertung wurde nach rechtlicher Pruefung vollstaendig entfernt."
            : "Ihre Bewertung bleibt nach Pruefung online.")}
        {(appealUrl is null ? string.Empty : $"\nWenn Sie mit der Entscheidung nicht einverstanden sind, koennen Sie hier Einspruch erheben (mindestens 6 Monate moeglich):\n{appealUrl}\n")}
        Mit freundlichen Gruessen
        WhatTheGym

        ({DraftMarker})
        """);

    public static (string Subject, string Body) AppealReceived(string caseNumber) => (
        $"[WhatTheGym] Einspruch eingegangen - Fall {caseNumber}",
        $"""
        Guten Tag,

        Ihr Einspruch zum Fall {caseNumber} ist eingegangen und wird geprueft.
        Sie werden ueber das Ergebnis per E-Mail informiert.

        Mit freundlichen Gruessen
        WhatTheGym

        ({DraftMarker})
        """);

    public static (string Subject, string Body) AppealDecided(string caseNumber, bool reversed) => (
        $"[WhatTheGym] Entscheidung zu Ihrem Einspruch - Fall {caseNumber}",
        $"""
        Guten Tag,

        ueber Ihren Einspruch zum Fall {caseNumber} wurde entschieden:

        {(reversed
            ? "Der urspruenglichen Entscheidung wurde nicht gefolgt; sie wurde aufgehoben."
            : "Die urspruengliche Entscheidung wurde bestaetigt.")}

        Mit freundlichen Gruessen
        WhatTheGym

        ({DraftMarker})
        """);

    public static (string Subject, string Body) ContactConfirmation(string name) => (
        "[WhatTheGym] Ihre Anfrage ist eingegangen",
        $"""
        Guten Tag {name},

        vielen Dank fuer Ihre Nachricht. Wir haben Ihre Anfrage erhalten und melden
        uns so bald wie moeglich.

        Mit freundlichen Gruessen
        WhatTheGym

        ({DraftMarker})
        """);
}
