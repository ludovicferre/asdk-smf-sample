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
using Altiris.NS.Security;

namespace Symantec.CWoC {
	class SoftwareImporter {	
		public static readonly string VERSION = @"0.6";
		private static bool DEBUG = false;

        #region public static readonly string HELP_MSG
        public static readonly string HELP_MSG = @"
Unknown Software Handler version " + VERSION + @"

EXPORT function:

    Phase 1 of any automated product creation requires the output from the
    unknown software component details to a CSV format for administrator
    validation. This is to ensure that the created product have been 
    reviewed and that the products to be created can be reviewed before they
    are committed to the Symantec CMDB.
    
    By default the tool will output data to a UTF-8 CSV file (with headers)
    named output.csv.
    
    The following command line is required for the export process to start:
    
        /export
        
            Start the export process. If no other command line arguments are
            provided the export will use the Major version mode (as if using
            /version_major).
    
    The following command lines arguments are available for this export:

        /exportfile=<path to write the output file to>

        /corpname=<company name>
        
            Match this string with the Company Name associated with the 
            software component to be exported. Note that this is an equality
            match.

        /corpfilter=<sql like filter>
        
            You can use SQL wildcard to specify which company(ies) should be
            exported. Wildcard as % to match a string of any length and _ to
            match a single string.
        
        /nullcorp
        
            Export components from which a company does not exist yet.
        
        /nullcorp=<catchall company name>
        
            Export components from which a company does not exist yet and set 
            it to <catchall company name> in the output file.
        
        /componentfilter=<sql like filter>

            You can use SQL wildcard to specify which components should be
            exported. Wildcard as % to match a string of any length and _ to
            match a single string.
        
        /version_none
        
            Remove any version data based on the Inv_Software_Component.Version
            field. In all cases the Software Product and product filter will
            not contain any version.
            
            If the version is not available the product name will use the
            component name as is.
            
            If the version is not the same in the component name as in the 
            Inv_Software_Component.Version table the product name will use the
            component name as is.
            
            If the version is present  in the software component name it will
            be remove before it is used as the product name.

        /version_major
        
            Use the data from Inv_Software_Component.MajorVersion as the 
            product and filter version string.
            
            If the version is not available the product name will use the
            component name as is.
            
            If the version is not the same in the component name as in the 
            Inv_Software_Component.Version table the product name will use the
            component name as is.
            
            If the version is present  in the software component name it will
            be replaced by the MajorVersion before it is used as the product
            name.

        /version_majorminor

            Use the data from Inv_Software_Component.MajorVersion and 
            the Inv_Software_Component.MinorVersion seperated by a dot as the
            product and filter version string.
            
            If the version is not available the product name will use the
            component name as is.
            
            If the version is not the same in the component name as in the 
            Inv_Software_Component.Version table the product name will use the
            component name as is.
            
            If the version is present in the software component name it will
            be replaced by the compound MajorVersion.MinorVersion before it is
            used as the product name.

        /version_exact
        
            Use the data from Inv_Software_Component.Version as the 
            product and filter version string.
            
            The Software Product is created with the Component name as-is.

IMPORT and CREATION:

    Phase 2 of the automated product creation requires an input.csv file to be
    placed in the running folder following the csv schema provided during the 
    export phase.
    
    The following command line is required for the import process to start:
    
        /import

            Start the import and creation process. If no other command line 
            arguments are provided the import run for each line in the input
            file.

    The following command line allow you to control the input and actions taken
    by the tool:

        /importfile=<path to UTF-8 csv>
        
            Specify a custom location for the input file.

        /dryrun
        
            Do not create any software product, simply output the data to the
            console.

        /testrun
        
            Run the import / creation process for up to 10 components / 
            products and exits.
";
        #endregion

		public static readonly string CSV_HEAD = "\"component_name\"\t\"product_name\"\t\"product_filter\"\t\"product_version\"\t\"component_company\"\t\"component_major_version\"\t\"component_minr\"\t\"component_vers\"\t\"component_guid\"";
		
