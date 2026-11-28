// asdamir-signature.js
// Loaded as an ES module by AsdamirSignaturePad.razor:
//   import("./_content/Asdamir.Web/js/asdamir-signature.js")
//
// The signature pad used to call twelve functions on a `window.enterpriseUtils` object that no script
// anywhere defined, so the component rendered a canvas nobody could draw on and every button threw.
// This module is the whole browser side: drawing, undo/redo, and PNG / SVG / PDF export. It has no
// dependency on any other library and a host needs no script tag for it.
//
// A signature is kept as STROKES (not pixels), with every point normalised to 0..1 of the canvas. That
// is what makes undo/redo exact, lets the SVG export be real vector paths, and lets the drawing survive
// the canvas being resized (fullscreen) — a canvas loses its pixels whenever its size attributes change.
//
// CSP: a real module file (script-src 'self'); no eval, no inline script, no markup injection.

import { downloadText, downloadDataUrl } from "./asdamir-download.js";

export { downloadText, downloadDataUrl };

const pads = new Map();   // canvas id → pad state

function stateOf(pad) {
    return {
        strokeCount: pad ? pad.strokes.length : 0,
        canUndo: !!pad && pad.strokes.length > 0,
        canRedo: !!pad && pad.redo.length > 0,
    };
}

function tracePath(ctx, points, w, h) {
    ctx.beginPath();
    ctx.moveTo(points[0].x * w, points[0].y * h);
    if (points.length === 1) {
        ctx.lineTo(points[0].x * w + 0.01, points[0].y * h + 0.01);   // a tap leaves a dot
    } else {
        // Quadratic curves through the midpoints: smooth, and identical for every renderer below.
        for (let i = 1; i < points.length - 1; i++) {
            const mx = (points[i].x + points[i + 1].x) / 2 * w;
            const my = (points[i].y + points[i + 1].y) / 2 * h;
            ctx.quadraticCurveTo(points[i].x * w, points[i].y * h, mx, my);
        }
        const last = points[points.length - 1];
        ctx.lineTo(last.x * w, last.y * h);
    }
}

function paint(ctx, strokes, w, h, scale) {
    ctx.lineCap = "round";
    ctx.lineJoin = "round";
    for (const s of strokes) {
        if (!s.points.length) continue;
        ctx.strokeStyle = s.color;
        ctx.lineWidth = s.width * scale;
        tracePath(ctx, s.points, w, h);
        ctx.stroke();
    }
}

function repaint(pad) {
    const c = pad.canvas;
    const ctx = c.getContext("2d");
    ctx.clearRect(0, 0, c.width, c.height);
    paint(ctx, pad.current ? [...pad.strokes, pad.current] : pad.strokes, c.width, c.height, 1);
}

function pointOf(pad, e) {
    const r = pad.canvas.getBoundingClientRect();
    const clamp = v => Math.min(1, Math.max(0, v));
    return {
        x: clamp(r.width ? (e.clientX - r.left) / r.width : 0),
        y: clamp(r.height ? (e.clientY - r.top) / r.height : 0),
    };
}

/**
 * Attaches drawing to a canvas. `options`: penColor, penThickness, onStrokeComplete (a .NET object
 * reference whose `OnStrokeCompleted` is invoked after each finished stroke). Returns false when the
 * canvas does not exist; calling it again for the same id keeps the existing drawing.
 */
export function init(canvasId, options) {
    const canvas = document.getElementById(canvasId);
    if (!canvas) return false;

    dispose(canvasId, true);
    const previous = pads.get(canvasId);

    const pad = {
        canvas,
        color: options?.penColor || "#000000",
        width: Number(options?.penThickness) || 2,
        dotnet: options?.onStrokeComplete || null,
        strokes: previous ? previous.strokes : [],
        redo: previous ? previous.redo : [],
        current: null,
        handlers: {},
    };

    pad.handlers.down = e => {
        if (e.button !== undefined && e.button !== 0) return;
        e.preventDefault();
        try { canvas.setPointerCapture(e.pointerId); } catch { /* synthetic pointer: nothing to capture */ }
        pad.current = { color: pad.color, width: pad.width, points: [pointOf(pad, e)] };
        repaint(pad);
    };
    pad.handlers.move = e => {
        if (!pad.current) return;
        e.preventDefault();
        pad.current.points.push(pointOf(pad, e));
        repaint(pad);
    };
    pad.handlers.up = e => {
        if (!pad.current) return;
        e.preventDefault();
        pad.strokes.push(pad.current);
        pad.current = null;
        pad.redo = [];   // a new stroke ends the redo history
        repaint(pad);
        if (pad.dotnet) pad.dotnet.invokeMethodAsync("OnStrokeCompleted").catch(() => { /* circuit gone */ });
    };

    canvas.style.touchAction = "none";   // a finger draws instead of scrolling the page
    canvas.addEventListener("pointerdown", pad.handlers.down);
    canvas.addEventListener("pointermove", pad.handlers.move);
    canvas.addEventListener("pointerup", pad.handlers.up);
    canvas.addEventListener("pointercancel", pad.handlers.up);

    pads.set(canvasId, pad);
    repaint(pad);
    return true;
}

