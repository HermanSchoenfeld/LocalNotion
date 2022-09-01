namespace LocalNotion.Core;

public class BreadCrumb {

	public BreadCrumbItem[] Trail { get; init; } = Array.Empty<BreadCrumbItem>();

	public static BreadCrumb Empty { get; } = new() { Trail = Array.Empty<BreadCrumbItem>() };
}
