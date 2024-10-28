<script>
    const currentScript = document.currentScript; // Reference to this script element

    // Find the first and second preceding <section> elements
    let firstSection = null;
    let secondSection = null;
    let element = currentScript.previousElementSibling;

    while (element) {
        if (element.tagName === "SECTION") {
            if (!firstSection) {
                firstSection = element; // First section before the script
            } else {
                secondSection = element; // Second section before the script
                break;
            }
        }
        element = element.previousElementSibling;
    }

    // Replace "py-5" with "pt-5" on the second preceding section
    if (secondSection) {
        secondSection.classList.replace("py-5", "pt-5");
    }

    // Replace "py-5" with "pb-5" on the first preceding section
    if (firstSection) {
        firstSection.classList.replace("py-5", "pb-5");
    }

    // Remove this script element from the DOM
    currentScript.remove();
</script>