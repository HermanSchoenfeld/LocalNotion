using Hydrogen;

namespace LocalNotion.Core;
	public class ObjectNotFoundException : SoftwareException{
		public ObjectNotFoundException(string objectID) : base($"Object '{objectID}' was not found") {
		}
	}

