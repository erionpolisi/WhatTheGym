import type { Metadata } from "next";
import { apiGet } from "@/lib/api";

export const metadata: Metadata = {
  title: "Transparenzbericht",
  description: "Aggregierte Kennzahlen zu Meldungen und Entscheidungen ohne Personenbezug.",
};

export const revalidate = 3600;

interface TransparencyReport {
  year: number;
  totalReports: number;
  keptOnline: number;
  fullyRemoved: number;
  pendingCases: number;
  fastTrackCases: number;
  appealsSubmitted: number;
  appealsReversed: number;
  notes: string;
}

export default async function TransparencyPage() {
  const year = new Date().getFullYear();
  const report = await apiGet<TransparencyReport>(`/api/v1/legal/transparency-report?year=${year}`, 3600);

  return (
    <div>
      <h1>Transparenzbericht {year}</h1>
      {report ? (
        <>
          <table className="scores">
            <tbody>
              <tr>
                <td>Eingegangene Meldungen</td>
                <td>{report.totalReports}</td>
              </tr>
              <tr>
                <td>Entscheidung: bleibt online</td>
                <td>{report.keptOnline}</td>
              </tr>
              <tr>
                <td>Entscheidung: vollstaendig entfernt</td>
                <td>{report.fullyRemoved}</td>
              </tr>
              <tr>
                <td>Offene Faelle</td>
                <td>{report.pendingCases}</td>
              </tr>
              <tr>
                <td>Schnellverfahren (offensichtlich rechtswidrig)</td>
                <td>{report.fastTrackCases}</td>
              </tr>
              <tr>
                <td>Einsprueche</td>
                <td>{report.appealsSubmitted}</td>
              </tr>
              <tr>
                <td>Aufgehobene Entscheidungen</td>
                <td>{report.appealsReversed}</td>
              </tr>
            </tbody>
          </table>
          <p className="muted">{report.notes}</p>
        </>
      ) : (
        <p className="muted">Der Bericht ist derzeit nicht verfuegbar.</p>
      )}
    </div>
  );
}
