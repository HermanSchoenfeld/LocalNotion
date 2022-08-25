namespace LocalNotion.Core;

public interface IBookmarkBuilder {

	public Task<LocalNotionBookmark> Build(string url);

}