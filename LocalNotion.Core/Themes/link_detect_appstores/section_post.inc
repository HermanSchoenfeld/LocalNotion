<script language="javascript">
	// TODO: this needs to be cleaned up
	function MakeDownloadLink(target) {
		var text = $(target).text().trim().toUpperCase();
		var isWindows = text === "WINDOWS";
		var isWindowsStore = text === "WINDOWS STORE";
		var isMacOS = text === "MACOS";
		var isAppleStore = text === "APPLE STORE";
		var isLinux = text == "LINUX";
		var isGooglePlay = text === "GOOGLE STORE";
		var isPlatform = isWindows || isMacOS || isLinux;
		var isStore = isWindowsStore || isAppleStore || isGooglePlay;
		var isDownload = isPlatform || isStore;
		var isButton = text.startsWith("[") && text.endsWith("]");
		if (!isDownload && !isButton)
			return;

		if (isButton) {
			$(target).html($(target).html().replace("[", "").replace("]", ""));
			$(target).attr('class', 'btn btn-sm btn-primary lift');
			$(target).attr('target', '_blank');
			return;
		}


		// remove <p> envelope
		var parent = $(target).parent();
		$(target).insertAfter(parent);
		parent.remove();

		// Remove underline
		var firstChildSpan = $(target).find('span:first');
		if (firstChildSpan.length) {
			firstChildSpan.attr('class', 'text-decoration-none');
		}

		if (isPlatform) {
			// Minify all links adjacent to download button
			$(target).nextAll(':not(:empty)').attr('class', 'small fw-light text-start');

			// add btn styling
			$(target).attr('class', 'btn btn-sm fw-bold btn-danger text-start');
			$(target).css('width', '9em');
			$(target).css('border-radius', '50em');
			$(target).css('padding-top', '0.7em');
			$(target).css('padding-bottom', '0.7em');
			$(target).css('padding-left', '1em');
		}

		if (isStore) {
			$(target).removeAttr('class');
			$(target).text('');
		}

		// add OS icon
		var platformImageStyle = "width: 16px; height: 16px; fill: currentcolor;margin-bottom: 0.27em;margin-right:0.5em; filter:  brightness(0) invert(1);";
		var storeImageStyle = "width:7.5em";

		if (isWindows) {
			$(target).prepend('<img src="https://cdn.jsdelivr.net/gh/sphere10/cdn/local-notion/themes/link_detect_appstores/resources/img/windows.svg" style="' + platformImageStyle + '">');
		}
		if (isWindowsStore) {
			$(target).prepend('<img src="https://cdn.jsdelivr.net/gh/sphere10/cdn/local-notion/themes/link_detect_appstores/resources/img/microsoft-store.svg" style="' + storeImageStyle + '">');
		}
		if (isMacOS) {
			$(target).prepend('<img src="https://cdn.jsdelivr.net/gh/sphere10/cdn/local-notion/themes/link_detect_appstores/resources/img/apple.svg" style="' + platformImageStyle + '">');
		}
		if (isAppleStore) {
			$(target).prepend('<img src="https://cdn.jsdelivr.net/gh/sphere10/cdn/local-notion/themes/link_detect_appstores/resources/img/apple-store.svg" style="' + storeImageStyle + '">');
		}
		if (isLinux) {
			$(target).prepend('<img src="https://cdn.jsdelivr.net/gh/sphere10/cdn/local-notion/themes/link_detect_appstores/resources/img/linux.svg" style="' + platformImageStyle + '">');
		}
		if (isGooglePlay) {
			$(target).prepend('<img src="https://cdn.jsdelivr.net/gh/sphere10/cdn/local-notion/themes/link_detect_appstores/resources/img/google-play.svg" style="' + storeImageStyle + '">');
		}
	}

	$(document).ready(function () {
		$("a").each(function () {
			MakeDownloadLink(this);
		});
	});
</script>