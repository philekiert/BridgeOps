using System;
using System.Net;
using System.Reflection.Metadata;
using System.Xml.Linq;
using System.Reflection;

// Application version
[assembly: AssemblyVersion("1.1.0")]
[assembly: AssemblyFileVersion("1.1.0")]
[assembly: AssemblyInformationalVersion("1.1.0")]

public static class Glo
{
    // Version
    public static string VersionNumber
    {
        get
        {
            Version version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version!;
            return $"v {version.Major}.{version.Minor}.{version.Build}";
        }
    }

    // Communication
    public const string PIPE_CONSOLE = "BridgeManagerAgent";
    public const int PORT_INBOUND_DEFAULT = 61_152;
    public const int PORT_OUTBOUND_DEFAULT = 61_153;
    public const string SQL_SERVER_NAME_DEFAULT = "SQLEXPRESS";

    public static string NL = Environment.NewLine;
    public static string DNL = Environment.NewLine + Environment.NewLine;

    public static string ROW_CLASH_WARNING = "This would result in one or more row clashes. ";
    public static string ROW_CLASH_FAILED_RESOLVE = "Could not resolve row clashes. " + 
                                                    "Increase the resource capacity or additional rows and try again.";
    public static string DIAL_CLASH_WARNING = "This would result in one or more dial number clashes.";
    public static string RESOURCE_OVERFLOW_WARNING = "This move would result in one or more resource overflows.";

    // Files and Folder Traversal
#if DEBUG
    public const string PATH_AGENT = "..\\..\\..\\..\\BridgeOpsAgent\\bin\\Debug\\net6.0\\";
    public const string PATH_CONSOLE = "..\\..\\..\\..\\BridgeOpsConsole\\bin\\Debug\\net6.0\\";
#else
    public const string PATH_AGENT = "";
    public const string PATH_CONSOLE = "";
#endif
    public const string EXE_AGENT = "BridgeManagerAgent.exe";
    public const string EXE_CONSOLE = "BridgeManagerConsole.exe";
    public static string PathConfigFiles
    { get { return System.IO.Path.Combine(Fun.ApplicationFolder(), "Config Files"); } }
    public static string PathImportFiles
    { get { return System.IO.Path.Combine(Fun.ApplicationFolder(), "Data Import"); } }
    public static string PathAgentErrorLog
    { get { return System.IO.Path.Combine(Fun.ApplicationFolder(), "agent-error-log.txt"); } }
    public const string CONFIG_TYPE_OVERRIDES = "type-overrides.txt";
    public const string CONFIG_COLUMN_ADDITIONS = "column-additions.txt";
    public const string CONFIG_NETWORK = "network-config.txt";
    public const string CONFIG_NETWORK_CLIENT = "server";
    public const string CONFIG_SQL_SERVER_NAME = "sql-server-name.txt";
    public const string CONFIG_SQL_SERVER_READER = "sql-reader.txt";
    public const string CONFIG_COLUMN_RECORD = "column-record";
    public const string CONFIG_FRIENDLY_NAMES = "friendly-names.txt";
    public const string CONFIG_HEADERS = "section-headers";
    public const string CONFIG_SOFT_DUPLICATE_CHECKS = "soft-duplicate-checks";
    public const string FOLDER_QUERY_BUILDER_PRESETS = "Query Builder Presets";

