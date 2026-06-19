/**
 * DataGrid Shadow DOM Fix
 * 
 * FluentUI web components use Shadow DOM which prevents external CSS from styling internal elements.
 * This module injects styles directly into Shadow DOM to ensure full data visibility.
 * 
 * Strategy: Use MutationObserver to detect FluentDataGrid elements and inject styles into their Shadow DOM.
 */

(function () {
    'use strict';

    const SHADOW_STYLES = `
        /* Force cells to show full content without truncation */
        .grid-wrapper,
        .grid,
        fluent-data-grid-row,
        fluent-data-grid-cell {
            white-space: nowrap !important;
            overflow: visible !important;
            text-overflow: clip !important;
            max-width: none !important;
        }
        
        /* Ensure cell content is fully visible */
        fluent-data-grid-cell div,
        fluent-data-grid-cell span,
        fluent-data-grid-cell p {
            white-space: nowrap !important;
            overflow: visible !important;
            text-overflow: clip !important;
            display: inline-block !important;
        }
        
        /* Table layout adjustments */
        table {
            table-layout: auto !important;
            width: max-content !important;
            min-width: 100% !important;
        }
        
        /* Column width auto-sizing */
        th, td {
            white-space: nowrap !important;
            overflow: visible !important;
            padding: 8px 12px !important;
        }
    `;

    /**
     * Inject styles into a Shadow DOM root
     */
    function injectShadowStyles(shadowRoot) {
        // Check if styles already injected
        if (shadowRoot.querySelector('#datagrid-fix-styles')) {
            return;
        }

        const styleElement = document.createElement('style');
        styleElement.id = 'datagrid-fix-styles';
        styleElement.textContent = SHADOW_STYLES;
        shadowRoot.prepend(styleElement);
        
        console.log('[DataGrid Shadow Fix] Styles injected into Shadow DOM');
    }

    /**
     * Process a FluentDataGrid element
     */
    function processDataGrid(dataGrid) {
        if (!dataGrid.shadowRoot) {
            console.warn('[DataGrid Shadow Fix] No Shadow DOM found');
            return;
        }

        injectShadowStyles(dataGrid.shadowRoot);
        
        // Also check for nested shadow roots (fluent-data-grid-row, fluent-data-grid-cell)
        const nestedComponents = dataGrid.shadowRoot.querySelectorAll('fluent-data-grid-row, fluent-data-grid-cell');
        nestedComponents.forEach(component => {
            if (component.shadowRoot) {
                injectShadowStyles(component.shadowRoot);
            }
        });
    }

    /**
     * Initialize observer for existing and future DataGrids
     */
    function initializeObserver() {
        // Process existing DataGrids
        document.querySelectorAll('fluent-data-grid').forEach(processDataGrid);

        // Watch for new DataGrids
        const observer = new MutationObserver((mutations) => {
            mutations.forEach((mutation) => {
                mutation.addedNodes.forEach((node) => {
                    if (node.nodeType === Node.ELEMENT_NODE) {
                        // Check if the node itself is a DataGrid
                        if (node.tagName === 'FLUENT-DATA-GRID') {
                            processDataGrid(node);
                        }
                        // Check for DataGrids within the added node
                        if (node.querySelectorAll) {
                            node.querySelectorAll('fluent-data-grid').forEach(processDataGrid);
                        }
                    }
                });
            });
        });

        observer.observe(document.body, {
            childList: true,
            subtree: true
        });

        console.log('[DataGrid Shadow Fix] Observer initialized');
    }

    /**
     * Public API
     */
    window.DataGridShadowFix = {
        /**
         * Initialize the Shadow DOM fix
         */
        initialize: function () {
            if (document.readyState === 'loading') {
                document.addEventListener('DOMContentLoaded', initializeObserver);
            } else {
                initializeObserver();
            }
        },

        /**
         * Manually fix a specific DataGrid element
         */
        fixDataGrid: function (element) {
            if (typeof element === 'string') {
                element = document.querySelector(element);
            }
            if (element && element.tagName === 'FLUENT-DATA-GRID') {
                processDataGrid(element);
            }
        },

        /**
         * Fix all DataGrids in the document
         */
        fixAllDataGrids: function () {
            document.querySelectorAll('fluent-data-grid').forEach(processDataGrid);
        }
    };

    // Auto-initialize
    window.DataGridShadowFix.initialize();

    console.log('[DataGrid Shadow Fix] Module loaded');
})();
