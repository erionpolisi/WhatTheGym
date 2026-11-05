import type { Metadata } from "next";
import { notFound } from "next/navigation";
import {
  apiGet,
  SITE_URL,
  categoryLabels,
  gymStatusLabels,
  trainingTypeLabels,
  type GymDetail,
  type PagedResult,
  type Review,
} from "@/lib/api";
import { ScoreBreakdown } from "@/components/Scores";
import { ReportForm, ReviewForm } from "@/components/ReviewForms";
import { TrackedExternalLink } from "@/components/Analytics";

export const revalidate = 60;

const dayNames = ["", "Montag", "Dienstag", "Mittwoch", "Donnerstag", "Freitag", "Samstag", "Sonntag"];

export async function generateMetadata({ params }: { params: { slug: string } }): Promise<Metadata> {
  const gym = await apiGet<GymDetail>(`/api/v1/gyms/${params.slug}`, 300);
  if (!gym) {
    return { title: "Studio nicht gefunden" };
  }
  return {
    title: `${gym.name} - Bewertungen`,
    description: `Bewertungen für ${gym.name} im ${gym.district}. Bezirk Wien: Mitgliedschaft, Geräte, Sauberkeit und mehr.`,
    alternates: { canonical: `${SITE_URL}/studios/${gym.slug}` },
  };
}

export default async function GymDetailPage({ params }: { params: { slug: string } }) {
  const gym = await apiGet<GymDetail>(`/api/v1/gyms/${params.slug}`, 60);
  if (!gym) {
    notFound();
  }

  const reviews = await apiGet<PagedResult<Review>>(`/api/v1/gyms/${params.slug}/reviews?pageSize=20`, 60);

  const jsonLd = {
    "@context": "https://schema.org",
    "@type": "ExerciseGym",
    name: gym.name,
    url: `${SITE_URL}/studios/${gym.slug}`,
    address: {
      "@type": "PostalAddress",
      streetAddress: gym.addressLine,
      postalCode: gym.postalCode,
      addressLocality: "Wien",
      addressCountry: "AT",
    },
    ...(gym.website ? { sameAs: gym.website } : {}),
    ...(gym.score.totalScore !== null
      ? {
          aggregateRating: {
            "@type": "AggregateRating",
            ratingValue: gym.score.totalScore,
            bestRating: 5,
            worstRating: 1,
            ratingCount: gym.score.reviewCount,
          },
        }
      : {}),
  };

  return (
    <div>
      <script type="application/ld+json" dangerouslySetInnerHTML={{ __html: JSON.stringify(jsonLd) }} />
      <span className="eyebrow">
        {gym.district}. Bezirk · {gym.postalCode} Wien
        {gym.chain ? ` · ${gym.chain.name}` : ""}
      </span>
      <h1>{gym.name}</h1>
      <p className="muted">{gym.addressLine}</p>
      {gym.targetAudience === "WomenOnly" || gym.status !== "Active" ? (
        <div className="chips">
          {gym.targetAudience === "WomenOnly" ? <span className="badge women">Nur für Frauen</span> : null}
          {gym.status !== "Active" ? (
            <span className="badge closed">{gymStatusLabels[gym.status] ?? gym.status}</span>
          ) : null}
        </div>
      ) : null}
      {gym.website ? (
        <p>
          <TrackedExternalLink
            href={gym.website}
            gymSlug={gym.slug}
            rel="noopener noreferrer nofollow"
            target="_blank"
          >
            Offizielle Website
          </TrackedExternalLink>
          {gym.phone ? <span className="muted"> - {gym.phone}</span> : null}
        </p>
      ) : null}
      {gym.description ? <p>{gym.description}</p> : null}

      <h2>Bewertung</h2>
      <ScoreBreakdown score={gym.score} />

      <h2>Studio-Infos</h2>
      <div className="info-panel">
        {gym.trainingTypes.length > 0 ? (
          <div className="card">
            <h3>Trainingsarten</h3>
            <div className="chips">
              {gym.trainingTypes.map((t) => (
                <span className="chip" key={t}>
                  {trainingTypeLabels[t] ?? t}
                </span>
              ))}
            </div>
          </div>
        ) : null}
        {gym.amenities.length > 0 ? (
          <div className="card">
            <h3>Ausstattung</h3>
            <div className="chips">
              {gym.amenities.map((a) => (
                <span className="chip" key={a.id}>
                  {a.name}
                </span>
              ))}
            </div>
          </div>
        ) : null}
        {gym.openingHours.length > 0 ? (
          <div className="card">
            <h3>Öffnungszeiten (offizielle Angaben)</h3>
            <ul>
              {gym.openingHours.map((h) => (
                <li key={h.isoDayOfWeek}>
                  {dayNames[h.isoDayOfWeek]}: {h.opensAt} - {h.closesAt}
                </li>
              ))}
            </ul>
          </div>
        ) : null}
      </div>

      <h2>Bewertungen ({reviews?.totalCount ?? 0})</h2>
      <p className="muted">
        Bewertungen stammen von angemeldeten Google-Konten. Ein tatsächlicher Besuch des Studios wird nicht
        überprüft.
      </p>
      {(reviews?.items ?? []).map((review) => (
        <div className="card" key={review.id}>
          <p>
            <strong>{review.author.displayName}</strong>
            {review.author.verifiedViaGoogle ? (
              <span className="badge" title="Google-Konto bestätigt - kein Nachweis eines Studiobesuchs">
                Verifiziert über Google
              </span>
            ) : null}
            <span className="muted"> - {new Date(review.createdAtUtc).toLocaleDateString("de-AT")}</span>
            {review.editCount > 0 ? <span className="muted"> (bearbeitet)</span> : null}
          </p>
          <p className="muted">
            {Object.entries(review.ratings)
              .filter(([, value]) => typeof value === "number")
              .map(([key, value]) => `${categoryLabels[key] ?? key}: ${value}/5`)
              .join(" - ")}
          </p>
          {review.text ? <p>{review.text}</p> : null}
          <ReportForm reviewId={review.id} />
        </div>
      ))}
      {(reviews?.items ?? []).length === 0 ? <p className="muted">Noch keine Bewertungen.</p> : null}

      <h2>Eigene Bewertung schreiben</h2>
      <p className="muted">
        Dafür ist eine Anmeldung mit Google notwendig. Konten mit bestätigter E-Mail-Adresse erhalten den Hinweis
        &quot;Verifiziert über Google&quot;; das ist kein Nachweis einer Mitgliedschaft.
      </p>
      <ReviewForm gymSlug={gym.slug} />
    </div>
  );
}
