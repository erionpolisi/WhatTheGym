import type { Metadata } from "next";
import { apiGet, type Amenity } from "@/lib/api";
import { RecommendationWizard } from "@/components/RecommendationWizard";

export const metadata: Metadata = {
  title: "Gym-Finder",
  description:
    "Beantworte ein paar Fragen zu Bezirk, Training und Ausstattung und erhalte die drei besten Gym-Empfehlungen für Wien.",
};

export const revalidate = 300;

export default async function RecommendationPage({ searchParams }: { searchParams: { modus?: string } }) {
  const amenities = (await apiGet<Amenity[]>("/api/v1/amenities", 300)) ?? [];

  return (
    <div>
      <span className="eyebrow">GYM-FINDER</span>
      <h1>Welches Gym passt zu dir?</h1>
      <p className="lead">
        Beantworte ein paar kurze Fragen — wir empfehlen dir die drei Studios in Wien, die am besten zu dir passen,
        inklusive Begründung.
      </p>
      <RecommendationWizard amenities={amenities} startInForm={searchParams.modus === "formular"} />
    </div>
  );
}
