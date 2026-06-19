// Enterprise OCR Component JavaScript Support
window.OCRComponent = {
    
    // Initialize the OCR component
    initialize: function(fileInputId) {
        console.log('OCR Component initialized');
        this.fileInputId = fileInputId;
        this.setupDragDrop();
    },

    // Setup drag and drop functionality
    setupDragDrop: function() {
        const dropZones = document.querySelectorAll('.drag-drop-area');
        
        dropZones.forEach(zone => {
            zone.addEventListener('dragover', (e) => {
                e.preventDefault();
                zone.classList.add('drag-over');
            });

            zone.addEventListener('dragleave', (e) => {
                e.preventDefault();
                zone.classList.remove('drag-over');
            });

            zone.addEventListener('drop', (e) => {
                e.preventDefault();
                zone.classList.remove('drag-over');
                
                const files = e.dataTransfer.files;
                if (files.length > 0) {
                    const fileInput = document.getElementById(this.fileInputId);
                    fileInput.files = files;
                    fileInput.dispatchEvent(new Event('change', { bubbles: true }));
                }
            });
        });
    },

    // Camera functionality
    currentStream: null,

    startCamera: async function() {
        try {
            const stream = await navigator.mediaDevices.getUserMedia({
                video: {
                    width: { ideal: 1280 },
                    height: { ideal: 720 },
                    facingMode: 'environment' // Use back camera if available
                }
            });

            const video = document.getElementById('camera-preview');
            if (video) {
                video.srcObject = stream;
                this.currentStream = stream;
            }
        } catch (err) {
            console.error('Camera access denied:', err);
            throw new Error('Camera access denied. Please allow camera permission.');
        }
    },

    stopCamera: function() {
        if (this.currentStream) {
            this.currentStream.getTracks().forEach(track => track.stop());
            this.currentStream = null;
        }
        
        const video = document.getElementById('camera-preview');
        if (video) {
            video.srcObject = null;
        }
    },

    captureImage: function() {
        const video = document.getElementById('camera-preview');
        if (!video) return '';

        const canvas = document.createElement('canvas');
        canvas.width = video.videoWidth;
        canvas.height = video.videoHeight;
        
        const ctx = canvas.getContext('2d');
        ctx.drawImage(video, 0, 0);
        
        return canvas.toDataURL('image/jpeg', 0.9);
    },

    // File download functionality
    downloadFile: function(content, filename, contentType) {
        const blob = new Blob([content], { type: contentType });
        const url = URL.createObjectURL(blob);
        
        const a = document.createElement('a');
        a.href = url;
        a.download = filename;
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        
        URL.revokeObjectURL(url);
    },

    // Image processing utilities
    preprocessImage: async function(imageData) {
        // This would integrate with image processing libraries
        // For now, return the original image
        return imageData;
    },

    // OCR Engine integrations (these would call actual APIs)
    callTesseractOCR: async function(imageData, language) {
        // Simulate Tesseract OCR API call
        await this.delay(2000);
        return {
            text: "Sample text extracted with Tesseract",
            confidence: 85,
            words: []
        };
    },

    callAzureVisionOCR: async function(imageData, language) {
        // Simulate Azure Computer Vision API call
        await this.delay(1500);
        return {
            text: "Sample text extracted with Azure Vision",
            confidence: 94,
            words: []
        };
    },

    callAWSTextract: async function(imageData, language) {
        // Simulate AWS Textract API call
        await this.delay(1800);
        return {
            text: "Sample text extracted with AWS Textract",
            confidence: 91,
            words: []
        };
    },

    callGoogleVisionOCR: async function(imageData, language) {
        // Simulate Google Vision API call
        await this.delay(1200);
        return {
            text: "Sample text extracted with Google Vision",
            confidence: 96,
            words: []
        };
    },

    // Utility functions
    delay: function(ms) {
        return new Promise(resolve => setTimeout(resolve, ms));
    },

    validateImageFile: function(file) {
        const allowedTypes = ['image/jpeg', 'image/png', 'image/tiff', 'application/pdf'];
        const maxSize = 10 * 1024 * 1024; // 10MB

        if (!allowedTypes.includes(file.type)) {
            throw new Error('Unsupported file type');
        }

        if (file.size > maxSize) {
            throw new Error('File size exceeds 10MB limit');
        }

        return true;
    },

    // Text processing utilities
    spellCheck: function(text, language) {
        // This would integrate with spell checking APIs
        return text;
    },

    extractMetadata: function(text) {
        return {
            wordCount: text.split(/\s+/).length,
            lineCount: text.split('\n').length,
            characterCount: text.length,
            languages: ['en'], // detected languages
            confidence: 0.85
        };
    }
};

// Initialize when DOM is ready
document.addEventListener('DOMContentLoaded', function() {
    console.log('OCR Component JavaScript loaded');
});