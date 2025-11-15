
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

export const SettingsProvider = ({ children }) => {
    const [settings, setSettings] = useState({
        name: 'Keytietkiem',
        slogan: '',
        logoUrl: null,
        primaryColor: '#2563EB',
        secondaryColor: '#111827',
        font: 'Inter',
        contact: {
            address: '',
            phone: '',
            email: ''
        },
        social: {
            facebook: '',
            instagram: '',
            zalo: '',
            tiktok: ''
        }
    });

    const [loading, setLoading] = useState(true);
    const [error, setError] = useState(null);

    useEffect(() => {
        loadSettings();
    }, []);

    const loadSettings = async () => {
        try {
            console.log('🔧 Loading website settings...');
            const data = await settingsApi.getSettings();

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

                // Apply CSS variables
                if (data.primaryColor) {
                    document.documentElement.style.setProperty('--primary', data.primaryColor);
                }
                if (data.secondaryColor) {
                    document.documentElement.style.setProperty('--secondary', data.secondaryColor);
                }
            }
        } catch (err) {
            console.error('❌ Load settings error:', err);
            setError(err.message || 'Không thể tải cấu hình website');
        } finally {
            setLoading(false);
        }
    };

    const value = {
        settings,
        loading,
        error,
        refreshSettings: loadSettings
    };

    return (
        <SettingsContext.Provider value={value}>
            {children}
        </SettingsContext.Provider>
    );
};

export default SettingsContext;