// Cookie Consent Manager
// Gestisce il consenso ai cookie con supporto multi-categoria

(function() {
    'use strict';

    const STORAGE_KEY = 'klod_cookie_preferences';
    const CONSENT_VERSION = '1.0';

    // Preferenze default
    const DEFAULT_PREFERENCES = {
        essential: true,      // Sempre true
        functional: false,
        analytics: false,     // Preparato per futuro
        timestamp: null,
        version: CONSENT_VERSION
    };

    // Inizializzazione al caricamento del DOM
    document.addEventListener('DOMContentLoaded', function() {
        initCookieConsent();
    });

    function initCookieConsent() {
        const preferences = getCookiePreferences();

        // Se non ci sono preferenze salvate, mostra il banner
        if (!preferences || !preferences.timestamp) {
            showBanner();
        } else {
            // Applica le preferenze salvate
            applyCookiePreferences(preferences);
        }

        // Bind eventi pulsanti
        bindEvents();
    }

    function bindEvents() {
        // Pulsanti banner compatto
        const acceptAllBtn = document.getElementById('cookieAcceptAll');
        const rejectBtn = document.getElementById('cookieReject');
        const customizeBtn = document.getElementById('cookieCustomize');

        if (acceptAllBtn) {
            acceptAllBtn.addEventListener('click', acceptAll);
        }
        if (rejectBtn) {
            rejectBtn.addEventListener('click', rejectAll);
        }
        if (customizeBtn) {
            customizeBtn.addEventListener('click', openCustomizePanel);
        }

        // Pulsanti panel personalizzazione
        const saveCustomBtn = document.getElementById('cookieSaveCustom');
        const acceptAllCustomBtn = document.getElementById('cookieAcceptAllCustom');
        const closePanel = document.getElementById('cookieClosePanel');

        if (saveCustomBtn) {
            saveCustomBtn.addEventListener('click', saveCustomPreferences);
        }
        if (acceptAllCustomBtn) {
            acceptAllCustomBtn.addEventListener('click', acceptAll);
        }
        if (closePanel) {
            closePanel.addEventListener('click', closeCustomizePanel);
        }

        // Chiudi panel con ESC
        document.addEventListener('keydown', function(e) {
            if (e.key === 'Escape') {
                const panel = document.getElementById('cookieCustomizePanel');
                if (panel && panel.style.display === 'block') {
                    closeCustomizePanel();
                }
            }
        });

        // Chiudi panel cliccando sull'overlay
        const overlay = document.getElementById('cookieOverlay');
        if (overlay) {
            overlay.addEventListener('click', closeCustomizePanel);
        }
    }

    function getCookiePreferences() {
        try {
            const stored = localStorage.getItem(STORAGE_KEY);
            if (stored) {
                return JSON.parse(stored);
            }
        } catch (e) {
            console.error('Errore nel leggere le preferenze cookie:', e);
        }
        return null;
    }

    function saveCookiePreferences(preferences) {
        try {
            preferences.timestamp = new Date().toISOString();
            preferences.version = CONSENT_VERSION;
            localStorage.setItem(STORAGE_KEY, JSON.stringify(preferences));

            // Imposta anche il cookie ASP.NET per compatibilità
            setAspNetConsentCookie(preferences);

            return true;
        } catch (e) {
            console.error('Errore nel salvare le preferenze cookie:', e);
            return false;
        }
    }

    function setAspNetConsentCookie(preferences) {
        // Imposta il cookie .AspNet.Consent per ASP.NET Core
        const consentValue = preferences.functional || preferences.analytics ? 'yes' : 'no';
        const expires = new Date();
        expires.setFullYear(expires.getFullYear() + 1); // 1 anno

        document.cookie = `.AspNet.Consent=${consentValue}; expires=${expires.toUTCString()}; path=/; SameSite=Lax`;
    }

    function showBanner() {
        const banner = document.getElementById('cookieConsentBanner');
        if (banner) {
            banner.style.display = 'block';
            // Trigger animazione
            setTimeout(() => {
                banner.classList.add('show');
            }, 10);
        }
    }

    function hideBanner() {
        const banner = document.getElementById('cookieConsentBanner');
        if (banner) {
            banner.classList.remove('show');
            setTimeout(() => {
                banner.style.display = 'none';
            }, 300); // Tempo animazione
        }
    }

    function acceptAll() {
        const preferences = {
            essential: true,
            functional: true,
            analytics: false  // Ancora false perché non usato
        };

        if (saveCookiePreferences(preferences)) {
            applyCookiePreferences(preferences);
            closeCustomizePanel();
            hideBanner();
        }
    }

    function rejectAll() {
        const preferences = {
            essential: true,
            functional: false,
            analytics: false
        };

        if (saveCookiePreferences(preferences)) {
            applyCookiePreferences(preferences);
            closeCustomizePanel();
            hideBanner();
        }
    }

    function openCustomizePanel() {
        const panel = document.getElementById('cookieCustomizePanel');
        const overlay = document.getElementById('cookieOverlay');

        if (panel && overlay) {
            // Carica preferenze correnti nei checkbox
            const currentPrefs = getCookiePreferences() || DEFAULT_PREFERENCES;

            const functionalCheck = document.getElementById('cookieFunctional');
            const analyticsCheck = document.getElementById('cookieAnalytics');

            if (functionalCheck) {
                functionalCheck.checked = currentPrefs.functional;
            }
            if (analyticsCheck) {
                analyticsCheck.checked = currentPrefs.analytics;
                analyticsCheck.disabled = true; // Disabilitato perché non in uso
            }

            overlay.style.display = 'block';
            panel.style.display = 'block';

            // Focus sul panel per accessibilità
            panel.focus();
        }
    }

    function closeCustomizePanel() {
        const panel = document.getElementById('cookieCustomizePanel');
        const overlay = document.getElementById('cookieOverlay');

        if (panel && overlay) {
            panel.style.display = 'none';
            overlay.style.display = 'none';
        }
    }

    function saveCustomPreferences() {
        const functionalCheck = document.getElementById('cookieFunctional');
        const analyticsCheck = document.getElementById('cookieAnalytics');

        const preferences = {
            essential: true,
            functional: functionalCheck ? functionalCheck.checked : false,
            analytics: false  // Sempre false per ora
        };

        if (saveCookiePreferences(preferences)) {
            applyCookiePreferences(preferences);
            closeCustomizePanel();
            hideBanner();
        }
    }

    function applyCookiePreferences(preferences) {
        // Qui puoi aggiungere logica per abilitare/disabilitare funzionalità
        // basate sulle preferenze dell'utente

        console.log('Preferenze cookie applicate:', preferences);

        // Esempio: se functional è false, potresti voler disabilitare alcune animazioni
        if (!preferences.functional) {
            // Disabilita animazioni AOS
            if (typeof AOS !== 'undefined') {
                AOS.init({ disable: true });
            }
        }

        // Quando implementerai Google Analytics:
        if (preferences.analytics && typeof gtag !== 'undefined') {
            // Abilita Google Analytics
            gtag('consent', 'update', {
                'analytics_storage': 'granted'
            });
        }
    }

    // Funzione per revocare il consenso (può essere chiamata da un link nel footer)
    window.revokeCookieConsent = function() {
        localStorage.removeItem(STORAGE_KEY);
        // Ricarica la pagina per mostrare di nuovo il banner
        window.location.reload();
    };

    // Funzione per ottenere le preferenze correnti (utile per debug)
    window.getCookiePreferences = getCookiePreferences;

})();
