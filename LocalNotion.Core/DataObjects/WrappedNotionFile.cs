// Copyright (c) Herman Schoenfeld 2018 - Present. All rights reserved. (https://sphere10.com/products/localnotion)
// Author: Herman Schoenfeld <herman@sphere10.com>
//
// Distributed under the GPLv3 software license, see the accompanying file LICENSE 
// or visit https://github.com/HermanSchoenfeld/localnotion/blob/master/LICENSE
//
// This notice must not be removed when duplicating this file or its contents, in whole or in part.

using Sphere10.Framework;
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
		Guard.Argument(LocalNotionHelper.IsNotionFileObject(obj), nameof(obj), $"Not a recognized notion file object type: {obj.GetType()}");
	
		switch (obj) {
			case FileObject file:
				Init(file);
				break;
			case FileObjectWithName namedFile:
				Init(namedFile);
				break;
			case CustomEmojiPageIcon customEmoji:
				Init(customEmoji);
				break;
			case FilePageIcon customIcon:
				Init(customIcon);
				break;
			default:
				throw new NotSupportedException(obj.GetType().ToString());
		}
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

	public void Init(CustomEmojiPageIcon customEmoji) {
		_getUrlFunc = () => customEmoji.CustomEmoji.Url;
		_setUrlFunc = v => customEmoji.CustomEmoji.Url = v;
		FileObject = customEmoji;
	}

	public void Init(FilePageIcon customFileIcon) {
		_getUrlFunc = () => customFileIcon.File.Url;
		_setUrlFunc = v => customFileIcon.File.Url = v;
		FileObject = customFileIcon;
	}

	public string GetUrl() => _getUrlFunc();

	public void SetUrl(string value) => _setUrlFunc(value);
	
	public object FileObject { get; set; }

}
