import type { MetadataRoute } from "next";

export default function manifest(): MetadataRoute.Manifest {
  return {
    name: "WhatTheGym",
    short_name: "WhatTheGym",
    description: "Ehrliche, verifizierte Fitnessstudio-Bewertungen für Wien.",
    start_url: "/",
    display: "standalone",
    background_color: "#0b0b0d",
    theme_color: "#0b0b0d",
    lang: "de-AT",
    icons: [
      {
        src: "/brand-icon.png",
        sizes: "512x512",
        type: "image/png",
        purpose: "any",
      },
    ],
  };
}
