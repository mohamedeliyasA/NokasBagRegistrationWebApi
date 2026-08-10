/**
 * Shared UI primitives for the NOKAS Integration Portal.
 *
 * Everything here is plain Tailwind + inline SVG - no extra dependencies.
 * Import from "@/components/ui" so pages stay visually consistent.
 */
import { useState, type ReactNode, type HTMLAttributes } from "react";
import { cn } from "../../lib/utils";

/* ------------------------------------------------------------------ */
/*  Copy button                                                        */
/* ------------------------------------------------------------------ */

export function CopyButton({
  value,
  label = "value",
  tone = "dark",
}: {
  value: string;
  label?: string;
  tone?: "dark" | "light";
}) {
  const [copied, setCopied] = useState(false);

  async function handleCopy() {
    try {
      await navigator.clipboard.writeText(value);
      setCopied(true);
      window.setTimeout(() => setCopied(false), 1800);
    } catch {
      // Clipboard requires a secure context; fail quietly rather than break the UI.
    }
  }

  const toneClasses =
    tone === "dark"
      ? "text-slate-300 hover:bg-white/10 hover:text-white"
      : "text-gray-500 hover:bg-gray-100 hover:text-gray-900";

  return (
    <button
      type="button"
      onClick={handleCopy}
      aria-label={copied ? `${label} copied` : `Copy ${label}`}
      title={copied ? "Copied" : "Copy"}
      className={cn(
        "inline-flex shrink-0 items-center gap-1.5 rounded-md px-2 py-1 text-xs font-medium transition focus:outline-none focus:ring-2 focus:ring-brand-400",
        toneClasses,
      )}
    >
      {copied ? (
        <>
          <svg viewBox="0 0 20 20" fill="currentColor" className="h-3.5 w-3.5 text-emerald-400">
            <path
              fillRule="evenodd"
              d="M16.7 5.3a1 1 0 0 1 0 1.4l-7.5 7.5a1 1 0 0 1-1.4 0l-3.5-3.5a1 1 0 1 1 1.4-1.4l2.8 2.8 6.8-6.8a1 1 0 0 1 1.4 0Z"
              clipRule="evenodd"
            />
          </svg>
          <span className="text-emerald-400">Copied</span>
        </>
      ) : (
        <>
          <svg viewBox="0 0 20 20" fill="currentColor" className="h-3.5 w-3.5">
            <path d="M7 3a2 2 0 0 0-2 2v8a2 2 0 0 0 2 2h6a2 2 0 0 0 2-2V5a2 2 0 0 0-2-2H7Z" />
            <path d="M3 7a2 2 0 0 1 1-1.73V15a2 2 0 0 0 2 2h7.73A2 2 0 0 1 12 18H6a3 3 0 0 1-3-3V7Z" />
          </svg>
          <span>Copy</span>
        </>
      )}
    </button>
  );
}

/* ------------------------------------------------------------------ */
/*  Code block                                                         */
/* ------------------------------------------------------------------ */

export function CodeBlock({
  label,
  code,
  className,
}: {
  label?: string;
  code: string;
  className?: string;
}) {
  return (
    <figure
      className={cn(
        "mt-2 overflow-hidden rounded-lg border border-slate-700 bg-slate-900 shadow-card",
        className,
      )}
    >
      {label && (
        <figcaption className="flex items-center justify-between border-b border-slate-700 bg-slate-800/80 px-3 py-1.5">
          <span className="text-[11px] font-semibold uppercase tracking-wider text-slate-400">
            {label}
          </span>
          <CopyButton value={code} label={label} />
        </figcaption>
      )}
      <div className="overflow-x-auto">
        <pre className="p-3 font-mono text-xs leading-relaxed text-slate-100">{code}</pre>
      </div>
    </figure>
  );
}

/* ------------------------------------------------------------------ */
/*  Badge                                                              */
/* ------------------------------------------------------------------ */

