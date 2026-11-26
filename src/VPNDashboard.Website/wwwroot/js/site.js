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
