using System;
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
}