import React, { useEffect } from "react";
import { useSettings } from "../../contexts/SettingContext.js";
import Header from "./PublicHeader.jsx";
import Footer from "./PublicFooter.jsx";

const ClientLayout = ({ children }) => {
  const { settings, loading } = useSettings();

  useEffect(() => {
    if (settings?.font) {
      const fontFamily = settings.font.includes("(")
        ? settings.font.split("(")[0].trim()
        : settings.font;

      document.body.style.fontFamily = `${fontFamily}, system-ui, -apple-system, 'Segoe UI', sans-serif`;
    }
  }, [settings?.font]);

  return (
    <div>
      <Header settings={settings} loading={loading} />
      <main className="al-admin-main">
        {children}
      </main>
      <Footer />
    </div>
  );
};

export default ClientLayout;
