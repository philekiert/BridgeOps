using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


public static class ColumnRecord
{
    public static object lockColumnRecord = new();
    // Add to this as development continues.
    public struct Column
    {
        // column name is stored as the key in Dictionary
        public string type;
        public int restriction; // Only for character strings
        public string[] allowed; // Allowed values, only ever present in user-added columns
        public string friendlyName;

        public Column(string type, int restriction, string[] allowed, string friendlyName)
        {
            this.type = type;
            this.restriction = restriction;
            this.allowed = allowed;
            this.friendlyName = friendlyName;
        }
    }

    public static string GetPrintName(KeyValuePair<string, Column> col)
    {
        if (col.Value.friendlyName != "")
            return col.Value.friendlyName.Replace('_', ' ');
        else
            return col.Key.Replace('_', ' ');
    }
    public static string GetPrintName(string key, Column col)
    {
        if (col.friendlyName != null && col.friendlyName != "")
            return col.friendlyName.Replace('_', ' ');
        else
            return key.Replace('_', ' ');
    }

    public static Dictionary<string, Column> organisation = new();
    public static Dictionary<string, string> organisationFriendlyNameReversal = new();
    public static Dictionary<string, Column> organisationChange = new();
    public static Dictionary<string, string> organisationChangeFriendlyNameReversal = new();
    public static Dictionary<string, Column> asset = new();
    public static Dictionary<string, string> assetFriendlyNameReversal = new();
    public static Dictionary<string, Column> assetChange = new();
    public static Dictionary<string, string> assetChangeFriendlyNameReversal = new();
    public static Dictionary<string, Column> contact = new();
    public static Dictionary<string, string> contactFriendlyNameReversal = new();
    public static Dictionary<string, Column> conferenceType = new();
    public static Dictionary<string, string> conferenceTypeFriendlyNameReversal = new();
    public static Dictionary<string, Column> conference = new();
    public static Dictionary<string, string> conferenceFriendlyNameReversal = new();
    public static Dictionary<string, Column> conferenceRecurrence = new();
    public static Dictionary<string, string> conferenceRecurrenceFriendlyNameReversal = new();
    public static Dictionary<string, Column> resource = new();
    public static Dictionary<string, string> resourceFriendlyNameReversal = new();
    public static Dictionary<string, Column> login = new();
    public static Dictionary<string, string> loginFriendlyNameReversal = new();

    public static List<int> organisationOrder = new();
    public static List<int> assetOrder = new();
    public static List<int> contactOrder = new();
    public static List<int> conferenceOrder = new();

    public static Dictionary<string, Column> orderedOrganisation = new();
    public static Dictionary<string, Column> orderedAsset = new();
    public static Dictionary<string, Column> orderedContact = new();
    public static Dictionary<string, Column> orderedConference = new();

    public static List<string[]> GetFriendlyNames()
    {
        List<string[]> friendlyNames = new();

        void GetNames(string table, Dictionary<string, Column> dict)
        {
            foreach (KeyValuePair<string, Column> col in dict)
                if (col.Value.friendlyName != "")
                    friendlyNames.Add(new string[] { table, col.Key, col.Value.friendlyName });
        }

        GetNames("Organisation", organisation);
        GetNames("Asset", asset);
        GetNames("Contact", contact);
        GetNames("Conference", conference);

        return friendlyNames;
    }

    public static bool OrderTable(Dictionary<string, Column> dictionary,
                                  List<int> order,
                                  Dictionary<string, Column> orderedDictionary)
    {
        try
        {
            KeyValuePair<string, Column>[] orderedArray = new KeyValuePair<string, Column>[order.Count];

            int i = 0;
            foreach (KeyValuePair<string, Column> kvp in dictionary)
            {
                for (int n = 0; n < orderedArray.Length; ++n)
                    if (order[n] == i)
                    {
                        orderedArray[n] = kvp;
                        break;
                    }
                ++i;
            }

            foreach (KeyValuePair<string, Column> kvp in orderedArray)
                orderedDictionary.Add(kvp.Key, kvp.Value);

            return true;
        }
        catch
        {
            return false;
        }
    }

