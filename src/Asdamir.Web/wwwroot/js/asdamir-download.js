// asdamir-download.js
// Loaded as an ES module:  import("./_content/Asdamir.Web/js/asdamir-download.js")
//
// File-download helpers for package components. Components used to call window globals
// (`enterpriseUtils.downloadFile`, a bare `downloadFile`) that no script in the package defined under
// that name, so every export button threw. The package ships its own implementation here, and a host
// needs no script tag for it.
//
// CSP: a real module file (script-src 'self'); no eval, no inline script, no markup injection.

function click(href, filename) {
    const link = document.createElement("a");
    link.href = href;
    link.download = filename || "download";
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
}

/** Saves text content as a file download. */
export function downloadText(filename, content, mime) {
    const url = URL.createObjectURL(new Blob([content ?? ""], { type: mime || "text/plain" }));
    click(url, filename);
    setTimeout(() => URL.revokeObjectURL(url), 1000);
}

/** Saves a data: URL (an exported image or document) as a file download. */
export function downloadDataUrl(dataUrl, filename) {
    if (!dataUrl) return;
    click(dataUrl, filename);
}
