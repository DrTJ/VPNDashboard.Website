// Download file helper for Blazor interop
function downloadFile(filename, base64Content) {
    const link = document.createElement('a');
    link.href = 'data:application/octet-stream;base64,' + base64Content;
    link.download = filename;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
}

// highlight.js integration for documentation
function highlightAllCode() {
    if (typeof hljs !== 'undefined') {
        document.querySelectorAll('pre code').forEach(function (block) {
            if (!block.classList.contains('hljs')) {
                hljs.highlightElement(block);
            }
        });
    }
}
