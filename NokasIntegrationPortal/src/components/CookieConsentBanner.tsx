// src/components/CookieConsentBanner.tsx
import { useState, useEffect } from "react";

export default function CookieConsentBanner() {
  const [showBanner, setShowBanner] = useState(false);

  useEffect(() => {
    const accepted = localStorage.getItem("cookiesAccepted");
    if (!accepted) {
      setShowBanner(true);
    }
  }, []);

  const handleAccept = () => {
    localStorage.setItem("cookiesAccepted", "true");
    setShowBanner(false);
  };

  if (!showBanner) return null;

  return (
    <div className="fixed inset-0 bg-black bg-opacity-70 flex items-center justify-center z-50">
      <div className="bg-white text-gray-900 text-sm p-6 rounded-lg shadow-lg max-w-md w-full mx-4 text-center">
        <p className="mb-4">
          We use only essential cookies to ensure the website functions properly
          and securely. We do not store personal data or use tracking
          technologies. By continuing to use this site, you agree to the use of
          these essential cookies.
        </p>
        <button
          onClick={handleAccept}
          className="bg-green-500 hover:bg-green-400 text-white font-semibold px-5 py-2 rounded transition"
        >
          Accept and Continue
        </button>
      </div>
    </div>
  );
}
