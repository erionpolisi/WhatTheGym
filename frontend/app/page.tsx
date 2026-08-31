import Link from "next/link";
import { apiGet, type GymListItem, type PagedResult } from "@/lib/api";
import { GymCard } from "@/components/Scores";

export const revalidate = 120;

export default async function HomePage() {
  const top = await apiGet<PagedResult<GymListItem>>("/api/v1/gyms?sort=score&pageSize=6", 120);

  return (
    <div>
      <section className="hero">
        <span className="eyebrow">Wien · Verifizierte Bewertungen</span>
        <h1>
          Finde das Gym, das <em>haelt</em>, was es verspricht.
        </h1>
        <p className="lead">
          WhatTheGym sammelt ehrliche, verifizierte Bewertungen zu Wiener Fitnessstudios - getrennt nach{" "}
          <strong>Mitgliedschaft</strong> und <strong>Studio</strong>, damit du vor der Unterschrift weisst, worauf du
          dich einlaesst.
        </p>
        <div className="chips" aria-label="Bewertungskategorien">
          <span className="chip accent">Preis-Leistung</span>
          <span className="chip accent">Vertrag &amp; Kuendigung</span>
          <span className="chip">Geraete</span>
          <span className="chip">Sauberkeit</span>
          <span className="chip">Personal</span>
          <span className="chip">Auslastung</span>
          <span className="chip">Umkleiden &amp; Duschen</span>
          <span className="chip">Atmosphaere</span>
        </div>
        <Link href="/studios">
          <button type="button">Alle Studios durchsuchen</button>
        </Link>
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
