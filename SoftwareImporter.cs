using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Data;
using System.Data.SqlClient;

using Altiris.ASDK.SMF;
using Altiris.Common;
using Altiris.Database;
using Altiris.NS.ContextManagement;

namespace Symantec.CWoC {
	class SoftwareImporter {
		public static readonly string ratguid_prodtocomp = "9D67B0C6-BEFF-4FCD-86C1-4A40028FE483";
		public static readonly string prodtocomp = "Software Product Contains Software Component";

		public static int Main() {
			string company_name = "Symantec CWoC";
			string product_name = "Software Importer";
			string product_description = "A software management program to create products from file";

			string software_component_guid = "";

			// Create the Software Product first
			SoftwareProductManagementLib managementLib = new SoftwareProductManagementLib();
			SoftwareProductDetails productDetails = managementLib.CreateSoftwareProduct(product_name, product_description, company_name);

			Console.WriteLine("Company guid = {0}.", productDetails.CompanyGuid.ToString());
			Console.WriteLine("Product name = {0}", productDetails.Name);
			Console.WriteLine("Product guid = {0}", productDetails.Guid.ToString());

			// Mark as managed in all cases (existing entry or not)
			string _sql_manage_product = @"
if exists (select 1 from Inv_Software_Product_State where _ResourceGuid = '{0}')
	update Inv_Software_Product_State set IsManaged = 1 where _resourceguid = '{0}'
else
	insert Inv_Software_Product_State (_ResourceGuid, IsManaged) values ('{0}', 1)
";
			string sql_manage_product = String.Format(_sql_manage_product, productDetails.Guid);
			DatabaseAPI.ExecuteNonQuery(sql_manage_product);

			// Associate Software Component if needed
			string _sql_associate_component = @"
			";
			string sql_associate_component = String.Format(_sql_associate_component, productDetails.Guid,);

			// Add the inventory dataclass entries
			string SoftwareNameQueryString = "";
			string CompanyQueryString = "";
			string VersionQueryString = "";

			string _sql_inventory_query = @"
SELECT	   component.Guid
		  ,component.Name
		  ,component.ResourceTypeGuid
		  ,scVersion.Version
		  ,company.Name As CompanyName 
		  ,company.Guid AS CompanyGuid
		  ,inst.Installs
		  ,swState.IsManaged
  FROM vRM_Software_Component_Item component
  JOIN (
		Select COUNT(DISTINCT _ResourceGuid) as Installs, _SoftwareComponentGuid
		  FROM Inv_InstalledSoftware
		 Where InstallFlag = 1
		 Group By _SoftwareComponentGuid
		) as inst 
    ON inst._SoftwareComponentGuid = component.Guid
  JOIN Inv_Software_Component_State swState
    ON swState._ResourceGuid = component.Guid
  LEFT JOIN (
		SELECT vci.Guid, vci.Name, ra.ParentResourceGuid AS ComponentGuid
		  FROM vRM_Company_Item vci
		  JOIN ResourceAssociation ra
		    ON ra.ChildResourceGuid = vci.Guid 
		   AND ra.ResourceAssociationTypeGuid = '292dbd81-1526-423a-ae6d-f44eb46c5b16'
		) company
    ON company.ComponentGuid = component.Guid
  LEFT JOIN Inv_Software_Component scVersion
    ON scVersion._ResourceGuid = component.Guid
 Where 1 = 1
   AND Lower(component.Name) Like Lower('{0}')
   AND Lower(ISNULL(company.Name, '')) Like Lower('{1}') 
   AND Lower(ISNULL(scVersion.Version, '')) Like Lower('{2}') 
 Order By component.Name Asc
 ";

			string sql_inventory_query = String.Format(_sql_inventory_query, SoftwareNameQueryString, CompanyQueryString, VersionQueryString);

			// Create the data row for table Inv_SoftwareProductFilter

			string _sql_software_product_filter = @"
if exists (select 1 from Inv_SoftwareProductFilter where _ResourceGuid = '{0}')
	update Inv_SoftwareProductFilter set NameFilter = '{1}', CompanyFilter = '{2}', VersionFilter = '{3}', SQLQueryFilter= '{4}' where _ResourceGuid = '{0}'
else
	insert Inv_SoftwareProductFilter (_ResourceGuid, NameFilter, CompanyFilter, VersionFilter, SQLQueryFilter)	values ('{0}', '{1}', '{2}', '{3}', '{4}')
";
			// Ensure we have a clear entry in the ResourceUpdateSummary table too
/* 
			// Software Release details
			SoftwareRelease sr = new SoftwareRelease();
			sr.name = "Orca 3.0d";
			sr.description = "Software release resource for Orca 3.0";
			
			// Software Package details
			SoftwarePackage pkg = new SoftwarePackage();
			
			pkg.sourcetype = (int) SoftwarePackage.SourceType.local;
			pkg.location = @"c:\packages\orca";
			pkg.installfile = "Orca30.msi";
			pkg.version = "3.0.1234";
			
			// Generic data
			SoftwareProduct prod = new SoftwareProduct();
			prod.company = "Microsoft";
			prod.product = "Orca (Product d)";
			
			ImportSoftware(sr, pkg, prod);
*/
			return 0;
		}

