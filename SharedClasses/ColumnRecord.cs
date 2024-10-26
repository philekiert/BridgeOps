using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SendReceiveClasses;


public static class ColumnRecord
{
    public static object lockColumnRecord = new();
    // Add to this as development continues.
    public struct Column
    {
        // column name is stored as the key in Dictionary
        public string type;
        public long restriction; // Only for character strings
        public string[] allowed; // Allowed values, only ever present in user-added columns
        public string friendlyName;
        public bool softDuplicateCheck;
        public bool unique;

        public Column(string type, long restriction, string[] allowed, string friendlyName, bool unique)
        {
            this.type = type;
            this.restriction = restriction;
            this.allowed = allowed;
            this.friendlyName = friendlyName;
            softDuplicateCheck = false;
            this.unique = unique;
        }
    }

    public static int columnRecordID;

    public static string GetPrintName(string colName, OrderedDictionary dictionary)
    {
        try
        {
            if (((Column)dictionary[colName]!).friendlyName != "")
                return ((Column)dictionary[colName]!).friendlyName.Replace('_', ' ');
            else
                return colName.Replace('_', ' ');
        }
        catch
        {
            return "";
        }
    }
    public static string GetPrintName(KeyValuePair<string, Column> col)
    {
        if (col.Value.friendlyName != "")
            return col.Value.friendlyName.Replace('_', ' ');
        else
            return col.Key.Replace('_', ' ');
    }
    public static string GetPrintName(DictionaryEntry de)
    {
        Column col = (Column)de.Value!;
        if (col.friendlyName != "")
            return col.friendlyName.Replace('_', ' ');
        else
            return ((string)de.Key).Replace('_', ' ');
    }
    public static string GetPrintName(string key, Column col)
    {
        if (col.friendlyName != null && col.friendlyName != "")
            return col.friendlyName.Replace('_', ' ');
        else
            return key.Replace('_', ' ');
    }
    public static string ReversePrintName(string name, OrderedDictionary dictionary)
    {
        name = name.Replace(' ', '_');
        if (dictionary.Contains(name))
            return name;
        foreach (DictionaryEntry de in dictionary)
            if (((Column)de.Value!).friendlyName.Replace(' ', '_') == name)
                return (string)de.Key;
        return "";
    }

    public static OrderedDictionary organisation = new();
    public static Dictionary<string, string> organisationFriendlyNameReversal = new();
    public static OrderedDictionary organisationChange = new();
    public static Dictionary<string, string> organisationChangeFriendlyNameReversal = new();
    public static OrderedDictionary asset = new();
    public static Dictionary<string, string> assetFriendlyNameReversal = new();
    public static OrderedDictionary assetChange = new();
    public static Dictionary<string, string> assetChangeFriendlyNameReversal = new();
    public static OrderedDictionary contact = new();
    public static Dictionary<string, string> contactFriendlyNameReversal = new();
    public static OrderedDictionary conferenceType = new();
    public static Dictionary<string, string> conferenceTypeFriendlyNameReversal = new();
    public static OrderedDictionary conference = new();
    public static Dictionary<string, string> conferenceFriendlyNameReversal = new();
    public static OrderedDictionary conferenceRecurrence = new();
    public static Dictionary<string, string> conferenceRecurrenceFriendlyNameReversal = new();
    public static OrderedDictionary connection = new();
    public static Dictionary<string, string> connectionFriendlyNameReversal = new();
    public static OrderedDictionary resource = new();
    public static Dictionary<string, string> resourceFriendlyNameReversal = new();
    public static OrderedDictionary login = new();
    public static Dictionary<string, string> loginFriendlyNameReversal = new();

    public static List<int> organisationOrder = new();
    public static List<int> assetOrder = new();
    public static List<int> contactOrder = new();
    public static List<int> conferenceOrder = new();

    public static OrderedDictionary orderedOrganisation = new();
    public static OrderedDictionary orderedAsset = new();
    public static OrderedDictionary orderedContact = new();
    public static OrderedDictionary orderedConference = new();

    public static List<ColumnOrdering.Header> organisationHeaders = new();
    public static List<ColumnOrdering.Header> assetHeaders = new();
    public static List<ColumnOrdering.Header> contactHeaders = new();
    public static List<ColumnOrdering.Header> conferenceHeaders = new();

    public static bool OrderTable(OrderedDictionary dictionary,
                                  List<int> order,
                                  OrderedDictionary orderedDictionary)
    {
        try
        {
            DictionaryEntry[] orderedArray = new DictionaryEntry[order.Count];

            int i = 0;
            foreach (DictionaryEntry de in dictionary)
            {
                for (int n = 0; n < orderedArray.Length; ++n)
                    if (order[n] == i)
                    {
                        orderedArray[n] = de;
                        break;
                    }
                ++i;
            }

            foreach (DictionaryEntry de in orderedArray)
                orderedDictionary.Add(de.Key, de.Value);

            return true;
        }
        catch
        {
            return false;
        }
    }

