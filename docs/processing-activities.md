# Processing activities record (working copy)

The authoritative, versioned record is generated from code
(`Gym.Application.Features.Legal.ProcessingActivitiesRecord`) and served at
`GET /api/v1/legal/processing-activities`. Tests enforce that every entity
persisting personal data is covered. This document mirrors version 1.0.

`ENTWURF - anwaltlich pruefen lassen`

| # | Activity | Purpose | Legal basis | Entities | Retention | Recipients |
|---|----------|---------|-------------|----------|-----------|------------|
| 1 | Kontoverwaltung | Google-Anmeldung, Rollen, Profil | Art. 6(1)(b) | User, RefreshToken | Bis Kontoloeschung; Tokens 30 Tage | Google LLC |
| 2 | Bewertungen | Nicht-anonyme Studio-Bewertungen inkl. Historie | Art. 6(1)(b)(f) | Review, ReviewRevision | Bis Loeschung; Revisionen 3 Jahre nach Entfernung | — |
| 3 | Rechtsfaelle | Meldungen, Entscheidungen, Einsprueche, Audit | Art. 6(1)(c)(f) | LegalCase, LegalCaseAppeal, LegalCaseEvent, LegalHold | Audit 7 Jahre nach Abschluss; Holds pausieren Loeschung | — |
| 4 | Kontaktanfragen | Anfragen, Studio-Vorschlaege, Korrekturen | Art. 6(1)(b)(f) | ContactRequest | Bis Erledigung + Nachweisfrist | — |
| 5 | Transaktionale E-Mails | Rechtlich erforderliche Benachrichtigungen | Art. 6(1)(c)(f) | OutboxEmail | 90 Tage nach Versand/Fehlschlag | Resend Inc. |
| 6 | Reichweitenmessung | PII-freie Statistik (kein IP, kein Fingerprinting) | Art. 6(1)(f) | AnalyticsEvent | Max. 400 Tage | — |

Principles enforced in code and tests:

- Every value logically linkable to a person is treated as personal data.
- Missing consent is never assumed; analytics stores no identifiers beyond a
  short-lived rotating HMAC bucket.
- Data subject rights: export (`GET /api/v1/me/export`), deletion/anonymization
  (`DELETE /api/v1/me`), information via the public record endpoint.
- Retention is configuration; active legal holds always pause deletion.
