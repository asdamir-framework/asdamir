window.Ocr = {
    recognize: async function (imageElementId, lang) {
        if (!window.Tesseract) { console.warn('Tesseract not loaded'); return null; }
        const image = document.getElementById(imageElementId);
        if (!image) return null;
        const { createWorker } = Tesseract;
        const worker = await createWorker(lang || 'eng');
        const { data: { text } } = await worker.recognize(image);
        await worker.terminate();
        return text;
    }
}


