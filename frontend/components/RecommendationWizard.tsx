"use client";

import { useMemo, useState } from "react";
import Link from "next/link";
import {
  API_BASE,
  CSRF_HEADER,
  districtNames,
  trainingTypeLabels,
  type Amenity,
  type GymRecommendation,
} from "@/lib/api";
import { ScorePill } from "@/components/Scores";

type Mode = "wizard" | "form" | "results";

interface Preferences {
  gender: string | null;
  womenOnly: boolean | null;
  districts: number[];
  trainingTypes: string[];
  amenitySlugs: string[];
  scoreImportance: "high" | "medium" | "low";
}

const initialPreferences: Preferences = {
  gender: null,
  womenOnly: null,
  districts: [],
  trainingTypes: [],
  amenitySlugs: [],
  scoreImportance: "medium",
};

const genderOptions = [
  { value: "female", label: "Frau" },
  { value: "male", label: "Mann" },
  { value: "", label: "Keine Angabe" },
];

const importanceOptions = [
  { value: "high", label: "Sehr wichtig" },
  { value: "medium", label: "Wichtig" },
  { value: "low", label: "Unwichtig" },
] as const;

function OptionChip({
  selected,
  onClick,
  children,
  rank,
}: {
  selected: boolean;
  onClick: () => void;
  children: React.ReactNode;
  rank?: number;
}) {
  return (
    <button
      type="button"
      className={`option-chip${selected ? " selected" : ""}`}
      aria-pressed={selected}
      onClick={onClick}
    >
      {selected && rank !== undefined ? <span className="rank">{rank}</span> : null}
      {children}
    </button>
  );
}

function toggle<T>(list: T[], value: T): T[] {
  return list.includes(value) ? list.filter((v) => v !== value) : [...list, value];
}

/** All question blocks, shared between step-by-step wizard and the all-in-one form. */
function QuestionBlocks({
  prefs,
  setPrefs,
  amenities,
  genderTouched,
  only,
}: {
  prefs: Preferences;
  setPrefs: (update: Partial<Preferences>) => void;
  amenities: Amenity[];
  genderTouched: boolean;
  only?: string;
}) {
  const show = (id: string) => only === undefined || only === id;

  return (
    <>
      {show("gender") ? (
        <section>
          <h2>Wie identifizierst du dich?</h2>
          <p className="hint">Das hilft uns z. B. bei Studios, die nur für Frauen zugänglich sind.</p>
          <div className="option-grid">
            {genderOptions.map((o) => (
              <OptionChip
                key={o.value}
                selected={genderTouched && prefs.gender === (o.value === "" ? null : o.value)}
                onClick={() =>
                  setPrefs({
                    gender: o.value === "" ? null : o.value,
                    womenOnly: o.value === "male" ? null : prefs.womenOnly,
                  })
                }
              >
                {o.label}
              </OptionChip>
            ))}
          </div>
        </section>
      ) : null}

      {show("womenOnly") && prefs.gender !== "male" ? (
        <section>
          <h2>Soll es ein reines Frauen-Studio sein?</h2>
          <p className="hint">Einige Ketten in Wien trainieren ausschließlich Frauen.</p>
          <div className="option-grid">
            <OptionChip selected={prefs.womenOnly === true} onClick={() => setPrefs({ womenOnly: true })}>
              Ja, nur Frauen-Studios
            </OptionChip>
            <OptionChip selected={prefs.womenOnly === false || prefs.womenOnly === null} onClick={() => setPrefs({ womenOnly: false })}>
              Egal
            </OptionChip>
          </div>
        </section>
      ) : null}

      {show("districts") ? (
        <section>
          <h2>In welchen Bezirken kannst du trainieren?</h2>
          <p className="hint">
            Die Reihenfolge deiner Auswahl ist dein Ranking: zuerst gewählte Bezirke zählen mehr. Keine Auswahl =
            ganz Wien.
          </p>
          <div className="option-grid">
            {Array.from({ length: 23 }, (_, i) => i + 1).map((d) => {
              const rank = prefs.districts.indexOf(d);
              return (
                <OptionChip
                  key={d}
                  selected={rank >= 0}
                  rank={rank >= 0 ? rank + 1 : undefined}
                  onClick={() => setPrefs({ districts: toggle(prefs.districts, d) })}
                >
                  {d}. {districtNames[d]}
                </OptionChip>
              );
            })}
          </div>
        </section>
      ) : null}

      {show("trainingTypes") ? (
        <section>
          <h2>Wie willst du trainieren?</h2>
          <p className="hint">Wähle alle Trainingsarten, die dir wichtig sind. Keine Auswahl = egal.</p>
          <div className="option-grid">
            {Object.entries(trainingTypeLabels).map(([value, label]) => (
              <OptionChip
                key={value}
                selected={prefs.trainingTypes.includes(value)}
                onClick={() => setPrefs({ trainingTypes: toggle(prefs.trainingTypes, value) })}
              >
                {label}
              </OptionChip>
            ))}
          </div>
        </section>
      ) : null}

      {show("amenities") ? (
        <section>
          <h2>Welche Ausstattung brauchst du?</h2>
          <p className="hint">Sauna, Duschen, 24/7 &amp; Co. Keine Auswahl = egal.</p>
          <div className="option-grid">
            {amenities.map((a) => (
              <OptionChip
                key={a.slug}
                selected={prefs.amenitySlugs.includes(a.slug)}
                onClick={() => setPrefs({ amenitySlugs: toggle(prefs.amenitySlugs, a.slug) })}
              >
                {a.name}
              </OptionChip>
            ))}
          </div>
        </section>
      ) : null}

      {show("score") ? (
        <section>
          <h2>Wie wichtig sind dir Community-Bewertungen?</h2>
          <p className="hint">Studios ohne Bewertungen werden neutral behandelt, nie bestraft.</p>
          <div className="option-grid">
            {importanceOptions.map((o) => (
              <OptionChip
                key={o.value}
                selected={prefs.scoreImportance === o.value}
                onClick={() => setPrefs({ scoreImportance: o.value })}
              >
                {o.label}
              </OptionChip>
            ))}
          </div>
        </section>
      ) : null}
    </>
  );
}

