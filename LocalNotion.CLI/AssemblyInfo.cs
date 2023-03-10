using Hydrogen.Application;
using System.Reflection;
using System.Runtime.InteropServices;

// In SDK-style projects such as this one, several assembly attributes that were historically
// defined in this file are now automatically added during build and populated with
// values defined in project properties. For details of which attributes are included
// and how to customise this process see: https://aka.ms/assembly-info-properties


// Setting ComVisible to false makes the types in this assembly not visible to COM
// components.  If you need to access a type in this assembly from COM, set the ComVisible
// attribute to true on that type.

[assembly: ComVisible(false)]

// The following GUID is for the ID of the typelib if this project is exposed to COM.

[assembly: Guid("860168e9-abe2-4e0c-ae02-e90a727746a9")]

[assembly: AssemblyTitle("Local Notion")]
[assembly: AssemblyDescription("Command-line interface for Local Notion")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("Sphere 10 Software Pty Ltd")]
[assembly: AssemblyAuthor("Herman Schoenfeld <herman@sphere10.com>")]
[assembly: AssemblyProduct("Local Notion")]
[assembly: AssemblyProductDistribution(ProductDistribution.ReleaseCandidate)]
[assembly: AssemblyProductLink("www.sphere10.com/products/localnotion")]
[assembly: AssemblyProductCode("A49ECEC9-1E83-436c-B432-BBF7B26055E7")]
[assembly: AssemblyProductSecret("5d53d16efdf24ae18d5b8bac48974642ce60bbea978330a8c4154df13f9349dd")]
#if DEBUG
[assembly: AssemblyProductDrmApi("http://localhost:5000/api/drm")]
#else
[assembly: AssemblyProductDrmApi("https://sphere10.com/api/drm")]
#endif
[assembly: AssemblyProductLicense(
	"""
	{
	  "authority": {
	    "name": "Sphere 10 Software General Software Products",
	    "dss": "ecdsa-secp256k1",
	    "publicKey": "A0xL8HSZ7Cl9IYUx92/e34NPhYZHkQEaWcyU2BuJx/2T"
	  },
	  "license": {
	    "Item": {
	      "name": "Local Notion v1 Free",
	      "productKey": "0000-0000-0000-0002",
	      "productCode": "a49ecec9-1e83-436c-b432-bbf7b26055e7",
	      "featureLevel": "free",
	      "expirationPolicy": "disable",
	      "majorVersionApplicable": 1,
	      "limitFeatureA": 1,
	      "limitFeatureB": 100
	    },
	    "Signature": "MEQCIAMJtW2j5vzj6YjDegeyfh5jdrYI/ws84JDehfG+uVF2AiAFNc3U+03vrwPqpIPF9GAZMAzS23/TsONHC+POm4ZDBw=="
	  },
	  "command": {
	    "Item": {
	      "productKey": "0000-0000-0000-0002",
	      "action": "enable"
	    },
	    "Signature": "MEQCIHcZcf+8MvivmZF/TTKDQt1v6dz/pscQNfrJ/OOqyop+AiBkLBj5ugambukYjCFa3EbdZPUuMqXtD2Bwe+My/x4wjQ=="
	  }
	}
	
	"""
)]    // full version product key
[assembly: AssemblyCopyright("Copyright © Sphere 10 Software 2015 - {CurrentYear}")]
[assembly: AssemblyCompanyLink("www.sphere10.com")]
[assembly: AssemblyCompanyNumber("ABN 39 600 596 316")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

// You can specify all the values or you can default the Build and Revision Numbers 
// by using the '*' as shown below:
// [assembly: AssemblyVersion("1.0.*")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]