		public static void ImportSoftware (SoftwareRelease rel, SoftwarePackage pak, SoftwareProduct prod) {
			int MergeAction = 0; // Create = 0; Merge = 1
			Guid ReleaseMergeTo = Guid.Empty; // Guid of release to merge to

			try {
				SoftwareComponentManagementLib compLib = new SoftwareComponentManagementLib();
				SoftwareComponentDetails details = compLib.ImportSoftwareRelease(
					rel.name,
					rel.description,
					pak.sourcetype,
					pak.location,
					pak.folder,
					pak.installfile,
					pak.version,
					prod.company,
					prod.product,
					MergeAction,
					ReleaseMergeTo);

				Console.WriteLine("Created software release with guid {0}", details.Guid.ToString());
			}
			catch (Exception e) {
				Console.WriteLine(e.Message);
				Console.WriteLine(e.StackTrace);
			}
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
				if (_sourcetype == (int) SourceType.local && !Directory.Exists(value))
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
				if (_sourcetype == (int) SourceType.local && !File.Exists(this.location + "\\" + value))
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
			_description = "";
		}

		public SoftwareProduct (string product, string company) {
			this.company = company;
			this.product = product;
			this.description = "";
		}

		public SoftwareProduct (string product, string description, string company) {
			this.company = company;
			this.product = product;
			this.description = description;
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

		private string _description;
		public string description {
			get {
				return _description;
			}

			set {
				_description = value;
			}
		}
	}

	class DatabaseAPI {
        public static DataTable GetTable(string sqlStatement) {
            DataTable t = new DataTable();
            try {
                using (DatabaseContext context = DatabaseContext.GetContext()) {
                    SqlCommand cmdAllResources = context.CreateCommand() as SqlCommand;
                    cmdAllResources.CommandText = sqlStatement;

                    using (SqlDataReader r = cmdAllResources.ExecuteReader()) {
                        t.Load(r);
                    }
                }
                return t;
            }
            catch {
                throw new Exception("Failed to execute SQL command...");
            }
        }

        public static int ExecuteNonQuery(string sqlStatement) {
            try {
                using (DatabaseContext context = DatabaseContext.GetContext()) {
                    SqlCommand sql_cmd = context.CreateCommand() as SqlCommand;
                    sql_cmd.CommandText = sqlStatement;

                    return sql_cmd.ExecuteNonQuery();
                }
            } catch {
                throw new Exception("Failed to execute non query SQL command...");
            }

        }

        public static int ExecuteScalar(string sqlStatement) {
            try {
                using (DatabaseContext context = DatabaseContext.GetContext()) {
                    SqlCommand cmd = context.CreateCommand() as SqlCommand;

                    cmd.CommandText = sqlStatement;
                    Object result = cmd.ExecuteScalar();

                    return Convert.ToInt32(result);
                }
            } catch (Exception e) {
                Console.WriteLine("Error: {0}\nException message = {1}\nStack trace = {2}.", e.Message, e.InnerException, e.StackTrace);
                throw new Exception("Failed to execute scalar SQL command...");
            }
        }
    }

}
