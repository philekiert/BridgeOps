using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;
using System.Text.Json;
using System.Text.Json.Serialization;

/*
+--------------------+
|  For Future Edits  |
+--------------------+

!  Column order DOES matter in DefineFields.
!  Composite primary and foreign keys are set in DatabaseCreator.CreateTables().

Any changes to primary key types MUST be made to their dedicated variables. The following methods will need updating:
  - UnloadToTextString() [primary key value settings]
  - DatabaseCreator.CreateTables(), [primary key definitions]

UNSIGNED usage:
  - UNSIGNED is not supported in SQL Server, but should be left in the type definitions to be removed later on in
    DatabaseCreator.CreateTables(). This is to allow for potentially using that feature when supporting other RDBMS's
    (relational database management systems) that do support it in the future, such as MySQL.
  - NB 30/08/2024 - That's pretty much redundant now as I've committed to SQL Server fully. I'll leave the note just
    because the UNSIGNED keyword is still littering the code here and there.

*/


public class FieldDefs
{
    public class Definition
    {
        // The length of the key name makes up the category.Important for outputting database-layout.txt
        public int categoryLength;
        public string columnName;
        public string sqlType;
        public string sqlTypeDefault;
        public bool canOverride;
        public bool primaryKey;
        public bool autoIncrement;
        public List<string>? primaryKeyLinks; // If the primary key is reference by foreign keys, track this.

        public Definition(int categoryLength, string columnName, string typeName,
                          bool canOverride, bool primaryKey, bool autoIncrement)
        {
            this.categoryLength = categoryLength;
            this.columnName = columnName;
            this.sqlType = typeName;
            sqlTypeDefault = typeName;
            this.canOverride = canOverride;
            this.primaryKey = primaryKey;
            this.autoIncrement = autoIncrement;
        }
        public Definition(int categoryLength, string columnName, string type,
                          bool canOverride, bool primaryKey, bool autoIncrement, List<string> primaryKeyLinks)
        {
            this.categoryLength = categoryLength;
            this.columnName = columnName;
            this.sqlType = type;
            sqlTypeDefault = type;
            this.canOverride = canOverride;
            this.primaryKey = primaryKey;
            this.autoIncrement = autoIncrement;
            this.primaryKeyLinks = primaryKeyLinks;
        }
    }

    public string Category(KeyValuePair<string, Definition> keyValuePair)
    {
        return keyValuePair.Key.Substring(0, keyValuePair.Value.categoryLength);
    }

    public Dictionary<string, Definition> defs = new Dictionary<string, Definition>();

    // Primary Key Types (defaults used only for override txt file generation)
    public string typeOrgID = "INT UNSIGNED";
    public string typeOrgRef = "VARCHAR(40)";
    public string typeOrgName = "VARCHAR(200)";
    public string typeContactID = "SMALLINT UNSIGNED";
    public string typeLoginID = "SMALLINT UNSIGNED";
    public string typeAssetID = "INT UNSIGNED";
    public string typeAssetRef = "VARCHAR(20)";
    public string typeResourceID = "SMALLINT UNSIGNED";
    public string typeResourceCapacity = "INT UNSIGNED";
    public string typeConfID = "INT UNSIGNED";
    public string typeRecurrenceID = "INT UNSIGNED";
    public string typeDialNo = "VARCHAR(20)";
    public string typeOrgChangeID = "INT UNSIGNED";
    public string typeAssetChangeID = "INT UNSIGNED";
    public string typeTaskID = "INT UNSIGNED";
    public string typeTaskRef = "VARCHAR(40)";
    public string typeVisitID = "INT UNSIGNED";
    public string typeDocumentID = "INT UNSIGNED";

