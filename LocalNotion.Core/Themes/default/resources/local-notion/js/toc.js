$(document).ready(function () {
    let heading = $(".ln-page-content").find(":header").not("[data-toc-skip]");
    for (let i = 0; i < heading.length; i++) {
        const head = heading[i];
        const jhead = $(head);
        var id = jhead.attr('id')
        if (id === "") {
            const URL = head.innerText.replace(/ /g, '_');
            jhead.attr('id', URL);
        }
        let header_level = parseInt(head.tagName.slice(-1));
        let spacing = "";
        spacing = `style="padding-left: ` + (header_level - 1) * 2 + `rem;"`
        const this_head = `<li class="py-1 ln-behaviour-hover-highlight" style="transform: rotate(0);"><a ${spacing} class="text-secondary stretched-link" href="#${id}">${head.innerText}</a></li>`
        $('.ln-toc').append(this_head)
    }
});