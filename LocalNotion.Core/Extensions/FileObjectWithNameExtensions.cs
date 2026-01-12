// Copyright (c) Herman Schoenfeld 2018 - Present. All rights reserved. (https://sphere10.com/products/localnotion)
// Author: Herman Schoenfeld <herman@sphere10.com>
//
// Distributed under the GPLv3 software license, see the accompanying file LICENSE 
// or visit https://github.com/HermanSchoenfeld/localnotion/blob/master/LICENSE
//
// This notice must not be removed when duplicating this file or its contents, in whole or in part.

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
