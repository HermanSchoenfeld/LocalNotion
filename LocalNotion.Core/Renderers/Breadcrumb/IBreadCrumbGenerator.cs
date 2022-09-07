namespace LocalNotion.Core;
public interface IBreadCrumbGenerator {
	BreadCrumb CalculateBreadcrumb(LocalNotionResource from);
}