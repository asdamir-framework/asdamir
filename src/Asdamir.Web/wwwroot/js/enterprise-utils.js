window.downloadFile = (filename, content, mimeType) => {
    const blob = new Blob([content], { type: mimeType });
    const url = window.URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = filename;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    window.URL.revokeObjectURL(url);
};

window.downloadBase64 = (filename, base64Content, mimeType) => {
    const byteCharacters = atob(base64Content);
    const byteNumbers = new Array(byteCharacters.length);
    for (let i = 0; i < byteCharacters.length; i++) {
        byteNumbers[i] = byteCharacters.charCodeAt(i);
    }
    const byteArray = new Uint8Array(byteNumbers);
    const blob = new Blob([byteArray], { type: mimeType });
    
    const url = window.URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = filename;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    window.URL.revokeObjectURL(url);
};

window.copyToClipboard = async (text) => {
    try {
        await navigator.clipboard.writeText(text);
        return true;
    } catch (err) {
        console.error('Failed to copy to clipboard:', err);
        return false;
    }
};

window.formatCurrency = (amount, currency = 'USD', locale = 'en-US') => {
    return new Intl.NumberFormat(locale, {
        style: 'currency',
        currency: currency
    }).format(amount);
};

window.formatDate = (date, locale = 'en-US', options = {}) => {
    const defaultOptions = {
        year: 'numeric',
        month: 'short',
        day: 'numeric',
        hour: '2-digit',
        minute: '2-digit'
    };
    return new Intl.DateTimeFormat(locale, { ...defaultOptions, ...options }).format(new Date(date));
};

window.getTimezone = () => {
    return Intl.DateTimeFormat().resolvedOptions().timeZone;
};

window.getBrowserInfo = () => {
    const ua = navigator.userAgent;
    let browser = "Unknown";
    let version = "Unknown";
    
    if (ua.indexOf("Chrome") > -1) {
        browser = "Chrome";
        version = ua.match(/Chrome\/([0-9.]+)/)[1];
    } else if (ua.indexOf("Firefox") > -1) {
        browser = "Firefox";
        version = ua.match(/Firefox\/([0-9.]+)/)[1];
    } else if (ua.indexOf("Safari") > -1) {
        browser = "Safari";
        version = ua.match(/Version\/([0-9.]+)/)[1];
    } else if (ua.indexOf("Edge") > -1) {
        browser = "Edge";
        version = ua.match(/Edge\/([0-9.]+)/)[1];
    }
    
    return {
        browser: browser,
        version: version,
        userAgent: ua,
        platform: navigator.platform,
        language: navigator.language
    };
};

window.validateForm = (formId) => {
    const form = document.getElementById(formId);
    if (!form) return false;
    
    return form.checkValidity();
};

window.scrollToElement = (elementId, behavior = 'smooth') => {
    const element = document.getElementById(elementId);
    if (element) {
        element.scrollIntoView({ behavior: behavior });
    }
};

window.showConfirmDialog = async (message, title = 'Confirm') => {
    return confirm(`${title}\n\n${message}`);
};

window.localStorageManager = {
    set: (key, value) => {
        try {
            localStorage.setItem(key, JSON.stringify(value));
            return true;
        } catch (e) {
            console.error('LocalStorage set error:', e);
            return false;
        }
    },
    
    get: (key) => {
        try {
            const item = localStorage.getItem(key);
            return item ? JSON.parse(item) : null;
        } catch (e) {
            console.error('LocalStorage get error:', e);
            return null;
        }
    },
    
    remove: (key) => {
        try {
            localStorage.removeItem(key);
            return true;
        } catch (e) {
            console.error('LocalStorage remove error:', e);
            return false;
        }
    },
    
    clear: () => {
        try {
            localStorage.clear();
            return true;
        } catch (e) {
            console.error('LocalStorage clear error:', e);
            return false;
        }
    }
};

