import type { Metadata } from "next";
import Link from "next/link";

export const metadata: Metadata = {
  title: "Über uns",
  description:
    "WhatTheGym ist Wiens unabhängige Community-Plattform für ehrliche Fitnessstudio-Bewertungen - von Mitgliedern für Mitglieder.",
};

export default function UeberUnsPage() {
  return (
    <div className="prose">
      <span className="eyebrow">ÜBER UNS</span>
      <h1>Wir bringen Transparenz in Wiens Gym-Landschaft.</h1>
      <p className="lead">
        <strong>WhatTheGym</strong> ist Wiens unabhängige Community-Plattform für Fitnessstudios. Keine bezahlten
        Platzierungen, keine Provisionen — nur echte Erfahrungen von echten Mitgliedern.
      </p>

      <h2>Warum es uns gibt</h2>
      <p>
        Wer in Wien ein Gym sucht, findet Hochglanz-Werbung, aber kaum ehrliche Antworten: Wie voll ist es wirklich
        um 18 Uhr? Wie läuft die Kündigung? Sind die Duschen sauber? Genau diese Fragen beantwortet unsere
        Community — Bewertung für Bewertung.
      </p>

      <h2>Wie wir bewerten</h2>
      <p>
        Jede Bewertung deckt zwei Bereiche ab: die <strong>Mitgliedschaft</strong> (Preis-Leistung, Vertrag,
        Abrechnung, Kündigung) und das <strong>Studio</strong> (Geräte, Sauberkeit, Personal, Auslastung, Umkleiden,
        Duschen, Atmosphäre). Die Gesamtnote gewichtet beide Bereiche 50/50, sobald beide bewertet wurden — fehlende
        Daten werden nie als Null gewertet.
      </p>

      <h2>Unsere Grundsätze</h2>
      <ul>
        <li>Bewertungen sind nie anonym: Hinter jeder Stimme steht ein Google-Konto.</li>
        <li>Wir zeigen keine Preise an und ranken nicht nach Provisionen.</li>
        <li>Gemeldete Inhalte prüfen wir nach einem dokumentierten Verfahren.</li>
        <li>Studios können Einträge nicht kaufen — Korrekturen laufen über unser <Link href="/kontakt">Kontaktformular</Link>.</li>
      </ul>

      <p>
        Fragen? Schau in die <Link href="/faq">FAQ</Link> oder schreib uns über das{" "}
        <Link href="/kontakt">Kontaktformular</Link>.
      </p>
    </div>
  );
}
