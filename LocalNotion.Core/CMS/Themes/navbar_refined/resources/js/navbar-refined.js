/* Scoped navigation semantics. Bootstrap owns opening, closing and keyboard movement. */
(() => {
    "use strict";

    const normalizePath = path => path.replace(/\/+$/, "") || "/";

    function refineNavigation(nav) {
        nav.querySelectorAll(".dropdown-menu .nav-link").forEach(link => {
            if (!link.hasAttribute("data-bs-toggle"))
                link.classList.add("dropdown-item");
        });

        const currentPath = normalizePath(window.location.pathname);
        const candidates = [];
        nav.querySelectorAll(".navbar-nav a[href]").forEach(link => {
            if (link.hasAttribute("data-bs-toggle"))
                return;

            link.removeAttribute("aria-current");
            link.classList.remove("active", "text-bg-primary");
            const href = link.getAttribute("href");
            if (!href || href.startsWith("#"))
                return;

            let url;
            try {
                url = new URL(href, window.location.href);
            } catch {
                return;
            }
            if (url.origin !== window.location.origin)
                return;

            const path = normalizePath(url.pathname);
            if (path === currentPath) {
                link.setAttribute("aria-current", "page");
                candidates.push({ link, path });
            } else if (path !== "/" && currentPath.startsWith(path + "/")) {
                candidates.push({ link, path });
            }
        });

        // Prefer the closest authored destination when menu branches overlap.
        const closestLength = candidates.reduce((length, item) => Math.max(length, item.path.length), 0);
        candidates.forEach(({ link, path }) => {
            if (path.length === closestLength)
                link.classList.add("active");
        });

        nav.querySelectorAll(".dropdown").forEach(dropdown => {
            const toggle = dropdown.querySelector(":scope > .dropdown-toggle");
            if (!toggle)
                return;
            if (dropdown.querySelector(".dropdown-menu .active"))
                toggle.setAttribute("data-ln-current-branch", "true");
            else
                toggle.removeAttribute("data-ln-current-branch");
        });
    }

    function initialize() {
        document.querySelectorAll("nav.ln-navbar-refined").forEach(refineNavigation);
    }

    if (document.readyState === "loading")
        document.addEventListener("DOMContentLoaded", initialize, { once: true });
    else
        initialize();

    // Stock CMS load-time initialization may add active classes; reconcile only this opt-in menu.
    window.addEventListener("load", initialize, { once: true });
    window.addEventListener("pageshow", initialize);
})();
