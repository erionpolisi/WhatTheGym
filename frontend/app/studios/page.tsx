import type { Metadata } from "next";
import Link from "next/link";
import {
  apiGet,
  scoreBasisLabels,
  trainingTypeLabels,
  type ChainSearchItem,
  type GymListItem,
  type PagedResult,
} from "@/lib/api";
import { GymCard, ScorePill } from "@/components/Scores";

export const metadata: Metadata = {
  title: "Studios durchsuchen",
  description: "Fitnessketten und Studios in Wien nach Bezirk und Bewertung filtern.",
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

function sharedParams(searchParams: SearchParams): URLSearchParams {
  const params = new URLSearchParams();
  if (searchParams.term) params.set("term", searchParams.term);
  if (searchParams.district) params.set("district", searchParams.district);
  if (searchParams.minTotalScore) params.set("minTotalScore", searchParams.minTotalScore);
  return params;
}

function FilterForm({ searchParams, chainMode }: { searchParams: SearchParams; chainMode: boolean }) {
  return (
    <form className="filters" method="get">
      {searchParams.chain ? <input type="hidden" name="chain" value={searchParams.chain} /> : null}
      <label className="field" htmlFor="term">
        Suche
        <input
          id="term"
          name="term"
          defaultValue={searchParams.term ?? ""}
          placeholder="Kette, Studio oder Adresse"
        />
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
      <label className="field" htmlFor="minTotalScore">
        Mindestbewertung
        <select id="minTotalScore" name="minTotalScore" defaultValue={searchParams.minTotalScore ?? ""}>
          <option value="">Egal</option>
          <option value="4">ab 4,0</option>
          <option value="3">ab 3,0</option>
          <option value="2">ab 2,0</option>
        </select>
      </label>
      {!chainMode ? (
        <label className="field" htmlFor="sort">
          Sortierung
          <select id="sort" name="sort" defaultValue={searchParams.sort ?? "score"}>
            <option value="score">Beste Bewertung</option>
            <option value="name">Name</option>
            <option value="newest">Neueste</option>
          </select>
        </label>
      ) : null}
      <button type="submit">Filtern</button>
    </form>
  );
}

function ChainCard({ chain, searchParams }: { chain: ChainSearchItem; searchParams: SearchParams }) {
  const params = sharedParams(searchParams);
  params.set("chain", chain.slug);
  return (
    <Link href={`/studios?${params.toString()}`} className="card-link">
      <div className="card">
        <div className="card-top">
          <h3>{chain.name}</h3>
          <ScorePill score={chain.averageScore} />
        </div>
        <div className="chain-card-count">
          <strong>{chain.gymCount}</strong> {chain.gymCount === 1 ? "Studio" : "Studios"}
          {chain.reviewCount > 0
            ? ` · ${chain.reviewCount === 1 ? "1 Bewertung" : `${chain.reviewCount} Bewertungen`}`
            : " · noch keine Bewertungen"}
        </div>
        <div className="chips">
          {chain.targetAudience === "WomenOnly" ? <span className="badge women">Nur für Frauen</span> : null}
          {chain.trainingTypes.slice(0, 4).map((t) => (
            <span key={t} className="chip small">
              {trainingTypeLabels[t] ?? t}
            </span>
          ))}
          {chain.trainingTypes.length > 4 ? <span className="chip small">+{chain.trainingTypes.length - 4}</span> : null}
        </div>
      </div>
    </Link>
  );
}

async function ChainDirectory({ searchParams }: { searchParams: SearchParams }) {
  const params = sharedParams(searchParams);
  const chains = await apiGet<ChainSearchItem[]>(`/api/v1/chains/search?${params.toString()}`, 30);
  const totalGyms = (chains ?? []).reduce((sum, c) => sum + c.gymCount, 0);

  return (
    <div>
      <h1>Studios in Wien</h1>
      <FilterForm searchParams={searchParams} chainMode />
      <p className="muted">
        {chains?.length ?? 0} Ketten mit {totalGyms} Studios gefunden. Klick auf eine Kette, um ihre Studios zu
        sehen — deine Filter bleiben aktiv.
      </p>
      <div className="chain-grid">
        {(chains ?? []).map((chain) => (
          <ChainCard key={chain.id} chain={chain} searchParams={searchParams} />
        ))}
      </div>
      {(chains ?? []).length === 0 ? (
        <p className="muted">Keine Treffer. Tipp: Suchbegriff kürzen oder Filter zurücksetzen.</p>
      ) : null}
    </div>
  );
}

async function GymList({ searchParams }: { searchParams: SearchParams }) {
  const params = sharedParams(searchParams);
  params.set("chain", searchParams.chain!);
  params.set("sort", searchParams.sort ?? "score");
  params.set("page", searchParams.page ?? "1");
  params.set("pageSize", "20");

  const [result, chains] = await Promise.all([
    apiGet<PagedResult<GymListItem>>(`/api/v1/gyms?${params.toString()}`, 30),
    apiGet<{ id: string; name: string; slug: string }[]>("/api/v1/chains", 300),
  ]);

  const chainName = chains?.find((c) => c.slug === searchParams.chain)?.name ?? searchParams.chain;
  const page = Number(searchParams.page ?? "1");
  const totalPages = result?.totalPages ?? 1;
  const backParams = sharedParams(searchParams);

  function pageLink(target: number): string {
    const p = sharedParams(searchParams);
    p.set("chain", searchParams.chain!);
    if (searchParams.sort) p.set("sort", searchParams.sort);
    p.set("page", String(target));
    return `?${p.toString()}`;
  }

  return (
    <div>
      <Link className="back-link" href={`/studios${backParams.size > 0 ? `?${backParams.toString()}` : ""}`}>
        ← Alle Ketten
      </Link>
      <h1>{chainName}</h1>
      <FilterForm searchParams={searchParams} chainMode={false} />
      <p className="muted">
        {result?.totalCount ?? 0} {result?.totalCount === 1 ? "Studio" : "Studios"} gefunden. Gesamtnote = 50/50 aus
        Mitgliedschaft und Studio, sofern beide Bereiche bewertet sind ({scoreBasisLabels.both}).
      </p>
      <div className="gym-grid">
        {(result?.items ?? []).map((gym) => (
          <GymCard key={gym.id} gym={gym} />
        ))}
      </div>
      {totalPages > 1 ? (
        <nav className="pagination" aria-label="Seitennavigation">
          {page > 1 ? <a href={pageLink(page - 1)}>Zurück</a> : null}
          <span>
            Seite {page} von {totalPages}
          </span>
          {page < totalPages ? <a href={pageLink(page + 1)}>Weiter</a> : null}
        </nav>
      ) : null}
    </div>
  );
}

export default async function StudiosPage({ searchParams }: { searchParams: SearchParams }) {
  if (searchParams.chain) {
    return <GymList searchParams={searchParams} />;
  }
  return <ChainDirectory searchParams={searchParams} />;
}
