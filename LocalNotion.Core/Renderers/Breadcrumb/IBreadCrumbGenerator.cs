namespace LocalNotion.Core;
public interface IBreadCrumbGenerator {
	BreadCrumb CalculateBreadcrumb(string resourceID);
}