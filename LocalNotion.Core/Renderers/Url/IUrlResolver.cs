using Hydrogen;
using Notion.Client;
using System.Threading;

namespace LocalNotion.Core;

public interface IUrlResolver {
	/// <summary>
	/// Generates a URL to resource <see cref="toResourceID"/> served by a rendering of resource <see cref="fromResourceID"/>.
	/// </summary>
	bool TryResolveLinkToResource(LocalNotionResourceType fromResourceType, string fromResourceID, string toResourceID, RenderType? renderType, out string url, out LocalNotionResource toResource);

}


public static class IUrlGeneratorExtensions {

	public static string Resolve(this IUrlResolver localResourceResolver, LocalNotionResourceType fromResourceType, string fromResourceID, string toResourceID, RenderType? renderType, out LocalNotionResource toResource)
		=> localResourceResolver.TryResolveLinkToResource(fromResourceType, fromResourceID, toResourceID, renderType, out var url, out toResource) ? url : throw new InvalidOperationException($"Unable to resolve URL for Resource ({toResourceID})");

	public static string ResolveOrDefault(this IUrlResolver localResourceResolver, LocalNotionResourceType fromResourceType, string fromResourceID, string toResourceID, RenderType? renderType, out LocalNotionResource toResource, string defaultValue)
		=> localResourceResolver.TryResolveLinkToResource(fromResourceType, fromResourceID, toResourceID, renderType, out var url, out toResource) ? url : defaultValue;

	public static bool TryResolveUploadedFileUrl(this IUrlResolver localResourceResolver, LocalNotionResourceType fromResourceType, string fromResourceID, UploadedFile file, out string url, out LocalNotionResource toResource) {
		if (LocalNotionHelper.TryParseNotionFileUrl(file.File.Url, out var toResourceID, out _)) {
			if (localResourceResolver.TryResolveLinkToResource(fromResourceType, fromResourceID, toResourceID, RenderType.File, out url, out toResource))
				return true;
		}
		url = default!;
		toResource = default!;
		return false;
	}

	public static string ResolveUploadedFileUrl(this IUrlResolver localResourceResolver, LocalNotionResourceType fromResourceType, string fromResourceID,  UploadedFile file, out LocalNotionResource toResource) {
		if (!localResourceResolver.TryResolveUploadedFileUrl(fromResourceType, fromResourceID, file, out var url, out toResource))
			throw new InvalidOperationException($"Uploaded file '{file.File.Url}' was not found as a local toResource");
		return url;
	}

}