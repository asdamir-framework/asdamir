// asdamir-grid.js
// Loaded as an ES module by AsdamirGrid.razor:  import("./_content/Asdamir.Web/js/asdamir-grid.js")
//
// The grid's Copy / Print / Download actions used to call three window globals (asdGridCopy,
// asdGridPrint, asdGridDownload) that only the AdminConsole host happened to define inline — in any
// other app (every generated app, MAUI) the buttons threw "asdGridCopy is undefined". The package now
// ships its own implementation, so a host needs no script tag for the grid.
//
// CSP: a real module file (script-src 'self'), no eval, no inline script, no document.write, and no
// markup-level style — styling is applied through the CSSOM, which a strict style-src does not block.

/** Saves base64 content as a file download. */
export function download(filename, base64, mime) {
    const bytes = atob(base64);
    const arr = new Uint8Array(bytes.length);
    for (let i = 0; i < bytes.length; i++) arr[i] = bytes.charCodeAt(i);

    const url = URL.createObjectURL(new Blob([arr], { type: mime || "application/octet-stream" }));
    const link = document.createElement("a");
    link.href = url;
    link.download = filename || "export";
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    setTimeout(() => URL.revokeObjectURL(url), 1000);
}

/** Copies text to the clipboard; falls back to a hidden textarea where the async API is unavailable. */
export async function copy(text) {
    try {
        await navigator.clipboard.writeText(text);
    } catch {
        const ta = document.createElement("textarea");
        ta.value = text;
        ta.style.position = "fixed";
        ta.style.opacity = "0";
        document.body.appendChild(ta);
        ta.select();
        try { document.execCommand("copy"); } finally { document.body.removeChild(ta); }
    }
}

/** Opens a print view of the table. Cell text is inserted as TEXT NODES, never as markup. */
export function print(title, headers, rows) {
    const w = window.open("", "_blank");
    if (!w) return;

    const doc = w.document;
    doc.title = String(title ?? "");

    const body = doc.body;
    body.style.fontFamily = "system-ui, Arial, sans-serif";
    body.style.padding = "20px";
    body.style.color = "#222";

    const h1 = doc.createElement("h1");
    h1.style.fontSize = "18px";
    h1.textContent = String(title ?? "");
    body.appendChild(h1);

    const table = doc.createElement("table");
    table.style.borderCollapse = "collapse";
    table.style.width = "100%";
    table.style.fontSize = "12px";

    const cell = (tag, text, background, color) => {
        const el = doc.createElement(tag);
        el.textContent = String(text ?? "");
        el.style.border = "1px solid #ccc";
        el.style.padding = "6px 8px";
        el.style.textAlign = "left";
        if (background) el.style.background = background;
        if (color) el.style.color = color;
        return el;
    };

    const headRow = doc.createElement("tr");
    for (const h of headers || []) headRow.appendChild(cell("th", h, "#0066CC", "#fff"));
    const thead = doc.createElement("thead");
    thead.appendChild(headRow);
    table.appendChild(thead);

    const tbody = doc.createElement("tbody");
    (rows || []).forEach((r, index) => {
        const tr = doc.createElement("tr");
        for (const c of r || []) tr.appendChild(cell("td", c, index % 2 === 1 ? "#f6f6f6" : null, null));
        tbody.appendChild(tr);
    });
    table.appendChild(tbody);
    body.appendChild(table);

    w.focus();
    setTimeout(() => { w.print(); }, 250);
}
