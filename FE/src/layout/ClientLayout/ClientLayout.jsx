import React, { useCallback, useEffect, useState } from "react";
import { useSettings } from "../../contexts/SettingContext.js";
import Header from "./PublicHeader.jsx";
import Footer from "./PublicFooter.jsx";
import profileService from "../../services/profile";
import ChatWidget from "../../components/SupportChat/ChatWidget";

const unwrap = (payload) =>
  payload?.data !== undefined ? payload.data : payload;

const ClientLayout = ({ children }) => {
  const { settings, loading } = useSettings();
  const [profile, setProfile] = useState(null);
  const [profileLoading, setProfileLoading] = useState(true);

  useEffect(() => {
    if (settings?.font) {
      const fontFamily = settings.font.includes("(")
        ? settings.font.split("(")[0].trim()
        : settings.font;

      document.body.style.fontFamily = `${fontFamily}, system-ui, -apple-system, 'Segoe UI', sans-serif`;
    }
  }, [settings?.font]);

  const loadProfile = useCallback(async () => {
    const hasWindow = typeof window !== "undefined";
    const token = hasWindow
      ? window.localStorage.getItem("access_token")
      : null;
    if (!token) {
      setProfile(null);
      setProfileLoading(false);
      return;
    }

    setProfileLoading(true);
    try {
      const response = await profileService.getProfile();
      setProfile(unwrap(response));
    } catch (error) {
      console.error("Unable to load customer profile:", error);
      setProfile(null);
    } finally {
      setProfileLoading(false);
    }
  }, []);

  useEffect(() => {
    loadProfile();
  }, [loadProfile]);

  useEffect(() => {
    if (typeof window === "undefined") return undefined;
    const handleProfileUpdated = () => loadProfile();
    window.addEventListener("profile-updated", handleProfileUpdated);
    return () => {
      window.removeEventListener("profile-updated", handleProfileUpdated);
    };
  }, [loadProfile]);

  return (
    <div>
      <Header
        settings={settings}
        loading={loading}
        profile={profile}
        profileLoading={profileLoading}
      />
      <main className="al-admin-main">{children}</main>
      <Footer />

      {/* ✅ Widget chat cho customer */}
      <ChatWidget />
    </div>
  );
};

export default ClientLayout;
