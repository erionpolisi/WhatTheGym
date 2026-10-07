"use client";

import { useEffect } from "react";
import { usePathname } from "next/navigation";
import type { AnchorHTMLAttributes, ReactNode } from "react";
import { API_BASE, CSRF_HEADER } from "@/lib/api";

// PII-free analytics: random per-tab session id (not a credential), no IP, no fingerprinting.
function getSessionId(): string {
  const key = "wtg.analytics.session";
  let id = window.sessionStorage.getItem(key);
  if (!id) {
    id = crypto.randomUUID();
    window.sessionStorage.setItem(key, id);
  }
  return id;
}

export function sendEvent(eventType: string, path?: string): void {
  try {
    void fetch(`${API_BASE}/api/v1/analytics/events`, {
      method: "POST",
      headers: { "Content-Type": "application/json", ...CSRF_HEADER },
      body: JSON.stringify({ eventType, path: path ?? window.location.pathname, sessionId: getSessionId() }),
      keepalive: true,
    });
  } catch {
    // Analytics must never break the page.
  }
}

export function PageViewTracker() {
  const pathname = usePathname();

  useEffect(() => {
    sendEvent("page_view", pathname);
  }, [pathname]);

  return null;
}

interface TrackedExternalLinkProps extends AnchorHTMLAttributes<HTMLAnchorElement> {
  children: ReactNode;
  gymSlug: string;
  href: string;
}

export function TrackedExternalLink({ children, gymSlug, href, onClick, ...props }: TrackedExternalLinkProps) {
  const destinationHost = new URL(href).hostname;

  return (
    <a
      {...props}
      href={href}
      onClick={(event) => {
        sendEvent("external_gym_website_click", `/studios/${gymSlug}/external/${destinationHost}`);
        onClick?.(event);
      }}
    >
      {children}
    </a>
  );
}
