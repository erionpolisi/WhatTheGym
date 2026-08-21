// Central API access. Server components fetch with ISR; client components send cookies.

export const API_BASE =
  process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:7001";

export const SITE_URL =
  process.env.NEXT_PUBLIC_SITE_URL ?? "http://localhost:3000";

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
  chain: { id: string; name: string; slug: string; website: string | null } | null;
  amenities: { id: string; name: string; slug: string }[];
  openingHours: { isoDayOfWeek: number; opensAt: string; closesAt: string }[];
  score: ScoreSummary;
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
    const response = await fetch(`${API_BASE}${path}`, {
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
  cancellationExperience: "Kuendigungserfahrung",
};

export const studioCategoryLabels: Record<string, string> = {
  equipment: "Geraete",
  cleanliness: "Sauberkeit",
  staff: "Personal",
  crowding: "Auslastung",
  changingRoom: "Umkleiden",
  showers: "Duschen",
  atmosphere: "Atmosphaere",
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
