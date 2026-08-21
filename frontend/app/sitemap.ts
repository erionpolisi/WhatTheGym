import type { MetadataRoute } from "next";
import { apiGet, SITE_URL, type GymListItem, type PagedResult } from "@/lib/api";

export default async function sitemap(): Promise<MetadataRoute.Sitemap> {
  const staticEntries: MetadataRoute.Sitemap = [
    { url: SITE_URL, changeFrequency: "daily", priority: 1 },
    { url: `${SITE_URL}/studios`, changeFrequency: "daily", priority: 0.9 },
    { url: `${SITE_URL}/kontakt`, changeFrequency: "monthly", priority: 0.3 },
    { url: `${SITE_URL}/transparenz`, changeFrequency: "monthly", priority: 0.3 },
    { url: `${SITE_URL}/rechtliches/impressum`, changeFrequency: "yearly", priority: 0.2 },
    { url: `${SITE_URL}/rechtliches/datenschutz`, changeFrequency: "yearly", priority: 0.2 },
    { url: `${SITE_URL}/rechtliches/nutzungsbedingungen`, changeFrequency: "yearly", priority: 0.2 },
  ];

  try {
    const gyms = await apiGet<PagedResult<GymListItem>>("/api/v1/gyms?pageSize=100", 3600);
    const gymEntries: MetadataRoute.Sitemap = (gyms?.items ?? []).map((gym) => ({
      url: `${SITE_URL}/studios/${gym.slug}`,
      changeFrequency: "weekly",
      priority: 0.7,
    }));
    return [...staticEntries, ...gymEntries];
  } catch {
    return staticEntries;
  }
}
