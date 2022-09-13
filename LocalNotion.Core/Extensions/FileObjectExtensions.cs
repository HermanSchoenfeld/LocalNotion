using JsonSubTypes;
using Notion.Client;

namespace LocalNotion.Core;

public static class FileObjectExtensions {
	public static string GetUrl(this FileObject pageParent) 
		=> pageParent switch {
			ExternalFile externalFile => externalFile.External.Url,
			UploadedFile uploadedFile => uploadedFile.File.Url,
			_ => throw new NotSupportedException(pageParent.GetType().ToString())
		};


	public static void SetUrl(this FileObject pageParent, string value)  {
		switch (pageParent) {
			case ExternalFile externalFile:
				externalFile.External.Url = value;
				break;
			case UploadedFile uploadedFile:
				uploadedFile.File.Url = value;
				break;
			default:
				throw new NotSupportedException(pageParent.GetType().ToString());
		}
	}

}
