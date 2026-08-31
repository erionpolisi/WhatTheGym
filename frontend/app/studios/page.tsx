import type { Metadata } from "next";
import { apiGet, scoreBasisLabels, type GymListItem, type PagedResult } from "@/lib/api";
import { GymCard } from "@/components/Scores";

export const metadata: Metadata = {
  title: "Studios durchsuchen",
  description: "Fitnessstudios in Wien nach Bezirk, Kette und Bewertung filtern.",
};

export const revalidate = 60;

interface SearchParams {
  term?: string;
  district?: string;
  chain?: string;
  minTotalScore?: string;
  sort?: string;
  page?: string;
}

export default async function StudiosPage({ searchParams }: { searchParams: SearchParams }) {
  const params = new URLSearchParams();
  if (searchParams.term) params.set("term", searchParams.term);
  if (searchParams.district) params.set("district", searchParams.district);
  if (searchParams.chain) params.set("chain", searchParams.chain);
  if (searchParams.minTotalScore) params.set("minTotalScore", searchParams.minTotalScore);
  params.set("sort", searchParams.sort ?? "score");
  params.set("page", searchParams.page ?? "1");
  params.set("pageSize", "20");

  const [result, chains] = await Promise.all([
    apiGet<PagedResult<GymListItem>>(`/api/v1/gyms?${params.toString()}`, 30),
    apiGet<{ id: string; name: string; slug: string }[]>("/api/v1/chains", 300),
  ]);

  const page = Number(searchParams.page ?? "1");
  const totalPages = result?.totalPages ?? 1;

  return (
    <div>
      <h1>Studios in Wien</h1>
      <form className="filters" method="get">
        <label className="field" htmlFor="term">
          Suche
          <input id="term" name="term" defaultValue={searchParams.term ?? ""} placeholder="Name oder Adresse" />
        </label>
        <label className="field" htmlFor="district">
          Bezirk
          <select id="district" name="district" defaultValue={searchParams.district ?? ""}>
            <option value="">Alle</option>
            {Array.from({ length: 23 }, (_, i) => i + 1).map((d) => (
              <option key={d} value={d}>
                {d}. Bezirk
              </option>
            ))}
          </select>
        </label>
        <label className="field" htmlFor="chain">
          Kette
          <select id="chain" name="chain" defaultValue={searchParams.chain ?? ""}>
            <option value="">Alle</option>
            {(chains ?? []).map((chain) => (
              <option key={chain.id} value={chain.slug}>
                {chain.name}
              </option>
            ))}
          </select>
        </label>
        <label className="field" htmlFor="minTotalScore">
          Mindestbewertung
          <select id="minTotalScore" name="minTotalScore" defaultValue={searchParams.minTotalScore ?? ""}>
            <option value="">Egal</option>
            <option value="4">ab 4,0</option>
            <option value="3">ab 3,0</option>
            <option value="2">ab 2,0</option>
          </select>
        </label>
        <label className="field" htmlFor="sort">
          Sortierung
          <select id="sort" name="sort" defaultValue={searchParams.sort ?? "score"}>
            <option value="score">Beste Bewertung</option>
            <option value="name">Name</option>
            <option value="newest">Neueste</option>
          </select>
        </label>
        <button type="submit">Filtern</button>
      </form>

      <p className="muted">
        {result?.totalCount ?? 0} Studios gefunden. Gesamtnote = 50/50 aus Mitgliedschaft und Studio, sofern beide
        Bereiche bewertet sind ({scoreBasisLabels.both}).
      </p>

      <div className="gym-grid">
        {(result?.items ?? []).map((gym) => (
          <GymCard key={gym.id} gym={gym} />
        ))}
      </div>

      {totalPages > 1 ? (
        <nav className="pagination" aria-label="Seitennavigation">
          {page > 1 ? (
            <a href={`?${new URLSearchParams({ ...searchParams, page: String(page - 1) }).toString()}`}>Zurueck</a>
          ) : null}
          <span>
            Seite {page} von {totalPages}
          </span>
          {page < totalPages ? (
            <a href={`?${new URLSearchParams({ ...searchParams, page: String(page + 1) }).toString()}`}>Weiter</a>
          ) : null}
        </nav>
      ) : null}
    </div>
  );
}
