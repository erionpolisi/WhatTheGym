import type { Metadata } from "next";
import { notFound } from "next/navigation";
import { apiGet, type LegalDocument } from "@/lib/api";

export const dynamic = "force-dynamic";

const slugToType: Record<string, string> = {
  impressum: "imprint",
  datenschutz: "privacyPolicy",
  nutzungsbedingungen: "termsOfUse",
};

export async function generateMetadata({ params }: { params: { doc: string } }): Promise<Metadata> {
  const type = slugToType[params.doc];
  if (!type) {
    return { title: "Rechtliches" };
  }
  const document = await apiGet<LegalDocument>(`/api/v1/legal/documents/${type}`, 300);
  return { title: document?.title ?? "Rechtliches" };
}

export default async function LegalDocumentPage({ params }: { params: { doc: string } }) {
  const type = slugToType[params.doc];
  if (!type) {
    notFound();
  }

  const document = await apiGet<LegalDocument>(`/api/v1/legal/documents/${type}`, 300);
  if (!document) {
    notFound();
  }

  return (
    <div>
      <h1>{document.title}</h1>
      <p className="muted">
        Version {document.version}
        {document.publishedAtUtc
          ? ` - veröffentlicht am ${new Date(document.publishedAtUtc).toLocaleDateString("de-AT")}`
          : ""}
      </p>
      {/* Content is trusted backend markdown; rendered as preformatted text for the MVP. */}
      <pre className="legal-text">{document.contentMarkdown}</pre>
    </div>
  );
}
