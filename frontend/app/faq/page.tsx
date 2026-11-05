import type { Metadata } from "next";
import Link from "next/link";

export const metadata: Metadata = {
  title: "FAQ",
  description: "Häufige Fragen zu WhatTheGym: Bewertungen, Gesamtnote, Verifizierung, Meldungen und Datenschutz.",
};

const faqs: { q: string; a: React.ReactNode }[] = [
  {
    q: "Wie entsteht die Gesamtnote eines Studios?",
    a: (
      <>
        Die Gesamtnote ist der 50/50-Durchschnitt aus den Bereichen <strong>Mitgliedschaft</strong> und{" "}
        <strong>Studio</strong> — sofern beide bewertet wurden. Gibt es nur einen Bereich, zählt nur dieser (das
        steht dann transparent dabei). Fehlende Kategorien werden nie als Null gewertet.
      </>
    ),
  },
  {
    q: "Was bedeutet \u201EVerifiziert über Google\u201C?",
    a: (
      <>
        Die Person hat sich mit einem Google-Konto mit bestätigter E-Mail-Adresse angemeldet. Das belegt die
        Identität des Kontos, aber <strong>nicht</strong>, dass die Person tatsächlich Mitglied im Studio ist.
      </>
    ),
  },
  {
    q: "Kann ich anonym bewerten?",
    a: (
      <>
        Nein. Jede Bewertung ist mit einem Google-Konto verknüpft und zeigt den Anzeigenamen. Das hält die Qualität
        hoch und schützt Studios vor Fake-Bewertungen.
      </>
    ),
  },
  {
    q: "Wie melde ich eine problematische Bewertung?",
    a: (
      <>
        Unter jeder Bewertung gibt es einen Melde-Link. Jede Meldung wird als Fall dokumentiert und geprüft.
        Gemeldete Inhalte bleiben bis zur Entscheidung online, außer sie sind offensichtlich rechtswidrig.
      </>
    ),
  },
  {
    q: "Warum zeigt ihr keine Preise an?",
    a: (
      <>
        Preise ändern sich ständig und sind oft verhandelbar oder aktionsabhängig. Statt veralteter Zahlen bewerten
        unsere Mitglieder die <strong>Preis-Leistung</strong> — das ist ehrlicher.
      </>
    ),
  },
  {
    q: "Wie funktioniert der Gym-Finder?",
    a: (
      <>
        Du beantwortest ein paar Fragen (Bezirke, Trainingsarten, Ausstattung, …) und erhältst die drei passendsten
        Studios inklusive Begründung. Probier ihn <Link href="/empfehlung">hier</Link> aus.
      </>
    ),
  },
  {
    q: "Mein Studio fehlt oder hat falsche Daten - was tun?",
    a: (
      <>
        Schick uns die Korrektur über das <Link href="/kontakt">Kontaktformular</Link>. Unser Team prüft jede
        Einsendung, bevor sie übernommen wird.
      </>
    ),
  },
  {
    q: "Was passiert mit meinen Daten, wenn ich mein Konto lösche?",
    a: (
      <>
        Deine Daten werden gemäß unserer <Link href="/rechtliches/datenschutz">Datenschutzerklärung</Link>{" "}
        anonymisiert bzw. eingeschränkt. Details zu Aufbewahrungsfristen stehen dort.
      </>
    ),
  },
];

export default function FaqPage() {
  return (
    <div className="prose">
      <span className="eyebrow">FAQ</span>
      <h1>Häufige Fragen</h1>
      {faqs.map((faq) => (
        <details key={faq.q}>
          <summary>
            <strong>{faq.q}</strong>
          </summary>
          <p>{faq.a}</p>
        </details>
      ))}
      <p className="muted">
        Deine Frage ist nicht dabei? Schreib uns über das <Link href="/kontakt">Kontaktformular</Link>.
      </p>
    </div>
  );
}