function ResultList({ results }: { results: GymRecommendation[] }) {
  if (results.length === 0) {
    return (
      <p className="muted">
        Keine passenden Studios gefunden. Tipp: Weniger Bezirke oder Kriterien auswählen und nochmal probieren.
      </p>
    );
  }
  return (
    <div className="reco-list">
      {results.map((r, index) => (
        <div className="card reco-card" key={r.gym.id}>
          <span className="reco-rank">Empfehlung {index + 1}</span>
          <div className="card-top">
            <h3>
              <Link href={`/studios/${r.gym.slug}`}>{r.gym.name}</Link>
            </h3>
            <span className="match-pill">
              {r.matchPercent}
              <span className="denom">% Match</span>
            </span>
          </div>
          <div className="muted">
            {r.gym.postalCode} Wien, {r.gym.district}. Bezirk
            {r.gym.chainName ? ` · ${r.gym.chainName}` : ""}
          </div>
          <div className="muted">
            Community-Note: <ScorePill score={r.gym.totalScore} basis={r.gym.scoreBasis} />
          </div>
          <ul className="reasons">
            {r.reasons.map((reason) => (
              <li key={reason}>{reason}</li>
            ))}
          </ul>
          {r.notes.length > 0 ? <p className="notes">{r.notes.join(" ")}</p> : null}
        </div>
      ))}
    </div>
  );
}

