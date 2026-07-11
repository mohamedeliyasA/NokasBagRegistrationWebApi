import PublicHeader from "../components/PublicHeader";
import CookieConsentBanner from "../components/CookieConsentBanner";

export default function PublicLayout({ children }: { children: React.ReactNode }) {
  return (
    <div className="flex flex-col min-h-screen bg-white text-gray-800">
      <PublicHeader />

      <main className="flex-1 px-6 py-12 max-w-5xl mx-auto animate-fade-in">
        {children}
      </main>

      <footer className="bg-[#0E4A3C] text-white py-12 px-8">
        <div className="max-w-6xl mx-auto flex flex-col md:flex-row items-center justify-between gap-6 text-center md:text-left">
          <div className="flex items-center">
            <img src="/logo.png" alt="NOKAS Logo" className="h-8 mr-2" />
            <div className="flex flex-col leading-tight text-left">
              <span className="text-base font-semibold text-white">NOKAS</span>
              <span className="text-sm text-gray-300">Payment solutions</span>
            </div>
          </div>
          <p className="max-w-md text-sm text-gray-300">
            Nokas Verdi­håndtering AS is a nationwide supplier of asset
            management services to banks, private businesses and public entities.
          </p>
        </div>
      </footer>
      {/* <CookieConsentBanner /> */}
    </div>
  );
}
