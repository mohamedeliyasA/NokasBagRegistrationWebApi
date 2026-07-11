import { BrowserRouter as Router, Routes, Route, useLocation, useNavigate } from "react-router-dom";
import { useIsAuthenticated } from "@azure/msal-react";
import { useEffect } from "react";

// Public Pages
import LandingPage from "./pages/public/LandingPage";
import Product from "./pages/public/Product";
import Contact from "./pages/public/Contact";
import AboutUs from "./pages/public/AboutUs";
import CustomerOnboarding from "./pages/public/CustomerOnboarding";

// Protected Pages
import Dashboard from "./pages/private/Dashboard";
import BusinessActivityMonitoring from "./pages/private/BusinessActivityMonitoring";
import Profile from "./pages/private/Profile";

// API Guide Sections
import GuideLayout from "./pages/private/api-guide/GuideLayout";
import OverviewSection from "./pages/private/api-guide/OverviewSection";
import ArchitectureSection from "./pages/private/api-guide/ArchitectureSection";
import AuthSection from "./pages/private/api-guide/AuthSection";
import EndpointsSection from "./pages/private/api-guide/EndpointsSection";
import ErrorsSection from "./pages/private/api-guide/ErrorsSection";
import SwaggerSection from "./pages/private/api-guide/SwaggerSection";
import CorrelationSection from "./pages/private/api-guide/CorrelationSection";

// Layouts and Auth
import SidebarLayout from "./layouts/SidebarLayout";
import RequireAuth from "./components/RequireAuth";

function RedirectAfterLogin() {
  const isAuthenticated = useIsAuthenticated();
  const location = useLocation();
  const navigate = useNavigate();

  useEffect(() => {
    const isPublicPage = [
      "/", "/product", "/contact", "/about-us", "/customer-onboarding"
    ].includes(location.pathname);

    if (isAuthenticated && isPublicPage) {
      navigate("/portal", { replace: true });
    }
  }, [isAuthenticated, location.pathname, navigate]);

  return null;
}

function App() {
  return (
    <Router>
      <RedirectAfterLogin />

      <Routes>
        {/* Public Routes */}
        <Route path="/" element={<LandingPage />} />
        <Route path="/product" element={<Product />} />
        <Route path="/customer-onboarding" element={<CustomerOnboarding />} />
        <Route path="/contact" element={<Contact />} />
        <Route path="/about-us" element={<AboutUs />} />

        {/* Protected Routes (Requires Login) */}
        <Route
          path="/portal"
          element={
            <RequireAuth>
              <SidebarLayout />
            </RequireAuth>
          }
        >
          {/* Portal Pages */}
          <Route index element={<Dashboard />} />
          <Route path="businessActivityMonitoring" element={<BusinessActivityMonitoring />} />
          <Route path="profile" element={<Profile />} />

          {/* API Guide Sections */}
          <Route path="api-guide" element={<GuideLayout />}>
            <Route index element={<OverviewSection />} />
            <Route path="architecture" element={<ArchitectureSection />} />
            <Route path="auth" element={<AuthSection />} />
            <Route path="endpoints" element={<EndpointsSection />} />
            <Route path="errors" element={<ErrorsSection />} />
            <Route path="swagger" element={<SwaggerSection />} />
            <Route path="correlation" element={<CorrelationSection />} />
          </Route>
        </Route>

        {/* Fallback Route (404) */}
        <Route
          path="*"
          element={
            <div className="p-12 text-center">
              <h1 className="text-3xl font-bold text-red-600">404 – Page Not Found</h1>
              <p className="mt-3 text-gray-600">The page you’re looking for doesn’t exist.</p>
            </div>
          }
        />
      </Routes>
    </Router>
  );
}

export default App;
