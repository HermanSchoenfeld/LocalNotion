function PageLoader_Init() {
    // Inject preloader into the body if necessary
    //$("body").prepend('<div id="preloader"><div id="preloader_status"></div></div>');

    var isBot = /bot|googlebot|bingbot|baiduspider|yandexbot|slurp|duckduckbot|applebot|sogou|seznambot|naverbot|exabot|rogerbot|ahrefsbot/i.test(navigator.userAgent);

    var preloader = document.getElementById('preloader');

    if (isBot || !preloader) {
        preloader?.remove();  // Remove if bot or preloader is not found
        return;
    }

    // Select status element as a child of preloader
    var status = preloader.querySelector('#preloader_status');
    if (!status) {
        preloader.remove();  // Clean up if no status found
        return;
    }

    preloader.style.opacity = 1;
    status.style.opacity = 1;

    window.addEventListener('load', function () {
        let fadeOutAmount = 0.1;

        // Fade out status element
        (function fadeStatus() {
            status.style.opacity -= fadeOutAmount;
            if (status.style.opacity <= 0) {
                status.style.display = "none";
            } else {
                setTimeout(fadeStatus, 50);
            }
        })();

        // After 400ms, start fading out the preloader
        setTimeout(() => {
            (function fadePreloader() {
                preloader.style.opacity -= fadeOutAmount;
                if (preloader.style.opacity <= 0) {
                    preloader.style.display = "none";
                    preloader.remove();  // Remove preloader from the DOM
                } else {
                    setTimeout(fadePreloader, 40);
                }
            })();
        }, 400);
    });
}

function SetActiveNavbarItems() {
    // Define the classes to add/remove
    const activeClasses = ['active', 'text-bg-primary'];

    // Get current URL path, query string, and hash
    const currentUrl = window.location.pathname + window.location.search + window.location.hash;

    // Get all nav-links within the navbar, including dropdown items
    const navLinks = document.querySelectorAll('.navbar-nav .nav-link, .navbar-nav .dropdown-item');

    // Iterate over navLinks
    for (let i = 0; i < navLinks.length; i++) {
        const link = navLinks[i];
        const href = link.getAttribute('href');

        // Ignore links with href="#" or empty href
        if (!href || href.includes('#') || href === '') {
            continue;
        }

        const linkUrl = new URL(link.href, window.location.origin);

        // Remove the active classes from all links
        link.classList.remove(...activeClasses);

        // Rule A: Exact URL match or homepage link when on the root
        if (
            (linkUrl.pathname === window.location.pathname) ||
            (linkUrl.pathname === '/' && window.location.pathname === '/')
        ) {
            link.classList.add(...activeClasses);
        }

        // Rule B: Current URL is a sub-folder of the link URL, but not the root
        else if (window.location.pathname.startsWith(linkUrl.pathname) && linkUrl.pathname !== '/') {
            link.classList.add(...activeClasses);
        }
    }
}

function InitializeNestedDropdowns() {
    const nestedDropdowns = document.querySelectorAll('.dropdown-menu .dropdown');

    for (let i = 0; i < nestedDropdowns.length; i++) {
        const submenu = nestedDropdowns[i];
        const parentLink = submenu.querySelector('.dropdown-toggle');

        parentLink.addEventListener('click', function (event) {
            event.preventDefault();
            event.stopPropagation(); // Prevent Bootstrap's logic from interfering with nested dropdowns
        });
    }
}

