// datagrid.js
// Loaded as an ES module from DataGrid.razor.
//
// Replaces four `JS.InvokeVoidAsync("eval", ...)` calls that were CSP-incompatible
// and accumulated state on `window`. Each function below is pure: pass the grid's
// root element + a DotNetObjectReference; resize subscriptions are tracked PER element,
// so disposing one grid does not invalidate other grids' listeners.

const handlers = new WeakMap();

/**
 * Force the FluentDataGrid inside the given root to render at max-content width
 * so horizontal scroll kicks in for wide grids. Idempotent and cheap.
 */
export function applyFullDataModeWidth(root) {
    if (!root) return;
    const grids = root.querySelectorAll("fluent-data-grid");
    grids.forEach((g) => {
        if (g.style.width !== "max-content") {
            g.style.width = "max-content";
            g.style.minWidth = "100%";
        }
    });
}

/**
 * Wire a debounced window-resize listener that calls dotNetRef.HandleWindowResize(width).
 * The handler is keyed on the root element via a WeakMap so that disposing one grid
 * does not affect other grids' listeners — a regression we shipped in the previous
 * implementation when a global `window.dataGridResize` array was wiped on every Dispose.
 */
export function subscribeResize(root, dotNetRef) {
    if (!root) return;
    if (handlers.has(root)) return; // already wired

    let t;
    const fn = () => {
        clearTimeout(t);
        t = setTimeout(() => {
            try {
                dotNetRef.invokeMethodAsync("HandleWindowResize", window.innerWidth);
            } catch {
                // .NET reference disposed; silently drop.
            }
        }, 150);
    };
    window.addEventListener("resize", fn);
    handlers.set(root, fn);
}

/**
 * Unsubscribe ONLY the listener bound to this root. The other DataGrid instances
 * on the page keep working — this is the multi-grid bug fix.
 */
export function unsubscribeResize(root) {
    if (!root) return;
    const fn = handlers.get(root);
    if (fn) {
        window.removeEventListener("resize", fn);
        handlers.delete(root);
    }
}

/**
 * Returns the current viewport width so the component can compute responsive column
 * visibility before the first render is even committed.
 */
export function getViewportWidth() {
    return window.innerWidth;
}
