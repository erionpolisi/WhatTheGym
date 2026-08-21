import Link from "next/link";
import {
  categoryLabels,
  membershipCategoryLabels,
  scoreBasisLabels,
  type GymListItem,
  type ScoreSummary,
} from "@/lib/api";

export function ScorePill({ score, basis }: { score: number | null; basis?: string }) {
  if (score === null) {
    return <span className="score-pill none">-</span>;
  }
  return (
    <span className="score-pill" title={basis ? `Basis: ${scoreBasisLabels[basis] ?? basis}` : undefined}>
      {score.toFixed(2).replace(".", ",")} / 5
    </span>
  );
}

export function GymCard({ gym }: { gym: GymListItem }) {
  return (
    <div className="card">
      <h3>
        <Link href={`/studios/${gym.slug}`}>{gym.name}</Link> <ScorePill score={gym.totalScore} basis={gym.scoreBasis} />
      </h3>
      <div className="muted">
        {gym.postalCode} Wien, {gym.district}. Bezirk - {gym.addressLine}
        {gym.chainName ? ` - Kette: ${gym.chainName}` : ""}
      </div>
      <div className="muted">
        {gym.reviewCount === 1 ? "1 Bewertung" : `${gym.reviewCount} Bewertungen`}
        {gym.scoreBasis !== "none" ? ` - Basis: ${scoreBasisLabels[gym.scoreBasis] ?? gym.scoreBasis}` : ""}
      </div>
    </div>
  );
}

export function ScoreBreakdown({ score }: { score: ScoreSummary }) {
  return (
    <div>
      <p>
        Gesamt: <ScorePill score={score.totalScore} basis={score.scoreBasis} />{" "}
        <span className="muted">
          ({scoreBasisLabels[score.scoreBasis]}, {score.reviewCount}{" "}
          {score.reviewCount === 1 ? "Bewertung" : "Bewertungen"})
        </span>
      </p>
      <table className="scores">
        <thead>
          <tr>
            <th>Kategorie</th>
            <th>Bereich</th>
            <th>Durchschnitt</th>
            <th>Anzahl</th>
          </tr>
        </thead>
        <tbody>
          <tr>
            <td>
              <strong>Mitgliedschaft</strong>
            </td>
            <td>membership</td>
            <td>{score.membershipScore !== null ? score.membershipScore.toFixed(2) : "-"}</td>
            <td />
          </tr>
          <tr>
            <td>
              <strong>Studio</strong>
            </td>
            <td>studio</td>
            <td>{score.studioScore !== null ? score.studioScore.toFixed(2) : "-"}</td>
            <td />
          </tr>
          {score.categories.map((category) => (
            <tr key={category.category}>
              <td>{categoryLabels[category.category] ?? category.category}</td>
              <td>{category.category in membershipCategoryLabels ? "membership" : "studio"}</td>
              <td>{category.average !== null ? category.average.toFixed(2) : "-"}</td>
              <td>{category.ratingCount > 0 ? category.ratingCount : "-"}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