/** Changes the pen for strokes drawn from now on. */
export function updateSettings(canvasId, options) {
    const pad = pads.get(canvasId);
    if (!pad) return;
    if (options?.penColor) pad.color = options.penColor;
    if (Number(options?.penThickness) > 0) pad.width = Number(options.penThickness);
}

/** Repaints from the stored strokes — call after the canvas size attributes change. */
export function redraw(canvasId) {
    const pad = pads.get(canvasId);
    if (pad) repaint(pad);
    return stateOf(pad);
}

/** Removes the last stroke; it can be brought back with redo. */
export function undo(canvasId) {
    const pad = pads.get(canvasId);
    if (pad && pad.strokes.length) {
        pad.redo.push(pad.strokes.pop());
        repaint(pad);
    }
    return stateOf(pad);
}

/** Restores the stroke most recently removed by undo. */
export function redo(canvasId) {
    const pad = pads.get(canvasId);
    if (pad && pad.redo.length) {
        pad.strokes.push(pad.redo.pop());
        repaint(pad);
    }
    return stateOf(pad);
}

/** Erases the drawing and its undo/redo history. */
export function clear(canvasId) {
    const pad = pads.get(canvasId);
    if (pad) {
        pad.strokes = [];
        pad.redo = [];
        pad.current = null;
        repaint(pad);
    }
    return stateOf(pad);
}

/** The drawing as a JSON string (what a saved template stores). */
export function getData(canvasId) {
    const pad = pads.get(canvasId);
    return JSON.stringify({ v: 1, strokes: pad ? pad.strokes : [] });
}

/** Replaces the drawing with one produced by getData. Unreadable data leaves the drawing untouched. */
export function loadData(canvasId, json) {
    const pad = pads.get(canvasId);
    if (!pad) return stateOf(pad);

    let parsed;
    try { parsed = JSON.parse(json); } catch { return stateOf(pad); }
    if (!parsed || !Array.isArray(parsed.strokes)) return stateOf(pad);

    pad.strokes = parsed.strokes
        .filter(s => s && Array.isArray(s.points) && s.points.length)
        .map(s => ({
            color: typeof s.color === "string" ? s.color : "#000000",
            width: Number(s.width) > 0 ? Number(s.width) : 2,
            points: s.points
                .filter(p => p && Number.isFinite(p.x) && Number.isFinite(p.y))
                .map(p => ({ x: p.x, y: p.y })),
        }))
        .filter(s => s.points.length);
    pad.redo = [];
    pad.current = null;
    repaint(pad);
    return stateOf(pad);
}

function rasterise(pad, lines, mime) {
    const w = pad.canvas.width;
    const h = pad.canvas.height;
    const lineHeight = 26;
    const extra = lines.length ? lines.length * lineHeight + 24 : 0;

    const out = document.createElement("canvas");
    out.width = w;
    out.height = h + extra;
    const ctx = out.getContext("2d");
    ctx.fillStyle = "#ffffff";   // an exported signature is on white, never on a transparent page
    ctx.fillRect(0, 0, out.width, out.height);
    paint(ctx, pad.strokes, w, h, 1);

    if (lines.length) {
        // Text is drawn by the browser, so any script (Turkish, Cyrillic, …) comes out right.
        ctx.fillStyle = "#222222";
        ctx.font = "16px sans-serif";
        ctx.textBaseline = "top";
        lines.forEach((line, i) => ctx.fillText(line, 16, h + 12 + i * lineHeight, w - 32));
    }
    return { dataUrl: out.toDataURL(mime, 0.92), width: out.width, height: out.height };
}

/** The drawing as a PNG data: URL on a white background. */
export function exportPng(canvasId) {
    const pad = pads.get(canvasId);
    return pad ? rasterise(pad, [], "image/png").dataUrl : "";
}

