// Plays the hex-to-text "decode" once on each example message as it scrolls into view.
// Called by Home.razor after its first interactive render, so it observes the live DOM rather than the prerendered one.
window.lookUpDecode = {
    observeExamples() {
        const section = document.querySelector('.examples-section');
        if (!section || section.classList.contains('decode-ready')) return;
        if (!('IntersectionObserver' in window) || window.matchMedia('(prefers-reduced-motion: reduce)').matches) return;

        const observer = new IntersectionObserver((entries) => {
            for (const entry of entries) {
                if (entry.isIntersecting) {
                    entry.target.classList.add('is-decoded');
                    observer.unobserve(entry.target);
                }
            }
        }, { threshold: 0 }); // Any visible part: a card peeking over the fold must never rest as raw hex.

        section.classList.add('decode-ready');
        section.querySelectorAll('.message-card').forEach((card) => observer.observe(card));
    }
};
