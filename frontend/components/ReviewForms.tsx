"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { API_BASE, categoryLabels, membershipCategoryLabels, studioCategoryLabels, type Ratings } from "@/lib/api";
import { sendEvent } from "@/components/Analytics";

function RatingSelect({
  id,
  label,
  value,
  onChange,
}: {
  id: string;
  label: string;
  value: number | null;
  onChange: (value: number | null) => void;
}) {
  return (
    <label className="field" htmlFor={id}>
      {label}
      <select
        id={id}
        value={value ?? ""}
        onChange={(event) => onChange(event.target.value === "" ? null : Number(event.target.value))}
      >
        <option value="">Keine Angabe</option>
        {[1, 2, 3, 4, 5].map((n) => (
          <option key={n} value={n}>
            {n} {n === 1 ? "(schlecht)" : n === 5 ? "(super)" : ""}
          </option>
        ))}
      </select>
    </label>
  );
}

export function ReviewForm({ gymSlug }: { gymSlug: string }) {
  const router = useRouter();
  const [ratings, setRatings] = useState<Record<string, number | null>>({});
  const [text, setText] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState(false);
  const [pending, setPending] = useState(false);

  async function submit(event: React.FormEvent) {
    event.preventDefault();
    setError(null);
    setPending(true);
    try {
      const response = await fetch(`${API_BASE}/api/v1/gyms/${gymSlug}/reviews`, {
        method: "POST",
        credentials: "include",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ ratings: ratings as Ratings, text: text.trim() === "" ? null : text.trim() }),
      });
      if (response.status === 401) {
        setError("Bitte zuerst anmelden, um eine Bewertung zu schreiben.");
        return;
      }
      if (!response.ok) {
        const problem = (await response.json()) as { detail?: string };
        setError(problem.detail ?? "Die Bewertung konnte nicht gespeichert werden.");
        return;
      }
      setSuccess(true);
      sendEvent("review_created");
      router.refresh();
    } catch {
      setError("Netzwerkfehler. Bitte spaeter erneut versuchen.");
    } finally {
      setPending(false);
    }
  }

  if (success) {
    return <p className="success">Danke! Deine Bewertung wurde veroeffentlicht.</p>;
  }

  return (
    <form className="stack" onSubmit={submit} style={{ maxWidth: "100%" }}>
      <p className="muted">
        Mindestens eine Kategorie mit 1-5 bewerten. Bewertungen sind nicht anonym und erscheinen mit deinem
        Anzeigenamen.
      </p>
      <h3>Mitgliedschaft</h3>
      <div className="rating-grid">
        {Object.keys(membershipCategoryLabels).map((key) => (
          <RatingSelect
            key={key}
            id={`rating-${key}`}
            label={categoryLabels[key]}
            value={ratings[key] ?? null}
            onChange={(value) => setRatings((current) => ({ ...current, [key]: value }))}
          />
        ))}
      </div>
      <h3>Studio</h3>
      <div className="rating-grid">
        {Object.keys(studioCategoryLabels).map((key) => (
          <RatingSelect
            key={key}
            id={`rating-${key}`}
            label={categoryLabels[key]}
            value={ratings[key] ?? null}
            onChange={(value) => setRatings((current) => ({ ...current, [key]: value }))}
          />
        ))}
      </div>
      <label className="field" htmlFor="review-text">
        Erfahrungsbericht (optional, max. 4000 Zeichen)
        <textarea
          id="review-text"
          rows={5}
          maxLength={4000}
          value={text}
          onChange={(event) => setText(event.target.value)}
        />
      </label>
      {error ? <p className="error">{error}</p> : null}
      <button type="submit" disabled={pending}>
        {pending ? "Wird gespeichert..." : "Bewertung veroeffentlichen"}
      </button>
    </form>
  );
}

