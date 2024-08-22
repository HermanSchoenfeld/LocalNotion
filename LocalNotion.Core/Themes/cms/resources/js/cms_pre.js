function ReplaceParentAlignment(span) {
    // Get the parent element (the p element)
    var parentP = span.parentElement;

    // List of alignment classes to check
    var alignmentClasses = ['text-start', 'text-center', 'text-left', 'text-end'];

    // Remove any existing alignment class on the parent
    parentP.classList.forEach(function (className) {
        if (alignmentClasses.includes(className)) {
            parentP.classList.remove(className);
        }
    });

    // Move the alignment class from span to parent p
    span.classList.forEach(function (className) {
        if (alignmentClasses.includes(className)) {
            parentP.classList.add(className);
            span.classList.remove(className);
        }
    });
}