    public void DefineFields()
    {
        // During database creation, table names are discerned with TableName().

        // Organisation
        defs.Add("Organisation ID", new Definition(12, Glo.Tab.ORGANISATION_ID, typeOrgID, false, true, true));
        defs.Add("Organisation Reference", new Definition(12, Glo.Tab.ORGANISATION_REF, typeOrgRef, false, true, false));
        defs.Add("Organisation Organisation Reference", new Definition(12, Glo.Tab.PARENT_REF, typeOrgRef, false, false, false));
        defs.Add("Organisation Name", new Definition(12, Glo.Tab.ORGANISATION_NAME, typeOrgName, false, true, false));
        defs.Add("Organisation Dial No", new Definition(12, Glo.Tab.DIAL_NO, typeDialNo, false, true, false));
        defs.Add("Organisation Available For Conferencing", new Definition(12, Glo.Tab.ORGANISATION_AVAILABLE, "BIT", false, false, false));
        defs.Add("Organisation Task Reference", new Definition(12, Glo.Tab.TASK_REFERENCE, typeTaskRef, false, false, false));
        defs.Add("Organisation Notes", new Definition(12, "Notes", "VARCHAR(MAX)", false, false, false));

        // Contact
        defs.Add("Contact ID", new Definition(7, Glo.Tab.CONTACT_ID, typeContactID, false, true, true));
        defs.Add("Contact Notes", new Definition(7, "Notes", "VARCHAR(MAX)", false, false, false));

        // User
        defs.Add("Login ID", new Definition(5, Glo.Tab.LOGIN_ID, typeLoginID, false, true, true));
        defs.Add("Login Username", new Definition(5, Glo.Tab.LOGIN_USERNAME, "VARCHAR(30)", true, false, false));
        defs.Add("Login Password", new Definition(5, Glo.Tab.LOGIN_PASSWORD, "BINARY(64)", false, false, false));
        defs.Add("Login Admin", new Definition(5, Glo.Tab.LOGIN_ADMIN, "BOOLEAN", false, false, false));
        defs.Add("Login Create Permissions", new Definition(5, Glo.Tab.LOGIN_CREATE_PERMISSIONS, "TINYINT UNSIGNED", false, false, false));
        defs.Add("Login Edit Permissions", new Definition(5, Glo.Tab.LOGIN_EDIT_PERMISSIONS, "TINYINT UNSIGNED", false, false, false));
        defs.Add("Login Delete Permissions", new Definition(5, Glo.Tab.LOGIN_DELETE_PERMISSIONS, "TINYINT UNSIGNED", false, false, false));
        defs.Add("Login Enabled", new Definition(5, Glo.Tab.LOGIN_ENABLED, "BOOLEAN", false, false, false));
        defs.Add("Login View Settings", new Definition(5, Glo.Tab.LOGIN_VIEW_SETTINGS, "VARCHAR(MAX)", false, false, false));

        // Asset
        defs.Add("Asset ID", new Definition(5, Glo.Tab.ASSET_ID, typeAssetID, false, true, true));
        defs.Add("Asset Reference", new Definition(5, Glo.Tab.ASSET_REF, typeAssetRef, false, true, false));
        defs.Add("Asset Organisation Reference", new Definition(5, Glo.Tab.ORGANISATION_REF, typeOrgRef, false, false, false));
        defs.Add("Asset Notes", new Definition(5, "Notes", "VARCHAR(MAX)", false, false, false));

        // Resource
        defs.Add("Resource ID", new Definition(8, Glo.Tab.RESOURCE_ID, typeResourceID, false, true, true));
        defs.Add("Resource Name", new Definition(8, Glo.Tab.RESOURCE_NAME, "VARCHAR(20)", true, false, false));
        //defs.Add("Resource Available From", new Definition(8, Glo.Tab.RESOURCE_FROM, "DATETIME", false, false, false));
        //defs.Add("Resource Available To", new Definition(8, Glo.Tab.RESOURCE_TO, "DATETIME", false, false, false));
        defs.Add("Resource Connection Capacity", new Definition(8, Glo.Tab.RESOURCE_CAPACITY_CONNECTION, typeResourceCapacity, false, false, false));
        defs.Add("Resource Conference Capacity", new Definition(8, Glo.Tab.RESOURCE_CAPACITY_CONFERENCE, typeResourceCapacity, false, false, false));
        defs.Add("Resource Rows Additional", new Definition(8, Glo.Tab.RESOURCE_ROWS_ADDITIONAL, typeResourceCapacity, false, false, false));

        // Conference
        defs.Add("Conference ID", new Definition(10, Glo.Tab.CONFERENCE_ID, typeConfID, false, true, true));
        defs.Add("Conference Resource ID", new Definition(10, Glo.Tab.RESOURCE_ID, typeResourceID, false, false, false));
        defs.Add("Conference Resource Row", new Definition(10, Glo.Tab.CONFERENCE_RESOURCE_ROW, typeResourceCapacity, false, false, false));
        defs.Add("Conference Recurrence ID", new Definition(10, Glo.Tab.RECURRENCE_ID, typeRecurrenceID, false, false, false));
        defs.Add("Conference Title", new Definition(10, Glo.Tab.CONFERENCE_TITLE, "VARCHAR(50)", true, false, false));
        defs.Add("Conference Start", new Definition(10, Glo.Tab.CONFERENCE_START, "DATETIME", false, false, false));
        defs.Add("Conference End", new Definition(10, Glo.Tab.CONFERENCE_END, "DATETIME", false, false, false));
        defs.Add("Conference Cancelled", new Definition(10, Glo.Tab.CONFERENCE_CANCELLED, "BIT", false, false, false));
        defs.Add("Conference Closure", new Definition(10, Glo.Tab.CONFERENCE_CLOSURE, "VARCHAR(10)", false, false, false));
        defs.Add("Conference Creation Login ID", new Definition(10, Glo.Tab.CONFERENCE_CREATION_LOGIN, typeLoginID, false, false, false));
        defs.Add("Conference Creation Time", new Definition(10, Glo.Tab.CONFERENCE_CREATION_TIME, "DATETIME", false, false, false));
        defs.Add("Conference Edit Login ID", new Definition(10, Glo.Tab.CONFERENCE_EDIT_LOGIN, typeLoginID, false, false, false));
        defs.Add("Conference Edit Time", new Definition(10, Glo.Tab.CONFERENCE_EDIT_TIME, "DATETIME", false, false, false));
        defs.Add("Conference Notes", new Definition(10, "Notes", "VARCHAR(MAX)", false, false, false));

        // Conference Type
        //defs.Add("Conference Type ID", new Definition(15, Glo.Tab.CONFERENCE_TYPE_ID, typeConfTypeID, false, true, true));
        //defs.Add("Conference Type Name", new Definition(15, Glo.Tab.CONFERENCE_TYPE_NAME, "VARCHAR(50)", true, false, false));

        // Conference Recurrence
        defs.Add("Recurrence ID", new Definition(10, Glo.Tab.RECURRENCE_ID, typeRecurrenceID, false, true, true));
        defs.Add("Recurrence Name", new Definition(10, Glo.Tab.RECURRENCE_NAME, "VARCHAR(50)", true, false, false));
        defs.Add("Recurrence Notes", new Definition(10, Glo.Tab.NOTES, "VARCHAR(MAX)", false, false, false));

        defs.Add("Task ID", new Definition(4, Glo.Tab.TASK_ID, typeTaskID, false, true, true));
        defs.Add("Task Reference", new Definition(4, Glo.Tab.TASK_REFERENCE, typeTaskRef, false, true, false));
        defs.Add("Task Opened", new Definition(4, Glo.Tab.TASK_OPENED, "DATE", false, false, false));
        defs.Add("Task Closed", new Definition(4, Glo.Tab.TASK_CLOSED, "DATE", false, false, false));
        defs.Add("Task Notes", new Definition(4, Glo.Tab.NOTES, "DATE", false, false, false));

        defs.Add("Visit ID", new Definition(5, Glo.Tab.VISIT_ID, typeVisitID, false, true, true));
        defs.Add("Visit Task Reference", new Definition(5, Glo.Tab.TASK_REFERENCE, typeTaskRef, false, false, false));
        defs.Add("Visit Date", new Definition(5, Glo.Tab.VISIT_DATE, "DATE", false, false, false));
        defs.Add("Visit Type", new Definition(5, Glo.Tab.VISIT_TYPE, "VARCHAR(50)", true, false, false));

        defs.Add("Document ID", new Definition(8, Glo.Tab.DOCUMENT_ID, typeDocumentID, false, true, true));
        defs.Add("Document Task Reference", new Definition(8, Glo.Tab.TASK_REFERENCE, typeTaskRef, false, false, false));
        defs.Add("Document Date", new Definition(8, Glo.Tab.DOCUMENT_DATE, "DATE", false, false, false));
        defs.Add("Document Type", new Definition(8, Glo.Tab.DOCUMENT_TYPE, "VARCHAR(50)", true, false, false));


        // ----- SUPPLEMENTARY TABLES ----- //

        // Dial No (for faster site lookups) // Scrapped in favour of indexing the Dial No column.
        //defs.Add("Dial No", new Definition(7, Glo.Tab.DIAL_NO, typeDialNo, false, true, false));
        //defs.Add("Dial No Organisation Reference", new Definition(7, Glo.Tab.ORGANISATION_REF, typeOrgID, false, false, false));

        // Site Connections
        defs.Add("Connection ID", new Definition(10, Glo.Tab.CONNECTION_ID, "INT", false, true, true));
        defs.Add("Connection Conference ID", new Definition(10, Glo.Tab.CONFERENCE_ID, typeConfID, false, false, false));
        defs.Add("Connection Organisation Dial No", new Definition(10, Glo.Tab.DIAL_NO, typeDialNo, false, false, false));
        defs.Add("Connection Is Managed", new Definition(10, Glo.Tab.CONNECTION_IS_MANAGED, "BOOLEAN", false, false, false));
        defs.Add("Connection Connection Time", new Definition(10, Glo.Tab.CONNECTION_TIME_FROM, "DATETIME", false, false, false));
        defs.Add("Connection Disconnection Time", new Definition(10, Glo.Tab.CONNECTION_TIME_TO, "DATETIME", false, false, false));
        defs.Add("Connection Row", new Definition(10, Glo.Tab.CONNECTION_ROW, "TINYINT", false, false, false));
        defs.Add("Connection Is Test", new Definition(10, Glo.Tab.CONNECTION_IS_TEST, "BOOLEAN", false, false, false));

        // Conferences by Day
        //defs.Add("Conferences by Day Date", new Definition(18, "Date", "DATE", false, true, false));
        //defs.Add("Conferences by Day Conference ID", new Definition(18, "Conference_ID", typeConfID, false, true, false));

        // Organisation Change Snapshot
        defs.Add("Organisation Change Organisation ID", new Definition(19, Glo.Tab.ORGANISATION_ID, typeOrgID, false, true, false));
        defs.Add("Organisation Change ID", new Definition(19, Glo.Tab.CHANGE_ID, typeOrgChangeID, false, true, true));
        defs.Add("Organisation Change Time", new Definition(19, Glo.Tab.CHANGE_TIME, "DATETIME", false, false, false));
        defs.Add("Organisation Change Login ID", new Definition(19, Glo.Tab.LOGIN_ID, typeLoginID, false, false, false));
        defs.Add("Organisation Change Reason", new Definition(19, Glo.Tab.CHANGE_REASON, "VARCHAR(200)", true, false, false));
        List<string> additionsKeys = new List<string>(); // Can't add to Dictionary while iterating through it!
        List<Definition> additionsValues = new List<Definition>();
        foreach (var d in defs)
            if (Category(d) == "Organisation")
            {
                if (d.Value.columnName != Glo.Tab.ORGANISATION_ID) // Already stated above, makes up part of the composite key.
                {
                    // All of these should have canOverride set to false.
                    additionsKeys.Add(d.Key + Glo.Tab.CHANGE_SUFFIX);
                    additionsValues.Add(new Definition(19, d.Value.columnName + Glo.Tab.CHANGE_SUFFIX, "BOOLEAN", false, false, false));
                    additionsKeys.Add(d.Key);
                    additionsValues.Add(new Definition(19, d.Value.columnName, d.Value.sqlType, false, false, false));
                }
            }
        for (int d = 0; d < additionsKeys.Count; ++d)
            defs.Add("Organisation Change / " + additionsKeys[d], additionsValues[d]);

        // Asset Change Snapshot
        defs.Add("Asset Change Asset ID", new Definition(12, Glo.Tab.ASSET_ID, typeAssetID, false, true, false));
        defs.Add("Asset Change ID", new Definition(12, Glo.Tab.CHANGE_ID, typeAssetChangeID, false, true, true));
        defs.Add("Asset Change Time", new Definition(12, "Time", "DATETIME", false, false, false));
        defs.Add("Asset Change Login ID", new Definition(12, Glo.Tab.LOGIN_ID, typeLoginID, false, false, false));
        defs.Add("Asset Change Reason", new Definition(12, Glo.Tab.CHANGE_REASON, "VARCHAR(200)", true, false, false));
        additionsKeys = new List<string>(); // Can't add to Dictionary while iterating through it!
        additionsValues = new List<Definition>();
        foreach (var d in defs)
            if (Category(d) == "Asset")
            {
                if (d.Value.columnName != Glo.Tab.ASSET_ID) // Already stated above, makes up part of the composite key.
                {
                    // All of these should have canOverride set to false. Bool must come first due to the way Agent.ClientSelectHistory() works.
                    additionsKeys.Add(d.Key + Glo.Tab.CHANGE_SUFFIX);
                    additionsValues.Add(new Definition(12, d.Value.columnName + Glo.Tab.CHANGE_SUFFIX, "BOOLEAN", false, false, false));
                    additionsKeys.Add(d.Key);
                    additionsValues.Add(new Definition(12, d.Value.columnName, d.Value.sqlType, false, false, false));
                }
            }
        for (int d = 0; d < additionsKeys.Count; ++d)
            defs.Add("Asset Change / " + additionsKeys[d], additionsValues[d]);


        // ----- MANY-TO-MANY JUNCTION TABLES ----- //

        // Organisation Contacts
        defs.Add("Organisation Contacts Organisation Reference", new Definition(21, Glo.Tab.ORGANISATION_REF, typeOrgRef, false, true, false));
        defs.Add("Organisation Contacts Contact ID", new Definition(21, Glo.Tab.CONTACT_ID, typeContactID, false, true, false));

        // Organisation Engineers
        //defs.Add("Organisation Engineers Organisation ID", new Definition(22, "Organisation_ID", typeOrgID, false, true, false));
        //defs.Add("Organisation Engineers Contact ID", new Definition(22, "Contact_ID", typeContactID, false, true, false));

        // Organisation Contacts
        //defs.Add("Organisation Change Contacts Organisation ID", new Definition(28, "Organisation_ID", typeOrgID, false, true, false));
        //defs.Add("Organisation Change Contacts Change ID", new Definition(28, "Change_ID", typeOrgChangeID, false, true, false));
        //defs.Add("Organisation Change Contacts Contact ID", new Definition(28, "Contact_ID", typeContactID, false, true, false));

        // Organisation Engineers
        //defs.Add("Organisation Change Engineers Organisation ID", new Definition(29, "Organisation_ID", typeOrgID, false, true, false));
        //defs.Add("Organisation Change Engineers Change ID", new Definition(29, "Change_ID", typeOrgChangeID, false, true, false));
        //defs.Add("Organisation Change Engineers Contact ID", new Definition(29, "Contact_ID", typeContactID, false, true, false));

        // Conference Resources
        //defs.Add("Conference Resource Conference", new Definition(19, Glo.Tab.CONFERENCE_ID, typeConfID, false, true, false));
        //defs.Add("Conference Resource Resource", new Definition(19, Glo.Tab.CONFERENCE_ID, typeResourceID, false, true, false));
    }
    public string TableName(string defKey)
    {
        if (defs.ContainsKey(defKey))
            return defKey.Substring(0, defs[defKey].categoryLength).Replace(" ", "");
        else
            return "Invalid argument for FieldDefs.TableName()";
    }

