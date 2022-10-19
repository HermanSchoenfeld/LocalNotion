using Hydrogen;
using Notion.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LocalNotion.Core;

public interface ILocalNotionCMS {

	IEnumerable<LocalNotionPage> GetNotionCMSPages(string root, params string[] categories);

	string[] GetRoots();

	string[] GetSubCategories(string root, params string[] categories);

	bool ContainsSlug(string slug);

	bool TryLookupSlug(string slug, out CMSArtifact[] artifacts);

}

public static class ILocalNotionCMSExtensions {

	public static CMSArtifact[] LookupResourceBySlug(this ILocalNotionCMS cms, string slug)
		=> cms.TryLookupSlug(slug, out var artifacts) ? artifacts : throw new InvalidOperationException($"No CMS artifact(s) addressable by the slug '{slug}' were found");

}