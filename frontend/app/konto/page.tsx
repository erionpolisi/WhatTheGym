"use client";

import { useState } from "react";
import { API_BASE, CSRF_HEADER, categoryLabels } from "@/lib/api";
import { LoginPanel, useMe } from "@/components/UserForms";

interface OwnReview {
  id: string;
  gymId: string;
  status: string;
  ratings: Record<string, number | null>;
  text: string | null;
  editCount: number;
  createdAtUtc: string;
}

export default function AccountPage() {
  const { me, loading, reload } = useMe();
  const [reviews, setReviews] = useState<OwnReview[] | null>(null);
  const [message, setMessage] = useState<string | null>(null);

  async function loadReviews() {
    const response = await fetch(`${API_BASE}/api/v1/me/reviews`, { credentials: "include" });
    if (response.ok) {
      setReviews((await response.json()) as OwnReview[]);
    }
  }

  async function deleteReview(reviewId: string) {
    await fetch(`${API_BASE}/api/v1/reviews/${reviewId}`, {
      method: "DELETE",
      credentials: "include",
      headers: CSRF_HEADER,
    });
    await loadReviews();
  }

  async function exportData() {
    const response = await fetch(`${API_BASE}/api/v1/me/export`, { credentials: "include" });
    if (!response.ok) {
      setMessage("Export fehlgeschlagen.");
      return;
    }
    const blob = new Blob([JSON.stringify(await response.json(), null, 2)], { type: "application/json" });
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement("a");
    anchor.href = url;
    anchor.download = "whatthegym-datenexport.json";
    anchor.click();
    URL.revokeObjectURL(url);
  }

  async function deleteAccount() {
    if (!window.confirm("Konto endgueltig loeschen? Deine Bewertungen werden entfernt und dein Konto anonymisiert.")) {
      return;
    }
    const response = await fetch(`${API_BASE}/api/v1/me`, {
      method: "DELETE",
      credentials: "include",
      headers: CSRF_HEADER,
    });
    if (response.ok) {
      setMessage("Dein Konto wurde geloescht.");
      reload();
    } else {
      setMessage("Loeschung fehlgeschlagen.");
    }
  }

  if (loading) {
    return <p className="muted">Laedt...</p>;
  }

  return (
    <div>
      <h1>Mein Konto</h1>
      <LoginPanel me={me} reload={reload} />
      {message ? <p className="notice">{message}</p> : null}
      {me ? (
        <>
          <h2>Meine Bewertungen</h2>
          <button type="button" className="secondary" onClick={loadReviews}>
            Bewertungen laden
          </button>
          {(reviews ?? []).map((review) => (
            <div className="card" key={review.id}>
              <p className="muted">
                Status: {review.status} - erstellt am {new Date(review.createdAtUtc).toLocaleDateString("de-AT")}
              </p>
              <p className="muted">
                {Object.entries(review.ratings)
                  .filter(([, value]) => typeof value === "number")
                  .map(([key, value]) => `${categoryLabels[key] ?? key}: ${value}/5`)
                  .join(" - ")}
              </p>
              {review.text ? <p>{review.text}</p> : null}
              {review.status === "Published" ? (
                <button type="button" className="danger" onClick={() => deleteReview(review.id)}>
                  Bewertung loeschen
                </button>
              ) : null}
            </div>
          ))}

          <h2>Datenschutz</h2>
          <p>
            <button type="button" className="secondary" onClick={exportData}>
              Meine Daten exportieren (JSON)
            </button>{" "}
            <button type="button" className="danger" onClick={deleteAccount}>
              Konto loeschen
            </button>
          </p>
          <p className="muted">
            Die Kontoloeschung anonymisiert dein Profil und entfernt deine Bewertungen aus der Oeffentlichkeit.
            Gesetzliche Aufbewahrungspflichten und Legal Holds bleiben unberuehrt.
          </p>
        </>
      ) : null}
    </div>
  );
}
