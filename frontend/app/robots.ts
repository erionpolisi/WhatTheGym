import type { MetadataRoute } from "next";
import { SITE_URL } from "@/lib/api";

export default function robots(): MetadataRoute.Robots {
  return {
    rules: [
      {
        userAgent: "*",
        allow: "/",
        disallow: ["/konto", "/rechtliches/fall/", "/rechtliches/einspruch/"],
      },
    ],
    sitemap: `${SITE_URL}/sitemap.xml`,
  };
}
