namespace LocalNotion.Core;

public interface IDatabaseRenderer<TOutput> {
	TOutput Render();
}
