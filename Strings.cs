using System;

namespace Symantec.CWoC {
	public class sql
	{
		#region public static readonly string set_product_managed = @"
		public static readonly string set_product_managed = @"
if exists (select 1 from Inv_Software_Product_State where _ResourceGuid = '{0}')
	update Inv_Software_Product_State set IsManaged = 1 where _resourceguid = '{0}'
else
	insert Inv_Software_Product_State (_ResourceGuid, IsManaged) values ('{0}', 1)
";
		#endregion

		#region public static readonly string inventory_query = @"
		public static readonly string product_filter_query = @"
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
		   AND ra.ResourceAssociationTypeGuid = ''292dbd81-1526-423a-ae6d-f44eb46c5b16''
		) company
    ON company.ComponentGuid = component.Guid
  LEFT JOIN Inv_Software_Component scVersion
    ON scVersion._ResourceGuid = component.Guid
 Where 1 = 1
   {0}
   AND Lower(ISNULL(company.Name, '''')) Like Lower(''%{1}%'') 
   AND Lower(ISNULL(scVersion.Version, '''')) Like Lower(''{2}%'') 
 Order By component.Name Asc
 ";
		#endregion

		#region public static readonly string product_filter_base = @"
		public static readonly string product_filter_base = @"
		   AND Lower(component.Name) Like Lower(''%{0}%'')";
		#endregion

		#region public static readonly string ins_product_filter = @"
		public static readonly string ins_product_filter = @"
if exists (select 1 from Inv_SoftwareProductFilter where _ResourceGuid = '{0}')
begin
	update Inv_SoftwareProductFilter set NameFilter = '{1}', CompanyFilter = '{2}', VersionFilter = '{3}', SQLQueryFilter = '{4}' where _ResourceGuid = '{0}'
	update ResourceUpdateSummary set ModifiedDate = getdate(), [RowCount] = 1, datahash = '', DataLastChangedDate = getdate()
	 where ResourceGuid = '{0}' and InventoryClassGuid = 'F27B37C9-7896-4C26-A3A5-0EC51232C4D2'
end
else
begin
	insert Inv_SoftwareProductFilter (_ResourceGuid, NameFilter, CompanyFilter, VersionFilter, SQLQueryFilter)
	values ('{0}', '{1}', '{2}', '{3}', '{4}')
	insert ResourceUpdateSummary (inventoryclassguid, ResourceGuid, CreatedDate, ModifiedDate, [RowCount], DataLastChangedDate)
	values ('F27B37C9-7896-4C26-A3A5-0EC51232C4D2', '{0}', getdate(), getdate(), 1, GetDate())
end
";	
		#endregion
		
		#region public static readonly string associate_component = @"
		public static readonly string associate_component = @"
if not exists (select 1 from ResourceAssociation where ResourceAssociationTypeGuid = '9D67B0C6-BEFF-4FCD-86C1-4A40028FE483' and ChildResourceGuid = '{0}' and parentresourceguid = '{1}')
begin
	insert ResourceAssociation
		(ResourceAssociationTypeGuid, ChildResourceGuid, ParentResourceGuid, CreatedDate)
	values
		('9D67B0C6-BEFF-4FCD-86C1-4A40028FE483', '{0}', '{1}', GETDATE()) -- Software Product Contains Software Component
end
";
		#endregion

		#region public static readonly string set_software_product_filter = @"
		public static readonly string set_software_product_filter = @"
if exists (select 1 from Inv_SoftwareProductFilter where _ResourceGuid = '{0}')
	update Inv_SoftwareProductFilter set NameFilter = '{1}', CompanyFilter = '{2}', VersionFilter = '{3}', SQLQueryFilter= '{4}' where _ResourceGuid = '{0}'
else
	insert Inv_SoftwareProductFilter (_ResourceGuid, NameFilter, CompanyFilter, VersionFilter, SQLQueryFilter)	values ('{0}', '{1}', '{2}', '{3}', '{4}')
";
		#endregion

		#region public static readonly string ins_product_version = @"
		public static readonly string ins_product_version = @"
		if exists (select 1 from Inv_Software_Product_Version where _ResourceGuid = '{0}')
		begin
			update Inv_Software_Product_Version set version = '{1}' where _ResourceGuid = '{0}'
			update ResourceUpdateSummary set ModifiedDate = getdate(), [RowCount] = 1, datahash = '', DataLastChangedDate = getdate()
			 where ResourceGuid = '{0}' and InventoryClassGuid = '135F104C-3833-4DD4-B8DE-7621C90D456E'
		end
		else
		begin
			insert Inv_Software_Product_Version (_ResourceGuid, Version) values('{0}', '{1}')
			insert ResourceUpdateSummary (inventoryclassguid, ResourceGuid, CreatedDate, ModifiedDate, [RowCount], DataLastChangedDate)
			values ('135F104C-3833-4DD4-B8DE-7621C90D456E', '{0}', getdate(), getdate(), 1, GetDate())
		end
		";
		#endregion
	} 		
		
}