export function ReportForm({ reviewId }: { reviewId: string }) {
  const [open, setOpen] = useState(false);
  const [state, setState] = useState({ category: "Defamation", reporterName: "", reporterEmail: "", description: "" });
  const [honeypot, setHoneypot] = useState("");
  const [result, setResult] = useState<{ caseNumber: string; statusToken: string } | null>(null);
  const [error, setError] = useState<string | null>(null);

  async function submit(event: React.FormEvent) {
    event.preventDefault();
    setError(null);
    const response = await fetch(`${API_BASE}/api/v1/reviews/${reviewId}/report`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ ...state, website: honeypot }),
    });
    if (response.status === 201) {
      setResult((await response.json()) as { caseNumber: string; statusToken: string });
      sendEvent("report_submitted");
      return;
    }
    if (response.status === 202) {
      setResult({ caseNumber: "", statusToken: "" });
      return;
    }
    const problem = (await response.json().catch(() => ({}))) as { detail?: string };
    setError(problem.detail ?? "Die Meldung konnte nicht uebermittelt werden.");
  }

  if (result) {
    return (
      <div className="notice">
        Meldung eingegangen{result.caseNumber ? ` - Fallnummer ${result.caseNumber}` : ""}.
        {result.statusToken ? (
          <>
            {" "}
            Status abrufbar unter{" "}
            <a href={`/rechtliches/fall/${result.caseNumber}?token=${result.statusToken}`}>diesem vertraulichen Link</a>
            . Bitte Link sicher aufbewahren.
          </>
        ) : null}
      </div>
    );
  }

  if (!open) {
    return (
      <button type="button" className="secondary" onClick={() => setOpen(true)}>
        Bewertung melden
      </button>
    );
  }

  return (
    <form className="stack" onSubmit={submit}>
      <label className="field" htmlFor={`cat-${reviewId}`}>
        Grund der Meldung
        <select
          id={`cat-${reviewId}`}
          value={state.category}
          onChange={(event) => setState({ ...state, category: event.target.value })}
        >
          <option value="Defamation">Ueble Nachrede / Kreditschaedigung</option>
          <option value="FalseFactualClaim">Falsche Tatsachenbehauptung</option>
          <option value="Insult">Beleidigung</option>
          <option value="PrivacyViolation">Verletzung der Privatsphaere</option>
          <option value="IllegalContent">Rechtswidriger Inhalt</option>
          <option value="Other">Sonstiges</option>
        </select>
      </label>
      <label className="field" htmlFor={`name-${reviewId}`}>
        Ihr Name
        <input
          id={`name-${reviewId}`}
          required
          maxLength={120}
          value={state.reporterName}
          onChange={(event) => setState({ ...state, reporterName: event.target.value })}
        />
      </label>
      <label className="field" htmlFor={`email-${reviewId}`}>
        Ihre E-Mail-Adresse
        <input
          id={`email-${reviewId}`}
          type="email"
          required
          value={state.reporterEmail}
          onChange={(event) => setState({ ...state, reporterEmail: event.target.value })}
        />
      </label>
      <label className="field" htmlFor={`desc-${reviewId}`}>
        Begruendung (mind. 20 Zeichen)
        <textarea
          id={`desc-${reviewId}`}
          required
          minLength={20}
          maxLength={4000}
          rows={4}
          value={state.description}
          onChange={(event) => setState({ ...state, description: event.target.value })}
        />
      </label>
      <div className="hp-field" aria-hidden="true">
        <label htmlFor={`website-${reviewId}`}>
          Website
          <input
            id={`website-${reviewId}`}
            tabIndex={-1}
            autoComplete="off"
            value={honeypot}
            onChange={(event) => setHoneypot(event.target.value)}
          />
        </label>
      </div>
      <p className="muted">
        Die gemeldete Bewertung bleibt waehrend der Pruefung grundsaetzlich online. Sie erhalten eine Fallnummer und
        werden ueber die Entscheidung informiert.
      </p>
      {error ? <p className="error">{error}</p> : null}
      <button type="submit">Meldung absenden</button>
    </form>
  );
}
