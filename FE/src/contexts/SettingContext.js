import React, { createContext, useContext, useState, useEffect } from 'react';
import { settingsApi } from '../services/settings';

const SettingsContext = createContext(null);

export const useSettings = () => {
    const context = useContext(SettingsContext);
    if (!context) {
        throw new Error('useSettings must be used within SettingsProvider');
    }
    return context;
};

function normalizeFontName(raw) {
    if (!raw) return null;
    return raw.split('(')[0].trim();
}

function loadGoogleFont(fontName) {
    if (!fontName) return;
    const safe = fontName.replace(/\s+/g, '+');
    // Avoid duplicate link tags
    if (document.head.querySelector(`link[data-gfont="${safe}"]`)) return;
    const href = `https://fonts.googleapis.com/css2?family=${encodeURIComponent(safe)}:wght@300;400;500;600;700&display=swap`;
    const link = document.createElement('link');
    link.rel = 'stylesheet';
    link.href = href;
    link.setAttribute('data-gfont', safe);
    document.head.appendChild(link);
}

// convert hex like "#112233" or "112233" or "#123" -> "17,34,51"
function hexToRgb(hex) {
    if (!hex) return null;
    hex = hex.replace('#', '').trim();
    if (hex.length === 3) {
        hex = hex.split('').map(c => c + c).join('');
    }
    if (hex.length !== 6) return null;
    const bigint = parseInt(hex, 16);
    const r = (bigint >> 16) & 255;
    const g = (bigint >> 8) & 255;
    const b = bigint & 255;
    return `${r},${g},${b}`;
}

export const SettingsProvider = ({ children }) => {
    const [settings, setSettings] = useState({
        name: 'Keytietkiem',
        slogan: '',
        logoUrl: null,
        primaryColor: '#2563EB',
        secondaryColor: '#111827',
        font: 'Inter',
        contact: { address: '', phone: '', email: '' },
        social: { facebook: '', instagram: '', zalo: '', tiktok: '' }
    });

    const [loading, setLoading] = useState(true);
    const [error, setError] = useState(null);

    useEffect(() => {
        loadSettings();
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, []);

    const loadSettings = async () => {
        try {
            console.log('🔧 Loading website settings...');
            // support both: axiosClient wrapper returning response.data or returning response
            const resp = await settingsApi.getPublicSettings();
            const data = resp && resp.data !== undefined ? resp.data : resp;

            console.log('✅ Settings loaded:', data);

            if (data && typeof data === 'object') {
                setSettings(prev => ({
                    ...prev,
                    name: data.name || prev.name,
                    slogan: data.slogan || prev.slogan,
                    logoUrl: data.logoUrl || prev.logoUrl,
                    primaryColor: data.primaryColor || prev.primaryColor,
                    secondaryColor: data.secondaryColor || prev.secondaryColor,
                    font: data.font || prev.font,
                    contact: {
                        address: data.contact?.address || prev.contact.address,
                        phone: data.contact?.phone || prev.contact.phone,
                        email: data.contact?.email || prev.contact.email,
                    },
                    social: {
                        facebook: data.social?.facebook || prev.social.facebook,
                        instagram: data.social?.instagram || prev.social.instagram,
                        zalo: data.social?.zalo || prev.social.zalo,
                        tiktok: data.social?.tiktok || prev.social.tiktok,
                    }
                }));

                // Apply CSS variables for colors
                if (data.primaryColor) {
                    document.documentElement.style.setProperty('--primary', data.primaryColor);
                }
                if (data.secondaryColor) {
                    document.documentElement.style.setProperty('--secondary', data.secondaryColor);
                    const rgb = hexToRgb(data.secondaryColor);
                    if (rgb) {
                        document.documentElement.style.setProperty('--secondary-rgb', rgb);
                    }
                }
                // extra helpers
                if (data.primaryInkColor) {
                    document.documentElement.style.setProperty('--primary-ink', data.primaryInkColor);
                }

                // FONT handling
                const rawFont = data.font || '';
                const fontName = normalizeFontName(rawFont);
                if (fontName) {
                    const fontFamilyValue = `'${fontName}', system-ui, -apple-system, 'Segoe UI', Roboto, Arial, sans-serif`;
                    document.documentElement.style.setProperty('--font-family', fontFamilyValue);
                    // immediate fallback (so text updates even if webfont loads async)
                    document.body.style.fontFamily = fontFamilyValue;
                    // Try load Google Font (safe: if font exists on Google)
                    loadGoogleFont(fontName);
                }
            }
        } catch (err) {
            console.error('❌ Load settings error:', err);
            setError(err?.message || 'Không thể tải cấu hình website');
        } finally {
            setLoading(false);
        }
    };

    const value = { settings, loading, error, refreshSettings: loadSettings };

    return (
        <SettingsContext.Provider value={value}>
            {children}
        </SettingsContext.Provider>
    );
};

export default SettingsContext;