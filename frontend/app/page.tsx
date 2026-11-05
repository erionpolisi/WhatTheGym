import Link from "next/link";
import { apiGet, type GymListItem, type PagedResult } from "@/lib/api";
import { GymCard } from "@/components/Scores";

export const revalidate = 120;

export default async function HomePage() {
  const top = await apiGet<PagedResult<GymListItem>>("/api/v1/gyms?sort=score&pageSize=6", 120);

  return (
    <div>
      <section className="hero">
        <span className="eyebrow">WIEN • GYM-FINDER & COMMUNITY</span>
        <h1>
          Dein Gym. <em>Deine Wahl.</em> Echte Wiener Insidertipps.
        </h1>
        <p className="lead">
          <strong>WhatTheGym</strong> ist Wiens Community-Forum für <strong>Fitnessstudios</strong>.
          Stelle spezifische Fragen an aktuelle Mitglieder, tausche Erfahrungen aus und finde das <strong>Gym</strong>, das wirklich zu dir passt.
       </p>
        <div className="chips" aria-label="Bewertungskategorien">
          <span className="chip accent">Preis-Leistung</span>
          <span className="chip accent">Vertrag &amp; Kündigung</span>
          <span className="chip">Geräte</span>
          <span className="chip">Sauberkeit</span>
          <span className="chip">Personal</span>
          <span className="chip">Auslastung</span>
          <span className="chip">Umkleiden &amp; Duschen</span>
          <span className="chip">Atmosphäre</span>
        </div>
        <div className="hero-actions">
          <Link href="/empfehlung">
            <button type="button">Probier es aus</button>
          </Link>
          <Link href="/studios">
            <button type="button" className="secondary">
              Studios durchstöbern
            </button>
          </Link>
        </div>
      </section>

      <h2>Bestbewertete Studios</h2>
      {top && top.items.length > 0 ? (
        <div className="gym-grid">
          {top.items.map((gym) => (
            <GymCard key={gym.id} gym={gym} />
          ))}
        </div>
      ) : (
        <p className="muted">Noch keine Studios vorhanden.</p>
      )}
    </div>
  );
}
