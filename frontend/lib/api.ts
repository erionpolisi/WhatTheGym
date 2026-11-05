// Central API access. Server components fetch with ISR; client components send cookies.

export const API_BASE =
  process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:7001";

export const SITE_URL =
  process.env.NEXT_PUBLIC_SITE_URL ?? "http://localhost:3000";

export const SERVER_API_BASE =
  process.env.API_BASE_URL ?? API_BASE;

// Cookie-authenticated state-changing requests must carry this header (CSRF defense in depth).
export const CSRF_HEADER = { "X-CSRF": "1" } as const;

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface CategoryScore {
  category: string;
  area: "membership" | "studio";
  average: number | null;
  ratingCount: number;
}

export interface ScoreSummary {
  totalScore: number | null;
  membershipScore: number | null;
  studioScore: number | null;
  scoreBasis: "both" | "membershipOnly" | "studioOnly" | "none";
  reviewCount: number;
  categories: CategoryScore[];
}

export interface GymListItem {
  id: string;
  name: string;
  slug: string;
  district: number;
  addressLine: string;
  postalCode: string;
  status: string;
  chainName: string | null;
  chainSlug: string | null;
  reviewCount: number;
  totalScore: number | null;
  membershipScore: number | null;
  studioScore: number | null;
  scoreBasis: string;
  trainingTypes: string[];
  targetAudience: string;
}

export interface GymDetail {
  id: string;
  name: string;
  slug: string;
  district: number;
  addressLine: string;
  postalCode: string;
  city: string;
  website: string | null;
  phone: string | null;
  description: string | null;
  status: string;
  chain: {
    id: string;
    name: string;
    slug: string;
    website: string | null;
    trainingTypes: string[];
    targetAudience: string;
  } | null;
  amenities: { id: string; name: string; slug: string }[];
  openingHours: { isoDayOfWeek: number; opensAt: string; closesAt: string }[];
  score: ScoreSummary;
  trainingTypes: string[];
  targetAudience: string;
}

export interface ChainSearchItem {
  id: string;
  name: string;
  slug: string;
  website: string | null;
  trainingTypes: string[];
  targetAudience: string;
  gymCount: number;
  reviewCount: number;
  averageScore: number | null;
}

export interface Amenity {
  id: string;
  name: string;
  slug: string;
}

export interface GymRecommendation {
  gym: GymListItem;
  matchPercent: number;
  reasons: string[];
  notes: string[];
}

export interface RecommendationRequest {
  gender?: string | null;
  womenOnly?: boolean;
  districts?: number[];
  trainingTypes?: string[];
  amenitySlugs?: string[];
  scoreImportance?: string | null;
  limit?: number;
}

export interface Ratings {
  priceValue?: number | null;
  contractTerms?: number | null;
  billing?: number | null;
  cancellationExperience?: number | null;
  equipment?: number | null;
  cleanliness?: number | null;
  staff?: number | null;
  crowding?: number | null;
  changingRoom?: number | null;
  showers?: number | null;
  atmosphere?: number | null;
}

export interface Review {
  id: string;
  gymId: string;
  author: { displayName: string; verifiedViaGoogle: boolean };
  ratings: Ratings;
  text: string | null;
  editCount: number;
  createdAtUtc: string;
}

export interface Me {
  id: string;
  email: string;
  emailVerified: boolean;
  displayName: string;
  role: string;
}

export interface LegalDocument {
  type: string;
  version: number;
  title: string;
  contentMarkdown: string;
  publishedAtUtc: string | null;
}

/** Server-side GET with incremental revalidation; returns null on 404 or when the API is unreachable (e.g. during build). */
export async function apiGet<T>(path: string, revalidateSeconds = 60): Promise<T | null> {
  try {
    const response = await fetch(`${SERVER_API_BASE}${path}`, {
      next: { revalidate: revalidateSeconds },
    });
    if (!response.ok) {
      return null;
    }
    return (await response.json()) as T;
  } catch {
    // API not reachable (build time / outage): pages degrade gracefully and ISR retries.
    return null;
  }
}

export const membershipCategoryLabels: Record<string, string> = {
  priceValue: "Preis-Leistung",
  contractTerms: "Vertragsbedingungen",
  billing: "Abrechnung",
  cancellationExperience: "Kündigungserfahrung",
};

export const studioCategoryLabels: Record<string, string> = {
  equipment: "Geräte",
  cleanliness: "Sauberkeit",
  staff: "Personal",
  crowding: "Auslastung",
  changingRoom: "Umkleiden",
  showers: "Duschen",
  atmosphere: "Atmosphäre",
};

export const categoryLabels: Record<string, string> = {
  ...membershipCategoryLabels,
  ...studioCategoryLabels,
};

export const scoreBasisLabels: Record<string, string> = {
  both: "Mitgliedschaft und Studio (50/50)",
  membershipOnly: "nur Mitgliedschaft",
  studioOnly: "nur Studio",
  none: "noch keine Bewertungen",
};

export const trainingTypeLabels: Record<string, string> = {
  Strength: "Krafttraining",
  Cardio: "Cardio",
  Functional: "Functional Training",
  GroupClasses: "Kurse",
  CrossFit: "CrossFit",
  CircuitTraining: "Zirkeltraining",
  PersonalTraining: "Personal Training",
  Wellness: "Wellness",
};

export const gymStatusLabels: Record<string, string> = {
  Active: "Geöffnet",
  TemporarilyClosed: "Vorübergehend geschlossen",
  PermanentlyClosed: "Dauerhaft geschlossen",
};

export const districtNames: Record<number, string> = {
  1: "Innere Stadt",
  2: "Leopoldstadt",
  3: "Landstraße",
  4: "Wieden",
  5: "Margareten",
  6: "Mariahilf",
  7: "Neubau",
  8: "Josefstadt",
  9: "Alsergrund",
  10: "Favoriten",
  11: "Simmering",
  12: "Meidling",
  13: "Hietzing",
  14: "Penzing",
  15: "Rudolfsheim-Fünfhaus",
  16: "Ottakring",
  17: "Hernals",
  18: "Währing",
  19: "Döbling",
  20: "Brigittenau",
  21: "Floridsdorf",
  22: "Donaustadt",
  23: "Liesing",
};
