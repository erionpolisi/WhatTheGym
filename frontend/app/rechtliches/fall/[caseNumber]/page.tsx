import type { Metadata } from "next";
import { API_BASE } from "@/lib/api";

export const metadata: Metadata = {
  title: "Fallstatus",
  robots: { index: false },
};

interface CaseStatus {
  caseNumber: string;
  status: string;
  decision: string | null;
  createdAtUtc: string;
  decidedAtUtc: string | null;
  appealDeadlineUtc: string | null;
}

const statusLabels: Record<string, string> = {
  Received: "Eingegangen",
  UnderReview: "In Pruefung",
  Decided: "Entschieden",
  Closed: "Abgeschlossen",
};

const decisionLabels: Record<string, string> = {
  KeepOnline: "Die Bewertung bleibt online.",
  FullyRemoved: "Die Bewertung wurde vollstaendig entfernt.",
};

export default async function CaseStatusPage({
  params,
  searchParams,
}: {
  params: { caseNumber: string };
  searchParams: { token?: string };
}) {
  if (!searchParams.token) {
    return (
      <div>
        <h1>Fallstatus</h1>
        <p className="error">Der vertrauliche Zugriffslink ist unvollstaendig (Token fehlt).</p>
      </div>
    );
  }

  const response = await fetch(
    `${API_BASE}/api/v1/legal/cases/${encodeURIComponent(params.caseNumber)}/status?token=${encodeURIComponent(searchParams.token)}`,
    { cache: "no-store" },
  );

  if (!response.ok) {
    return (
      <div>
        <h1>Fallstatus</h1>
        <p className="error">Der Fall wurde nicht gefunden oder der Link ist ungueltig.</p>
      </div>
    );
  }

  const status = (await response.json()) as CaseStatus;

  return (
    <div>
      <h1>Fall {status.caseNumber}</h1>
      <div className="card">
        <p>
          Status: <strong>{statusLabels[status.status] ?? status.status}</strong>
        </p>
        <p className="muted">Eingegangen am {new Date(status.createdAtUtc).toLocaleDateString("de-AT")}</p>
        {status.decision ? (
          <>
            <p>{decisionLabels[status.decision] ?? status.decision}</p>
            {status.appealDeadlineUtc ? (
              <p className="muted">
                Einspruch moeglich bis {new Date(status.appealDeadlineUtc).toLocaleDateString("de-AT")}.
              </p>
            ) : null}
          </>
        ) : (
          <p className="muted">
            Die gemeldete Bewertung bleibt waehrend der Pruefung grundsaetzlich online, sofern kein offensichtlich
            rechtswidriger Inhalt vorliegt.
          </p>
        )}
      </div>
    </div>
  );
}
