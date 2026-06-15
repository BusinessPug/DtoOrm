//  DtoOrm - Fremlæggelse (slideshow controller)
//  Plain, dependency-free. Keyboard-first, built for presenting live.
(function () {
    "use strict";

    var slides = Array.prototype.slice.call(document.querySelectorAll(".slide"));
    var bar = document.getElementById("bar");
    var curEl = document.getElementById("cur");
    var totEl = document.getElementById("tot");
    var hint = document.getElementById("hint");
    var overview = document.getElementById("overview");
    var help = document.getElementById("help");
    var jumpList = document.getElementById("jumpList");

    if (slides.length === 0) { return; }

    var index = 0;
    var total = slides.length;
    if (totEl) { totEl.textContent = String(total); }

    // Build the overview jump list from each slide's data-title.
    if (jumpList) {
        slides.forEach(function (slide, i) {
            var li = document.createElement("li");
            var title = slide.getAttribute("data-title") || ("Slide " + (i + 1));
            li.textContent = (i + 1) + ".  " + title;
            li.addEventListener("click", function () {
                go(i);
                closeOverlays();
            });
            jumpList.appendChild(li);
        });
    }

    function clamp(n) {
        if (n < 0) { return 0; }
        if (n > total - 1) { return total - 1; }
        return n;
    }

    function render() {
        slides.forEach(function (slide, i) {
            slide.classList.remove("anim");
            slide.classList.toggle("is-active", i === index);
        });
        // Force reflow so the entrance animation replays on each visit.
        var active = slides[index];
        if (active) {
            void active.offsetWidth;
            active.classList.add("anim");
        }
        if (curEl) { curEl.textContent = String(index + 1); }
        if (bar) {
            var pct = total > 1 ? (index / (total - 1)) * 100 : 100;
            bar.style.width = pct + "%";
        }
        try {
            if (location.hash !== "#" + (index + 1)) {
                history.replaceState(null, "", "#" + (index + 1));
            }
        } catch (e) { /* ignore */ }
    }

    function go(n) {
        index = clamp(n);
        render();
    }

    function next() { if (index < total - 1) { go(index + 1); } }
    function prev() { if (index > 0) { go(index - 1); } }

    function fadeHint() {
        if (hint && !hint.classList.contains("fade")) {
            hint.classList.add("fade");
        }
    }

    function closeOverlays() {
        if (overview) { overview.classList.remove("open"); }
        if (help) { help.classList.remove("open"); }
    }

    function toggle(el) {
        if (!el) { return; }
        var isOpen = el.classList.contains("open");
        closeOverlays();
        if (!isOpen) { el.classList.add("open"); }
    }

    document.addEventListener("keydown", function (e) {
        // Let the browser handle modified keys (e.g. Ctrl+R, Cmd+L).
        if (e.ctrlKey || e.metaKey || e.altKey) { return; }

        var key = e.key;

        if (key === "Escape") { closeOverlays(); return; }

        switch (key) {
            case "ArrowRight":
            case "PageDown":
            case " ":
            case "Enter":
                e.preventDefault(); next(); fadeHint(); break;
            case "ArrowLeft":
            case "PageUp":
            case "Backspace":
                e.preventDefault(); prev(); fadeHint(); break;
            case "Home":
                e.preventDefault(); go(0); break;
            case "End":
                e.preventDefault(); go(total - 1); break;
            case "n":
            case "N":
                document.body.classList.toggle("notes-on"); fadeHint(); break;
            case "o":
            case "O":
                toggle(overview); break;
            case "?":
            case "h":
            case "H":
                toggle(help); break;
            case "f":
            case "F":
                toggleFullscreen(); break;
            default:
                if (key >= "0" && key <= "9") {
                    // Numeric quick-jump: type a number, brief debounce assembles multi-digit.
                    handleDigit(key);
                }
                break;
        }
    });

    var digitBuffer = "";
    var digitTimer = null;
    function handleDigit(d) {
        digitBuffer += d;
        if (digitTimer) { clearTimeout(digitTimer); }
        digitTimer = setTimeout(function () {
            var n = parseInt(digitBuffer, 10);
            digitBuffer = "";
            if (!isNaN(n) && n >= 1 && n <= total) { go(n - 1); }
        }, 350);
    }

    function toggleFullscreen() {
        var doc = document;
        var el = doc.documentElement;
        if (!doc.fullscreenElement) {
            if (el.requestFullscreen) { el.requestFullscreen(); }
        } else if (doc.exitFullscreen) {
            doc.exitFullscreen();
        }
    }

    // Click navigation: left third = back, right two-thirds = forward.
    // Ignore clicks on interactive elements and inside overlays/talk cards.
    document.addEventListener("click", function (e) {
        if (e.target.closest("a, button, .overlay, .talk, .hud, pre")) { return; }
        var fromLeft = e.clientX / window.innerWidth;
        if (fromLeft < 0.33) { prev(); } else { next(); }
        fadeHint();
    });

    // Touch swipe for tablets.
    var touchX = null;
    document.addEventListener("touchstart", function (e) {
        touchX = e.changedTouches[0].clientX;
    }, { passive: true });
    document.addEventListener("touchend", function (e) {
        if (touchX === null) { return; }
        var dx = e.changedTouches[0].clientX - touchX;
        if (Math.abs(dx) > 50) { if (dx < 0) { next(); } else { prev(); } fadeHint(); }
        touchX = null;
    }, { passive: true });

    // HUD buttons.
    bindClick("btnPrev", prev);
    bindClick("btnNext", next);
    bindClick("btnNotes", function () { document.body.classList.toggle("notes-on"); });
    bindClick("btnOverview", function () { toggle(overview); });

    function bindClick(id, fn) {
        var el = document.getElementById(id);
        if (el) {
            el.addEventListener("click", function (ev) {
                ev.stopPropagation();
                fn();
                fadeHint();
            });
        }
    }

    // Close overlay when clicking its dim backdrop.
    [overview, help].forEach(function (ov) {
        if (!ov) { return; }
        ov.addEventListener("click", function (e) {
            if (e.target === ov) { closeOverlays(); }
        });
    });

    // Deep-link support: open on #<n>.
    var fromHash = parseInt((location.hash || "").replace("#", ""), 10);
    if (!isNaN(fromHash) && fromHash >= 1 && fromHash <= total) {
        index = fromHash - 1;
    }

    render();

    // Auto-fade the hint after a few seconds.
    setTimeout(fadeHint, 6000);
})();
