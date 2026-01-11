using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sphere10.Framework;
using Microsoft.Extensions.Logging;
using Notion.Client;
using Tools;

namespace LocalNotion.Core {
	public class PagePropertyItems : Dictionary<string, IPropertyItemObject> {

		public PagePropertyItems(Page page, IEnumerable<KeyValuePair<string, IPropertyItemObject>> propertyItems) : base(propertyItems) {
			Guard.ArgumentNotNull(page, nameof(page));
			Guard.ArgumentNotNull(propertyItems, nameof(propertyItems));
			Page = page;
		}

		protected Page Page { get; }

		public bool ContainsPropertyObject(string propertyName)
		=> Page.Properties.TryGetValue(propertyName, out var propID) && ContainsKey(propID.Id);

		public bool TryGetPropertyObject(string propertyName, out IPropertyItemObject propertyItemObject) {
			propertyItemObject = null;
			return Page.Properties.TryGetValue(propertyName, out var propID) && TryGetValue(propID.Id, out propertyItemObject);
		}

		public string GetTitle()
			=> GetPropertyObjectByID("title").ToPlainText();

		public IPropertyItemObject GetPropertyObject(string propertyName) {
			if (!Page.Properties.TryGetValue(propertyName, out var propId))
				throw new ArgumentException($"Property '{nameof(propertyName)}' not found");
			return GetPropertyObjectByID(propId.Id);
		}

		public IPropertyItemObject GetPropertyObjectByID(string propertyID) {
			Guard.ArgumentNotNull(propertyID, nameof(propertyID));
			if (!TryGetValue(propertyID, out var propObj))
				throw new InvalidOperationException($"Property object '{propertyID}' not found");
			return propObj;
		}

		public string GetPropertyPlainTextValue(string propertyName)
			=> GetPropertyObject(propertyName).ToPlainText();

		public DateTimeOffset? GetPropertyDateValue(string propertyName) {
			var dateProp = GetPropertyObject<DatePropertyItem>(propertyName);
			return dateProp.Date?.Start ?? dateProp.Date?.End;
		}

		public TProperty GetPropertyObject<TProperty>(string propertyName) where TProperty : class, IPropertyItemObject
			=> GetPropertyObject(propertyName) as TProperty ?? throw new InvalidOperationException($"Property '{propertyName}' not a '{typeof(TProperty).Name}' property");

		

	}
}