    public static Column? GetColumn(string table, string column)
    {
        try
        {
            switch (table)
            {
                case "Organisation":
                    return organisation[column];
                case "OrganisationChange":
                    return organisationChange[column];
                case "Asset":
                    return asset[column];
                case "AssetChange":
                    return assetChange[column];
                case "Contact":
                    return contact[column];
                case "ConferenceType":
                    return conferenceType[column];
                case "Conference":
                    return conference[column];
                case "ConferenceRecurrence":
                    return conferenceRecurrence[column];
                case "Resource":
                    return resource[column];
                case "Login":
                    return login[column];
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
        foreach (var kvp in dictionary)
        {
            if (kvp.Key == column)
                return i;
            ++i;
        }
        return -1;
    }
    public static Dictionary<string, Column>? GetDictionary(string table, bool ordered)
    {
        if (table == "Organisation")
            return ordered ? orderedOrganisation : organisation;
        else if (table == "Asset")
            return ordered ? orderedAsset : asset;
        else if (table == "Contact")
            return ordered ? orderedContact : contact;
        else if (table == "Conference")
            return ordered ? orderedConference : conference;
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

    public static bool IsTypeString(string type)
    { return type == "TEXT" || type == "VARCHAR"; }
    public static bool IsTypeString(Column col)
    { return col.type == "TEXT" || col.type == "VARCHAR"; }
    public static bool IsTypeInt(string type)
    { return type == "INT"; }
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

        try
        {
            string[] lines = columns.Split('\n');
            // Start at 1, since the first line is an edit warning.
            int n = 1;
            for (; n < lines.Length; ++n)
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
                int max = 0;

                // If type is text, change type name and set max length.
                int r;
                if (int.TryParse(restriction, out r))
                {
                    type = "VARCHAR";
                    max = r;
                }
                else if (type == "TINYINT")
                    max = 255;
                else if (type == "SMALLINT")
                    max = 32_767;
                else if (type == "INT" || type == "TEXT")
                    max = 2_147_483_647;
                // else max remains 0 for dates.

                Column col = new Column(type, max, allowedArr, "");


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
                else if (table == "Recurrence")
                    conferenceRecurrence.Add(column, col);
                else if (table == "Resource")
                    resource.Add(column, col);
                else if (table == "Login")
                    login.Add(column, col);
            }

            for (; n < lines.Length; ++n) // Won't run if there are no friendly names.
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

                void AddFriendlyName(Dictionary<string, Column> dict)
                {
                    if (dict.ContainsKey(friendlySplit[1]))
                    {
                        Column col = dict[friendlySplit[1]];
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
                if (friendlySplit[0] == "Recurrence")
                    AddFriendlyName(conferenceRecurrence);
                if (friendlySplit[0] == "Resource")
                    AddFriendlyName(resource);
                if (friendlySplit[0] == "Login")
                    AddFriendlyName(login);
            }

            for (int o = 0; n < lines.Length && o < 4; ++n, ++o)
            {
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
            foreach (KeyValuePair<string, Column> kvp in organisation)
                organisationFriendlyNameReversal.Add(GetPrintName(kvp), kvp.Key);
            foreach (KeyValuePair<string, Column> kvp in organisationChange)
                organisationChangeFriendlyNameReversal.Add(GetPrintName(kvp), kvp.Key);
            foreach (KeyValuePair<string, Column> kvp in asset)
                assetFriendlyNameReversal.Add(GetPrintName(kvp), kvp.Key);
            foreach (KeyValuePair<string, Column> kvp in assetChange)
                assetChangeFriendlyNameReversal.Add(GetPrintName(kvp), kvp.Key);
            foreach (KeyValuePair<string, Column> kvp in contact)
                contactFriendlyNameReversal.Add(GetPrintName(kvp), kvp.Key);
            foreach (KeyValuePair<string, Column> kvp in conferenceType)
                conferenceTypeFriendlyNameReversal.Add(GetPrintName(kvp), kvp.Key);
            foreach (KeyValuePair<string, Column> kvp in conference)
                conferenceFriendlyNameReversal.Add(GetPrintName(kvp), kvp.Key);
            foreach (KeyValuePair<string, Column> kvp in conferenceRecurrence)
                conferenceRecurrenceFriendlyNameReversal.Add(GetPrintName(kvp), kvp.Key);
            foreach (KeyValuePair<string, Column> kvp in resource)
                resourceFriendlyNameReversal.Add(GetPrintName(kvp), kvp.Key);
            foreach (KeyValuePair<string, Column> kvp in login)
                loginFriendlyNameReversal.Add(GetPrintName(kvp), kvp.Key);

            // Phew!
            return true;
        }
        catch
        {
            return false;
        }
    }
}