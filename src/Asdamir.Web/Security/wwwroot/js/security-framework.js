/**
 * Security Framework JavaScript utilities
 * Handles activity tracking, session monitoring, and security features
 */
window.SecurityFramework = (function() {
    let activityCallback = null;
    let activityEvents = ['mousedown', 'mousemove', 'keypress', 'scroll', 'touchstart', 'click'];
    let isListening = false;

    function handleActivity() {
        if (activityCallback) {
            activityCallback.invokeMethodAsync('RecordActivity');
        }
    }

    function registerActivityListeners(dotNetObjectRef) {
        // Only register if not already listening
        if (isListening) {
            console.log('Security Framework: Activity listeners already registered, skipping');
            return;
        }

        activityCallback = dotNetObjectRef;
        isListening = true;

        activityEvents.forEach(event => {
            document.addEventListener(event, handleActivity, true);
        });

        console.log('Security Framework: Activity listeners registered');
    }

    function unregisterActivityListeners() {
        if (!isListening) return;

        activityEvents.forEach(event => {
            document.removeEventListener(event, handleActivity, true);
        });

        activityCallback = null;
        isListening = false;

        console.log('Security Framework: Activity listeners unregistered');
    }

    // Session timeout warning
    function showSessionWarning(timeUntilLogout) {
        const minutes = Math.ceil(timeUntilLogout / 60000);
        const message = `Your session will expire in ${minutes} minute(s). Please save your work.`;
        
        // Show browser notification if permission granted
        if ('Notification' in window && Notification.permission === 'granted') {
            new Notification('Session Warning', {
                body: message,
                icon: '/favicon.ico',
                requireInteraction: true
            });
        }
        
        // Also show console warning
        console.warn('Security Framework:', message);
    }

    // Request notification permission
    function requestNotificationPermission() {
        if ('Notification' in window && Notification.permission === 'default') {
            Notification.requestPermission().then(permission => {
                console.log('Security Framework: Notification permission:', permission);
            });
        }
    }

    // Clear all sensitive data from browser storage
    function clearSensitiveData() {
        try {
            // Clear localStorage items that might contain sensitive data
            const keysToRemove = [];
            for (let i = 0; i < localStorage.length; i++) {
                const key = localStorage.key(i);
                if (key && (key.includes('auth') || key.includes('token') || key.includes('user'))) {
                    keysToRemove.push(key);
                }
            }
            keysToRemove.forEach(key => localStorage.removeItem(key));

            // Clear sessionStorage items
            const sessionKeysToRemove = [];
            for (let i = 0; i < sessionStorage.length; i++) {
                const key = sessionStorage.key(i);
                if (key && (key.includes('auth') || key.includes('token') || key.includes('user'))) {
                    sessionKeysToRemove.push(key);
                }
            }
            sessionKeysToRemove.forEach(key => sessionStorage.removeItem(key));

            console.log('Security Framework: Sensitive data cleared');
        } catch (error) {
            console.error('Security Framework: Error clearing sensitive data:', error);
        }
    }

    // Secure page navigation
    function secureNavigate(url, forceReload = false) {
        if (forceReload) {
            window.location.href = url;
        } else {
            window.location.assign(url);
        }
    }

    // Prevent back button after logout
    function preventBackNavigation() {
        history.pushState(null, null, location.href);
        window.onpopstate = function() {
            history.go(1);
        };
    }

    // Enterprise-standard graceful logout - NO aggressive clearing
    function performGracefulLogout() {
        try {
            // 1. Clear only authentication-related items (selective)
            const authKeys = ['auth.tokens', 'auth.me', 'blazored_localstorage_token', 'blazored_sessionStorage_token'];
            
            authKeys.forEach(key => {
                try {
                    if (window.localStorage && localStorage.getItem(key)) {
                        localStorage.removeItem(key);
                    }
                    if (window.sessionStorage && sessionStorage.getItem(key)) {
                        sessionStorage.removeItem(key);
                    }
                } catch (e) {
                    // Ignore individual item errors
                }
            });

            // 2. Clear only authentication cookies (selective)
            const authCookies = ['auth_token', 'refresh_token', '.AspNetCore.Session'];
            authCookies.forEach(cookieName => {
                try {
                    document.cookie = `${cookieName}=; expires=Thu, 01 Jan 1970 00:00:00 UTC; path=/; SameSite=Strict`;
                } catch (e) {
                    // Ignore cookie errors
                }
            });

            // 3. Set authentication state flag
            window.isAuthenticated = false;
            
            // 4. NO history manipulation, NO cache clearing, NO aggressive actions
            console.log('Framework.Security: Graceful logout completed');
            
        } catch (error) {
            console.warn('Framework.Security: Graceful logout error:', error.message);
            // Graceful degradation - continue without throwing
        }
    }

    // Public API
    // Simple cache and redirect function without loops
    function clearCacheAndRedirect(redirectUrl = '/login') {
        try {
            // Clear browser storage immediately
            if (window.sessionStorage) {
                sessionStorage.clear();
            }
            if (window.localStorage) {
                localStorage.clear();
            }

            // Clear authentication cookies
            document.cookie.split(";").forEach(function(c) { 
                document.cookie = c.replace(/^ +/, "").replace(/=.*/, "=;expires=" + new Date().toUTCString() + ";path=/"); 
            });

            // Clear cache API
            if ('caches' in window) {
                caches.keys().then(function(names) {
                    for (let name of names) {
                        caches.delete(name);
                    }
                });
            }

            // Clear authentication state
            window.isAuthenticated = false;
            
            // Simple redirect without aggressive history manipulation
            window.location.href = redirectUrl + '?logout=true';

        } catch (error) {
            console.error('Error clearing cache:', error);
            // Fallback: just redirect
            window.location.replace(redirectUrl);
        }
    }

    return {
        registerActivityListeners: registerActivityListeners,
        unregisterActivityListeners: unregisterActivityListeners,
        showSessionWarning: showSessionWarning,
        requestNotificationPermission: requestNotificationPermission,
        clearSensitiveData: clearSensitiveData,
        secureNavigate: secureNavigate,
        preventBackNavigation: preventBackNavigation,
        clearCacheAndRedirect: clearCacheAndRedirect,
        performGracefulLogout: performGracefulLogout, // Enterprise-standard method
        
        // Utility functions
        isActivityTracking: () => isListening,
        getActivityEvents: () => [...activityEvents]
    };
})();

// Global namespace for enterprise integration
window['Framework'] = window['Framework'] || {};
window['Framework']['Security'] = SecurityFramework;

// Auto-initialize notification permission request
document.addEventListener('DOMContentLoaded', function() {
    SecurityFramework.requestNotificationPermission();
});

// Global alias for easier access
window.securityFramework = SecurityFramework;