using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Hydrogen;
using Notion.Client;

namespace LocalNotion.Core {
	public class PageProperties : Dictionary<string, IPropertyItemObject> {

		public PageProperties() {
		}

		public PageProperties(IEnumerable<KeyValuePair<string, IPropertyItemObject>> values) : base(values) {
		}
		
		/// <summary>
		/// This calculates a UUID ID of a Page property so that it can be stored in the Local Notion Objects database. This is needed
		/// because Notion does not use UUID's for Page properties.
		/// </summary>
		/// <remarks>The algorithm used is: Guid.Parse( Blake2B_160( ToGuidBytes(PageID) ++ ToAsciiEncodedBytes(PropertyID) ) ) </remarks>
		/// <param name="pageID">The ID of the Page containing the property</param>
		/// <param name="propertyID">The Notion issued ID of the Page property (a string that is unique only to the Page)</param>
		/// <returns>A globally unique ID for the property.</returns>
		public static string CalculatePagePropertyUUID(string pageID, string propertyID) {
			Guard.ArgumentNotNull(pageID, nameof(pageID));
			Guard.ArgumentNotNull(propertyID, nameof(propertyID));
			Guard.ArgumentParse<Guid>(pageID, nameof(pageID), out var pageGuid);
			var pageIDBytes = pageGuid.ToByteArray();
			var propIDBytes = Encoding.ASCII.GetBytes(propertyID);
			return LocalNotionHelper.ObjectGuidToId(new Guid(Hashers.JoinHash(CHF.Blake2b_160, pageIDBytes, propIDBytes)));
		}
		
	}
}
