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
            className="bg-green-400 hover:bg-green-300 text-black font-semibold px-6 py-2 rounded-full transition"
          >
            Launch Portal
          </button>
        </div>
      </div>

      {/* Introduction */}
      <section className="bg-white py-12 px-6 text-center">
        <h1 className="text-3xl sm:text-4xl md:text-5xl font-bold mb-4 text-[#013c2f]">
          Welcome to the NOKAS Integration Portal
        </h1>
        <p className="text-gray-700 text-lg max-w-3xl mx-auto">
          Seamlessly access, analyze, and manage your financial integrations
          from a single, intuitive dashboard. Empowering both internal teams
          and external partners with complete visibility into bag registration,
          order tracking, and actionable insights.
        </p>
      </section>

      {/* Key Features */}
      <section id="products" className="bg-white text-black py-20 px-8">
        <div className="max-w-5xl mx-auto text-center">
          <h2 className="text-3xl font-bold mb-6">Key Features</h2>
          <div className="grid grid-cols-1 md:grid-cols-3 gap-8">
            <div className="bg-gray-100 p-6 rounded shadow">
              <h3 className="text-xl font-semibold mb-2">Real-time Monitoring</h3>
              <p>Track integration events and bag registrations as they happen.</p>
            </div>
            <div className="bg-gray-100 p-6 rounded shadow">
              <h3 className="text-xl font-semibold mb-2">Business Workflows</h3>
              <p>Visualize and manage your end-to-end processes efficiently.</p>
            </div>
            <div className="bg-gray-100 p-6 rounded shadow">
              <h3 className="text-xl font-semibold mb-2">API Documentation</h3>
              <p className="text-sm text-gray-700">
                Explore detailed Swagger-based API docs for Bag Registration,
                including endpoints, request formats, and examples.
              </p>
            </div>
          </div>
        </div>
      </section>
    </PublicLayout>
  );
}
