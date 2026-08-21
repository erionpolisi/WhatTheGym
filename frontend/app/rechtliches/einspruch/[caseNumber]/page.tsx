"use client";

import { useState } from "react";
import { useParams, useSearchParams } from "next/navigation";
import { API_BASE } from "@/lib/api";

export default function AppealPage() {
  const params = useParams<{ caseNumber: string }>();
  const searchParams = useSearchParams();
  const token = searchParams.get("token") ?? "";
  const [text, setText] = useState("");
  const [honeypot, setHoneypot] = useState("");
  const [done, setDone] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function submit(event: React.FormEvent) {
    event.preventDefault();
    setError(null);
    const response = await fetch(`${API_BASE}/api/v1/legal/cases/${encodeURIComponent(params.caseNumber)}/appeal`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ token, text, website: honeypot }),
    });
    if (response.status === 201 || response.status === 202) {
      setDone(true);
      return;
    }
    const problem = (await response.json().catch(() => ({}))) as { detail?: string };
    setError(problem.detail ?? "Der Einspruch konnte nicht uebermittelt werden.");
  }

  if (!token) {
    return (
      <div>
        <h1>Einspruch</h1>
        <p className="error">Der vertrauliche Einspruchslink ist unvollstaendig (Token fehlt).</p>
      </div>
    );
  }

  if (done) {
    return (
      <div>
        <h1>Einspruch zu Fall {params.caseNumber}</h1>
        <p className="success">
          Ihr Einspruch ist eingegangen und wird geprueft. Sie werden per E-Mail ueber das Ergebnis informiert.
        </p>
      </div>
    );
  }

  return (
    <div>
      <h1>Einspruch zu Fall {params.caseNumber}</h1>
      <p className="muted">
        Einsprueche sind mindestens sechs Monate nach der urspruenglichen Entscheidung moeglich.
      </p>
      <form className="stack" onSubmit={submit}>
        <label className="field" htmlFor="appeal-text">
          Begruendung des Einspruchs
          <textarea
            id="appeal-text"
            required
            minLength={10}
            maxLength={4000}
            rows={6}
            value={text}
            onChange={(event) => setText(event.target.value)}
          />
        </label>
        <div className="hp-field" aria-hidden="true">
          <label htmlFor="appeal-website">
            Website
            <input
              id="appeal-website"
              tabIndex={-1}
              autoComplete="off"
              value={honeypot}
              onChange={(event) => setHoneypot(event.target.value)}
            />
          </label>
        </div>
        {error ? <p className="error">{error}</p> : null}
        <button type="submit">Einspruch absenden</button>
      </form>
    </div>
  );
}
