import { useEffect, useState } from "react";
import { useMsal, useIsAuthenticated } from "@azure/msal-react";
import { useLocation, useNavigate } from "react-router-dom";
import PublicLayout from "../../layouts/PublicLayout";

const rotatingTexts = [
  "We are the European leader in cash handling and digital payments.",
  "Bridging the gap between cash and digital – efficiently and securely.",
  "Your trusted partner for payment solutions.",
  "Ensuring access to cash — anytime, anywhere.",
];

export default function LandingPage() {
  const { instance, inProgress } = useMsal();
  const isAuthenticated = useIsAuthenticated();
  const navigate = useNavigate();
  const location = useLocation();

  const [textIndex, setTextIndex] = useState(0);
  const [fadeIn, setFadeIn] = useState(true);
  const from = location.state?.from?.pathname || "/portal";

  const handleSignIn = () => {
    instance.loginRedirect().catch((err) => {
      console.error("Login redirect failed:", err);
    });
  };

  // Redirect if already authenticated
  useEffect(() => {
    if (inProgress === "none" && isAuthenticated) {
      navigate(from, { replace: true });
    }
  }, [inProgress, isAuthenticated, navigate, from]);

  // Text rotation logic
  useEffect(() => {
    const interval = setInterval(() => {
      setFadeIn(false);
      setTimeout(() => {
        setTextIndex((prev) => (prev + 1) % rotatingTexts.length);
        setFadeIn(true);
      }, 400);
    }, 4000);
    return () => clearInterval(interval);
  }, []);

  return (
    <PublicLayout>
      {/*
        PublicLayout used to apply this container to every page. The other pages
        now render full-bleed and manage their own width, so the original home
        page keeps its previous look by re-applying that container here.
      */}
      <div className="mx-auto max-w-5xl px-6 py-12">
      {/* Hero Section */}
      <div className="relative h-[50vh] w-full overflow-hidden">
        <video
          className="absolute inset-0 w-full h-full object-cover"
          autoPlay
          muted
          loop
          playsInline
          preload="metadata"
        >
          <source src="/hero-video.mp4" type="video/mp4" />
          Your browser does not support the video tag.
        </video>
        <div className="absolute inset-0 bg-black bg-opacity-50 z-10 flex flex-col justify-center items-center text-center px-4">
          <p
            className={`text-white text-lg sm:text-xl md:text-2xl font-medium max-w-2xl mb-6 transition-opacity duration-700 ${
              fadeIn ? "opacity-100" : "opacity-0"
            }`}
          >
            {rotatingTexts[textIndex]}
          </p>
          <button
            onClick={handleSignIn}
            className="bg-brand-400 hover:bg-brand-300 text-brand-900 font-semibold px-6 py-2 rounded-full transition"
          >
            Launch Portal
          </button>
        </div>
      </div>

      {/* Introduction */}
      <section className="bg-white py-12 px-6 text-center">
        <h1 className="text-3xl sm:text-4xl md:text-5xl font-bold mb-4 text-brand-800">
          Welcome to the NOKAS Integration Portal
        </h1>
        <p className="text-gray-700 text-lg max-w-3xl mx-auto">
          Seamlessly access and manage your cash handling integrations from a
          single, intuitive portal. Empowering both internal teams and external
          partners with secure bag registration, counting results, and complete
          API documentation.
        </p>
      </section>

      {/* Key Features */}
      <section id="products" className="bg-white text-black py-20 px-8">
        <div className="max-w-5xl mx-auto text-center">
          <h2 className="text-3xl font-bold mb-6">Key Features</h2>
          <div className="grid grid-cols-1 md:grid-cols-3 gap-8">
            <div className="bg-gray-100 p-6 rounded shadow">
              <h3 className="text-xl font-semibold mb-2">Bag Registration</h3>
              <p className="text-sm text-gray-700">
                Declare cash bags through a secure REST API before pickup, with
                duplicate protection and end-to-end traceability.
              </p>
            </div>
            <div className="bg-gray-100 p-6 rounded shadow">
              <h3 className="text-xl font-semibold mb-2">API Documentation</h3>
              <p className="text-sm text-gray-700">
                Explore detailed Swagger-based API docs for Bag Registration,
                including endpoints, request formats, and examples.
              </p>
            </div>
            <div className="bg-gray-100 p-6 rounded shadow">
              <div className="mb-2 flex items-center justify-center gap-2">
                <h3 className="text-xl font-semibold">Order Tracking</h3>
                <span className="rounded-full bg-brand-100 px-2 py-0.5 text-xs font-semibold text-brand-800">
                  Coming soon
                </span>
              </div>
              <p className="text-sm text-gray-700">
                Follow every bag from registration through pickup to counted
                result, directly in the portal.
              </p>
            </div>
          </div>
        </div>
      </section>
      </div>
    </PublicLayout>
  );
}
