// Copyright (c) Herman Schoenfeld 2018 - Present. All rights reserved. (https://sphere10.com/products/localnotion)
// Author: Herman Schoenfeld <herman@sphere10.com>
//
// Distributed under the GPLv3 software license, see the accompanying file LICENSE 
// or visit https://github.com/HermanSchoenfeld/localnotion/blob/master/LICENSE
//
// This notice must not be removed when duplicating this file or its contents, in whole or in part.

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


	public static void SetUrl(this FileObject fileObject, string value) {
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