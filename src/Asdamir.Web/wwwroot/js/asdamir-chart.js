// asdamir-chart.js
// Loaded as an ES module by AsdamirChart.razor:  import("./_content/Asdamir.Web/js/asdamir-chart.js")
//
// The chart component used to call a `window.fluentCharts` object from a script no page loaded, which in
// turn fetched Chart.js from a CDN — blocked by any strict script policy — while the copy shipped in this
// package was a 190-byte placeholder. The package now ships the real Chart.js (see
// THIRD-PARTY-NOTICES.txt) next to this module, and this module loads it from the application's own
// origin on first use. A host needs no script tag and no CDN allowance.
//
// CSP: a real module file plus one same-origin <script src> (script-src 'self'); no eval, no inline
// script, no markup injection.

let chartJs;                       // one load per page, shared by every chart
const charts = new WeakMap();      // canvas element → Chart instance

function loadChartJs() {
    // `register` tells the real library from anything else that happens to be called Chart.
    if (typeof globalThis.Chart?.register === "function") return Promise.resolve(globalThis.Chart);

    chartJs ??= new Promise((resolve, reject) => {
        const script = document.createElement("script");
        script.src = new URL("./chartjs.umd.min.js", import.meta.url).href;
        script.onload = () => typeof globalThis.Chart?.register === "function"
            ? resolve(globalThis.Chart)
            : reject(new Error("chartjs.umd.min.js loaded but did not define Chart"));
        script.onerror = () => { chartJs = undefined; reject(new Error("chartjs.umd.min.js could not be loaded")); };
        document.head.appendChild(script);
    });
    return chartJs;
}

// .NET sends unset options as null; Chart.js expects them to be absent.
function withoutNulls(value) {
    if (Array.isArray(value)) return value.map(withoutNulls);
    if (value && typeof value === "object") {
        const out = {};
        for (const [k, v] of Object.entries(value)) if (v !== null && v !== undefined) out[k] = withoutNulls(v);
        return out;
    }
    return value;
}

/** Draws (or redraws) the chart described by `config` on the canvas. */
export async function render(canvas, config) {
    if (!canvas) return;
    const Chart = await loadChartJs();
    charts.get(canvas)?.destroy();
    charts.set(canvas, new Chart(canvas.getContext("2d"), withoutNulls(config)));
}

/** Saves the chart as a PNG download. */
export function download(canvas, filename) {
    if (!canvas) return;
    const link = document.createElement("a");
    link.download = filename || "chart.png";
    link.href = canvas.toDataURL("image/png");
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
}

/** Destroys the chart on the canvas and releases its listeners. */
export function dispose(canvas) {
    if (!canvas) return;
    charts.get(canvas)?.destroy();
    charts.delete(canvas);
}
