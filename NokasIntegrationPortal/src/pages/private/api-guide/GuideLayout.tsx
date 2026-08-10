import { Outlet, NavLink } from "react-router-dom";

const sections = [
  { id: "overview", label: "Overview", icon: "📘" },
  { id: "architecture", label: "Architecture", icon: "🧩" },
  { id: "auth", label: "Authentication", icon: "🔐" },
  { id: "endpoints", label: "Endpoints", icon: "🧭" },
  { id: "use-cases", label: "Use cases", icon: "💡" },
  { id: "correlation", label: "Correlation", icon: "🔗" },
  { id: "errors", label: "Errors", icon: "❗" },
  { id: "swagger", label: "Swagger", icon: "🧪" },
];

export default function GuideLayout() {
  return (
    <div className="flex h-full w-full gap-4">
      {/* Section nav */}
      <aside className="w-16 shrink-0 sm:w-52">
        <nav className="sticky top-4 flex flex-col gap-1 rounded-xl border border-gray-200 bg-white p-2 shadow-card">
          <p className="hidden px-2 pb-1 pt-1.5 text-[11px] font-semibold uppercase tracking-widest text-gray-400 sm:block">
            API Guide
          </p>
          {sections.map((s) => (
            <NavLink
              key={s.id}
              to={s.id === "overview" ? "." : s.id}
              end
              title={s.label}
              className={({ isActive }) =>
                [
                  "flex w-full items-center gap-2.5 rounded-lg p-2 text-sm font-medium transition sm:justify-start",
                  isActive
                    ? "bg-brand-50 text-brand-800 ring-1 ring-inset ring-brand-200"
                    : "text-gray-600 hover:bg-gray-100 hover:text-brand-800",
                ].join(" ")
              }
            >
              <span className="text-base leading-none">{s.icon}</span>
              <span className="hidden sm:inline">{s.label}</span>
            </NavLink>
          ))}
        </nav>
      </aside>

      {/* Content */}
      <main className="min-w-0 flex-1">
        <div className="mx-auto max-w-5xl rounded-xl border border-gray-200 bg-white p-5 shadow-card sm:p-8">
          <Outlet />
        </div>
      </main>
    </div>
  );
}