    // Risky, but if the column record is ever out of sync we want the program to crash out anyway.
    public static Column GetColumn(OrderedDictionary table, string column)
    {
        return (Column)GetColumnNullable(table, column)!;
    }
    public static Column? GetColumnNullable(OrderedDictionary table, string column)
    {
        try
        {
            return (Column?)table[column];
        }
        catch
        {
            return null;
        }
    }
    public static Column? GetColumnNullable(string table, string column)
    {
        try
        {
            switch (table)
            {
                case "Organisation":
                    return (Column?)organisation[column];
                case "OrganisationChange":
                    return (Column?)organisationChange[column];
                case "Asset":
                    return (Column?)asset[column];
                case "AssetChange":
                    return (Column?)assetChange[column];
                case "Contact":
                    return (Column?)contact[column];
                case "ConferenceType":
                    return (Column?)conferenceType[column];
                case "Conference":
                    return (Column?)conference[column];
                case "Recurrence":
                    return (Column?)conferenceRecurrence[column];
                case "Resource":
                    return (Column?)resource[column];
                case "Login":
                    return (Column?)login[column];
                default:
                    return null;
            }
        }
        catch
        {
            return null;
        }
    }
    public static int GetColumnIndex(string table, string column)
    {
        var dictionary = GetDictionary(table, false);
        if (dictionary == null)
            return -1;
        int i = 0;
        foreach (DictionaryEntry de in dictionary)
        {
            if ((string)de.Key == column)
                return i;
            ++i;
        }
        return -1;
    }
    public static OrderedDictionary? GetDictionary(string table, bool ordered)
    {
        if (table == "Organisation")
            return ordered ? orderedOrganisation : organisation;
        else if (table == "Asset")
            return ordered ? orderedAsset : asset;
        else if (table == "Contact")
            return ordered ? orderedContact : contact;
        else if (table == "Conference")
            return ordered ? orderedConference : conference;
        else if (table == "Resource")
            return resource;
        else if (table == "OrganisationChange")
            return organisationChange; // No ordered version.
        else if (table == "AssetChange")
            return assetChange; // No ordered version.
        else if (table == "OrganisationContacts") // Not present in the column record.
            return new OrderedDictionary
            {
                { Glo.Tab.ORGANISATION_REF, organisation[Glo.Tab.ORGANISATION_REF] },
                { Glo.Tab.CONTACT_ID, contact[Glo.Tab.CONTACT_ID] }
            };
        else
            return null;
    }
    public static List<int>? GetOrder(string table)
    {
        if (table == "Organisation")
            return organisationOrder;
        else if (table == "Asset")
            return assetOrder;
        else if (table == "Contact")
            return contactOrder;
        else if (table == "Conference")
            return conferenceOrder;
        else
            return null;
    }
    public static List<KeyValuePair<string, Column>> GenerateKvpList(OrderedDictionary od)
    {
        List<KeyValuePair<string, Column>> kvpList = new();
        foreach (DictionaryEntry de in od)
            kvpList.Add(new KeyValuePair<string, Column>((string)de.Key, (Column)de.Value!));
        return kvpList;
    }

    public static bool IsTypeString(string type)
    { return type == "VARCHAR"; }
    public static bool IsTypeString(Column col)
    { return col.type == "VARCHAR"; }
    public static bool IsTypeInt(string type)
    { return type.Contains("INT"); }
    public static bool IsTypeInt(Column col)
    { return col.type.Contains("INT"); }

