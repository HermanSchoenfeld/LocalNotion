using Hydrogen;

namespace LocalNotion.Core;

public interface IThemeManager {

	bool TryLoadTemplate(string template, out ThemeInfo themeInfo);

}

public static class TemplateManagerExtensions {

	public static ThemeInfo LoadTemplate(this IThemeManager themeManager, string template) {
		Guard.ArgumentNotNull(template, nameof(template));
		if (!themeManager.TryLoadTemplate(template, out var templateInfo))
			throw new InvalidOperationException($"Unable to load template info for '{template}'. Possible errors include missing or corrupt '{Constants.TemplateInfoFilename}' in template folder, or a cyclic dependency detected among template inheritance graph.");
		return templateInfo;
	}
}