using Hydrogen;

namespace LocalNotion.Core;

public interface ITemplateManager {

	bool TryLoadTemplate(string template, out TemplateInfo templateInfo);

}

public static class TemplateManagerExtensions {

	public static TemplateInfo LoadTemplate(this ITemplateManager templateManager, string template) {
		Guard.ArgumentNotNull(template, nameof(template));
		if (!templateManager.TryLoadTemplate(template, out var templateInfo))
			throw new InvalidOperationException($"Unable to load template info for '{template}'. Possible errors include missing or corrupt '{Constants.TemplateInfoJsonFilename}' in template folder, or a cyclic dependency detected among template inheritance graph.");
		return templateInfo;
	}
}