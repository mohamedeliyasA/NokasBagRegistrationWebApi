import { useMsal } from "@azure/msal-react";
import { useEffect, useMemo, useState } from "react";
import { CopyButton, PageHeader, SectionHeading } from "@/components/ui";

type Claims = Record<string, unknown>;

function asString(value: unknown): string | null {
  if (typeof value === "string" && value.trim() !== "") return value;
  if (Array.isArray(value) && typeof value[0] === "string") return value[0];
  return null;
}

function InfoRow({
  label,
  value,
  copyable = false,
  mono = false,
}: {
  label: string;
  value: string | null;
  copyable?: boolean;
  mono?: boolean;
}) {
  const missing = value === null;

  return (
    <div className="rounded-xl border border-gray-200 bg-white p-4 shadow-card">
      <dt className="text-xs font-semibold uppercase tracking-wider text-gray-500">{label}</dt>
      <dd className="mt-1.5 flex items-start gap-2">
        <span
          className={[
            "min-w-0 flex-1 break-words text-sm",
            mono ? "font-mono" : "font-medium",
            missing ? "italic text-gray-400" : "text-gray-900",
          ].join(" ")}
        >
          {value ?? "Not provided"}
        </span>
        {copyable && !missing && (
          <span className="shrink-0 rounded bg-gray-100">
            <CopyButton value={value} label={label} tone="light" />
          </span>
        )}
      </dd>
    </div>
  );
}

export default function Profile() {
  const { instance } = useMsal();
  const [claims, setClaims] = useState<Claims | null>(null);

  useEffect(() => {
    document.title = "Portal | Profile";

    const accounts = instance.getAllAccounts();
    if (accounts.length === 0) {
      setClaims(null);
      return;
    }

    const activeAccount = instance.getActiveAccount() ?? accounts[0];
    instance.setActiveAccount(activeAccount);
    // Deliberately not logged - ID token claims contain personal data.
    setClaims((activeAccount.idTokenClaims ?? null) as Claims | null);
  }, [instance]);

  const profile = useMemo(() => {
    if (!claims) return null;

    const given = asString(claims.given_name);
    const family = asString(claims.family_name);
    const fullName = [given, family].filter(Boolean).join(" ") || null;

    return {
      fullName,
      // B2C returns `emails` as an array; fall back to the singular form.
      email: asString(claims.emails) ?? asString(claims.email),
      company: asString(claims.api_companyName),
      clientId: asString(claims.api_clientId),
      contactPerson: asString(claims.api_contactPerson),
      contactEmail: asString(claims.api_contactPersonEmail),
      loginTime:
        typeof claims.auth_time === "number"
          ? new Date(claims.auth_time * 1000).toLocaleString()
          : null,
      initials:
        given && family ? `${given[0]}${family[0]}`.toUpperCase() : (given?.[0] ?? "N").toUpperCase(),
    };
  }, [claims]);

  if (!profile) {
    return (
      <section className="space-y-6">
        <PageHeader title="User profile" />
        <div className="rounded-xl border border-dashed border-gray-300 bg-white p-10 text-center">
          <p className="text-sm text-gray-500">No profile data available.</p>
          <p className="mt-1 text-xs text-gray-400">Try signing out and back in.</p>
        </div>
      </section>
    );
  }

  return (
    <section className="space-y-8">
      {/* ---------------- Identity header ---------------- */}
      <div className="overflow-hidden rounded-2xl border border-gray-200 bg-white shadow-card">
        <div className="h-20 bg-gradient-to-r from-brand-800 to-brand-600" />
        <div className="flex flex-col gap-4 px-6 pb-6 sm:flex-row sm:items-end">
          <span className="-mt-10 flex h-20 w-20 shrink-0 items-center justify-center rounded-2xl border-4 border-white bg-brand-800 text-2xl font-bold text-white shadow-card">
            {profile.initials}
          </span>
          <div className="min-w-0 sm:pb-1">
            <h2 className="truncate text-xl font-bold text-gray-900">
              {profile.fullName ?? "Welcome"}
            </h2>
            {profile.email && <p className="truncate text-sm text-gray-500">{profile.email}</p>}
          </div>
        </div>
      </div>

      {/* ---------------- Account ---------------- */}
      <div className="space-y-4">
        <SectionHeading id="account" icon="👤">
          Account details
        </SectionHeading>

        <dl className="grid gap-4 sm:grid-cols-2">
          <InfoRow label="Full name" value={profile.fullName} />
          <InfoRow label="Email" value={profile.email} />
          <InfoRow label="Last sign-in" value={profile.loginTime} />
        </dl>
      </div>

      {/* ---------------- Organisation ---------------- */}
      <div className="space-y-4">
        <SectionHeading id="organisation" icon="🏢">
          Organisation
        </SectionHeading>

        <dl className="grid gap-4 sm:grid-cols-2">
          <InfoRow label="Company name" value={profile.company} />
          <InfoRow label="API client ID" value={profile.clientId} copyable mono />
          <InfoRow label="Contact person" value={profile.contactPerson} />
          <InfoRow label="Contact person email" value={profile.contactEmail} />
        </dl>
      </div>
    </section>
  );
}
