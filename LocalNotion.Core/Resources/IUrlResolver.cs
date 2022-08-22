using Hydrogen;
using Notion.Client;

namespace LocalNotion;

public interface IUrlResolver {
	bool TryResolve(string resourceId, out string resourceUrl, out LocalNotionResource resource);

	ILocalNotionRepository Repository { get; }
}

public static class IUrlResolverExtensions {

	public static string Resolve(this IUrlResolver localResourceResolver, string resourceID, out LocalNotionResource resource) 
		=> localResourceResolver.TryResolve(resourceID, out var resourceUrl, out resource) ? resourceUrl : throw new InvalidOperationException($"Unable to resolve URL for resource ({resourceID})");

	public static string ResolveOrDefault(this IUrlResolver localResourceResolver, string resourceID, string defaultValue, out LocalNotionResource resource) 
		=> localResourceResolver.TryResolve(resourceID, out var resourceUrl, out resource) ? resourceUrl : defaultValue;

	public static bool TryResolveUploadedFileUrl(this IUrlResolver localResourceResolver, UploadedFile file, out string resourceUrl, out LocalNotionResource resource) {
		if (LocalNotionHelper.TryParseNotionFileUrl(file.File.Url, out var resourceId, out var filename)) {
			if (localResourceResolver.TryResolve(resourceId, out resourceUrl, out resource))
				return true;
		}
		resourceUrl = default!;
		resource = default!;
		return false;
	}


	public static string ResolveUploadedFileUrl(this IUrlResolver localResourceResolver, UploadedFile file, out LocalNotionResource resource) {
		if (!localResourceResolver.TryResolveUploadedFileUrl(file, out var resourceUrl, out resource))
			throw new InvalidOperationException($"Uploaded file '{file.File.Url}' was not found as a local resource");
		return resourceUrl;
	}


}