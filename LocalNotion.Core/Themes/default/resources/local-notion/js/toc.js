$(document).ready(function ($) {
    let heading = $(".ln-page-content").find(":header").not("[data-toc-skip]");

    for (let i = 0; i < heading.length; i++) {
        const head = heading[i];
        const URL = head.innerText.replace(/ /g, '_')
        $(head).attr('id', URL)
        let header_level = parseInt(head.tagName.slice(-1));
        let spacing = "";
        spacing = `style="padding-left: ` + (header_level - 1) * 2 + `rem;"`
        const this_head = `<li class="py-1"><a ${spacing} class="text-secondary" href="#${URL}">${head.innerText}</a></li>`
        $('#toc').append(this_head)
    }
});