    public string UnloadToTxtOverrideString()
    {
        // First, record longest lengths for each category to enable alignment of columns in text.
        List<int> longestKeys = GetLongestNamesByCategory(false, true);


        string str = $"# The intended purpose of this file is only to allow alterations to the sizes of data types. Invalid{Glo.NL}" +
                     $"# values are ignored.{Glo.DNL}" +
                     $"# Fields cannot be renamed.{Glo.NL}" +
                     $"# All values must be placed at the end of a line after a double-space, with no white space afterwards.{Glo.NL}" +
                     $"# VARCHAR length values must be between 1 and 8000.{Glo.NL}" +
                     $"# Type values are limited to TINYINT, SMALLINT and INT.{Glo.DNL}" +
                     $"# Primary key value MUST be placed above all others. Any placed out of order will be ignored.{Glo.DNL}";

        // The primary keys need setting before anything else.

        str += $"{Glo.NL}# Primary/Foreign Keys" +
               $"{Glo.NL}# ------------" +
               $"{Glo.NL}" +
               $"{Glo.NL}Organisation ID:                      Type = " + typeOrgID + // Table contains unique keys, making later type alterations impossible.
               $"{Glo.NL}Organisation Reference:               Max Length = " + ExtractVARCHARLength(typeOrgRef) +
               $"{Glo.NL}Organisation Dial No:                 Max Length = " + ExtractVARCHARLength(typeDialNo) +
               $"{Glo.NL}Organisation Name:                    Max Length = " + ExtractVARCHARLength(typeOrgName) +
               $"{Glo.NL}Asset ID:                             Type = " + typeAssetID + // Table contains unique keys, making later type alterations impossible.
               $"{Glo.NL}Asset Reference:                      Max Length = " + ExtractVARCHARLength(typeAssetRef) +
               $"{Glo.NL}Contact ID:                           Type = " + typeContactID +
               $"{Glo.NL}Login ID:                             Type = " + typeLoginID +
               $"{Glo.NL}Resource ID:                          Type = " + typeResourceID +
               $"{Glo.NL}Conference ID:                        Type = " + typeConfID +
               $"{Glo.NL}Task ID:                              Type = " + typeTaskID + // Table contains unique keys, making later type alterations impossible.
               $"{Glo.NL}Task Reference:                       Max Length = " + ExtractVARCHARLength(typeTaskRef) +
               $"{Glo.NL}Visit ID:                             Type = " + typeVisitID +
               $"{Glo.NL}Document ID:                          Type = " + typeDocumentID +
               $"{Glo.NL}Organisation Change ID:               Type = " + typeOrgChangeID +
               $"{Glo.NL}Asset Change ID:                      Type = " + typeAssetChangeID +
               $"{Glo.NL}" +
               $"{Glo.NL}" +
               $"{Glo.NL}# Everything Else" +
               $"{Glo.NL}# ---------------" +
               $"{Glo.NL}";

        // Then iterate through all definitions and format lines for relatively easy reading by the user.
        string lastCategory = "";
        int categoryIndex = -1;
        foreach (var d in defs)
        {
            if (d.Value.canOverride)
            {
                string category = d.Key.Substring(0, d.Value.categoryLength);
                if (category != lastCategory)
                {
                    str += Glo.NL;
                    ++categoryIndex;
                    lastCategory = category;
                }

                str += d.Key + ":" + WhiteSpace(longestKeys[categoryIndex] - d.Key.Length) + "  ";

                string formattedVal = d.Value.sqlTypeDefault;

                // If it's a LIST, extract the type first.
                if (d.Value.sqlTypeDefault.StartsWith("LIST"))
                    formattedVal = ExtractLISTType(formattedVal);

                if (formattedVal.StartsWith("VARC"))
                    str += "Max Length = " + ExtractVARCHARLength(formattedVal);
                else if (formattedVal.StartsWith("CHAR"))
                    str += "Fixed Length = " + ExtractCHARLength(formattedVal);
                else if (TestIntegralType(formattedVal))// Integral types
                    str += "Type = " + formattedVal;
                else
                    str += "Cannot modify";

                str += Glo.NL;
            }
        }

        return str;
    }
    public void UnloadTxtStringToOverridesFile(string text)
    {
        try
        {
            // Automatically generates the file if one isn't present.
            if (!Directory.Exists(Glo.PathConfigFiles))
                Directory.CreateDirectory(Glo.PathConfigFiles);
            File.WriteAllText(Path.Combine(Glo.PathConfigFiles, Glo.CONFIG_TYPE_OVERRIDES), text);
            Writer.Affirmative("Overrides file written successfully.");
        }
        catch (System.Exception e)
        {
            Writer.Message("Could not create file. See error:", ConsoleColor.Red);
            Writer.Message(e.Message, ConsoleColor.Red);
        }
    }
    public void PrintToConsole()
    {
        // First, record longest lengths for each category to enable alignment of columns in text.
        List<int> longestKeys = GetLongestNamesByCategory(true, false);

        // Iterate through every definition and print to console in a readable format.
        string lastCategory = "";
        int categoryIndex = -1;
        foreach (var d in defs)
        {
            string category = d.Key.Remove(d.Value.categoryLength);
            if (lastCategory != category)
            {
                ++categoryIndex;
                lastCategory = category;
                Writer.Header(category + " Database Table");
            }
            Console.WriteLine(d.Value.columnName +
                              WhiteSpace(longestKeys[categoryIndex] - d.Value.columnName.Length) + "  " +
                              d.Value.sqlType.Replace("UNSIGNED", ""));
        }
    }

