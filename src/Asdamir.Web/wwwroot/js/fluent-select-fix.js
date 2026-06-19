// FluentSelect Dropdown Position Fix
// Fixes dropdown positioning to prevent clipping by parent containers
(function () {
    'use strict';

    // Initialize on DOM ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initSelectPositioning);
    } else {
        initSelectPositioning();
    }

    function initSelectPositioning() {
        // Watch for fluent-select elements
        const observer = new MutationObserver(mutations => {
            mutations.forEach(mutation => {
                mutation.addedNodes.forEach(node => {
                    if (node.nodeType === 1) {
                        if (node.tagName === 'FLUENT-SELECT' || node.tagName === 'FLUENT-COMBOBOX') {
                            setupSelect(node);
                        }
                        // Also check children
                        node.querySelectorAll?.('fluent-select, fluent-combobox').forEach(setupSelect);
                    }
                });
            });
        });

        observer.observe(document.body, {
            childList: true,
            subtree: true
        });

        // Setup existing selects
        document.querySelectorAll('fluent-select, fluent-combobox').forEach(setupSelect);
    }

    function setupSelect(select) {
        if (select._positioningSetup) return;
        select._positioningSetup = true;

        // Listen for open event
        select.addEventListener('click', () => {
            requestAnimationFrame(() => {
                positionListbox(select);
            });
        });

        // Also watch for attribute changes
        const attrObserver = new MutationObserver(() => {
            if (select.hasAttribute('open') || select.getAttribute('open') === '') {
                requestAnimationFrame(() => {
                    positionListbox(select);
                });
            }
        });

        attrObserver.observe(select, {
            attributes: true,
            attributeFilter: ['open']
        });
    }

    function positionListbox(select) {
        try {
            const shadowRoot = select.shadowRoot;
            if (!shadowRoot) return;

            const listbox = shadowRoot.querySelector('[part="listbox"]');
            if (!listbox) return;

            const rect = select.getBoundingClientRect();
            const viewportHeight = window.innerHeight;
            const listboxHeight = listbox.offsetHeight || 200;

            // Calculate if there's space below
            const spaceBelow = viewportHeight - rect.bottom;
            const spaceAbove = rect.top;

            let top, maxHeight;

            if (spaceBelow >= listboxHeight || spaceBelow >= spaceAbove) {
                // Open below
                top = rect.bottom + window.scrollY;
                maxHeight = Math.min(listboxHeight, spaceBelow - 10);
            } else {
                // Open above
                top = rect.top + window.scrollY - listboxHeight;
                maxHeight = Math.min(listboxHeight, spaceAbove - 10);
            }

            listbox.style.cssText = `
                position: fixed !important;
                left: ${rect.left}px !important;
                top: ${rect.bottom}px !important;
                width: ${rect.width}px !important;
                max-height: ${maxHeight}px !important;
                z-index: 10000 !important;
                overflow-y: auto !important;
            `;

        } catch (e) {
            console.warn('FluentSelect positioning error:', e);
        }
    }

    // Handle window resize/scroll
    let resizeTimer;
    window.addEventListener('resize', () => {
        clearTimeout(resizeTimer);
        resizeTimer = setTimeout(() => {
            document.querySelectorAll('fluent-select[open], fluent-combobox[open]').forEach(positionListbox);
        }, 100);
    });

    window.addEventListener('scroll', () => {
        document.querySelectorAll('fluent-select[open], fluent-combobox[open]').forEach(select => {
            requestAnimationFrame(() => positionListbox(select));
        });
    }, true);

})();
