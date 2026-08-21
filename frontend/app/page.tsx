import Link from "next/link";
import { apiGet, type GymListItem, type PagedResult } from "@/lib/api";
import { GymCard } from "@/components/Scores";

export const revalidate = 120;

export default async function HomePage() {
  const top = await apiGet<PagedResult<GymListItem>>("/api/v1/gyms?sort=score&pageSize=5", 120);

  return (
    <div>
      <h1>Fitnessstudios in Wien - ehrlich bewertet</h1>
      <p>
        WhatTheGym sammelt verifizierte Bewertungen zu Wiener Fitnessstudios: getrennt nach{" "}
        <strong>Mitgliedschaft</strong> (Preis-Leistung, Vertrag, Abrechnung, Kuendigung) und{" "}
        <strong>Studio</strong> (Geraete, Sauberkeit, Personal, Auslastung, Umkleiden, Duschen, Atmosphaere).
      </p>
      <p>
        <Link href="/studios">
          <button type="button">Alle Studios durchsuchen</button>
        </Link>
      </p>
      <h2>Bestbewertete Studios</h2>
      {top && top.items.length > 0 ? (
        top.items.map((gym) => <GymCard key={gym.id} gym={gym} />)
      ) : (
        <p className="muted">Noch keine Studios vorhanden.</p>
      )}
    </div>
  );
}