		public static int Main(string [] Args) {

			CLIConfig conf = GetCLIConfig(Args);
			
			if (DEBUG) {
				conf.PrintConfig();
			}
			
			if (conf.display_help) {
				Console.WriteLine(HELP_MSG);
				return 0;
			}
			
			if (!SecurityTrusteeManager.IsUserLocalAdmin()) {
				Console.WriteLine("This tool needs to run with LocalAdmin access to the system...");
				return 0;
			}
			
			if (conf.export_mode == true) {
				// Craft the export SQL based on the CLI configuration
				string nullcorp_sqlstring = "";
				if (conf.nullcorp_allowed) {
					nullcorp_sqlstring = " or comp.name is null";
				}
				string unk_sql = String.Format(sql.undefined_software_query_base, conf.company_filter, conf.company_name, conf.component_filter, nullcorp_sqlstring);
				
				if (DEBUG) {
					Console.WriteLine(unk_sql);
				}

				DataTable unknown_components = DatabaseAPI.GetTable(unk_sql);

				// Open the output file for writing as UTF-8
				if (File.Exists(conf.export_path) == false) {
					conf.export_path = @"output.csv";
				}
				StreamWriter writer = new StreamWriter(conf.export_path, false, Encoding.UTF8);

				// Write the CSV file header for clarity
				WriteCSVOutput(CSV_HEAD, writer);
				
				// Output each line with the computed product name and filter strings
				foreach (DataRow r in unknown_components.Rows) {
					compute_product_data c = new compute_product_data(r, conf.versioning_mode);

					if (c.component_company == "") {
						c.component_company = conf.nullcorp_name;
						if (DEBUG) {
							//Console.WriteLine("Found a component company empty - replacing with {0}.", conf.nullcorp_name);
						}
					}

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
				string path = conf.import_path;
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
				
				component_name    = r[0].ToString().Replace("\r\n", "");
				component_guid    = r[1].ToString();
				component_company = r[2].ToString().Replace("\r\n", "");
				component_major   = r[3].ToString().Replace("\r\n", "");
				component_minor   = r[4].ToString().Replace("\r\n", "");
				component_version = r[5].ToString().Replace("\r\n", "");
				
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

		public static CLIConfig GetCLIConfig (string [] Args) {
			CLIConfig conf = new CLIConfig();
			foreach (string Arg in Args) {
				string arg = Arg.ToLower();
				
				//Export control switches
				if (arg == "/dryrun") {
					conf.dryrun = true;
					conf.testrun = false;
					conf.export_mode = false;
				} else if (arg == "/testrun") {
					conf.testrun = true;
					conf.dryrun = false;
					conf.export_mode = false;
				// Import & export core control switches
				} else if (arg =="/import") {
					conf.export_mode = false;
					conf.display_help = false;
				} else if (arg =="/export") {
					conf.export_mode = true;
					conf.display_help = false;
				} else if (arg.StartsWith("/importfile=")) {
					conf.import_path = arg.Substring("/importfile=".Length);
				} else if (arg.StartsWith("/exportfile=")) {
					conf.export_path = arg.Substring("/exportfile=".Length);
				// Export filter controls
				} else if (arg.StartsWith("/corpname=")) {
					conf.company_name = arg.Substring("/corpname=".Length);
				} else if (arg.StartsWith("/corpfilter=")) {
					conf.company_filter = arg.Substring("/corpfilter=".Length);
				} else if (arg.StartsWith("/componentfilter=")) {
					conf.component_filter = arg.Substring("/componentfilter=".Length);
				} else if (arg=="/nullcorp") {
					conf.nullcorp_allowed = true;
				} else if (arg.StartsWith("/nullcorp=")) {
					// Use the Arg as-is (i.e. not lower case)
					conf.nullcorp_name = Arg.Substring("/nullcorp=".Length);
					conf.nullcorp_allowed = true;
				// Version control switches
				} else if (arg =="/version_exact") {
					conf.versioning_mode = Versioning.Exact;
				} else if (arg == "/version_major") {
					conf.versioning_mode = Versioning.Major;
				} else if (arg == "/version_majorminor") {
					conf.versioning_mode = Versioning.Major_Minor;
				} else if (arg == "/version_none") {
					conf.versioning_mode = Versioning.None;
				// Help message control
				} else if (arg == "/?" || arg == "/help") {
					conf.display_help = true;
					break;
				}
			}
			// Make sure that we include all company name when no filters are defined
			if (conf.company_filter == "" && conf.company_name == "" && conf.nullcorp_allowed != true) {
				conf.company_filter = "%";
			} else if (conf.company_filter =="" && conf.company_name == "" && conf.nullcorp_allowed == true) {
				conf.company_filter = "";
			}

/*
	/nullcorp		
	/nullcorp=<catchall company name>
*/

			
			return conf;
		}
	}
	
	class CLIConfig {
		
		public bool display_help;
		public bool export_mode;
		
		public string import_path;
		public string export_path;
		
		public string company_name;
		public string company_filter;
		public string component_filter;
		
		public bool nullcorp_allowed;
		public string nullcorp_name;
		
		public bool dryrun;
		public bool testrun;
		
		public bool managed_mode;		
		public Versioning versioning_mode;	
		
		public CLIConfig() {
			// Initialise the config properties
			display_help = true;
			export_mode = true;
			
			import_path = "input.csv";
			export_path = "output.csv";

			company_name = "";
			company_filter = "";
			
			nullcorp_allowed = false;
			nullcorp_name = "";
			
			component_filter = "%";
			
			dryrun= false;
			testrun = false;
		
			managed_mode = true;
			versioning_mode = Versioning.Major;
		}

		public void PrintConfig() {
			Console.WriteLine("Display help: {0}", display_help.ToString());
			Console.WriteLine("Export mode: {0}", export_mode.ToString());
			Console.WriteLine("Import path: {0}", import_path.ToString());
			Console.WriteLine("Export path: {0}", export_path.ToString());
			Console.WriteLine("Company name: {0}", company_name.ToString());
			Console.WriteLine("Company filter: {0}", company_filter.ToString());
			Console.WriteLine("Component filter: {0}", component_filter.ToString());
			Console.WriteLine("Nullcorp allowed: {0}", nullcorp_allowed.ToString());
			Console.WriteLine("Null corp name: {0}", nullcorp_name.ToString());
			Console.WriteLine("Dry run: {0}", dryrun.ToString());
			Console.WriteLine("Test run: {0}", testrun.ToString());
			Console.WriteLine("Managed mode: {0}", managed_mode.ToString());
			Console.WriteLine("Versioning mode: {0}", versioning_mode.ToString());
		}
	}
	
	enum Versioning {
		None,
		Major,
		Major_Minor,
		Exact
	}

	class DatabaseAPI {
		public static DataTable GetTable(string sqlStatement) {
			DataTable t = new DataTable();

			try {
				 using (AdminDatabaseContext context = DatabaseContext<AdminDatabaseContext>.GetContext()) {
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
				 using (AdminDatabaseContext context = DatabaseContext<AdminDatabaseContext>.GetContext()) {
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
				 using (AdminDatabaseContext context = DatabaseContext<AdminDatabaseContext>.GetContext()) {
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