/** The drawing as an SVG document with one vector path per stroke. */
export function exportSvg(canvasId) {
    const pad = pads.get(canvasId);
    if (!pad) return "";
    const w = pad.canvas.width;
    const h = pad.canvas.height;
    const n = v => Math.round(v * 100) / 100;
    const safeColor = c => (/^#[0-9a-fA-F]{3,8}$/.test(c) ? c : "#000000");

    const paths = pad.strokes.map(s => {
        const p = s.points;
        let d = `M${n(p[0].x * w)} ${n(p[0].y * h)}`;
        if (p.length === 1) {
            d += ` L${n(p[0].x * w + 0.01)} ${n(p[0].y * h + 0.01)}`;
        } else {
            for (let i = 1; i < p.length - 1; i++) {
                d += ` Q${n(p[i].x * w)} ${n(p[i].y * h)} ${n((p[i].x + p[i + 1].x) / 2 * w)} ${n((p[i].y + p[i + 1].y) / 2 * h)}`;
            }
            d += ` L${n(p[p.length - 1].x * w)} ${n(p[p.length - 1].y * h)}`;
        }
        return `<path d="${d}" fill="none" stroke="${safeColor(s.color)}" stroke-width="${n(s.width)}" stroke-linecap="round" stroke-linejoin="round"/>`;
    });

    return `<?xml version="1.0" encoding="UTF-8"?>\n`
        + `<svg xmlns="http://www.w3.org/2000/svg" width="${w}" height="${h}" viewBox="0 0 ${w} ${h}">`
        + `<rect width="100%" height="100%" fill="#ffffff"/>${paths.join("")}</svg>`;
}

/**
 * The drawing as a one-page A4 PDF data: URL. `details.lines` (optional) are printed under the
 * signature — signer, date, purpose — already formatted by the caller.
 */
export function exportPdf(canvasId, details) {
    const pad = pads.get(canvasId);
    if (!pad) return "";

    const lines = Array.isArray(details?.lines) ? details.lines.filter(l => typeof l === "string" && l.trim()) : [];
    const image = rasterise(pad, lines, "image/jpeg");
    const jpeg = Uint8Array.from(atob(image.dataUrl.split(",")[1]), ch => ch.charCodeAt(0));

    // A4 in points, 40pt margins; the image keeps its aspect ratio and sits at the top of the page.
    const pageW = 595, pageH = 842, margin = 40;
    const drawW = pageW - margin * 2;
    const drawH = Math.min(pageH - margin * 2, drawW * image.height / image.width);
    const content = `q ${drawW} 0 0 ${drawH.toFixed(2)} ${margin} ${(pageH - margin - drawH).toFixed(2)} cm /Im0 Do Q`;

    const enc = new TextEncoder();
    const chunks = [];
    const offsets = [];
    let length = 0;
    const push = part => {
        const bytes = typeof part === "string" ? enc.encode(part) : part;
        chunks.push(bytes);
        length += bytes.length;
    };
    const object = (id, body) => { offsets[id] = length; push(`${id} 0 obj\n`); body(); push(`\nendobj\n`); };

    push("%PDF-1.4\n");
    object(1, () => push("<< /Type /Catalog /Pages 2 0 R >>"));
    object(2, () => push("<< /Type /Pages /Kids [3 0 R] /Count 1 >>"));
    object(3, () => push(`<< /Type /Page /Parent 2 0 R /MediaBox [0 0 ${pageW} ${pageH}] `
        + "/Resources << /XObject << /Im0 4 0 R >> >> /Contents 5 0 R >>"));
    object(4, () => {
        push(`<< /Type /XObject /Subtype /Image /Width ${image.width} /Height ${image.height} `
            + `/ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode /Length ${jpeg.length} >>\nstream\n`);
        push(jpeg);
        push("\nendstream");
    });
    object(5, () => push(`<< /Length ${content.length} >>\nstream\n${content}\nendstream`));

    const xref = length;
    push("xref\n0 6\n0000000000 65535 f \n");
    for (let id = 1; id <= 5; id++) push(`${String(offsets[id]).padStart(10, "0")} 00000 n \n`);
    push(`trailer\n<< /Size 6 /Root 1 0 R >>\nstartxref\n${xref}\n%%EOF\n`);

    const pdf = new Uint8Array(length);
    let at = 0;
    for (const c of chunks) { pdf.set(c, at); at += c.length; }

    let binary = "";
    for (let i = 0; i < pdf.length; i += 0x8000) binary += String.fromCharCode.apply(null, pdf.subarray(i, i + 0x8000));
    return "data:application/pdf;base64," + btoa(binary);
}

/** Detaches drawing from the canvas. `keep` (internal) preserves the strokes for a re-init. */
export function dispose(canvasId, keep) {
    const pad = pads.get(canvasId);
    if (!pad) return;
    pad.canvas.removeEventListener("pointerdown", pad.handlers.down);
    pad.canvas.removeEventListener("pointermove", pad.handlers.move);
    pad.canvas.removeEventListener("pointerup", pad.handlers.up);
    pad.canvas.removeEventListener("pointercancel", pad.handlers.up);
    if (!keep) pads.delete(canvasId);
}
