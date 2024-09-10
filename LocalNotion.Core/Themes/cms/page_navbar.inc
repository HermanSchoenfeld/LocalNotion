<!--- This is an exemplary navbar -->
<style>
    /* Base styles for .nav-link */
    .nav-link {
        border-bottom: 2px solid transparent; /* Transparent border when not hovered */
        border-radius: var(--bs-border-radius); /* Rounded corners */
        transition: border-color .1s ease-in-out, border-radius .1s ease-in-out; /* Smooth transition for both border color and radius */
    }

        /* Hover effect */
        .nav-link:hover {
            border-bottom-color: currentColor; /* Solid border on hover */
            border-radius: 0; /* No rounding on hover */
        }

        /* Active state - rounded and no hover effect */
        .nav-link.active {
            border-bottom-color: currentColor; /* Solid border for active state */
            border-radius: var(--bs-border-radius); /* Rounded when active */
            pointer-events: none; /* Disable hover effects when active */
        }
</style>

<nav class="navbar sticky-top navbar-expand-lg {include://color_bg.inc} {include://color_text.inc}">
    <div class="container-fluid">
        <a class="navbar-brand" href="/">
            <img src="/files/d3adc251-6283-45db-83ed-f8817ebf996e/sphere10.png" alt="Logo" class="d-inline-block align-text-top" style="max-height:1.85em">
        </a>

        <button class="navbar-toggler" type="button" data-bs-toggle="collapse" data-bs-target="#navbarNav" aria-controls="navbarNav" aria-expanded="false" aria-label="Toggle navigation">
            <span class="navbar-toggler-icon"></span>
        </button>
        <div class="collapse navbar-collapse ms-auto" id="navbarNav">
            <ul class="navbar-nav nav-pills ms-auto">
                <li class="nav-item mx-2 ">
                    <a class="nav-link px-3" href="/">Home</a>
                </li>
                <li class="nav-item mx-2 ">
                    <a class="nav-link px-3" href="/services">Services</a>
                </li>
                <li class="nav-item mx-2 ">
                    <a class="nav-link px-3" href="/articles">Articles</a>
                </li>
                <li class="nav-item mx-2 ">
                    <a class="nav-link px-3 disabled" aria-disabled="true">Disabled</a>
                </li>
                <li class="nav-item mx-2 dropdown">
                    <a class="nav-link px-3  dropdown-toggle" href="#" role="button" data-bs-toggle="dropdown" aria-expanded="false">
                        Dropdown
                    </a>
                    <ul class="dropdown-menu dropdown-menu-end">
                        <li><a class="dropdown-item " href="/articles/how-to/computer-security/understanding-pem-and-pfx-files">Understanding PEM and PFX Files</a></li>
                        <li><a class="dropdown-item " href="/articles/raspberry-pi/sub-category/how-to-x">How To X</a></li>
                        <li><a class="dropdown-item " href="/how/to/do/x">How To X (Dark)</a></li>
                        <li><hr class="dropdown-divider"></li>
                        <li><a class="dropdown-item " href="#">Something else here</a></li>
                    </ul>
                </li>
            </ul>
        </div>

    </div>
</nav>

<script>
    // Define the classes to add/remove
    const activeClasses = ['active', '{include://color_text_aux.inc}'];

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
</script>