    public static bool Initialise(string columns)
    {
        // Locking these shouldn't be necessary, as the ColumnRecord should never be edited outside the
        // App.PullColumnRecord function, and that's written to be limited to one thread at once.

        organisation = new();
        organisationFriendlyNameReversal = new();
        organisationChange = new();
        organisationChangeFriendlyNameReversal = new();
        asset = new();
        assetFriendlyNameReversal = new();
        assetChange = new();
        assetChangeFriendlyNameReversal = new();
        contact = new();
        contactFriendlyNameReversal = new();
        conferenceType = new();
        conferenceTypeFriendlyNameReversal = new();
        conference = new();
        conferenceFriendlyNameReversal = new();
        conferenceRecurrence = new();
        conferenceRecurrenceFriendlyNameReversal = new();
        connection = new();
        connectionFriendlyNameReversal = new();
        resource = new();
        resourceFriendlyNameReversal = new();
        login = new();
        loginFriendlyNameReversal = new();

        organisationOrder = new();
        assetOrder = new();
        contactOrder = new();
        conferenceOrder = new();

        orderedOrganisation = new();
        orderedAsset = new();
        orderedContact = new();
        orderedConference = new();

        organisationHeaders = new();
        assetHeaders = new();
        contactHeaders = new();
        conferenceHeaders = new();

        try
        {
            System.IO.StringReader reader = new(columns);
            List<string> lines = new();
            string? lineString = reader.ReadLine();
            while (lineString != null)
            {
                lines.Add(lineString);
                lineString = reader.ReadLine();
            }

            if (!int.TryParse(lines[0], out columnRecordID))
                return false;

            // Start at 2, since the first line is the column record ID.
            int n = 2;
            for (; n < lines.Count; ++n)
            {
                if (lines[n] == "-")
                {
                    ++n;
                    break; // Proceed to reading friendly names.
                }

                int cInd = lines[n].IndexOf("[C]");
                int rInd = lines[n].IndexOf("[R]");
                int aInd = lines[n].IndexOf("[A]"); // Likely the first of many

                string table = lines[n].Remove(cInd);

                // Record uniqueness.
                bool unique = table.StartsWith("[U]");
                if (unique)
                    table = table.Substring(3);

                string column = lines[n].Substring(cInd + 3, rInd - (cInd + 3));
                string restriction = lines[n].Substring(rInd + 3);
                string[] allowedArr = new string[0];
                if (aInd != -1)
                {
                    restriction = restriction.Remove(restriction.IndexOf("[A]"));
                    string allowed = lines[n].Substring(aInd + 3);
                    allowedArr = allowed.Split("[A]");
                }

                // Type and restriction will be corrected in a sec.
                string type = restriction;
                long max = 0;
                    
                // If type is text, change type name and set max length.
                int r;
                if (int.TryParse(restriction, out r))
                {
                    type = "VARCHAR";
                    max = r == -1 ? Int32.MaxValue : r;
                }
                else if (type == "TINYINT")
                    max = 255;
                else if (type == "SMALLINT")
                    max = 32_767;
                else if (type == "BIGINT")
                    max = 9_223_372_036_854_775_807;
                else if (type == "INT")
                    max = 2_147_483_647;
                // else max remains 0 for dates.

                Column col = new Column(type, max, allowedArr, "", unique);


                // Add column to the relevant Dictionary, using the column name as the key.
                if (table == "Organisation")
                    organisation.Add(column, col);
                if (table == "OrganisationChange")
                    organisationChange.Add(column, col);
                else if (table == "Contact")
                    contact.Add(column, col);
                else if (table == "Asset")
                    asset.Add(column, col);
                else if (table == "AssetChange")
                    assetChange.Add(column, col);
                else if (table == "ConferenceType")
                    conferenceType.Add(column, col);
                else if (table == "Conference")
                    conference.Add(column, col);
                else if (table == "Connection")
                    connection.Add(column, col);
                else if (table == "Recurrence")
                    conferenceRecurrence.Add(column, col);
                else if (table == "Resource")
                    resource.Add(column, col);
                else if (table == "Login")
                    login.Add(column, col);
            }

            for (; n < lines.Count; ++n) // Won't run if there are no friendly names.
            {
                if (lines[n] == ">")
                {
                    // Proceed on to the column orders.
                    ++n;
                    break;
                }

                string[] friendlySplit = lines[n].Split(";;");
                // The option is open for the user to specify friendly names using spaces instead of
                // underscores, so make that uniform here.
                friendlySplit[1] = friendlySplit[1].Replace(' ', '_');

                void AddFriendlyName(OrderedDictionary dict)
                {
                    if (dict.Contains(friendlySplit[1]))
                    {
                        Column col = (Column)dict[friendlySplit[1]]!;
                        col.friendlyName = friendlySplit[2];
                        dict[friendlySplit[1]] = col;
                    }
                }

                if (friendlySplit[0] == "Organisation")
                    AddFriendlyName(organisation);
                if (friendlySplit[0] == "OrganisationChange")
                    AddFriendlyName(organisationChange);
                if (friendlySplit[0] == "Asset")
                    AddFriendlyName(asset);
                if (friendlySplit[0] == "AssetChange")
                    AddFriendlyName(assetChange);
                if (friendlySplit[0] == "Contact")
                    AddFriendlyName(contact);
                if (friendlySplit[0] == "ConferenceType")
                    AddFriendlyName(conferenceType);
                if (friendlySplit[0] == "Conference")
                    AddFriendlyName(conference);
                if (friendlySplit[0] == "Connection")
                    AddFriendlyName(connection);
                if (friendlySplit[0] == "Recurrence")
                    AddFriendlyName(conferenceRecurrence);
                if (friendlySplit[0] == "Resource")
                    AddFriendlyName(resource);
                if (friendlySplit[0] == "Login")
                    AddFriendlyName(login);
            }

            for (int o = 0; n < lines.Count && o < 4; ++n, ++o)
            {
                if (lines[n] == "<")
                {
                    // Proceed on to the table section headers.
                    ++n;
                    break;
                }

                string[] indices = lines[n].Split(',');
                List<int> order;
                if (o == 0)
                    order = organisationOrder;
                else if (o == 1)
                    order = assetOrder;
                else if (o == 2)
                    order = contactOrder;
                else // if 3
                    order = conferenceOrder;

                foreach (string s in indices)
                {
                    int i;
                    if (int.TryParse(s, out i))
                        order.Add(i);
                }
            }

            // We might have exited out of friendly names without encountering < and incrementing.
            if (lines[n] == "<")
                ++n;

            // This input is heavily vetted by the agent when creating the column record, so no need to check much.
            for (int o = 0; n < lines.Count && o < 4; ++n, ++o)
            {
                List<ColumnOrdering.Header> headerList;
                if (o == 0)
                    headerList = organisationHeaders;
                else if (o == 1)
                    headerList = assetHeaders;
                else if (o == 2)
                    headerList = contactHeaders;
                else
                    headerList = conferenceHeaders;

                string[] vals = lines[n].Split(';');

                for (int i = 0; i < vals.Length - 1; i += 2)
                {
                    ColumnOrdering.Header header = new();
                    header.name = vals[i];
                    if (int.TryParse(vals[i + 1], out header.position))
                        headerList.Add(header);
                }

                // Make sure they're stored in order.
                headerList = headerList.OrderBy(h => h.position).ToList();
            }

            // Soft duplicate checks
            string[] columnsToSet = lines[lines.Count - 1].Split(';');
            foreach (string c in columnsToSet)
            {
                string[] names = c.Split(".");
                if (names.Length != 2)
                    continue;
                OrderedDictionary? dict = GetDictionary(names[0], false);
                if (dict == null)
                    continue;
                if (names[1].Length > 0 && dict.Contains(names[1]))
                {
                    Column col = (Column)dict[names[1]]!;
                    col.softDuplicateCheck = true;
                    dict[names[1]] = col;
                }
            }

            // Check the order integrity.
            if (organisation.Count != organisationOrder.Count ||
                asset.Count != assetOrder.Count ||
                contact.Count != contactOrder.Count ||
                conference.Count != conferenceOrder.Count)
                return false;

            for (int i = 0; i < organisationOrder.Count; ++i)
                if (!organisationOrder.Contains(i))
                    return false;
            for (int i = 0; i < assetOrder.Count; ++i)
                if (!assetOrder.Contains(i))
                    return false;
            for (int i = 0; i < contactOrder.Count; ++i)
                if (!contactOrder.Contains(i))
                    return false;
            for (int i = 0; i < conferenceOrder.Count; ++i)
                if (!conferenceOrder.Contains(i))
                    return false;

            if (!OrderTable(organisation, organisationOrder, orderedOrganisation) ||
                !OrderTable(asset, assetOrder, orderedAsset) ||
                !OrderTable(contact, contactOrder, orderedContact) ||
                !OrderTable(conference, conferenceOrder, orderedConference))
                return false;


            // Populate the friendly name reversal dictionaries.
            foreach (DictionaryEntry de in organisation)
                organisationFriendlyNameReversal.Add(GetPrintName(de), (string)de.Key);
            foreach (DictionaryEntry de in organisationChange)
                organisationChangeFriendlyNameReversal.Add(GetPrintName(de), (string)de.Key);
            foreach (DictionaryEntry de in asset)
                assetFriendlyNameReversal.Add(GetPrintName(de), (string)de.Key);
            foreach (DictionaryEntry de in assetChange)
                assetChangeFriendlyNameReversal.Add(GetPrintName(de), (string)de.Key);
            foreach (DictionaryEntry de in contact)
                contactFriendlyNameReversal.Add(GetPrintName(de), (string)de.Key);
            foreach (DictionaryEntry de in conferenceType)
                conferenceTypeFriendlyNameReversal.Add(GetPrintName(de), (string)de.Key);
            foreach (DictionaryEntry de in conference)
                conferenceFriendlyNameReversal.Add(GetPrintName(de), (string)de.Key);
            foreach (DictionaryEntry de in connection)
                connectionFriendlyNameReversal.Add(GetPrintName(de), (string)de.Key);
            foreach (DictionaryEntry de in conferenceRecurrence)
                conferenceRecurrenceFriendlyNameReversal.Add(GetPrintName(de), (string)de.Key);
            foreach (DictionaryEntry de in resource)
                resourceFriendlyNameReversal.Add(GetPrintName(de), (string)de.Key);
            foreach (DictionaryEntry de in login)
                loginFriendlyNameReversal.Add(GetPrintName(de), (string)de.Key);

            // Phew!
            return true;
        }
        catch
        {
            return false;
        }
    }
}