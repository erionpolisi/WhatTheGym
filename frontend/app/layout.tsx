import type { Metadata } from "next";
import Link from "next/link";
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
        <header className="site">
          <div className="container">
            <Link href="/" className="brand">
              WhatTheGym
            </Link>
            <nav className="main">
              <Link href="/studios">Studios</Link>
              <Link href="/kontakt">Kontakt</Link>
              <Link href="/konto">Mein Konto</Link>
            </nav>
          </div>
        </header>
        <main className="container">{children}</main>
        <footer className="site">
          <div className="container">
            <Link href="/rechtliches/impressum">Impressum</Link>
            <Link href="/rechtliches/datenschutz">Datenschutz</Link>
            <Link href="/rechtliches/nutzungsbedingungen">Nutzungsbedingungen</Link>
            <Link href="/transparenz">Transparenzbericht</Link>
            <div className="muted" style={{ marginTop: "0.5rem" }}>
              WhatTheGym - Bewertungen fuer Fitnessstudios in Wien.
            </div>
          </div>
        </footer>
        <PageViewTracker />
      </body>
    </html>
  );
}
