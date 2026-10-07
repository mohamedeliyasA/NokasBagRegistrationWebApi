import { BrowserRouter as Router, Routes, Route, useLocation, useNavigate, Link, Navigate } from "react-router-dom";
import { useIsAuthenticated } from "@azure/msal-react";
import { Suspense, lazy, useEffect } from "react";

// Public Pages
import LandingPage from "./pages/public/LandingPage";
import Product from "./pages/public/Product";
import Contact from "./pages/public/Contact";
import AboutUs from "./pages/public/AboutUs";
import CustomerOnboarding from "./pages/public/CustomerOnboarding";

// Swagger embeds a large third-party surface - keep it out of the initial bundle.
const PublicSwagger = lazy(() => import("./pages/public/PublicSwagger"));

// Protected Pages
// NOTE: Dashboard is deliberately not routed — it renders placeholder data, and
// /portal redirects straight to the API guide instead.
import BusinessActivityMonitoring from "./pages/private/BusinessActivityMonitoring";
import Profile from "./pages/private/Profile";

// API Guide - shell, market picker and market dispatch
import GuideLayout from "./pages/private/api-guide/GuideLayout";
import MarketPicker from "./pages/private/api-guide/MarketPicker";
import {
  ArchitecturePage,
  BagRegistrationEndpointsPage,
  BagRegistrationUseCasesPage,
  CountingResultEndpointsPage,
  CountingResultUseCasesPage,
} from "./pages/private/api-guide/sections";

// API Guide - sections shared by every market
import OverviewSection from "./pages/private/api-guide/OverviewSection";
import AuthSection from "./pages/private/api-guide/AuthSection";
import ErrorsSection from "./pages/private/api-guide/ErrorsSection";
import SwaggerSection from "./pages/private/api-guide/SwaggerSection";
import CorrelationSection from "./pages/private/api-guide/CorrelationSection";

// Layouts and Auth
import SidebarLayout from "./layouts/SidebarLayout";
import RequireAuth from "./components/RequireAuth";
import PublicLayout from "./layouts/PublicLayout";

const publicPaths = ["/", "/product", "/contact", "/about-us", "/customer-onboarding", "/swagger"];

function RedirectAfterLogin() {
  const isAuthenticated = useIsAuthenticated();
  const location = useLocation();
  const navigate = useNavigate();

  useEffect(() => {
    if (isAuthenticated && publicPaths.includes(location.pathname)) {
      navigate("/portal", { replace: true });
    }
  }, [isAuthenticated, location.pathname, navigate]);

  return null;
}

function PageLoader() {
  return (
    <div className="flex min-h-[60vh] items-center justify-center">
      <div className="text-center">
        <span className="mx-auto block h-8 w-8 animate-spin rounded-full border-2 border-brand-200 border-t-brand-700" />
        <p className="mt-3 text-sm font-medium text-gray-600">Loading…</p>
      </div>
    </div>
  );
}

function NotFound() {
  useEffect(() => {
    document.title = "Page not found | NOKAS";
  }, []);

  return (
    <PublicLayout>
      <section className="flex min-h-[60vh] items-center justify-center px-6 py-20">
        <div className="max-w-md text-center">
          <p className="text-7xl font-bold tracking-tight text-brand-200">404</p>
          <h1 className="mt-4 text-2xl font-bold text-gray-900">Page not found</h1>
          <p className="mt-3 text-sm leading-relaxed text-gray-600">
            The page you are looking for doesn't exist, or may have moved.
          </p>
          <div className="mt-7 flex flex-wrap justify-center gap-3">
            <Link
              to="/"
              className="rounded-full bg-brand-800 px-6 py-2.5 text-sm font-semibold text-white transition hover:bg-brand-700"
            >
              Back to home
            </Link>
            <Link
              to="/contact"
              className="rounded-full border border-gray-300 bg-white px-6 py-2.5 text-sm font-semibold text-gray-700 transition hover:bg-gray-50"
            >
              Contact us
            </Link>
          </div>
        </div>
      </section>
    </PublicLayout>
  );
}

function App() {
  return (
    <Router>
      <RedirectAfterLogin />

      <Suspense fallback={<PageLoader />}>
        <Routes>
          {/* Public Routes */}
          <Route path="/" element={<LandingPage />} />
          <Route path="/product" element={<Product />} />
          <Route path="/customer-onboarding" element={<CustomerOnboarding />} />
          <Route path="/contact" element={<Contact />} />
          <Route path="/about-us" element={<AboutUs />} />
          <Route path="/swagger" element={<PublicSwagger />} />

          {/* Protected Routes (Requires Login) */}
          <Route
            path="/portal"
            element={
              <RequireAuth>
                <SidebarLayout />
              </RequireAuth>
            }
          >
            {/* Redirect at the route level so the placeholder Dashboard never renders. */}
            <Route index element={<Navigate to="api-guide" replace />} />
            <Route path="businessActivityMonitoring" element={<BusinessActivityMonitoring />} />
            <Route path="profile" element={<Profile />} />

            {/* API Guide. The market is a route segment rather than client-side state so
                every page is deep-linkable and a partner can bookmark their own market. */}
            <Route path="api-guide">
              <Route index element={<MarketPicker />} />

              {/* Pre-market links. Static segments outrank the :market pattern, so these
                  keep working instead of being read as a market name. */}
              {[
                "architecture",
                "auth",
                "bag-registration",
                "bag-registration/use-cases",
                "counting-result",
                "counting-result/use-cases",
                "correlation",
                "errors",
                "swagger",
              ].map((legacy) => (
                <Route
                  key={legacy}
                  path={legacy}
                  element={<Navigate to={`/portal/api-guide/norway/${legacy}`} replace />}
                />
              ))}

              <Route path=":market" element={<GuideLayout />}>
                <Route index element={<OverviewSection />} />
                <Route path="architecture" element={<ArchitecturePage />} />
                <Route path="auth" element={<AuthSection />} />
                <Route path="bag-registration" element={<BagRegistrationEndpointsPage />} />
                <Route path="bag-registration/use-cases" element={<BagRegistrationUseCasesPage />} />
                <Route path="counting-result" element={<CountingResultEndpointsPage />} />
                <Route path="counting-result/use-cases" element={<CountingResultUseCasesPage />} />
                <Route path="correlation" element={<CorrelationSection />} />
                <Route path="errors" element={<ErrorsSection />} />
                <Route path="swagger" element={<SwaggerSection />} />
              </Route>
            </Route>
          </Route>

          {/* Fallback Route (404) */}
          <Route path="*" element={<NotFound />} />
        </Routes>
      </Suspense>
    </Router>
  );
}

export default App;