const badgeTones = {
  brand: "bg-brand-50 text-brand-800 ring-brand-700/20",
  accent: "bg-accent-50 text-accent-700 ring-accent-700/20",
  green: "bg-emerald-50 text-emerald-700 ring-emerald-600/20",
  amber: "bg-amber-50 text-amber-700 ring-amber-600/20",
  red: "bg-rose-50 text-rose-700 ring-rose-600/20",
  gray: "bg-gray-100 text-gray-700 ring-gray-500/20",
} as const;

export type BadgeTone = keyof typeof badgeTones;

export function Badge({
  tone = "gray",
  children,
  className,
}: {
  tone?: BadgeTone;
  children: ReactNode;
  className?: string;
}) {
  return (
    <span
      className={cn(
        "inline-flex items-center rounded-md px-2 py-0.5 text-xs font-semibold ring-1 ring-inset",
        badgeTones[tone],
        className,
      )}
    >
      {children}
    </span>
  );
}

/* ------------------------------------------------------------------ */
/*  HTTP method badge                                                  */
/* ------------------------------------------------------------------ */

export type HttpMethod = "POST" | "GET" | "PUT" | "DELETE";

const methodTone: Record<HttpMethod, BadgeTone> = {
  POST: "green",
  GET: "accent",
  PUT: "amber",
  DELETE: "red",
};

export function MethodBadge({ method }: { method: HttpMethod }) {
  return (
    <Badge tone={methodTone[method]} className="px-2 py-1 font-bold tracking-wide">
      {method}
    </Badge>
  );
}

/* ------------------------------------------------------------------ */
/*  Card                                                               */
/* ------------------------------------------------------------------ */

export function Card({
  className,
  hoverable = false,
  children,
  ...props
}: HTMLAttributes<HTMLDivElement> & { hoverable?: boolean }) {
  return (
    <div
      className={cn(
        "rounded-xl border border-gray-200 bg-white shadow-card",
        hoverable && "transition hover:-translate-y-0.5 hover:shadow-card-hover",
        className,
      )}
      {...props}
    >
      {children}
    </div>
  );
}

export function CardContent({ className, children, ...props }: HTMLAttributes<HTMLDivElement>) {
  return (
    <div className={cn("p-5", className)} {...props}>
      {children}
    </div>
  );
}

/* ------------------------------------------------------------------ */
/*  Callout                                                            */
/* ------------------------------------------------------------------ */

const calloutTones = {
  info: { wrap: "border-accent-500 bg-accent-50/70", title: "text-accent-900", body: "text-accent-900/80" },
  success: { wrap: "border-emerald-500 bg-emerald-50/70", title: "text-emerald-900", body: "text-emerald-900/80" },
  warning: { wrap: "border-amber-500 bg-amber-50/70", title: "text-amber-900", body: "text-amber-900/80" },
  danger: { wrap: "border-rose-500 bg-rose-50/70", title: "text-rose-900", body: "text-rose-900/80" },
} as const;

export function Callout({
  tone = "info",
  title,
  children,
}: {
  tone?: keyof typeof calloutTones;
  title?: string;
  children: ReactNode;
}) {
  const t = calloutTones[tone];
  return (
    <aside className={cn("rounded-lg border-l-4 p-4", t.wrap)}>
      {title && <h4 className={cn("text-sm font-semibold", t.title)}>{title}</h4>}
      <div className={cn("text-xs leading-relaxed", t.body, title && "mt-1")}>{children}</div>
    </aside>
  );
}

/* ------------------------------------------------------------------ */
/*  Page + section headings                                            */
/* ------------------------------------------------------------------ */

export function PageHeader({
  eyebrow,
  title,
  lead,
  children,
}: {
  eyebrow?: string;
  title: string;
  lead?: ReactNode;
  children?: ReactNode;
}) {
  return (
    <header className="space-y-3">
      {eyebrow && (
        <p className="text-xs font-semibold uppercase tracking-widest text-brand-600">{eyebrow}</p>
      )}
      <h2 className="text-2xl font-bold tracking-tight text-gray-900">{title}</h2>
      {lead && <p className="max-w-3xl text-sm leading-relaxed text-gray-600">{lead}</p>}
      {children}
    </header>
  );
}

