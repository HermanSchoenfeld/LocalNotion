using Hydrogen;
using LocalNotion.Core.Repository;
using Notion.Client;

namespace LocalNotion.Core;

public interface IRenderer {
	/// <summary>
	/// Renders a Local Notion resource (page or database).
	/// </summary>
	/// <param name="resourceID">ID of the resource (page or database)</param>
	/// <param name="renderOutput">Type of render to perform</param>
	/// <param name="renderMode">Mode rendering should be performed in</param>
	/// <returns>Filename of rendered file</returns>
	string RenderLocalResource(string resourceID, RenderOutput renderOutput, RenderMode renderMode);

	string RenderLocalPage(string pageID, RenderOutput renderOutput, RenderMode renderMode);

	public string RenderLocalDatabase(string databaseID, RenderOutput renderOutput, RenderMode renderMode);
}

