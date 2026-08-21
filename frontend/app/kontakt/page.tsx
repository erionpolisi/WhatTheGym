import type { Metadata } from "next";
import { ContactForm } from "@/components/UserForms";

export const metadata: Metadata = {
  title: "Kontakt",
  description: "Kontakt aufnehmen, ein Studio vorschlagen oder eine Datenkorrektur melden.",
};

export default function ContactPage() {
  return (
    <div>
      <h1>Kontakt</h1>
      <p>
        Studio nicht gefunden oder falsche Daten entdeckt? Neue Studios werden nach redaktioneller Pruefung
        aufgenommen; Korrekturen werden mit offiziellen Quellen abgeglichen.
      </p>
      <ContactForm />
    </div>
  );
}
