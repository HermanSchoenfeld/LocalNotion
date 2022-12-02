$(document).ready(function ($) {

    // remove empty block children
    $(".ln-block-children:not(:has(*))").remove();
    document.querySelectorAll('.ln-block-children').forEach(elem => {
        var $elem = $(elem);
        if ($elem.html().trim().length === 0)
            $elem.remove();
    });
    
});