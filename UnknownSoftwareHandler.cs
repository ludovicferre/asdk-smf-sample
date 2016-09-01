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
		#region public static readonly string HELP_MSG
		public static readonly string HELP_MSG = @"
Software Product creation tool

EXPORT function:

    Phase 1 of any automated product creation requires the output from the
	unknown software component details to a CSV foirmat for administrator
	validation. This is to ensure that the created product have been 
	reviewed and that the products to be created can be reviewed before they
	are committed to the Symantec CMDB.
	
	By default the tool will output data to a UTF-8 CSV file (with headers)
	named output.csv.
	
	The following command lines arguments are available for this export:
	
		/version_none
		/version_major
		/version_majorminor
		/version_exact
		
		/corpname=<company name>
		/corpfilter=<sql like filter>

IMPORT and CREATION:

";
		#endregion

		public static readonly string CSV_HEAD = "\"component_name\"\t\"product_name\"\t\"product_filter\"\t\"product_version\"\t\"component_company\"\t\"component_major_version\"\t\"component_minr\"\t\"component_vers\"\t\"component_guid\"";
		
		public static int Main(string [] Args) {

			CLIConfig conf = GetCLIConfig(Args);
			
			if (conf.display_help) {
				Console.WriteLine(HELP_MSG);
				return 0;
			}
			
			if (conf.export_mode == true) {
				// Craft the export SQL based on the CLI configuration
				string unk_sql = String.Format(sql.undefined_software_query_base, conf.company_filter, conf.company_name);
				
				// Console.WriteLine(unk_sql);
				DataTable unknown_components = DatabaseAPI.GetTable(unk_sql);

				// Open the output file for writing as UTF-8
				StreamWriter writer = new StreamWriter(@"output.csv", false, Encoding.UTF8);

				// Write the CSV file header for clarity
				WriteCSVOutput(CSV_HEAD, writer);
				
				// Output each line with the computed product name and filter strings
				foreach (DataRow r in unknown_components.Rows) {
					
					compute_product_data c = new compute_product_data(r, conf.versioning_mode);
					WriteCSVOutput(
						"\"" + c.component_name + "\"\t" +
						"\"" + c.product_name + "\"\t" +
						"\"" + c.product_filter + "\"\t" +
						"\"" + c.product_version + "\"\t" +
						"\"" + c.component_company + "\"\t" +
						"\"" + c.component_major + "\"\t" +
						"\"" + c.component_minor + "\"\t" +
						"\"" + c.component_version + "\"\t" +
						"\"" + c.component_guid + "\"",
						writer			
					);
				}
			
				writer.Close();
				return 0;
			} else {
				// Import data from CSV and run the product creation based on that.
				string path = "input.csv";
				string [] in_data;
				
				int i = 0;
				DateTime t = DateTime.Now;
				string description = "";
				
				if (File.Exists(path)) {
					using (StreamReader sr = new StreamReader(path)) {
						while (sr.Peek() >= 0) {
							in_data = sr.ReadLine().Split("\t".ToCharArray());
							if (in_data[0] == "\"component_name\"") {
								continue;
							}
							i++;
							description = String.Format("Created by UnknownSoftwareHandler.exe on {0}, entry id in batch={1}", t.ToString(), i.ToString());
							if (conf.dryrun) {
								Console.WriteLine("\n\tProduct\t{0}\n\tDescr.\t{1}\n\tFilter\t{2}\n\tVersion\t{3}\n\tCompany\t{5}\n\tGuid\t{4}\n"
									, in_data[1].Replace("\"", "")	// Product
									, description					// Description
									, in_data[2].Replace("\"", "")	// Filter
									, in_data[3].Replace("\"", "")  // Version
									, in_data[8].Replace("\"", "")	// Guid
									, in_data[4].Replace("\"", "")	// Company
								);
								continue;
							}
							if (conf.testrun && i > 10) {
								break;
							}
							create_software_product(
								  in_data[1].Replace("\"", "")	// Product
								, description					// Description
								, in_data[2].Replace("\"", "")	// Filter
								, in_data[3].Replace("\"", "")  // Version
								, in_data[8].Replace("\"", "")	// Guid
								, in_data[4].Replace("\"", "")	// Company
							);
						}
					}
				return 0;
				} else {
					Console.WriteLine(HELP_MSG);
					return - 1;
				}
			}
		}

		public static void create_software_product(string product, string description, string filter, string version, string guid, string company) {
			SoftwareProductManagementLib managementLib = new SoftwareProductManagementLib();
			SoftwareProductDetails productDetails = managementLib.CreateSoftwareProduct(product, description, company);

			Console.WriteLine("Company guid = {0}.", productDetails.CompanyGuid.ToString());
			Console.WriteLine("Product name = {0}", productDetails.Name);
			Console.WriteLine("Product guid = {0}", productDetails.Guid.ToString());

			string sql_manage_product = String.Format(sql.set_product_managed, productDetails.Guid);
			DatabaseAPI.ExecuteNonQuery(sql_manage_product);

            string sql_mark_component_managed = String.Format("update Inv_Software_Component_State set IsManaged = 1 where _ResourceGuid = '{0}'", guid);
            DatabaseAPI.ExecuteNonQuery(sql_mark_component_managed);


			// Associate Software Component if needed
			string sql_associate_component = String.Format(sql.associate_component, guid, productDetails.Guid.ToString());
			// Console.WriteLine("\n" + sql_associate_component + "\n");
			DatabaseAPI.ExecuteNonQuery(sql_associate_component);
			
			if (version != "") {
				string sql_insert_version = String.Format(sql.ins_product_version, productDetails.Guid, version);
				DatabaseAPI.ExecuteNonQuery(sql_insert_version);
			}
			
			// We only add filters if _both_ the company and product are present in the config data
			if (filter != "" && company != "") {
				
				string sql_filter = String.Format(sql.product_filter_query, filter, company, version);
				string sql_insert_filter = String.Format(sql.ins_product_filter, productDetails.Guid, filter, company, version, sql_filter);
				
				DatabaseAPI.ExecuteNonQuery(sql_insert_filter);
			}
		}

		public class compute_product_data {
			public string product_name;
			public string product_description;
			public string product_filter;
			public string product_version;

			public string component_name;
			public string component_guid;
			public string component_company;
			public string component_major;
			public string component_minor;
			public string component_version;
			
			public compute_product_data(DataRow r, Versioning version_mode) {
				
				product_name = "";
				product_filter = "";
				product_description = "";
				product_version = "";
				
				component_name    = r[0].ToString();
				component_guid    = r[1].ToString();
				component_company = r[2].ToString();
				component_major   = r[3].ToString();
				component_minor   = r[4].ToString();
				component_version = r[5].ToString();
				
				if (version_mode == Versioning.None) {	
					if (component_version.Length > 0) {
						// If a version exist we try to remove it from the component name (-> product name)
						product_name = component_name.Replace(component_version, "");
					} else {
						// If not we use the component name as is
						product_name = component_name.Trim();
					}
					// Create the product filter based on generated product name (because it doesn't have version)
					product_filter = product_name.Replace(" ", "+").Trim("+ ".ToCharArray());
					product_version = "";
				}
				
				if (version_mode == Versioning.Major) {
					if (component_major.Length > 0 && component_version.Length > 0) {
						// If major version and version exist we can replace accordingly
						product_name = component_name.Replace(component_version, component_major);
						product_filter = component_name.Replace(component_version, "").Replace(" ", "+").Trim("+ ".ToCharArray());						
						product_version = component_major;
					} else if (component_major.Length == 0 && component_version.Length > 0){
						// If not we try to remove the version altogether (because we cannot define with major)
						product_name = component_name.Replace(component_version, "");
						product_filter = product_name.Replace(" ", "+").Trim("+ ".ToCharArray());
						// Product version doesn't matter in this case -> it's in the prod name
						product_version = "";
					} else {
						// In all other case we can't do much - just trim!
						product_name = component_name.Trim();
						product_filter = product_name.Replace(" ", "+").Trim("+ ".ToCharArray());
						product_version = "";
					}
				}
				
				if (version_mode == Versioning.Major_Minor) {
					if (component_major.Length > 0 && component_minor.Length > 0 && component_version.Length > 0) {
						// If major and minor version and version exist we can replace accordingly
						product_name = component_name.Replace(component_version, component_major + "." + component_minor);
						product_filter = component_name.Replace(component_version, "").Replace(" ", "+").Trim("+ ".ToCharArray());
						product_version = component_major + "." + component_minor;
					} else if ((component_major.Length == 0 || component_minor.Length == 0) && component_version.Length > 0){
						// If not we try to remove the version altogether (because we cannot define without major)
						product_name = component_name.Replace(component_version, "");
						product_filter = product_name.Replace(" ", "+").Trim("+ ".ToCharArray());
						product_version = "";
					} else {
						// In all other case we can't do much - just trim!
						product_filter = product_name.Replace(" ", "+").Trim("+ ".ToCharArray());
						product_version = "";
					}
				}

				if (version_mode == Versioning.Exact) {
					product_name = component_name;
					if (component_version.Length > 0) {
						product_filter = product_name.Replace(" ", "+").Trim("+ ".ToCharArray());
					}
					product_version = component_version;
				}

			}
		}

		public static void WriteCSVOutput(string line, StreamWriter writer) {
			writer.WriteLine(line);
		}
		
		public static string GetProductNameFilter(string name_filter) {
			char [] delim = {'+'};
			string [] name_filters = name_filter.Split(delim);
			
			StringBuilder b = new StringBuilder();
			foreach (String name in name_filters) {
				b.AppendFormat(sql.product_filter_base, name);
			}
			return b.ToString();
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

		public static CLIConfig GetCLIConfig (string [] Args) {
			CLIConfig conf = new CLIConfig();
			foreach (string arg in Args) {
				string _arg = arg.ToLower();
				if (arg == "/dryrun") {
					conf.dryrun = true;
					conf.testrun = false;
					conf.export_mode = false;
				} else if (arg == "/testrun") {
					conf.testrun = true;
					conf.dryrun = false;
					conf.export_mode = false;
				} else if (arg =="/import") {
					conf.export_mode = false;
					conf.display_help = false;
				} else if (arg =="/export") {
					conf.export_mode = true;
					conf.display_help = false;
				} else if (arg =="/version_exact") {
					conf.versioning_mode = Versioning.Exact;
				} else if (arg == "/version_major") {
					conf.versioning_mode = Versioning.Major;
				} else if (arg == "/version_majorminor") {
					conf.versioning_mode = Versioning.Major_Minor;
				} else if (arg == "/version_none") {
					conf.versioning_mode = Versioning.None;
				} else if (arg.StartsWith("/corpname=")) {
					conf.company_name = arg.Substring("/corpname=".Length);
				} else if (arg.StartsWith("/corpfilter=")) {
					conf.company_filter = arg.Substring("/corpfilter=".Length);
				} else if (arg == "/?" || arg == "/help") {
					conf.display_help = true;
				}
			}
			if (conf.company_filter == "" && conf.company_name == "") {
				conf.company_filter="%";
			}
			
			return conf;
		}
	}
	
	class CLIConfig {
		
		public bool display_help;
		public bool export_mode;
		
		public string company_name;
		public string company_filter;
		
		public bool dryrun;
		public bool testrun;
		
		public bool managed_mode;		
		public Versioning versioning_mode;	
		
		public CLIConfig() {
			// Initialise the config properties
			display_help = true;
			export_mode = true;

			company_name = "";
			company_filter = "";
			
			dryrun= false;
			testrun = false;
		
			managed_mode = true;
			versioning_mode = Versioning.Major;
		}
	}
	
	enum Versioning {
		None,
		Major,
		Major_Minor,
		Exact
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

	class FileData {
		public SoftwareProduct product;
		
		public FileData () {
			product = new SoftwareProduct();
			SoftwareComponentGuid = new Guid();
		}
		
		public Guid SoftwareComponentGuid;
		
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
			}
			catch {
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
			}
			catch (Exception e) {
				Console.WriteLine("Error: {0}\nException message = {1}\nStack trace = {2}.", e.Message, e.InnerException, e.StackTrace);
				throw new Exception("Failed to execute scalar SQL command...");
			}
		}
	}
}



/* OLD TEST CODE - Worth keeping for now

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
