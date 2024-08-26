//////////////////////////
// Initialization
/////////////////////////

// Apply feather icons
feather.replace();

// Apply some cosmetic fixes
$(document).ready(function () {
    // remove empty block children
    $(".ln-block-children:not(:has(*))").remove();
    $(".ln-callout-children:not(:has(*))").remove();
});

// Render database frames (ensure async loaded script is loaded first)
$(document).ready(function () {
    // Initialize the datatables (after lazy loaded datatables script loads)
    var maxRetries = 50; // Maximum number of polling attempts
    var interval = 100; // Polling interval in milliseconds

    function SetDataTables(retries) {
        if (typeof $.fn.DataTable !== 'undefined') {
            // DataTables is loaded, initialize it
            $('.ln-database').DataTable();
        } else if (retries > 0) {
            // DataTables is not loaded, check again after a delay
            setTimeout(function () {
                SetDataTables(retries - 1);
            }, interval);
        } else {
            // Max retries reached, log an error or handle as needed
            console.error('DataTables script did not load in time.');
        }
    }
    // Start polling with the maximum number of retries
    SetDataTables(maxRetries);
});

////////////////////////////////////
// Global Functions
////////////////////////////////////

// Used to search articles on website, just redirects to google
function DoSearch(event) {
    if (event.key === 'Enter') {
        event.preventDefault();
        const query = event.target.value;
        if (query.trim() !== "") {
            const domainAndPath = window.location.host + window.location.pathname;
            const googleSearchUrl = `https://www.google.com/search?q=${encodeURIComponent(query)} site:${domainAndPath}`;
            window.location.href = googleSearchUrl;
        }
    }
}