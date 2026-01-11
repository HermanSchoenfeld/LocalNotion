using Sphere10.Framework;
using Sphere10.Framework.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LocalNotion.Core.Repository {

	public class ReverseGuidStringFileStore : GuidStringFileStore {

		public ReverseGuidStringFileStore(string baseDirectory, Func<Guid, string> fromGuid, Func<string, Guid> toGuid, string fileExtension = null)
		: base(baseDirectory, g => fromGuid(Reverse(g)), s => Reverse(toGuid(s)), fileExtension) {
		}

		private static Guid Reverse(Guid guid) {
			Span<byte> bytes = stackalloc byte[16];
			guid.TryWriteBytes(bytes);
			bytes.Reverse();
			return new Guid(bytes);
		}
	}

}

