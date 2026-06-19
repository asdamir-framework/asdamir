window.BarcodeReader = {
    start: function (dotNetRef, elementId) {
        const el = document.getElementById(elementId);
        if (!el) return;
        if (!window.Quagga) { console.warn('Quagga not loaded'); return; }
        Quagga.init({
            inputStream: { type: "LiveStream", target: el },
            decoder: { readers: ["code_128_reader", "ean_reader", "ean_8_reader"] }
        }, function (err) {
            if (err) { console.log(err); return; }
            Quagga.start();
        });
        Quagga.onDetected(function (data) {
            const code = data && data.codeResult && data.codeResult.code;
            if (code) { dotNetRef.invokeMethodAsync("OnBarcodeDetectedInternal", code); }
        });
    },
    stop: function () { try { Quagga.stop(); } catch { } }
};


