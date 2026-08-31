import type { Metadata } from "next";
import Link from "next/link";
// Self-hosted variable fonts (no external requests at build or runtime).
import "@fontsource-variable/inter";
import "@fontsource-variable/space-grotesk";
import "./globals.css";
import { SITE_URL } from "@/lib/api";
import { PageViewTracker } from "@/components/Analytics";

export const metadata: Metadata = {
  metadataBase: new URL(SITE_URL),
  title: {
    default: "WhatTheGym - Fitnessstudio-Bewertungen fuer Wien",
    template: "%s | WhatTheGym",
  },
  description:
    "Finde Fitnessstudios in Wien und vergleiche echte, verifizierte Bewertungen zu Mitgliedschaft und Studio.",
  openGraph: {
    siteName: "WhatTheGym",
    locale: "de_AT",
    type: "website",
  },
};

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="de-AT">
      <body>
        <a href="#main" className="skip-link">
          Zum Inhalt springen
        </a>
        <header className="site">
          <div className="container">
            <Link href="/" className="brand">
              WhatThe<span className="brand-mark">Gym</span>
            </Link>
            <nav className="main" aria-label="Hauptnavigation">
              <Link href="/studios">Studios</Link>
              <Link href="/kontakt">Kontakt</Link>
              <Link href="/konto">Mein Konto</Link>
            </nav>
          </div>
        </header>
        <main id="main" className="container">
          {children}
        </main>
        <footer className="site">
          <div className="container">
            <nav aria-label="Rechtliches">
              <Link href="/rechtliches/impressum">Impressum</Link>
              <Link href="/rechtliches/datenschutz">Datenschutz</Link>
              <Link href="/rechtliches/nutzungsbedingungen">Nutzungsbedingungen</Link>
              <Link href="/transparenz">Transparenzbericht</Link>
            </nav>
            <div>WhatTheGym - ehrliche Bewertungen fuer Fitnessstudios in Wien.</div>
          </div>
        </footer>
        <PageViewTracker />
      </body>
    </html>
  );
}
