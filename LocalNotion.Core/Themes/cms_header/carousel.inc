{include://carousel_pre_start.inc}
<div id="{object_id}" class="carousel slide {include://carousel_type.inc} {include://carousel_height.inc} {include://color_bg.inc} {include://color_text.inc}" data-bs-ride="carousel">
    {include://carousel_post_start.inc}
    {include://carousel_indicators.inc}
    <div class="carousel-inner">
        {children}
    </div>
    {include://carousel_buttons.inc}

    <!-- Script to connect indicators to slides -->
    <script>
        var scriptTag = document.currentScript;
        var parentElement = scriptTag.parentElement;
        var carouselItems = parentElement.getElementsByClassName('carousel-inner')[0].querySelectorAll('.carousel-item');
        var indicatorsContainer = parentElement.querySelector('.carousel-indicators');

        if (carouselItems.length > 0) {
            // Add active class to the first item
            carouselItems[0].classList.add('active');

            // Fix up slide HTML (can't be done in editor)
            for (var i = 0; i < carouselItems.length; i++) {
                // For every carousel item, configure the background image (if present)
                var caption = carouselItems[i].querySelector('.carousel-caption');
                var backgroundImage = carouselItems[i].querySelector('img[alt="{BACKGROUND}"], img[alt=" {BACKGROUND}"], img[alt="{BACKGROUND} "]');
                if (backgroundImage) {
                    // Remember the parent of background image
                    var imageParent = backgroundImage.parentNode;

                    // Move background image before caption (and set classes)
                    backgroundImage.className = "d-block h-100 w-100 object-fit-cover";
                    carouselItems[i].insertBefore(backgroundImage, caption); // Move the image to be a direct child of the carousel-item

                    // Remove all the old ancestors of image
                    while (imageParent && imageParent != caption) {
                        var imageGrandParent = imageParent.parentNode;
                        imageGrandParent.removeChild(imageParent);
                        imageParent = imageGrandParent;
                    }
                }
            }
        }

        // Generate indicators if container exists and there are multiple items
        if (carouselItems.length > 1 && indicatorsContainer) {
            for (var i = 0; i < carouselItems.length; i++) {
                var indicator = document.createElement('button');
                indicator.type = 'button';
                indicator.setAttribute('data-bs-target', '#{object_id}');
                indicator.setAttribute('data-bs-slide-to', i.toString());

                // Set active and aria-current only for the first item
                if (i === 0) {
                    indicator.classList.add('active');
                    indicator.setAttribute('aria-current', 'true');
                }

                indicator.setAttribute('aria-label', 'Slide ' + (i + 1));
                indicatorsContainer.appendChild(indicator);
            }
        } else if (!indicatorsContainer) {
            console.warn('Indicators container not found, skipping indicator generation.');
        } else {
            // Remove Slide Navigation
            var controlButtons = parentElement.querySelectorAll(".carousel-indicators, .carousel-control-prev, .carousel-control-next");
            for (var button of controlButtons) {
                button.remove();
            }
        }

        scriptTag.remove(); // You can uncomment this to remove the script after execution
    </script>

    {include://shape.inc}
    {include://carousel_pre_end.inc}
</div>
{include://carousel_post_end.inc}