export function SectionHeading({
  id,
  icon,
  children,
}: {
  id?: string;
  icon?: string;
  children: ReactNode;
}) {
  return (
    <h3
      id={id}
      className="scroll-mt-6 border-b border-gray-200 pb-2 text-lg font-bold text-gray-900"
    >
      {icon && <span className="mr-2">{icon}</span>}
      {children}
    </h3>
  );
}

/* ------------------------------------------------------------------ */
/*  Jump-link chips ("on this page")                                   */
/* ------------------------------------------------------------------ */

export function JumpNav({ items }: { items: { id: string; label: string }[] }) {
  return (
    <nav aria-label="On this page" className="flex flex-wrap gap-2">
      {items.map((item) => (
        <a
          key={item.id}
          href={`#${item.id}`}
          className="rounded-full border border-gray-300 bg-white px-3 py-1 text-xs font-medium text-gray-700 transition hover:border-brand-400 hover:bg-brand-50 hover:text-brand-800"
        >
          {item.label}
        </a>
      ))}
    </nav>
  );
}

/* ------------------------------------------------------------------ */
/*  Table                                                              */
/* ------------------------------------------------------------------ */

export function Table({ headers, children }: { headers: string[]; children: ReactNode }) {
  return (
    <div className="overflow-x-auto rounded-lg border border-gray-200">
      <table className="min-w-full divide-y divide-gray-200 text-left text-xs">
        <thead className="bg-gray-50">
          <tr>
            {headers.map((h) => (
              <th key={h} scope="col" className="px-3 py-2 font-semibold text-gray-700">
                {h}
              </th>
            ))}
          </tr>
        </thead>
        <tbody className="divide-y divide-gray-100 bg-white">{children}</tbody>
      </table>
    </div>
  );
}

/* ------------------------------------------------------------------ */
/*  Environment URL list                                               */
/* ------------------------------------------------------------------ */

/**
 * Renders a Test/Production pair of URLs with copy buttons. Used anywhere an
 * endpoint is shown so partners are never left guessing which environment a
 * URL belongs to.
 */
export function EnvUrlList({
  title,
  entries,
}: {
  title?: string;
  entries: { label: string; url: string }[];
}) {
  return (
    <div className="space-y-2">
      {title && (
        <p className="text-xs font-semibold uppercase tracking-wider text-gray-500">{title}</p>
      )}
      {entries.map((e) => (
        <div
          key={e.label}
          className="flex items-center gap-2 overflow-hidden rounded-md border border-gray-200 bg-gray-50 px-2 py-1.5"
        >
          <Badge tone={e.label === "Production" ? "brand" : "gray"}>{e.label}</Badge>
          <code className="min-w-0 flex-1 break-all font-mono text-xs text-gray-800">{e.url}</code>
          <span className="shrink-0 rounded bg-slate-800">
            <CopyButton value={e.url} label={`${e.label} URL`} />
          </span>
        </div>
      ))}
    </div>
  );
}

/* ------------------------------------------------------------------ */
/*  Inline code                                                        */
/* ------------------------------------------------------------------ */

export function InlineCode({ children }: { children: ReactNode }) {
  return (
    <code className="rounded bg-gray-100 px-1 py-0.5 font-mono text-[0.9em] text-gray-800">
      {children}
    </code>
  );
}

/* ------------------------------------------------------------------ */
/*  Misc                                                               */
/* ------------------------------------------------------------------ */

export function Separator({ className }: { className?: string }) {
  return <hr className={cn("border-t border-gray-200 my-4", className)} />;
}

export function ScrollArea({ className, ...props }: HTMLAttributes<HTMLDivElement>) {
  return <div className={cn("overflow-auto", className)} {...props} />;
}
