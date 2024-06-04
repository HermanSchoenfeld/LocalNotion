namespace LocalNotion.Core;

#pragma warning disable CS8618
public class DownloadOptions {

	public bool Render { get; init; } = true;

	public RenderType RenderType { get; init; } = RenderType.HTML;

	public RenderMode RenderMode { get; init; } = RenderMode.ReadOnly;

	public bool FaultTolerant { get; init; } = true;

	public bool ForceRefresh { get; init; } = false;

	public static DownloadOptions Default { get; } = new();

	public static DownloadOptions WithoutRender(DownloadOptions options)
		=> new () {
			Render = false,
			RenderType = options.RenderType,
			RenderMode = options.RenderMode,
			FaultTolerant = options.FaultTolerant,
			ForceRefresh = options.ForceRefresh
		};
}