    // Client/Agent Function Specifiers
    public const int CLIENT_PULL_COLUMN_RECORD = 0;
    public const int CLIENT_LOGIN = 1;
    public const int CLIENT_LOGOUT = 2;
    public const int CLIENT_CLOSE = 3;
    public const int CLIENT_PASSWORD_RESET = 4;
    public const int CLIENT_LOGGEDIN_LIST = 5;
    public const int CLIENT_PULL_USER_SETTINGS = 6;
    public const int CLIENT_NEW_LOGIN = 10;
    public const int CLIENT_NEW_ORGANISATION = 11;
    public const int CLIENT_NEW_CONTACT = 12;
    public const int CLIENT_NEW_ASSET = 13;
    public const int CLIENT_NEW_CONFERENCE_TYPE = 14;
    public const int CLIENT_NEW_CONFERENCE = 15;
    public const int CLIENT_NEW_RECURRENCE = 16;
    public const int CLIENT_NEW_RESOURCE = 17;
    public const int CLIENT_UPDATE = 18;
    public const int CLIENT_UPDATE_LOGIN = 19;
    public const int CLIENT_UPDATE_ORGANISATION = 20;
    public const int CLIENT_UPDATE_CONTACT = 21;
    public const int CLIENT_UPDATE_ASSET = 22;
    public const int CLIENT_UPDATE_CONFERENCE_TYPE = 23;
    public const int CLIENT_UPDATE_CONFERENCE = 24;
    public const int CLIENT_UPDATE_RESOURCE = 25;
    public const int CLIENT_UPDATE_RECURRENCE = 26;
    public const int CLIENT_UPDATE_CHANGE_REASON = 27;
    public const int CLIENT_SELECT_COLUMN_PRIMARY = 30;
    public const int CLIENT_SELECT_QUICK = 31;
    public const int CLIENT_SELECT = 32;
    public const int CLIENT_SELECT_STATEMENT = 33;
    public const int CLIENT_SELECT_WIDE = 34;
    public const int CLIENT_SELECT_EXISTS = 35;
    public const int CLIENT_DELETE = 36;
    public const int CLIENT_LINK_CONTACT = 40;
    public const int CLIENT_LINKED_CONTACT_SELECT = 41;
    public const int CLIENT_SELECT_HISTORY = 42;
    public const int CLIENT_SELECT_HISTORICAL_RECORD = 43;
    public const int CLIENT_TABLE_MODIFICATION = 50;
    public const int CLIENT_COLUMN_ORDER_UPDATE = 51;
    public const int CLIENT_SELECT_BUILDER_PRESET_SAVE = 60;
    public const int CLIENT_SELECT_BUILDER_PRESET_LOAD = 61;
    public const int CLIENT_SELECT_BUILDER_PRESET_DELETE = 62;
    public const int CLIENT_SELECT_BUILDER_PRESET_RENAME = 63;
    public const int CLIENT_CONFERENCE_VIEW_SEARCH = 70;
    public const int CLIENT_CONFERENCE_SELECT = 71;
    public const int CLIENT_CONFERENCE_CANCEL = 72;
    public const int CLIENT_CONFERENCE_QUICK_MOVE = 73;
    public const int CLIENT_CONFERENCE_DUPLICATE_SERIES = 74;
    public const int CLIENT_CONFERENCE_ADJUST = 75;
    public const int CLIENT_CONFERENCE_SELECT_CONNECTIONS = 76;

    public const int SERVER_CLIENT_NUDGE = 0;
    public const int SERVER_COLUMN_RECORD_UPDATED = 1;
    public const int SERVER_RESOURCES_UPDATED = 2;
    public const int SERVER_FORCE_LOGOUT = 3;
    public const int SERVER_CONFERENCES_UPDATED = 4;
    public const int SERVER_CLIENT_CLOSE = 5;


    // Console/Agent Function Specifiers
    public const int CONSOLE_GET_AGENT_VERSION = 0;
    public const int CONSOLE_CLIENT_LIST = 1;
    public const int CONSOLE_LOGOUT_USER = 2;
    public const int CONSOLE_CLOSE_CLIENT = 3;

    // Operational
    public const int CLIENT_SESSION_INVALID = 0;
    public const int CLIENT_INSUFFICIENT_PERMISSIONS = 2;
    public const int CLIENT_REQUEST_FAILED = 3;
    public const int CLIENT_REQUEST_FAILED_MORE_TO_FOLLOW = 4;
    public const int CLIENT_REQUEST_FAILED_RECORD_DELETED = 5;
    public const int CLIENT_REQUEST_FAILED_FOREIGN_KEY = 6;
    public const int CLIENT_REQUEST_SUCCESS = 7;
    public const int CLIENT_REQUEST_SUCCESS_MORE_TO_FOLLOW = 8;
    public const int CLIENT_CONFIRM = 9;
    public const int CLIENT_CONFIRM_MORE_TO_FOLLOW = 10;
    public const int CLIENT_CANCEL = 11;
    public const string CLIENT_LOGIN_ACCEPT = "Welcome";
    public const string CLIENT_LOGIN_REJECT_USER_INVALID = "Must have been the wind";
    public const string CLIENT_LOGIN_REJECT_USER_DISABLED = "I used to be an adventurer like you";
    public const string CLIENT_LOGIN_REJECT_USER_DUPLICATE = "User already logged in"; // Currently unused by agent
    public const string CLIENT_LOGIN_REJECT_IP_DUPLICATE = "IP already connected"; // Currently unused by agent
    public const string CLIENT_LOGIN_REJECT_IP_UNKNOWN = "IP unknown";

    // Permissions
    public const int PERMISSION_CREATE = 0;
    public const int PERMISSION_EDIT = 1;
    public const int PERMISSION_DELETE = 2;
    public const int PERMISSION_RECORDS = 0; // Keep at zero, and subsequent values in order.
    public const int PERMISSION_CONFERENCES = 1;
    public const int PERMISSION_RESOURCES = 2;
    public const int PERMISSION_UNUSED = 3;
    public const int PERMISSION_REPORTS = 4;
    public const int PERMISSION_USER_ACC_MGMT = 5;
    public const int PERMISSIONS_MAX_VALUE = 63;

