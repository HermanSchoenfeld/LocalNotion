using Notion.Client;

namespace LocalNotion.Core;

public interface IUrlResolver {

	bool TryResolveResourceRender(string url, out LocalNotionResource resource, out RenderEntry entry);

	/// <summary>
	/// Generates a URL to resource <see cref="toResourceID"/> that is served by a rendering of resource <see cref="from"/>.
	/// </summary>
	bool TryResolve(LocalNotionResource from, string toResourceID, RenderType? renderType, out string url, out LocalNotionResource toResource);

}

public static class IUrlGeneratorExtensions {

	public static string Resolve(this IUrlResolver localResourceResolver, LocalNotionResource from, string toResourceID, RenderType? renderType, out LocalNotionResource toResource)
		=> localResourceResolver.TryResolve(from, toResourceID, renderType, out var url, out toResource) ? url : throw new InvalidOperationException($"Unable to resolve URL for Resource ({toResourceID})");

	public static string ResolveOrDefault(this IUrlResolver localResourceResolver, LocalNotionResource from, string toResourceID, RenderType? renderType, out LocalNotionResource toResource, string defaultValue)
		=> localResourceResolver.TryResolve(from, toResourceID, renderType, out var url, out toResource) ? url : defaultValue;

	public static bool TryResolveUploadedFileUrl(this IUrlResolver localResourceResolver, LocalNotionResource from, UploadedFile file, out string url, out LocalNotionResource toResource) {
		if (localResourceResolver.TryResolveResourceRender(file.File.Url, out toResource, out _))			
			if (localResourceResolver.TryResolve(from, toResource.ID, RenderType.File, out url, out toResource))
				return true;

		url = default!;
		toResource = default!;
		return false;
	}

	public static string ResolveResourceRender(this IUrlResolver localResourceResolver, LocalNotionResource from, UploadedFile file, out LocalNotionResource toResource) {
		if (!localResourceResolver.TryResolveUploadedFileUrl(from, file, out var url, out toResource))
			throw new InvalidOperationException($"Uploaded file '{file.File.Url}' was not found as a local resource");
		return url;
	}

}