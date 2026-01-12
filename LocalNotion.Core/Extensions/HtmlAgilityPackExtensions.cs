// Copyright (c) Herman Schoenfeld 2018 - Present. All rights reserved. (https://sphere10.com/products/localnotion)
// Author: Herman Schoenfeld <herman@sphere10.com>
//
// Distributed under the GPLv3 software license, see the accompanying file LICENSE 
// or visit https://github.com/HermanSchoenfeld/localnotion/blob/master/LICENSE
//
// This notice must not be removed when duplicating this file or its contents, in whole or in part.

using System.Text;
using HtmlAgilityPack;
using Sphere10.Framework;
using Notion.Client;

namespace LocalNotion.Core;

public static class HtmlAgilityPackExtensions {

	public static void RemoveWhitespace(this HtmlNode node) {
		foreach (var childNode in node.ChildNodes) {
			if (childNode.NodeType == HtmlNodeType.Text) {
				if (string.IsNullOrWhiteSpace(childNode.InnerHtml))
					node.RemoveChild(childNode);
			} else RemoveWhitespace(childNode);
		}
	}

	public static void Beautify(this HtmlDocument htmlDocument) {
		foreach (var topNode in htmlDocument.DocumentNode.ChildNodes) {
			switch (topNode.NodeType) {
				case HtmlNodeType.Comment: {
					HtmlCommentNode cn = (HtmlCommentNode)topNode;
					if (string.IsNullOrEmpty(cn.Comment)) continue;
					if (!cn.Comment.EndsWith("\n")) cn.Comment += "\n";
				}
				break;
				case HtmlNodeType.Element: {
					Beautify(topNode, 0);
					topNode.AppendChild(htmlDocument.CreateTextNode("\n"));
					//doc.DocumentNode.InsertAfter(doc.CreateTextNode("\n"), topNode);
				}
				break;
				case HtmlNodeType.Text:
					break;
				default:
					break;
			}
		}
	}

	public static bool Beautify(HtmlNode node, int level) {
		if (!node.HasChildNodes) 
			return false;
		var childNodes = node.ChildNodes.ToArray();
		if (childNodes.All(x => x.NodeType == HtmlNodeType.Text))
			return false;
		var newLineIndent = "\n" + new string('\t', level);
		foreach (var child in childNodes) {
			node.InsertBefore(node.OwnerDocument.CreateTextNode(newLineIndent), child);
			if (child.NodeType != HtmlNodeType.Element || !child.HasChildNodes)
				continue;

			if (Beautify(child, level + 1)) 
				child.AppendChild(child.OwnerDocument.CreateTextNode(newLineIndent));
			
		}
		return true;
	}

	public static string SaveToString(this HtmlDocument document) {
		var stringBuilder = new StringBuilder();
		using var writer = new StringWriter(stringBuilder);
		document.Save(writer);
		return stringBuilder.ToString();
	}

}