// Theme management
window.themeManager = {
    setTheme: (theme) => {
        document.documentElement.setAttribute('data-theme', theme);
        localStorage.setItem('app-theme', theme);
    },
    
    getTheme: () => {
        return localStorage.getItem('app-theme') || 
               (window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light');
    },
    
    toggleTheme: () => {
        const current = window.themeManager.getTheme();
        const newTheme = current === 'dark' ? 'light' : 'dark';
        window.themeManager.setTheme(newTheme);
        return newTheme;
    }
};

// Initialize theme on load
document.addEventListener('DOMContentLoaded', () => {
    const theme = window.themeManager.getTheme();
    window.themeManager.setTheme(theme);
});

// Performance monitoring
window.performanceMonitor = {
    mark: (name) => {
        if (performance.mark) {
            performance.mark(name);
        }
    },
    
    measure: (name, startMark, endMark) => {
        if (performance.measure) {
            performance.measure(name, startMark, endMark);
            return performance.getEntriesByName(name, 'measure')[0];
        }
        return null;
    },
    
    getMetrics: () => {
        if (!performance.getEntriesByType) return null;
        
        const navigation = performance.getEntriesByType('navigation')[0];
        const paint = performance.getEntriesByType('paint');
        
        return {
            domContentLoaded: navigation.domContentLoadedEventEnd - navigation.domContentLoadedEventStart,
            loadComplete: navigation.loadEventEnd - navigation.loadEventStart,
            firstPaint: paint.find(p => p.name === 'first-paint')?.startTime,
            firstContentfulPaint: paint.find(p => p.name === 'first-contentful-paint')?.startTime
        };
    }
};

// Keyboard shortcuts
window.keyboardShortcuts = {
    shortcuts: new Map(),
    
    register: (key, callback, description = '') => {
        window.keyboardShortcuts.shortcuts.set(key.toLowerCase(), {
            callback: callback,
            description: description
        });
    },
    
    unregister: (key) => {
        window.keyboardShortcuts.shortcuts.delete(key.toLowerCase());
    },
    
    getShortcuts: () => {
        return Array.from(window.keyboardShortcuts.shortcuts.entries()).map(([key, value]) => ({
            key: key,
            description: value.description
        }));
    }
};

document.addEventListener('keydown', (e) => {
    const key = `${e.ctrlKey ? 'ctrl+' : ''}${e.shiftKey ? 'shift+' : ''}${e.altKey ? 'alt+' : ''}${e.key.toLowerCase()}`;
    const shortcut = window.keyboardShortcuts.shortcuts.get(key);
    
    if (shortcut && typeof shortcut.callback === 'function') {
        e.preventDefault();
        shortcut.callback(e);
    }
});

// Network status monitoring
window.networkMonitor = {
    isOnline: () => navigator.onLine,
    
    onStatusChange: (callback) => {
        window.addEventListener('online', () => callback(true));
        window.addEventListener('offline', () => callback(false));
    }
};

// Chart.js Integration
window.fluentCharts = {
    charts: new Map(),
    
    render: async (canvasElement, config) => {
        try {
            // Ensure Chart.js is loaded
            if (typeof Chart === 'undefined') {
                await window.fluentCharts.loadChartJs();
            }
            
            // Destroy existing chart if exists
            const chartId = canvasElement.id || canvasElement.getAttribute('data-chart-id') || 'chart-' + Date.now();
            canvasElement.setAttribute('data-chart-id', chartId);
            
            if (window.fluentCharts.charts.has(chartId)) {
                window.fluentCharts.charts.get(chartId).destroy();
            }
            
            // Create new chart
            const ctx = canvasElement.getContext('2d');
            const chart = new Chart(ctx, config);
            
            window.fluentCharts.charts.set(chartId, chart);
            
            return chart;
        } catch (error) {
            console.error('Error rendering chart:', error);
            throw error;
        }
    },
    
    update: (canvasElement, newData) => {
        const chartId = canvasElement.getAttribute('data-chart-id');
        const chart = window.fluentCharts.charts.get(chartId);
        
        if (chart) {
            chart.data = newData;
            chart.update();
        }
    },
    
    destroy: (canvasElement) => {
        const chartId = canvasElement.getAttribute('data-chart-id');
        const chart = window.fluentCharts.charts.get(chartId);
        
        if (chart) {
            chart.destroy();
            window.fluentCharts.charts.delete(chartId);
        }
    },
    
    download: (canvasElement, filename = 'chart.png') => {
        try {
            const link = document.createElement('a');
            link.download = filename;
            link.href = canvasElement.toDataURL('image/png');
            document.body.appendChild(link);
            link.click();
            document.body.removeChild(link);
        } catch (error) {
            console.error('Error downloading chart:', error);
        }
    },
    
    resize: (canvasElement) => {
        const chartId = canvasElement.getAttribute('data-chart-id');
        const chart = window.fluentCharts.charts.get(chartId);
        
        if (chart) {
            chart.resize();
        }
    },
    
    loadChartJs: async () => {
        return new Promise((resolve, reject) => {
            if (typeof Chart !== 'undefined') {
                resolve();
                return;
            }
            
            const script = document.createElement('script');
            script.src = 'https://cdn.jsdelivr.net/npm/chart.js@4.4.0/dist/chart.umd.js';
            script.onload = () => {
                // Register Chart.js plugins
                if (typeof Chart !== 'undefined') {
                    Chart.register(...Chart.registerables);
                }
                resolve();
            };
            script.onerror = reject;
            document.head.appendChild(script);
        });
    },
    
    // Utility functions for chart operations
    addDataPoint: (canvasElement, seriesIndex, data) => {
        const chartId = canvasElement.getAttribute('data-chart-id');
        const chart = window.fluentCharts.charts.get(chartId);
        
        if (chart && chart.data.datasets[seriesIndex]) {
            chart.data.datasets[seriesIndex].data.push(data);
            chart.update();
        }
    },
    
    removeDataPoint: (canvasElement, seriesIndex, index) => {
        const chartId = canvasElement.getAttribute('data-chart-id');
        const chart = window.fluentCharts.charts.get(chartId);
        
        if (chart && chart.data.datasets[seriesIndex]) {
            chart.data.datasets[seriesIndex].data.splice(index, 1);
            chart.update();
        }
    },
    
    toggleSeries: (canvasElement, seriesIndex) => {
        const chartId = canvasElement.getAttribute('data-chart-id');
        const chart = window.fluentCharts.charts.get(chartId);
        
        if (chart && chart.data.datasets[seriesIndex]) {
            const meta = chart.getDatasetMeta(seriesIndex);
            meta.hidden = !meta.hidden;
            chart.update();
        }
    },
    
    changeChartType: (canvasElement, newType) => {
        const chartId = canvasElement.getAttribute('data-chart-id');
        const chart = window.fluentCharts.charts.get(chartId);
        
        if (chart) {
            chart.config.type = newType;
            chart.update();
        }
    },
    
    exportData: (canvasElement, format = 'csv') => {
        const chartId = canvasElement.getAttribute('data-chart-id');
        const chart = window.fluentCharts.charts.get(chartId);
        
        if (!chart) return null;
        
        const data = chart.data;
        
        if (format === 'csv') {
            let csv = 'Label,' + data.datasets.map(ds => ds.label).join(',') + '\n';
            
            data.labels.forEach((label, i) => {
                const row = [label];
                data.datasets.forEach(dataset => {
                    row.push(dataset.data[i] || '');
                });
                csv += row.join(',') + '\n';
            });
            
            return csv;
        } else if (format === 'json') {
            return JSON.stringify(data, null, 2);
        }
        
        return null;
    }
};

// Auto-resize charts on window resize
window.addEventListener('resize', () => {
    window.fluentCharts.charts.forEach(chart => {
        chart.resize();
    });
});

// Cleanup charts when page unloads
window.addEventListener('beforeunload', () => {
    window.fluentCharts.charts.forEach(chart => {
        chart.destroy();
    });
    window.fluentCharts.charts.clear();
});

// ========================================
// DATAGRID FULLSCREEN PORTAL
// Moves the datagrid element to <body> when fullscreen so that
// dialogs opened from within it are NOT inside the fixed stacking
// context and can render above everything.
// ========================================
window._datagridFullscreenPlaceholders = new Map();

window.datagridEnterFullscreen = (element) => {
    if (!element || window._datagridFullscreenPlaceholders.has(element)) return;
    // Create a placeholder comment node at original location
    const placeholder = document.createComment('datagrid-fullscreen-placeholder');
    element.parentNode.insertBefore(placeholder, element);
    window._datagridFullscreenPlaceholders.set(element, placeholder);
    document.body.appendChild(element);
};

window.datagridExitFullscreen = (element) => {
    if (!element) return;
    const placeholder = window._datagridFullscreenPlaceholders.get(element);
    if (!placeholder) return;
    placeholder.parentNode.insertBefore(element, placeholder);
    placeholder.parentNode.removeChild(placeholder);
    window._datagridFullscreenPlaceholders.delete(element);
};