    public const string CLIENT_LOGOUT_ACCEPT = "Safe travels";
    public const string CLIENT_LOGOUT_SESSION_NOT_FOUND = "Session not found";
    public const string CLIENT_CLOSE_SESSION_NOT_FOUND = "Target lost";

    public const string CLIENT_LOGOUT_SESSION_INVALID = "Session invalid";
    

    // Network Config
    public const int NETWORK_SETTINGS_LENGTH = 15; // Increase if longer settings names are used in network-config.txt
    public const string NETWORK_SETTINGS_PORT_INBOUND = "inbound port:  ";
    public const string NETWORK_SETTINGS_PORT_OUTBOUND = "outbound port: ";


    public const string VARCHARMAX = "VARCHAR(MAX)";
    public const string INT = "INT";
    public const string SMALLINT = "SMALLINT";
    public const string TINYINT = "TINYINT";

    // Column Names
    public static class Tab
    {
        public const string ORGANISATION_ID = "Organisation_ID";
        public const string ORGANISATION_REF = "Organisation_Reference";
        public const string PARENT_REF = "Parent_Reference";
        public const string ORGANISATION_NAME = "Organisation_Name";
        public const string DIAL_NO = "Dial_No";
        public const string ORGANISATION_AVAILABLE = "Available_for_Conferencing";

        public const string CHANGE_ID = "Change_ID";
        public const string CHANGE_TIME = "Time";
        public const string CHANGE_REASON = "Reason";
        public const string CHANGE_SUFFIX = "_Register";

        public const string CONTACT_ID = "Contact_ID";

        public const string ASSET_ID = "Asset_ID";
        public const string ASSET_REF = "Asset_Reference";

        public const string CONFERENCE_RESOURCE_ROW = "Resource_Row";
        public const string CONFERENCE_ID = "Conference_ID";
        public const string CONFERENCE_TITLE = "Title";
        public const string CONFERENCE_START = "Start_Time";
        public const string CONFERENCE_END = "End_Time";
        public const string CONFERENCE_CANCELLED = "Cancelled";
        public const string CONFERENCE_CLOSURE = "Closure";
        public const string CONFERENCE_CREATION_LOGIN = "Creation_Login_ID";
        public const string CONFERENCE_CREATION_TIME = "Creation_Time";
        public const string CONFERENCE_EDIT_LOGIN = "Edit_Login_ID";
        public const string CONFERENCE_EDIT_TIME = "Edit_Time";

        public const string CONFERENCE_ADD_HOST_DIAL_NO = "Host_Dial_No";
        public const string CONFERENCE_ADD_IS_TEST = "Is_Test";
        public const string CONFERENCE_ADD_CONNECTIONS = "Connection_Count";
        public const string CONFERENCE_ADD_CONNECTED = "Connected_Count";
        public const string CONFERENCE_ADD_CONNECTED_START = "Connected_Start";
        public const string CONFERENCE_ADD_CONNECTED_END = "Connected_End";
        public const string CONFERENCE_ADD_CONNECTED_START_DATE = "Connected_Start_Date";
        public const string CONFERENCE_ADD_CONNECTED_END_DATE = "Connected_End_Date";
        public const string CONFERENCE_ADD_CONNECTED_START_TIME = "Connected_Start_Time";
        public const string CONFERENCE_ADD_CONNECTED_END_TIME = "Connected_End_Time";
        public const string CONFERENCE_ADD_CONNECTED_DURATION = "Connected_Duration";
        public const string CONFERENCE_ADD_START_DATE = "Start_Date";
        public const string CONFERENCE_ADD_END_DATE = "End_Date";
        public const string CONFERENCE_ADD_START_TIME = "Start_Time";
        public const string CONFERENCE_ADD_END_TIME = "End_Time";
        public const string CONFERENCE_ADD_DURATION = "Duration";

        public const string CONNECTION_ID = "Connection_ID";
        public const string CONNECTION_IS_MANAGED = "Is_Managed";
        public const string CONNECTION_TIME_FROM = "Connection_Time";
        public const string CONNECTION_TIME_TO = "Disconnection_Time";
        public const string CONNECTION_ROW = "Row";
        public const string CONNECTION_IS_TEST = "Is_Test";
        public const string RESOURCE_ID = "Resource_ID";
        public const string RESOURCE_NAME = "Resource_Name";
        public const string RESOURCE_CAPACITY_CONNECTION = "Connection_Capacity";
        public const string RESOURCE_CAPACITY_CONFERENCE = "Conference_Capacity";
        public const string RESOURCE_ROWS_ADDITIONAL = "Rows_Additional";