    public void CreateNewColumnAdditionsTemplate()
    {
        string template = "# Use this file to add columns to the database. Use the following format (without the '# '):" +
                        $"{Glo.NL}# [table] TableName [column] ColumnName [type] TYPE [allowed] list;of;values;semicolon;separated" +
                        $"{Glo.NL}# Example:" +
                        $"{Glo.NL}# [table] Contact [column] Favourite Food [type] VARCHAR(10) [allowed] Sandwiches;Pizza;Seicon" +
                        $"{Glo.DNL}# Notes:" +
                        $"{Glo.NL}#  - Spaces in the column name will be replaced with underscores in the database, and conversely, underscores" +
                        $"{Glo.NL}#    will be replaced with spaces when displayed in the client software. For this reason, underscores cannot" +
                        $"{Glo.NL}#    be displayed." +
                        $"{Glo.NL}#  - Do not end any column names with '_Register', as this could interfere with some operations, including " +
                        $"{Glo.NL}#    database creation." +
                        $"{Glo.NL}#  - Only the [allowed] section is optional and can be omitted." +
                        $"{Glo.NL}#  - [type] is restricted to the following values:" +
                        $"{Glo.NL}#      Integral: TINYINT, SMALLINT, INT (these cannot accept [allowed] values)" +
                        $"{Glo.NL}#      Floating Point: FLOAT (cannot accept [allowed] values)" +
                        $"{Glo.NL}#      Date/Time: DATE, DATETIME, TIME (cannot accept [allowed] values)" +
                        $"{Glo.NL}#      Text: VARCHAR(0 - 8000), VARCHAR(MAX) (either can accept [allowed] values)" +
                        $"{Glo.NL}#      Boolean: BOOLEAN (cannot accept [allowed] values)" +
                        $"{Glo.NL}#  - Only the Organisation, Contact, Asset and Conference tables can be added to." +
                        $"{Glo.DNL}# ! Some knowledge of the listed SQL types is recommended before using this file." +
                        $"{Glo.NL}# ! One slight quirk... [allowed] values cannot contain square brackets (\"[\" or \"]\") as this would " +
                        $"{Glo.NL}#   interfere with some vital operations." +
                        $"{Glo.DNL}";
        try
        {
            File.WriteAllText(Path.Combine(Glo.PathConfigFiles, Glo.CONFIG_COLUMN_ADDITIONS), template);
            Writer.Affirmative("Column additions file written successfully.");
        }
        catch (Exception e)
        {
            Writer.Message("Encountered an error when trying to write file. See error:", ConsoleColor.Red);
            Writer.Message(e.Message, ConsoleColor.Red);
        }
    }

