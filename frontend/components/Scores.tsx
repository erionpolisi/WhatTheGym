import Link from "next/link";
import {
  categoryLabels,
  membershipCategoryLabels,
  scoreBasisLabels,
  studioCategoryLabels,
  type CategoryScore,
  type GymListItem,
  type ScoreSummary,
} from "@/lib/api";

function formatScore(score: number): string {
  return score.toFixed(2).replace(".", ",");
}

/** Compact score chip: prominent numeral with a small "/5" denominator. */
export function ScorePill({ score, basis }: { score: number | null; basis?: string }) {
  if (score === null) {
    return <span className="score-pill none">–</span>;
  }
  return (
    <span className="score-pill" title={basis ? `Basis: ${scoreBasisLabels[basis] ?? basis}` : undefined}>
      {formatScore(score)}
      <span className="denom">/5</span>
    </span>
  );
}

/** Clickable gym card for list and home pages. */
export function GymCard({ gym }: { gym: GymListItem }) {
  return (
    <Link href={`/studios/${gym.slug}`} className="card-link">
      <div className="card">
        <div className="card-top">
          <h3>{gym.name}</h3>
          <ScorePill score={gym.totalScore} basis={gym.scoreBasis} />
        </div>
        <div className="muted">
          {gym.postalCode} Wien, {gym.district}. Bezirk
          {gym.chainName ? ` · ${gym.chainName}` : ""}
        </div>
        <div className="muted">
          {gym.reviewCount === 1 ? "1 Bewertung" : `${gym.reviewCount} Bewertungen`}
          {gym.scoreBasis !== "none" && gym.scoreBasis !== "both"
            ? ` · ${scoreBasisLabels[gym.scoreBasis] ?? gym.scoreBasis}`
            : ""}
        </div>
      </div>
    </Link>
  );
}

/** One horizontal category bar: label, fill proportional to the 1-5 average, numeral. */
function ScoreBar({ category }: { category: CategoryScore }) {
  const label = categoryLabels[category.category] ?? category.category;
  if (category.average === null) {
    return (
      <div className="score-bar empty">
        <span className="label">{label}</span>
        <span className="track" aria-hidden="true">
          <span className="fill" style={{ width: 0 }} />
        </span>
        <span className="value">–</span>
      </div>
    );
  }
  const percent = (category.average / 5) * 100;
  return (
    <div
      className="score-bar"
      role="img"
      aria-label={`${label}: ${formatScore(category.average)} von 5 aus ${category.ratingCount} ${
        category.ratingCount === 1 ? "Bewertung" : "Bewertungen"
      }`}
    >
      <span className="label">{label}</span>
      <span className="track" aria-hidden="true">
        <span className="fill" style={{ width: `${percent}%` }} />
      </span>
      <span className="value">
        {formatScore(category.average)} <span className="count">({category.ratingCount})</span>
      </span>
    </div>
  );
}

/** Full score presentation: giant total numeral, area split, category bars. */
export function ScoreBreakdown({ score }: { score: ScoreSummary }) {
  const membershipCategories = score.categories.filter((c) => c.category in membershipCategoryLabels);
  const studioCategories = score.categories.filter((c) => c.category in studioCategoryLabels);

  return (
    <div>
      <div className="score-hero">
        {score.totalScore !== null ? (
          <div className="score-hero-value" aria-label={`Gesamtnote ${formatScore(score.totalScore)} von 5`}>
            {formatScore(score.totalScore)}
            <span className="denom">/5</span>
          </div>
        ) : (
          <div className="score-hero-value none">Noch keine Bewertungen</div>
        )}
        <div className="score-hero-meta">
          <div className="area-score">
            <span className="label">Mitgliedschaft</span>
            {score.membershipScore !== null ? (
              <span className="value">{formatScore(score.membershipScore)}</span>
            ) : (
              <span className="value none">keine Daten</span>
            )}
          </div>
          <div className="area-score">
            <span className="label">Studio</span>
            {score.studioScore !== null ? (
              <span className="value">{formatScore(score.studioScore)}</span>
            ) : (
              <span className="value none">keine Daten</span>
            )}
          </div>
          <span className="muted">
            Basis: {scoreBasisLabels[score.scoreBasis]} ·{" "}
            {score.reviewCount === 1 ? "1 Bewertung" : `${score.reviewCount} Bewertungen`}
          </span>
        </div>
      </div>

      <div className="bar-group-title">Mitgliedschaft</div>
      <div className="score-bars">
        {membershipCategories.map((category) => (
          <ScoreBar key={category.category} category={category} />
        ))}
      </div>

      <div className="bar-group-title">Studio</div>
      <div className="score-bars">
        {studioCategories.map((category) => (
          <ScoreBar key={category.category} category={category} />
        ))}
      </div>
    </div>
  );
}