        public const string RECURRENCE_ID = "Recurrence_ID";
        public const string RECURRENCE_NAME = "Name";

        public const string LOGIN_ID = "Login_ID";
        public const string LOGIN_USERNAME = "Username";
        public const string LOGIN_PASSWORD = "Password";
        public const string LOGIN_ADMIN = "Admin";
        public const string LOGIN_CREATE_PERMISSIONS = "Create_Permissions";
        public const string LOGIN_EDIT_PERMISSIONS = "Edit_Permissions";
        public const string LOGIN_DELETE_PERMISSIONS = "Delete_Permissions";
        public const string LOGIN_ENABLED = "Enabled";
        public const string LOGIN_VIEW_SETTINGS = "Organisation_Order";

        public const string TASK_ID = "Task_ID";
        public const string TASK_REFERENCE = "Task_Reference";
        public const string TASK_OPENED = "Task_Opened";
        public const string TASK_CLOSED = "Task_Closed";

        public const string VISIT_ID = "Visit_ID";
        public const string VISIT_DATE = "Visit_Date";
        public const string VISIT_TYPE = "Visit_Type";

        public const string DOCUMENT_ID = "Document_ID";
        public const string DOCUMENT_DATE = "Document_Date";
        public const string DOCUMENT_TYPE = "Document_Type";

        public const string FRIENDLY_TABLE = "TableName";
        public const string FRIENDLY_COLUMN = "ColumnName";
        public const string FRIENDLY_NAME = "FriendlyName";

        // These represent how many core columns are in the respective tables, unable to be re-ordered.
        public const int ORGANISATION_STATIC_COUNT = 8;
        public const int ASSET_STATIC_COUNT = 4;
        public const int CONTACT_STATIC_COUNT = 2;
        public const int CONFERENCE_STATIC_COUNT = 14;
        public const int TASK_STATIC_COUNT = 5;
        public const int VISIT_STATIC_COUNT = 5;
        public const int DOCUMENT_STATIC_COUNT = 5;

        public const string NOTES = "Notes";
    }


    // Handy Functions
    public static class Fun
    {
        public static bool[] GetPermissionsArray(int bits)
        {
            bool[] permissions = new bool[6];
            for (int n = 0; n < 6; ++n)
                permissions[n] = (bits & (1 << n)) != 0;
            return permissions;
        }
        public static int GetPermissionsInt(bool[] bits)
        {
            int permissions = 0;
            for (int n = 0; n < 6; ++n)
                permissions += 1 << n;
            return permissions;
        }
        public static int GetPermissionRelevancy(string table)
        {
            if (table == "Organisation" || table == "Asset" || table == "Contact")
                return PERMISSION_RECORDS;
            if (table == "Conference" || table == "Recurrence")
                return PERMISSION_CONFERENCES;
            if (table == "Resource")
                return PERMISSION_RESOURCES;
            if (table == "ConferenceType")
                return PERMISSION_UNUSED;
            if (table == "Login")
                return PERMISSION_USER_ACC_MGMT;
            else return -1;
        }