function InitializeSectionSeparatorShapes() {
    function GetEffectiveBackgroundColor(element) {
        let color = window.getComputedStyle(element).backgroundColor;

        // Loop through parent elements to find the first non-transparent, non-translucent color
        while (IsTranslucent(color) && element.parentElement) {
            element = element.parentElement;
            color = window.getComputedStyle(element).backgroundColor;
        }

        return color;
    }

    function IsTranslucent(color) {
        const rgbaMatch = color.match(/^rgba?\((\d+),\s*(\d+),\s*(\d+),?\s*([\d.]+)?\)$/);
        if (rgbaMatch && rgbaMatch[4] !== undefined) {
            const alpha = parseFloat(rgbaMatch[4]);
            return alpha < 1 && alpha >= 0; // Include fully opaque and translucent colors
        }
        return false;
    }

    function IsVisible(element) {
        const nonVisualTags = ['script', 'style', 'meta', 'link'];
        return !nonVisualTags.includes(element.tagName.toLowerCase()) &&
            getComputedStyle(element).display !== 'none' &&
            getComputedStyle(element).visibility !== 'hidden';
    }

    document.querySelectorAll('.shape').forEach(function (shape) {
        const shapeContainerDiv = shape.closest('.shape-container'); // Get the shape container
        if (!shapeContainerDiv)
            return; // some other shape (not a page separating shape)
        let currentContent = shapeContainerDiv.parentElement;

        let nextContent = currentContent.nextElementSibling; // Get the next sibling

        // If no next sibling exists, get the parent's next sibling
        if (!nextContent) {
            nextContent = currentContent.parentElement.nextElementSibling;
        } else if (nextContent.tagName.toLowerCase() === 'main') {
            // If the nextContent is a <main>, get its first child
            nextContent = nextContent.firstElementChild;
            // Keep iterating until we find a non-script tag
            while (nextContent && !IsVisible(nextContent)) {
                nextContent = nextContent.nextElementSibling;
            }
        }

        if (nextContent) {
            const sectionColor = GetEffectiveBackgroundColor(nextContent); // Get effective background color
            const svgPath = shape.querySelector('svg path'); // Find the path inside the SVG
            if (svgPath) {
                // Apply the computed background color as fill, including any alpha
                svgPath.setAttribute('fill', sectionColor); // Set the fill attribute to the background color
            }

            // Adjust the shape bottom so it sits at end of currentContent
            const paddingBottom = window.getComputedStyle(currentContent).paddingBottom;
            shape.style.bottom = `-${paddingBottom}`;
        }
    });
}

function InitializeTypedText() {
    const toggles = document.querySelectorAll('[data-typed]');
    toggles.forEach((toggle) => {
        const elementOptions = toggle.dataset.typed ? JSON.parse(toggle.dataset.typed) : {};

        const defaultOptions = {
            typeSpeed: 40,
            backSpeed: 40,
            backDelay: 1000,
            loop: true,
        };

        const options = {
            ...defaultOptions,
            ...elementOptions,
        };

        new Typed(toggle, options);
    });

}

function InitializeAnimateOnScroll() {
    const options = {
        duration: 700,
        easing: 'ease-out-quad',
        once: true,
        delay: 10,
        startEvent: 'load',
    };
    AOS.init(options);
    window.AOS = AOS; // Make available globally
}

function InitializeGalleries() {
    const toggles = document.querySelectorAll('[data-isotope]');
    const filters = document.querySelectorAll('[data-filter]');

    toggles.forEach(function (toggle) {
        imagesLoaded(toggle, function () {
            const options = JSON.parse(toggle.dataset.isotope);

            new Isotope(toggle, options);
        });
    });

    filters.forEach(function (filter) {
        filter.addEventListener('click', function (e) {
            e.preventDefault();

            const cat = filter.dataset.filter;
            const target = filter.dataset.bsTarget;
            const instance = Isotope.data(target);

            instance.arrange({
                filter: cat,
            });
        });
    });

    // Make available globally
    window.Isotope = Isotope;
    window.imagesLoaded = imagesLoaded;
}

function InitializeBigPictures() {
    const toggles = document.querySelectorAll('[data-bigpicture]');

    toggles.forEach(function (toggle) {
        toggle.addEventListener('click', function (e) {
            e.preventDefault();

            const elementOptions = JSON.parse(toggle.dataset.bigpicture);

            const defaultOptions = {
                el: toggle,
                noLoader: true,
            };

            const options = {
                ...defaultOptions,
                ...elementOptions,
            };

            BigPicture(options);
        });
    });

    // Make available globally
    window.BigPicture = BigPicture;
}

/************************************************ MAIN **********************************************************************/

PageLoader_Init();
SetActiveNavbarItems();
InitializeNestedDropdowns();
InitializeSectionSeparatorShapes();
InitializeTypedText();
InitializeAnimateOnScroll();
InitializeGalleries();
InitializeBigPictures();
