using Hydrogen;
using Notion.Client;
using System.Threading;

namespace LocalNotion.Core;

public interface IUrlResolver {
	/// <summary>
	/// Generates a URL to resource <see cref="toResourceID"/> served by a rendering of resource <see cref="fromResourceID"/>.
	/// </summary>
	bool TryResolveLinkToResource(LocalNotionResource from, string toResourceID, RenderType? renderType, out string url, out LocalNotionResource toResource);

}


public static class IUrlGeneratorExtensions {

	public static string Resolve(this IUrlResolver localResourceResolver, LocalNotionResource from, string toResourceID, RenderType? renderType, out LocalNotionResource toResource)
		=> localResourceResolver.TryResolveLinkToResource(from, toResourceID, renderType, out var url, out toResource) ? url : throw new InvalidOperationException($"Unable to resolve URL for Resource ({toResourceID})");

	public static string ResolveOrDefault(this IUrlResolver localResourceResolver, LocalNotionResource from, string toResourceID, RenderType? renderType, out LocalNotionResource toResource, string defaultValue)
		=> localResourceResolver.TryResolveLinkToResource(from, toResourceID, renderType, out var url, out toResource) ? url : defaultValue;

	public static bool TryResolveUploadedFileUrl(this IUrlResolver localResourceResolver, LocalNotionResource from, UploadedFile file, out string url, out LocalNotionResource toResource) {
		if (LocalNotionHelper.TryParseNotionFileUrl(file.File.Url, out var toResourceID, out _)) {
			if (localResourceResolver.TryResolveLinkToResource(from, toResourceID, RenderType.File, out url, out toResource))
				return true;
		}
		url = default!;
		toResource = default!;
		return false;
	}

	public static string ResolveUploadedFileUrl(this IUrlResolver localResourceResolver, LocalNotionResource from,  UploadedFile file, out LocalNotionResource toResource) {
		if (!localResourceResolver.TryResolveUploadedFileUrl(from, file, out var url, out toResource))
			throw new InvalidOperationException($"Uploaded file '{file.File.Url}' was not found as a local toResource");
		return url;
	}

}