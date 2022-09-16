using JsonSubTypes;
using Notion.Client;

namespace LocalNotion.Core;

public static class FileObjectExtensions {
	public static string GetUrl(this FileObject fileObject) 
		=> fileObject switch {
			ExternalFile externalFile => externalFile.External.Url,
			UploadedFile uploadedFile => uploadedFile.File.Url,
			_ => throw new NotSupportedException(fileObject.GetType().ToString())
		};


	public static void SetUrl(this FileObject fileObject, string value)  {
		switch (fileObject) {
			case ExternalFile externalFile:
				externalFile.External.Url = value;
				break;
			case UploadedFile uploadedFile:
				uploadedFile.File.Url = value;
				break;
			default:
				throw new NotSupportedException(fileObject.GetType().ToString());
		}
	}

}
