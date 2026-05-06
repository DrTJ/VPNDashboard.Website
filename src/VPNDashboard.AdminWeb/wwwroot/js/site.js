function highlightAllCode() {
    document.querySelectorAll('pre code').forEach(function (block) {
        if (!block.dataset.highlighted) {
            hljs.highlightElement(block);
            block.dataset.highlighted = 'true';
        }
    });
}
