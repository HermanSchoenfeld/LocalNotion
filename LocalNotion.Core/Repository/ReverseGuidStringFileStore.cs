// Copyright (c) Herman Schoenfeld 2018 - Present. All rights reserved. (https://sphere10.com/products/localnotion)
// Author: Herman Schoenfeld <herman@sphere10.com>
//
// Distributed under the GPLv3 software license, see the accompanying file LICENSE 
// or visit https://github.com/HermanSchoenfeld/localnotion/blob/master/LICENSE
//
// This notice must not be removed when duplicating this file or its contents, in whole or in part.

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

