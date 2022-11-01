$(document).ready(function ($) {

    // remove page link icon spans that have no icon
    $(".sp10-link-icon:not(:has(*))").remove();
    $(".sp10-link-icon:has(svg:only-child)").remove();

    // remove empty callout children
    $(".ln-callout-children:not(:has(*))").remove();
    
});