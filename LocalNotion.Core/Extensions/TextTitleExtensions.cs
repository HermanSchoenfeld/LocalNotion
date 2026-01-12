// Copyright (c) Herman Schoenfeld 2018 - Present. All rights reserved. (https://sphere10.com/products/localnotion)
// Author: Herman Schoenfeld <herman@sphere10.com>
//
// Distributed under the GPLv3 software license, see the accompanying file LICENSE 
// or visit https://github.com/HermanSchoenfeld/localnotion/blob/master/LICENSE
//
// This notice must not be removed when duplicating this file or its contents, in whole or in part.

using System.Xml.Schema;
using Sphere10.Framework;
using Notion.Client;

namespace LocalNotion.Core;

public static class DatabaseExtensions {

	public static string GetTextTitle(this DataSource datasource) 
		=> datasource.Title.ToPlainText();

	
	public static string GetTextTitle(this Database database) 
		=> database.Title.ToPlainText();

	public static string GetTextTitle(this Block block)
		=> $"[Block Type=\"{block.Type}\" ID=\"{block.Id}\" ]";

	public static string GetTextTitle(this Comment comment)
		=> $"[Comment ID=\"{comment.Id}\" Text=\"{comment.RichText.ToPlainText().TrimToLength(10, "...")}\"]";

	public static string GetTextTitle(this FileUpload fileUpload)
		=> $"[FileUpload ID=\"{fileUpload.Id}\" Name=\"{fileUpload.FileName}\"]";
}