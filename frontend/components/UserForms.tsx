"use client";

import { useEffect, useState } from "react";
import { API_BASE, type Me } from "@/lib/api";
import { sendEvent } from "@/components/Analytics";

export function ContactForm({ gymSlug }: { gymSlug?: string }) {
  const [state, setState] = useState({ type: "General", name: "", email: "", message: "" });
  const [honeypot, setHoneypot] = useState("");
  const [done, setDone] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function submit(event: React.FormEvent) {
    event.preventDefault();
    setError(null);
    const response = await fetch(`${API_BASE}/api/v1/contact-requests`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ ...state, gymSlug: gymSlug ?? null, website: honeypot }),
    });
    if (response.ok) {
      setDone(true);
      sendEvent("contact_submitted");
      return;
    }
    const problem = (await response.json().catch(() => ({}))) as { detail?: string };
    setError(problem.detail ?? "Die Anfrage konnte nicht gesendet werden.");
  }

  if (done) {
    return <p className="success">Danke! Ihre Anfrage ist eingegangen. Sie erhalten eine Bestaetigung per E-Mail.</p>;
  }

  return (
    <form className="stack" onSubmit={submit}>
      <label className="field" htmlFor="contact-type">
        Art der Anfrage
        <select
          id="contact-type"
          value={state.type}
          onChange={(event) => setState({ ...state, type: event.target.value })}
        >
          <option value="General">Allgemeine Anfrage</option>
          <option value="GymSuggestion">Studio vorschlagen</option>
          <option value="DataCorrection">Daten korrigieren</option>
        </select>
      </label>
      <label className="field" htmlFor="contact-name">
        Name
        <input
          id="contact-name"
          required
          maxLength={120}
          value={state.name}
          onChange={(event) => setState({ ...state, name: event.target.value })}
        />
      </label>
      <label className="field" htmlFor="contact-email">
        E-Mail-Adresse
        <input
          id="contact-email"
          type="email"
          required
          value={state.email}
          onChange={(event) => setState({ ...state, email: event.target.value })}
        />
      </label>
      <label className="field" htmlFor="contact-message">
        Nachricht (mind. 10 Zeichen)
        <textarea
          id="contact-message"
          required
          minLength={10}
          maxLength={4000}
          rows={5}
          value={state.message}
          onChange={(event) => setState({ ...state, message: event.target.value })}
        />
      </label>
      <div className="hp-field" aria-hidden="true">
        <label htmlFor="contact-website">
          Website
          <input
            id="contact-website"
            tabIndex={-1}
            autoComplete="off"
            value={honeypot}
            onChange={(event) => setHoneypot(event.target.value)}
          />
        </label>
      </div>
      {error ? <p className="error">{error}</p> : null}
      <button type="submit">Anfrage senden</button>
    </form>
  );
}

export function useMe(): { me: Me | null; loading: boolean; reload: () => void } {
  const [me, setMe] = useState<Me | null>(null);
  const [loading, setLoading] = useState(true);
  const [tick, setTick] = useState(0);

  useEffect(() => {
    let cancelled = false;
    void (async () => {
      try {
        const response = await fetch(`${API_BASE}/api/v1/me`, { credentials: "include" });
        if (!cancelled) {
          setMe(response.ok ? ((await response.json()) as Me) : null);
        }
      } catch {
        if (!cancelled) {
          setMe(null);
        }
      } finally {
        if (!cancelled) {
          setLoading(false);
        }
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [tick]);

  return { me, loading, reload: () => setTick((t) => t + 1) };
}

export function LoginPanel({ me, reload }: { me: Me | null; reload: () => void }) {
  const [devEmail, setDevEmail] = useState("");
  const [devName, setDevName] = useState("");
  const [error, setError] = useState<string | null>(null);

  if (me) {
    return (
      <div className="card">
        <p>
          Angemeldet als <strong>{me.displayName}</strong> ({me.email}) - Rolle: {me.role}
          {me.emailVerified ? <span className="badge">Verifiziert ueber Google</span> : null}
        </p>
        <button
          type="button"
          className="secondary"
          onClick={async () => {
            await fetch(`${API_BASE}/api/v1/auth/logout`, { method: "POST", credentials: "include" });
            reload();
          }}
        >
          Abmelden
        </button>
      </div>
    );
  }

  async function devLogin(event: React.FormEvent) {
    event.preventDefault();
    setError(null);
    const response = await fetch(`${API_BASE}/api/v1/auth/dev-login`, {
      method: "POST",
      credentials: "include",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ email: devEmail, displayName: devName || devEmail }),
    });
    if (response.ok) {
      reload();
      return;
    }
    setError("Dev-Login ist in dieser Umgebung nicht verfuegbar.");
  }

  return (
    <div className="card">
      <p>
        <a href={`${API_BASE}/api/v1/auth/google/start?returnUrl=${encodeURIComponent(typeof window === "undefined" ? "/" : window.location.href)}`}>
          <button type="button">Mit Google anmelden</button>
        </a>
      </p>
      <details>
        <summary className="muted">Lokaler Dev-Login (nur Entwicklung)</summary>
        <form className="stack" onSubmit={devLogin} style={{ marginTop: "0.6rem" }}>
          <label className="field" htmlFor="dev-email">
            E-Mail
            <input id="dev-email" type="email" required value={devEmail} onChange={(e) => setDevEmail(e.target.value)} />
          </label>
          <label className="field" htmlFor="dev-name">
            Anzeigename
            <input id="dev-name" value={devName} onChange={(e) => setDevName(e.target.value)} />
          </label>
          {error ? <p className="error">{error}</p> : null}
          <button type="submit" className="secondary">
            Dev-Login
          </button>
        </form>
      </details>
    </div>
  );
}