export function RecommendationWizard({ amenities, startInForm }: { amenities: Amenity[]; startInForm: boolean }) {
  const [mode, setMode] = useState<Mode>(startInForm ? "form" : "wizard");
  const [step, setStep] = useState(0);
  const [prefs, setPrefsState] = useState<Preferences>(initialPreferences);
  const [genderTouched, setGenderTouched] = useState(false);
  const [results, setResults] = useState<GymRecommendation[] | null>(null);
  const [pending, setPending] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const setPrefs = (update: Partial<Preferences>) => {
    setPrefsState((prev) => ({ ...prev, ...update }));
    if ("gender" in update) {
      setGenderTouched(true);
    }
  };

  // Steps are dynamic: men never see the women-only question.
  const steps = useMemo(() => {
    const list = ["gender"];
    if (prefs.gender !== "male") list.push("womenOnly");
    list.push("districts", "trainingTypes", "amenities", "score");
    return list;
  }, [prefs.gender]);

  const currentStep = steps[Math.min(step, steps.length - 1)];
  const genderStepValid = currentStep !== "gender" || genderTouched;

  async function submit() {
    setPending(true);
    setError(null);
    try {
      const response = await fetch(`${API_BASE}/api/v1/gyms/recommendations`, {
        method: "POST",
        credentials: "include",
        headers: { "Content-Type": "application/json", ...CSRF_HEADER },
        body: JSON.stringify({
          gender: prefs.gender,
          womenOnly: prefs.womenOnly === true,
          districts: prefs.districts,
          trainingTypes: prefs.trainingTypes,
          amenitySlugs: prefs.amenitySlugs,
          scoreImportance: prefs.scoreImportance,
          limit: 3,
        }),
      });
      if (!response.ok) {
        setError("Die Empfehlung konnte nicht berechnet werden. Bitte versuch es später erneut.");
        return;
      }
      setResults((await response.json()) as GymRecommendation[]);
      setMode("results");
    } catch {
      setError("Der Server ist gerade nicht erreichbar. Bitte versuch es später erneut.");
    } finally {
      setPending(false);
    }
  }

  function restart() {
    setPrefsState(initialPreferences);
    setGenderTouched(false);
    setResults(null);
    setStep(0);
    setMode("wizard");
  }

  if (mode === "results") {
    return (
      <div className="wizard">
        <h2>Deine Top-Empfehlungen</h2>
        <p className="hint">Basierend auf deinen Angaben — inklusive Begründung, warum das Studio zu dir passt.</p>
        <ResultList results={results ?? []} />
        <div className="wizard-nav">
          <button type="button" className="secondary" onClick={restart}>
            Nochmal von vorne
          </button>
          <Link href="/studios">
            <button type="button" className="secondary">
              Alle Studios ansehen
            </button>
          </Link>
        </div>
      </div>
    );
  }

  if (mode === "form") {
    return (
      <div className="wizard">
        <h2>Alles auf einen Blick</h2>
        <p className="hint">Beantworte nur, was dir wichtig ist — alles ist optional.</p>
        <QuestionBlocks prefs={prefs} setPrefs={setPrefs} amenities={amenities} genderTouched={genderTouched} />
        {error ? <p className="error">{error}</p> : null}
        <div className="wizard-nav">
          <button type="button" className="secondary" onClick={() => setMode("wizard")}>
            Zurück zu den Fragen
          </button>
          <button type="button" onClick={submit} disabled={pending}>
            {pending ? "Berechne…" : "Empfehlung anzeigen"}
          </button>
        </div>
      </div>
    );
  }

  const isLastStep = step >= steps.length - 1;

  return (
    <>
      <div className="wizard">
        <div className="wizard-progress" aria-hidden="true">
          {steps.map((s, i) => (
            <span key={s} className={i <= step ? "active" : ""} />
          ))}
        </div>
        <p className="muted">
          Frage {Math.min(step + 1, steps.length)} von {steps.length}
        </p>
        <QuestionBlocks prefs={prefs} setPrefs={setPrefs} amenities={amenities} genderTouched={genderTouched} only={currentStep} />
        {error ? <p className="error">{error}</p> : null}
        <div className="wizard-nav">
          <button type="button" className="secondary" onClick={() => setStep(Math.max(0, step - 1))} disabled={step === 0}>
            Zurück
          </button>
          {isLastStep ? (
            <button type="button" onClick={submit} disabled={pending}>
              {pending ? "Berechne…" : "Empfehlung anzeigen"}
            </button>
          ) : (
            <button type="button" onClick={() => setStep(step + 1)} disabled={!genderStepValid}>
              Weiter
            </button>
          )}
        </div>
      </div>
      <div className="wizard-skip">
        <button type="button" className="secondary" onClick={() => setMode("form")}>
          Keine Geduld? Direkt zum Formular
        </button>
      </div>
    </>
  );
}
