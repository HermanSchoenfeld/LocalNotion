using Hydrogen;
using Notion.Client;

namespace LocalNotion.Core.DataObjects;


/// <summary>
/// Used to wrap a <see cref="FileObject"/> or <see cref="FileObjectWithName"/> who share no common ancestry.
/// </summary>
public class WrappedNotionFile {

	private Func<string> _getUrlFunc;
	private Action<string> _setUrlFunc;

	public WrappedNotionFile(object obj) {
		Guard.ArgumentNotNull(obj, nameof(obj));
		switch (obj) {
			case ExternalFile externalFile:
				Init(externalFile);
				break;
			case UploadedFile uploadedFile:
				Init(uploadedFile);
				break;
			default:
				throw new NotSupportedException(obj.GetType().ToString());
		}
	}

	public WrappedNotionFile(FileObject fileObject) {
		_getUrlFunc = fileObject.GetUrl;
		_setUrlFunc = fileObject.SetUrl;
		FileObject = fileObject;
	}

	public WrappedNotionFile(FileObjectWithName fileObjectWithName) {
		_getUrlFunc = fileObjectWithName.GetUrl;
		_setUrlFunc = fileObjectWithName.SetUrl;
		FileObject = fileObjectWithName;
	}

	public void Init(FileObject fileObject) {
		_getUrlFunc = fileObject.GetUrl;
		_setUrlFunc = fileObject.SetUrl;
		FileObject = fileObject;
	}

	public void Init(FileObjectWithName fileObjectWithName) {
		_getUrlFunc = fileObjectWithName.GetUrl;
		_setUrlFunc = fileObjectWithName.SetUrl;
		FileObject = fileObjectWithName;
	}

	public string GetUrl() => _getUrlFunc();

	public void SetUrl(string value) => _setUrlFunc(value);
	
	public object FileObject { get; set; }

}
