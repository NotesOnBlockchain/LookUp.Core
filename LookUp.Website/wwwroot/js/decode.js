// The site's "decode" theme: each block of text first appears as its raw UTF-8 bytes in hex, then wipes into the
// readable text, left to right, the way Look Up decodes OP_RETURN data. It runs on landing, whenever a block scrolls
// back into view, and when a block's text changes (for example the search result heading).
//
// Blazor owns the page markup and replaces the prerendered DOM when it becomes interactive, so this script never adds
// nodes inside it: the hex is drawn in a separate layer at the end of <body>, and the text is revealed with the Web
// Animations API, which leaves the element's attributes alone. Home.razor calls start() after its first interactive render.
(() => {
    const TARGETS = '.hero-name, .hero-tagline, .home-container h2, .home-container p:not(.visually-hidden), .search-empty li, .message-text, .tech-stack li';
    const DURATION = 700;
    const EASING = 'cubic-bezier(0.65, 0, 0.35, 1)';
    const STAGGER = 50;
    const MAX_STAGGER = 400;
    const MAX_HEX_CHARS = 600;

    const state = new WeakMap(); // element -> { inView, animation, overlay }
    let layer = null;
    let started = false;

    const stateOf = (el) => {
        let s = state.get(el);
        if (!s) state.set(el, s = { inView: false, animation: null, overlay: null });
        return s;
    };

    const hexOf = (text) =>
        Array.from(new TextEncoder().encode(text.replace(/\s+/g, ' ').trim()), (b) => b.toString(16).padStart(2, '0')).join('');

    // The visible text only: screen-reader-only additions (such as "(opens in a new tab)") are not part of what is decoded.
    const visibleTextOf = (el) => {
        const copy = el.cloneNode(true);
        copy.querySelectorAll('.visually-hidden').forEach((hidden) => hidden.remove());
        return copy.textContent;
    };

    const isDecodable = (el) => !el.closest('.visually-hidden') && el.getBoundingClientRect().width > 1;

    function decode(el, delay) {
        if (!isDecodable(el)) return;
        const s = stateOf(el);
        s.animation?.cancel();
        s.overlay?.remove();

        const rect = el.getBoundingClientRect();
        const style = getComputedStyle(el);
        const overlay = document.createElement('div');
        overlay.className = 'decode-hex';
        overlay.dir = 'ltr';
        overlay.textContent = hexOf(visibleTextOf(el)).slice(0, MAX_HEX_CHARS);
        Object.assign(overlay.style, {
            left: `${rect.left + window.scrollX}px`,
            top: `${rect.top + window.scrollY}px`,
            width: `${rect.width}px`,
            height: `${rect.height}px`,
            fontSize: `${Math.min(56, Math.max(13, parseFloat(style.fontSize) * 0.5))}px`,
            textAlign: style.textAlign === 'center' ? 'center' : 'left'
        });
        layer.appendChild(overlay);

        const timing = { duration: DURATION, easing: EASING, delay, fill: 'backwards' };
        const reveal = el.animate([{ clipPath: 'inset(0 100% 0 0)' }, { clipPath: 'inset(0 0 0 0)' }], timing);
        const cover = overlay.animate([{ clipPath: 'inset(0 0 0 0)' }, { clipPath: 'inset(0 0 0 100%)' }], { ...timing, fill: 'both' });

        s.animation = reveal;
        s.overlay = overlay;
        const done = () => {
            overlay.remove();
            if (s.overlay === overlay) { s.overlay = null; s.animation = null; }
        };
        cover.finished.then(done, done);
        reveal.finished.catch(() => cover.cancel());
    }

    // Decodes a batch top to bottom with a short, capped stagger.
    function decodeBatch(elements) {
        elements
            .map((el) => ({ el, top: el.getBoundingClientRect().top }))
            .sort((a, b) => a.top - b.top)
            .forEach(({ el }, i) => decode(el, Math.min(i * STAGGER, MAX_STAGGER)));
    }

    const io = new IntersectionObserver((entries) => {
        const entering = [];
        for (const entry of entries) {
            const s = stateOf(entry.target);
            if (entry.isIntersecting && !s.inView) entering.push(entry.target);
            s.inView = entry.isIntersecting;
        }
        if (entering.length) decodeBatch(entering);
    }, { threshold: 0 });

    const targetsIn = (node) => {
        if (!(node instanceof Element)) return [];
        const found = node.matches(TARGETS) ? [node] : [];
        return found.concat(Array.from(node.querySelectorAll(TARGETS)));
    };

    // New blocks (search results, state messages) start observed; blocks whose text changes decode again if visible.
    const mo = new MutationObserver((mutations) => {
        const changed = new Set();
        for (const m of mutations) {
            if (layer && layer.contains(m.target)) continue;
            for (const node of m.addedNodes) targetsIn(node).forEach((el) => io.observe(el));
            const host = (m.target instanceof Element ? m.target : m.target.parentElement)?.closest(TARGETS);
            if (host && stateOf(host).inView && (m.type === 'characterData' || m.addedNodes.length)) changed.add(host);
        }
        if (changed.size) decodeBatch(Array.from(changed));
    });

    window.lookUpDecode = {
        start() {
            const root = document.documentElement;
            const motionOk = !window.matchMedia('(prefers-reduced-motion: reduce)').matches && 'animate' in Element.prototype;
            if (started || !motionOk) {
                root.classList.remove('decode-intro');
                return;
            }
            started = true;

            layer = document.createElement('div');
            layer.id = 'decode-layer';
            layer.setAttribute('aria-hidden', 'true');
            document.body.appendChild(layer);

            // Landing: start the visible blocks' animations first, then lift the pre-decode state, so nothing flashes.
            const all = targetsIn(document.body).filter(isDecodable);
            const visible = all.filter((el) => { const r = el.getBoundingClientRect(); return r.bottom > 0 && r.top < window.innerHeight; });
            visible.forEach((el) => { stateOf(el).inView = true; });
            decodeBatch(visible);
            root.classList.remove('decode-intro');

            all.forEach((el) => io.observe(el));
            mo.observe(document.body, { childList: true, subtree: true, characterData: true });
        }
    };
})();