    /* considerCanOverride needs to be switched off if you are iterating through everything, and vice versa. This is
     * because the category indices will be mismatched due to some categories having no overridable values. This will
     * lead to out-of-bounds exceptions. */
    List<int> GetLongestNamesByCategory(bool friendlyNames, bool considerCanOverride)
    {
        string lastCategory = "";
        List<int> longestNames = new List<int>();
        foreach (var d in defs)
        {
            if (d.Value.canOverride || !considerCanOverride)
            {
                string category = d.Key.Substring(0, d.Value.categoryLength);
                int nameLength = friendlyNames ? d.Value.columnName.Length : d.Key.Length;
                if (category != lastCategory)
                {
                    lastCategory = category;
                    longestNames.Add(nameLength);
                }
                else if (nameLength > longestNames[longestNames.Count - 1])
                    longestNames[longestNames.Count - 1] = nameLength;
            }
        }

        return longestNames;
    }

    string WhiteSpace(int length)
    {
        string ws = "";
        for (int n = 0; n < length; ++n)
            ws += " ";
        return ws;
    }

    public string ExtractVARCHARLength(string varchar)
    {
        if (varchar.StartsWith("VARCHAR(") && varchar.EndsWith(")"))
            return varchar.Substring(8, varchar.Length - 9);
        else
            return "Invalid length";
    }
    public string ExtractCHARLength(string ch)
    {
        if (ch.StartsWith("CHAR(") && ch.EndsWith(")"))
            return ch.Substring(5, ch.Length - 6);
        else
            return "Invalid length";
    }
    public string ExtractLISTType(string list)
    {
        if (list.StartsWith("LIST(") && list.EndsWith(")"))
            return list.Substring(5, list.Length - 6);
        else
            return "Invalid list type";
    }

    public bool TestIntegralType(string s)
    {
        return (s == "TINYINT" || s == "SMALLINT" || s == "INT" ||
                s == "TINYINT UNSIGNED" ||
                s == "SMALLINT UNSIGNED" ||
                s == "INT UNSIGNED");
    }
}
