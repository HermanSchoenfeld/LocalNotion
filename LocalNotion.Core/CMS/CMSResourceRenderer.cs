//using Sphere10.Framework;
//using Notion.Client;

//namespace LocalNotion.Core;

//public class CMSRenderer  {
//	private readonly ICMSDatabase _icmsDatabase;

//	public CMSResourceRenderer(ICMSDatabase icmsDatabase, ILocalNotionRepository repository, ILogger logger = null)
//		: base(repository, logger) {
//		_icmsDatabase = icmsDatabase;
//	}


//	protected override IRenderer<string> CreateRenderer(LocalNotionResource resource, ItemType renderType, RenderMode renderMode, ILocalNotionRepository repository, ILogger logger) {
//		if (resource is not LocalNotionEditableResource { CMSProperties.CustomSlug: not null } cmsResource) {
//			return base.CreateRenderer(resource, renderType, renderMode, repository, logger);
//		}
//		switch (renderType) {
//			case ItemType.HTML:
//				var themeManager = new HtmlThemeManager(repository.Paths, logger);
//				var urlGenerator = LinkGeneratorFactory.Create(repository);
//				//		var menuPage = (LocalNotionPage)null;
//				//		var footerPage = TryGetFooterPageBySlug(cmsResource.CMSProperties.CustomSlug, out var footer) ? footer : null;
//				var breadcrumbGenerator = new BreadCrumbGenerator(repository, urlGenerator);
//				return new HtmlRenderer(renderMode, repository, themeManager, urlGenerator, breadcrumbGenerator, logger);
//			case ItemType.PDF:
//			case ItemType.File:
//			default:
//				throw new NotImplementedException(renderType.ToString());
//		}
//	}



//}