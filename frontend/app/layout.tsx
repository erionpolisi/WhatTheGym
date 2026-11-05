import type { Metadata } from "next";
import Image from "next/image";
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
    default: "WhatTheGym - Fitnessstudio-Bewertungen für Wien",
    template: "%s | WhatTheGym",
  },
  description:
    "Finde Fitnessstudios in Wien und vergleiche echte, verifizierte Bewertungen zu Mitgliedschaft und Studio.",
  manifest: "/manifest.webmanifest",
  icons: {
    icon: [{ url: "/icon.png", type: "image/png", sizes: "512x512" }],
    shortcut: "/icon.png",
    apple: [{ url: "/apple-icon.png", type: "image/png", sizes: "180x180" }],
  },
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
              <span>
                WhatThe<span className="brand-mark">Gym</span>
              </span>
              <Image src="/brand-icon.png" alt="" width={40} height={40} priority />
            </Link>
            <nav className="main" aria-label="Hauptnavigation">
              <Link href="/">Home</Link>
              <Link href="/studios">Studios</Link>
              <Link href="/empfehlung">Gym-Finder</Link>
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
            <div className="footer-grid">
              <div className="footer-brand">
                <Link href="/" className="brand">
                  <span>
                    WhatThe<span className="brand-mark">Gym</span>
                  </span>
                  <Image src="/brand-icon.png" alt="" width={32} height={32} />
                </Link>
                <p>WhatTheGym - keine Überraschungen mehr.</p>
                <p className="muted">
                  Echte, verifizierte Bewertungen zu Fitnessstudios in Wien - getrennt nach Mitgliedschaft und
                  Studio.
                </p>
              </div>
              <nav className="footer-col" aria-label="Allgemein">
                <h3>Allgemein</h3>
                <Link href="/ueber-uns">Über uns</Link>
                <Link href="/kontakt">Kontakt</Link>
                <Link href="/faq">FAQ</Link>
              </nav>
              <nav className="footer-col" aria-label="Rechtliches">
                <h3>Rechtliches</h3>
                <Link href="/rechtliches/impressum">Impressum</Link>
                <Link href="/rechtliches/datenschutz">Datenschutz</Link>
                <Link href="/rechtliches/nutzungsbedingungen">AGB</Link>
              </nav>
              <div className="footer-col" aria-label="Social Media">
                <h3>Social Media</h3>
                <div className="footer-social">
                  {/* Placeholder links until the official accounts exist. */}
                  <a href="#" aria-label="TikTok (bald verfügbar)" title="TikTok - bald verfügbar">
                    <svg viewBox="0 0 24 24" width="22" height="22" fill="currentColor" aria-hidden="true">
                      <path d="M19.59 6.69a4.83 4.83 0 0 1-3.77-4.25V2h-3.45v13.67a2.89 2.89 0 0 1-5.2 1.74 2.89 2.89 0 0 1 2.31-4.64 2.93 2.93 0 0 1 .88.13V9.4a6.84 6.84 0 0 0-1-.05A6.33 6.33 0 0 0 5 20.1a6.34 6.34 0 0 0 10.86-4.43v-7a8.16 8.16 0 0 0 4.77 1.52v-3.4a4.85 4.85 0 0 1-1.04-.1z" />
                    </svg>
                  </a>
                  <a href="#" aria-label="Instagram (bald verfügbar)" title="Instagram - bald verfügbar">
                    <svg viewBox="0 0 24 24" width="22" height="22" fill="currentColor" aria-hidden="true">
                      <path d="M12 2.16c3.2 0 3.58.01 4.85.07 1.17.05 1.8.25 2.23.41.56.22.96.48 1.38.9.42.42.68.82.9 1.38.16.42.36 1.06.41 2.23.06 1.27.07 1.65.07 4.85s-.01 3.58-.07 4.85c-.05 1.17-.25 1.8-.41 2.23-.22.56-.48.96-.9 1.38-.42.42-.82.68-1.38.9-.42.16-1.06.36-2.23.41-1.27.06-1.65.07-4.85.07s-3.58-.01-4.85-.07c-1.17-.05-1.8-.25-2.23-.41a3.72 3.72 0 0 1-1.38-.9 3.72 3.72 0 0 1-.9-1.38c-.16-.42-.36-1.06-.41-2.23-.06-1.27-.07-1.65-.07-4.85s.01-3.58.07-4.85c.05-1.17.25-1.8.41-2.23.22-.56.48-.96.9-1.38.42-.42.82-.68 1.38-.9.42-.16 1.06-.36 2.23-.41 1.27-.06 1.65-.07 4.85-.07M12 0C8.74 0 8.33.01 7.05.07 5.78.13 4.9.33 4.14.63a5.88 5.88 0 0 0-2.13 1.38A5.88 5.88 0 0 0 .63 4.14C.33 4.9.13 5.78.07 7.05.01 8.33 0 8.74 0 12s.01 3.67.07 4.95c.06 1.27.26 2.15.56 2.91.3.79.72 1.46 1.38 2.13a5.88 5.88 0 0 0 2.13 1.38c.76.3 1.64.5 2.91.56 1.28.06 1.69.07 4.95.07s3.67-.01 4.95-.07c1.27-.06 2.15-.26 2.91-.56a5.88 5.88 0 0 0 2.13-1.38 5.88 5.88 0 0 0 1.38-2.13c.3-.76.5-1.64.56-2.91.06-1.28.07-1.69.07-4.95s-.01-3.67-.07-4.95c-.06-1.27-.26-2.15-.56-2.91a5.88 5.88 0 0 0-1.38-2.13A5.88 5.88 0 0 0 19.86.63c-.76-.3-1.64-.5-2.91-.56C15.67.01 15.26 0 12 0zm0 5.84A6.16 6.16 0 1 0 18.16 12 6.16 6.16 0 0 0 12 5.84zm0 10.15A4 4 0 1 1 16 12a4 4 0 0 1-4 3.99zm7.85-10.4a1.44 1.44 0 1 1-1.44-1.44 1.44 1.44 0 0 1 1.44 1.44z" />
                    </svg>
                  </a>
                  <a href="#" aria-label="LinkedIn (bald verfügbar)" title="LinkedIn - bald verfügbar">
                    <svg viewBox="0 0 24 24" width="22" height="22" fill="currentColor" aria-hidden="true">
                      <path d="M20.45 20.45h-3.56v-5.57c0-1.33-.02-3.04-1.85-3.04-1.85 0-2.14 1.45-2.14 2.94v5.67H9.34V9h3.42v1.56h.05a3.75 3.75 0 0 1 3.37-1.85c3.6 0 4.27 2.37 4.27 5.46v6.28zM5.34 7.43a2.07 2.07 0 1 1 0-4.13 2.07 2.07 0 0 1 0 4.13zm1.78 13.02H3.56V9h3.56v11.45zM22.22 0H1.77C.79 0 0 .77 0 1.72v20.56C0 23.23.79 24 1.77 24h20.45c.98 0 1.78-.77 1.78-1.72V1.72C24 .77 23.2 0 22.22 0z" />
                    </svg>
                  </a>
                </div>
                <p className="muted">Unsere Kanäle starten in Kürze.</p>
              </div>
            </div>
            <div className="footer-bottom">
              <span>© {new Date().getFullYear()} WhatTheGym</span>
              <span>Alle Bewertungen stammen aus der Community - ohne bezahlte Platzierungen.</span>
            </div>
          </div>
        </footer>
        <PageViewTracker />
      </body>
    </html>
  );
}
