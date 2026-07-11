import { Outlet, NavLink } from "react-router-dom";

const sections = [
  { id: "overview", label: "Overview", icon: "📘" },
  { id: "architecture", label: "Architecture", icon: "🧩" },
  { id: "auth", label: "Authentication", icon: "🔐" },
  { id: "endpoints", label: "Endpoints", icon: "🧭" },
  { id: "correlation", label: "Correlation", icon: "🔗" },
  { id: "use-cases", label: "Use Cases", icon: "📚" },
  { id: "errors", label: "Errors", icon: "❗" },
  { id: "swagger", label: "Swagger", icon: "🧪" },

];

export default function GuideLayout() {
  return (
    <div className="flex h-full w-full">
      {/* Sidebar */}
      <aside className="w-16 sm:w-48 border-r bg-white p-2 flex flex-col items-center sm:items-stretch">
        {sections.map((s) => (
          <NavLink
            key={s.id}
            to={s.id === "overview" ? "." : s.id}
            end
            className={({ isActive }) =>
              `flex items-center sm:justify-start gap-2 w-full p-2 rounded hover:bg-gray-100 transition text-sm font-medium ${
                isActive ? "bg-gray-200" : ""
              }`
            }
          >
            <span className="text-lg">{s.icon}</span>
            <span className="hidden sm:inline">{s.label}</span>
          </NavLink>
        ))}
      </aside>

      {/* Content */}
      <main className="flex-1 overflow-auto p-4">
        <div className="max-w-5xl mx-auto p-6 bg-white rounded shadow">
          <Outlet />
        </div>
      </main>
    </div>
  );
}
