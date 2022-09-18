using Notion.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LocalNotion.Core;

public interface INotionCMS {

	IEnumerable<LocalNotionPage> GetNotionCMSPages(string root, params string[] categories);

	string[] GetRoots();

	string[] GetSubCategories(string root, params string[] categories);

	bool TryLookupResourceBySlug(string slug, out string resourceID);

}

public static class INotionCMSExtensions {

	public static string LookupResourceBySlug(this INotionCMS cms, string slug)
		=> cms.TryLookupResourceBySlug(slug, out var resourceID) ? resourceID : throw new InvalidOperationException($"No resource addressable by the slug '{slug}' was found");

}