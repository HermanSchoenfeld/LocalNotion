using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Hydrogen;

namespace LocalNotion.Core.Repository;
	public class ObjectNotFoundException : SoftwareException{
		public ObjectNotFoundException(string objectID) : base($"Object '{objectID}' was not foun d") {
		}
	}

