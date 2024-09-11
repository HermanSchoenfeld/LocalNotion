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
        if (!href || href === '#' || href === '') {
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



/************************************************ MAIN **********************************************************************/

// Navbar init
SetActiveNavbarItems();
InitializeNestedDropdowns();

// Toggles
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
