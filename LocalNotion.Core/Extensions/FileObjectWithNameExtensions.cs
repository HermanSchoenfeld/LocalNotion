using Notion.Client;

namespace LocalNotion.Core;

public static class FileObjectWithNameExtensions {
	public static string GetUrl(this FileObjectWithName fileObject) 
		=> fileObject switch {
			ExternalFileWithName externalFile => externalFile.External.Url,
			UploadedFileWithName uploadedFile => uploadedFile.File.Url,
			_ => throw new NotSupportedException(fileObject.GetType().ToString())
		};


	public static void SetUrl(this FileObjectWithName fileObject, string value)  {
		switch (fileObject) {
			case ExternalFileWithName externalFile:
				externalFile.External.Url = value;
				break;
			case UploadedFileWithName uploadedFile:
				uploadedFile.File.Url = value;
				break;
			default:
				throw new NotSupportedException(fileObject.GetType().ToString());
		}
	}

}
