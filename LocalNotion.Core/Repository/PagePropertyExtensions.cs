//using Sphere10.Framework;
//using Notion.Client;

//namespace LocalNotion.Core;

//public static class PagePropertyExtensions {

//	public static void AddPageProperty(this ILocalNotionRepository repository, string pageID, string propertyID, IPropertyItemObject propertyObject) {
//		var objectID = PageProperties.CalculatePagePropertyUUID(pageID, propertyID);
//		repository.AddObject(objectID, object);
//	}

//	public static void DeletePageProperty(this ILocalNotionRepository repository, string pageID, string propertyID) {
//		var objectID = PageProperties.CalculatePagePropertyUUID(pageID, propertyID);
//		repository.RemoveObject(objectID);
//	}

//	public static bool ContainsPageProperty(this ILocalNotionRepository repository, string pageID, string propertyID) {
//		var objectID = PageProperties.CalculatePagePropertyUUID(pageID, propertyID);
//		return repository.ContainsObject(objectID);
//	}

//	public static bool TryGetPageProperty(this ILocalNotionRepository repository, string pageID, string propertyID, out IFuture<IPropertyItemObject> pageProperty) {
//		var objectID = PageProperties.CalculatePagePropertyUUID(pageID, propertyID);
//		if (!repository.TryGetObject(objectID, out var objFuture)) {
//			pageProperty = default;
//			return false;
//		}
//		pageProperty = Tools.Values.Future.Projection(objFuture, obj => (IPropertyItemObject)obj);
//		return true;
//	}

//	public static IPropertyItemObject GetProperty(this ILocalNotionRepository repository, string pageID, string propertyID)
//		=> repository.TryGetPageProperty(pageID, propertyID, out var @object) ? @object.Value : throw new InvalidOperationException($"Page property '{pageID}:{propertyID}' not found");

//}


//using System.Xml.Schema;
//using Sphere10.Framework;
//using Notion.Client;

//namespace LocalNotion.Core;

//public static class PageExtensions {

//	public static bool ContainsPropertyObject(this Page page, PageProperties propertyObjects, string propertyName) 
//		=> page.Properties.TryGetValue(propertyName, out var propID) && propertyObjects.ContainsKey(propID.Id);

//	public static bool TryGetPropertyObject(this Page page, PageProperties propertyObjects, string propertyName, out IPropertyItemObject propertyItemObject) {
//		propertyItemObject = null;
//		return page.Properties.TryGetValue(propertyName, out var propID) && propertyObjects.TryGetValue(propID.Id, out propertyItemObject);
//	}

//	public static string GetTitle(this Page page, PageProperties propertyObjects) 
//		=> page.GetPropertyObjectByID(propertyObjects, "title").ToPlainText();

//	public static IPropertyItemObject GetPropertyObject(this Page page, PageProperties propertyObjects, string propertyName) {
//		Guard.ArgumentNotNull(page, nameof(page));
//		Guard.Ensure(propertyObjects != null, "Properties have not been fetched");
//		if (!page.Properties.TryGetValue(propertyName, out var propId))
//			throw new ArgumentException($"Property '{nameof(propertyName)}' not found");
//		return page.GetPropertyObjectByID(propertyObjects, propId.Id);
//	}

//	public static IPropertyItemObject GetPropertyObjectByID(this Page page, PageProperties propertyObjects, string propertyID) {
//		Guard.ArgumentNotNull(page, nameof(page));
//		Guard.ArgumentNotNull(page, nameof(propertyID));
//		if (!propertyObjects.TryGetValue(propertyID, out var propObj))
//			throw new InvalidOperationException($"Property object '{propertyID}' not found");
//		return propObj;
//	}

//	public static string GetPropertyPlainTextValue(this Page page, PageProperties propertyObjects, string propertyName) 
//		=> page.GetPropertyObject(propertyObjects, propertyName).ToPlainText();

//	public static DateTime? GetPropertyDateValue(this Page page, PageProperties propertyObjects, string propertyName) {
//		var dateProp = page.GetPropertyObject<DatePropertyItem>(propertyObjects, propertyName);
//		return dateProp.Date?.Start ?? dateProp.Date?.End;
//	}

//	public static TProperty GetPropertyObject<TProperty>(this Page page, PageProperties propertyObjects, string propertyName) where TProperty : class, IPropertyItemObject
//		=> page.GetPropertyObject(propertyObjects, propertyName) as TProperty ?? throw new InvalidOperationException($"Property '{propertyName}' not a '{typeof(TProperty).Name}' property");


//	public static void ValidatePropertiesExist(this Page page, params string[] propertyNames)
//		=> page.ValidatePropertiesExist((IEnumerable<string>)propertyNames);

//	public static void ValidatePropertiesExist(this Page page, IEnumerable<string> propertyNames)
//		=> propertyNames.ForEach(x => ValidatePropertyExist(page, x));

//	public static void ValidatePropertyExist(this Page page, string propertyName)
//		=> Guard.Ensure(page.Properties.ContainsKey(propertyName), $"Missing property '{propertyName}'");

//	public static ChildPageBlock AsChildPageBlock(this Page page, IDictionary<string, IPropertyItemObject> propertyObjects)
//		=> new() {
//			Id = page.Id,
//			CreatedTime = page.CreatedTime,
//			LastEditedOn = page.LastEditedOn,
//			HasChildren = true,
//			ChildPage = new ChildPageBlock.Info {
//				Title = page.GetTitle(propertyObjects)
//			}
//		};
//}