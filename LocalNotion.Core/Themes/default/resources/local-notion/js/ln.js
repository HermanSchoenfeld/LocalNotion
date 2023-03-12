$(document).ready(function() {

    // remove empty block children
    $(".ln-block-children:not(:has(*))").remove();
    $(".ln-callout-children:not(:has(*))").remove();
});