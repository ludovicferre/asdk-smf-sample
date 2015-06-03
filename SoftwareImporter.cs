using System;
using System.IO;
using Altiris.ASDK.SMF;

namespace Symantec.CWoC {
	class SoftwareImporter {
		public static int Main() {
		
			// Software Release details
			string SRName = "Orca 3.0b";
			string SRDescription = "Software relesae resource for Orca 3.0";
			
			// Software Package details
			int SourceType = 2;
			string PackageLocation = @"c:\packages\orca";
			string PackageFolder = null;		// For url packages only
			string InstallationFile = "Orca30.msi";
			string Version = "3.0.1234";
			
			// Generic data
			string Company = "Microsoft";
			string Product = "Orca (Product)";
			
			// Import flags
			int MergeAction = 0; // Create = 0; Merge = 1
			
			Guid ReleaseMergeTo = Guid.Empty; // Guid of release to merge to
			
			try {
				SoftwareComponentManagementLib compLib = new SoftwareComponentManagementLib();
				SoftwareComponentDetails details = compLib.ImportSoftwareRelease(
					SRName,
					SRDescription,
					SourceType,
					PackageLocation,
					PackageFolder,
					InstallationFile,
					Version,
					Company,
					Product, 
					MergeAction,
					ReleaseMergeTo);
				
				Console.WriteLine("Created software release with guid {0}", details.Guid.ToString());
				
			} catch (Exception e) {
				Console.WriteLine(e.Message);
				Console.WriteLine(e.StackTrace);
			}

			return 0;
		}
	}
	
	class SoftwareRelease {	
	
		public SoftwareRelease () {
			_name = "";
			_description = "";
		}
		
		public SoftwareRelease(string name, string description) {
			this.name = name;
			this.description = description;
		}
		
		private string _name;
		public string name {
			get {
				return _name;
			}
			set {
				if (value.Length > 255)
					throw new System.ArgumentException("Software Release name cannot be more than 255 character long.", "SoftwareRelease");
				_name = value;
			}
		}
		
		private string _description;
		public string description {
			get {
				return _description;
			}
			set {
				if (value.Length > 255)
					throw new System.ArgumentException("Software Release name cannot be more than 255 character long.", "SoftwareRelease");
				_description = value;
			
			}
		}
		
	}
	
	class SoftwarePackage {
		public enum SourceType {
			unc = 1,
			local = 2,
			url = 4,
			library = 5			
		}
		
		public SoftwarePackage() {
			_sourcetype = (int) SourceType.local;
			_location = "";
			_folder = null;
			_installfile = null;
			_version = "";
		}

		public SoftwarePackage(SourceType type, string location, string folder, string installfile, string version) {
			_sourcetype = (int) type;
			this.location = location;
			if (folder == "" || folder == String.Empty || folder == null)
				this.folder = null;
			else
				this.folder = folder;		
			this.installfile = installfile;
			this.version = version;
		}
		
		private int _sourcetype;
		public int sourcetype {
			get {
				return _sourcetype;
			}
			set {
				_sourcetype = value;
			}
		}

		private string _location;
		public string location {
			get {
				return _location;
			}
			set {
				if (_sourcetype == (int) SourceType.local &&  !Directory.Exists(value))
					throw new System.ArgumentException("Package location must be a valid folder on the server.", "SoftwarePackage");
				_location = value;
			}
		}

		private string _folder;
		public string folder {
			set {
				_folder = value;
			}
			get {
				return _folder;
			}
		}

		private string _installfile;
		public string installfile {
			set {
				if (_sourcetype == (int) SourceType.local &&  !File.Exists(this.location + "\\" + value))
					throw new System.ArgumentException("Install file must exists on the package folder on the server.", "SoftwarePackage");
				_installfile = value;
			}
			get {
				return _installfile;
			}
		}

		private string _version;
		public string version {
			set {
				_version = value;
			}
			get {
				return _version;
			}
		}
		
	}

	class SoftwareProduct {
	
		public SoftwareProduct () {
			_company = "";
			_product = "";
		}
		public SoftwareProduct (string company, string product) {
			this.company = company;
			this.product = product;
		}
		private string _company;
		public string company {
			get {
				return _company;
			}
			set {
				_company = value;
			}
		}
		private string _product;
		public string product {
			get {
				return _product;
			}
			set {
				_product = product;
			}
		}
	}
}
