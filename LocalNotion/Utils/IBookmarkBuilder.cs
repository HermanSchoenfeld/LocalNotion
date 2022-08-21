namespace LocalNotion;

public interface IBookmarkBuilder {

	public Task<LocalNotionBookmark> Build(string url);

}