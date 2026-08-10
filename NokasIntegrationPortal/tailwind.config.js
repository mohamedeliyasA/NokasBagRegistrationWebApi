/** @type {import('tailwindcss').Config} */
export default {
  content: ["./index.html", "./src/**/*.{js,ts,jsx,tsx}"],
  theme: {
    extend: {
      colors: {
        /*
         * NOKAS brand green.
         *
         * brand-800 (#0e4938) is the exact colour sampled from the official logo
         * artwork (Nokas_Digital_Logo.png / _stacked.svg). The rest of the scale
         * is tints and shades of it.
         *
         * Note: the codebase previously used #014737 in ~48 places, which is NOT
         * the brand green — that is why nothing quite matched the logo.
         */
        brand: {
          50: "#f0f9f5",
          100: "#d7f0e6",
          200: "#b0e1cd",
          300: "#7ecbaf",
          400: "#48ad8c",
          500: "#26906f",
          600: "#17755a",
          700: "#125c47",
          800: "#0e4938",
          900: "#0a3527",
        },
        /* Secondary navy - derived from the existing #1f477d. */
        accent: {
          50: "#f2f6fb",
          100: "#e2ebf6",
          200: "#c6d8ed",
          300: "#9bbade",
          400: "#6b95ca",
          500: "#4a76b4",
          600: "#3a5d97",
          700: "#1f477d",
          800: "#1c3c66",
          900: "#1a3355",
        },
      },
      boxShadow: {
        card: "0 1px 2px 0 rgb(0 0 0 / 0.04), 0 1px 3px 0 rgb(0 0 0 / 0.06)",
        "card-hover": "0 4px 12px -2px rgb(0 0 0 / 0.10), 0 2px 6px -2px rgb(0 0 0 / 0.06)",
      },
      keyframes: {
        "fade-in-up": {
          from: { opacity: "0", transform: "translateY(6px)" },
          to: { opacity: "1", transform: "translateY(0)" },
        },
      },
      animation: {
        "fade-in-up": "fade-in-up 0.35s ease-out both",
      },
    },
  },
  plugins: [],
};
