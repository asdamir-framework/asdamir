window.Sig = {
    init: function (canvasId) {
        const canvas = document.getElementById(canvasId);
        if (!canvas) return;
        if (!window.SignaturePad) { console.warn('SignaturePad not loaded'); return; }
        const sigPad = new window.SignaturePad(canvas, { backgroundColor: 'rgba(255,255,255,1)' });
        window.__sigPad = sigPad;
    },
    clear: function () { if (window.__sigPad) window.__sigPad.clear(); },
    toDataUrl: function () { return window.__sigPad ? window.__sigPad.toDataURL("image/png") : null; }
};

window.downloadDataUrl = function (dataUrl, filename) {
    const a = document.createElement('a');
    a.href = dataUrl;
    a.download = filename || 'download.png';
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
}


