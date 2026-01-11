using Sphere10.Framework;

namespace LocalNotion.Core;

public interface IThemeManager {

	bool TryLoadTheme(string theme, out ThemeInfo themeInfo);

	IEnumerable<string> FilterAvailableThemes(IEnumerable<string> themes);

}

public static class TemplateManagerExtensions {

	public static ThemeInfo LoadTheme(this IThemeManager themeManager, string theme) {
		Guard.ArgumentNotNull(theme, nameof(theme));
		if (!themeManager.TryLoadTheme(theme, out var templateInfo))
			throw new InvalidOperationException($"Unable to load theme info for '{theme}'. Possible errors include missing or corrupt '{Constants.ThemeInfoFileName}' in theme folder, or a cyclic dependency detected among theme inheritance graph.");
		return templateInfo;
	}
} 