        public static bool ColumnRemovalAllowed(string table, string column)
        {
            // This isn't super exhaustive and isn't exactly ideal, but I've added to it over time as needs become
            // apparent.

            return column != "Notes" &&
                !((table == "Organisation" &&
                    (column == Glo.Tab.ORGANISATION_ID ||
                     column == Glo.Tab.ORGANISATION_REF ||
                     column == Glo.Tab.PARENT_REF ||
                     column == Glo.Tab.ORGANISATION_NAME ||
                     column == Glo.Tab.DIAL_NO ||
                     column == Glo.Tab.ORGANISATION_AVAILABLE ||
                     column == Glo.Tab.TASK_REFERENCE ||
                     column == Glo.Tab.NOTES)) ||
                  (table == "Asset" &&
                    (column == Glo.Tab.ASSET_ID ||
                     column == Glo.Tab.ASSET_REF ||
                     column == Glo.Tab.ORGANISATION_REF ||
                     column == Glo.Tab.NOTES)) ||
                  (table == "Contact" &&
                    (column == Glo.Tab.CONTACT_ID ||
                     column == Glo.Tab.NOTES)) ||
                  (table == "Conference" &&
                    (column == Glo.Tab.CONFERENCE_ID ||
                     column == Glo.Tab.RESOURCE_ID ||
                     column == Glo.Tab.CONFERENCE_RESOURCE_ROW ||
                     column == Glo.Tab.CONFERENCE_TITLE ||
                     column == Glo.Tab.CONFERENCE_START ||
                     column == Glo.Tab.CONFERENCE_END ||
                     column == Glo.Tab.CONFERENCE_CANCELLED ||
                     column == Glo.Tab.CONFERENCE_CLOSURE ||
                     column == Glo.Tab.ORGANISATION_REF ||
                     column == Glo.Tab.RECURRENCE_ID ||
                     column == Glo.Tab.CONFERENCE_CREATION_LOGIN ||
                     column == Glo.Tab.CONFERENCE_CREATION_TIME ||
                     column == Glo.Tab.CONFERENCE_EDIT_LOGIN ||
                     column == Glo.Tab.CONFERENCE_EDIT_TIME ||
                     column == Glo.Tab.NOTES)) ||
                  (table == "Recurrence" &&
                    (column == Glo.Tab.RECURRENCE_ID ||
                     column == Glo.Tab.RECURRENCE_NAME ||
                     column == Glo.Tab.NOTES)) ||
                   (table == "Login" &&
                    (column == Glo.Tab.LOGIN_ID ||
                     column == Glo.Tab.LOGIN_USERNAME ||
                     column == Glo.Tab.LOGIN_PASSWORD ||
                     column == Glo.Tab.LOGIN_VIEW_SETTINGS ||
                     column == Glo.Tab.LOGIN_CREATE_PERMISSIONS ||
                     column == Glo.Tab.LOGIN_EDIT_PERMISSIONS ||
                     column == Glo.Tab.LOGIN_DELETE_PERMISSIONS ||
                     column == Glo.Tab.LOGIN_ADMIN ||
                     column == Glo.Tab.LOGIN_ENABLED)) ||
                   (table == "Task" &&
                    (column == Glo.Tab.TASK_ID ||
                     column == Glo.Tab.TASK_REFERENCE ||
                     column == Glo.Tab.TASK_OPENED ||
                     column == Glo.Tab.TASK_CLOSED ||
                     column == Glo.Tab.NOTES)) ||
                   (table == "Visit" &&
                    (column == Glo.Tab.VISIT_ID ||
                     column == Glo.Tab.TASK_REFERENCE ||
                     column == Glo.Tab.VISIT_DATE ||
                     column == Glo.Tab.VISIT_TYPE ||
                     column == Glo.Tab.NOTES)) ||
                   (table == "Document" &&
                    (column == Glo.Tab.DOCUMENT_ID ||
                     column == Glo.Tab.TASK_REFERENCE ||
                     column == Glo.Tab.DOCUMENT_DATE ||
                     column == Glo.Tab.DOCUMENT_TYPE ||
                     column == Glo.Tab.NOTES))
                 );
        }

        public static IPAddress? GetIPAddressFromString(string ip)
        {
            IPAddress? clientIP;
            IPAddress.TryParse(ip, out clientIP);
            return clientIP;
        }

        public static int LongToInt(long val)
        {
            return val > int.MaxValue ? int.MaxValue : (int)val;
        }

        public static bool IsValidInt(string s, int min, int max)
        { int i; return int.TryParse(s, out i) && i >= min && i <= max; }

        public static void ExistsOrCreateFolder() { ExistsOrCreateFolder(ApplicationFolder()); }
        public static void ExistsOrCreateFolder(string folder)
        {
            if (!System.IO.Directory.Exists(folder))
                System.IO.Directory.CreateDirectory(folder);
        }
        public static string ApplicationFolder()
        {
            return System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                                          "Bridge Manager");
        }
        public static string ApplicationFolder(string subdir)
        {
            return System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                                          "Bridge Manager", subdir);
        }

        public static bool IsUnsafeForCSV(string s)
        {
            return s != null && (s.Contains('"') || s.Contains(',') ||
                   s.Contains('\n') || s.Contains('\r') || s.Contains(NL));
        }
        public static string MakeSafeForCSV(string s)
        {
            if (IsUnsafeForCSV(s))
                return "\"" + s.Replace("\"", "\"\"") + "\"";
            return s;
        }

        public static Int32? GetInt32FromNullableObject(object? i)
        {
            if (i is byte b)
                return b;
            if (i is Int16 i16)
                return i16;
            else if (i is Int32 i32)
                return i32;
            return null;
        }
        public static Int64? GetInt64FromNullableObject(object? i)
        {
            if (i is Int16 i16)
                return i16;
            else if (i is Int32 i32)
                return i32;
            else if (i is Int64 i64)
                return i64;
            return null;
        }
    }
}