using System;

public static class Glo
{
    // Communication
    public const string PIPE_CONSOLE = "BridgeOpsConsole";
    public const int PORT_INBOUND_DEFAULT = 61_152;
    public const int PORT_OUTBOUND_DEFAULT = 61_153;

    // Files and Folder Traversal
    public const string PATH_AGENT = "..\\..\\..\\..\\BridgeOpsAgent\\bin\\Debug\\net6.0\\";
    public const string EXE_AGENT = "BridgeOpsAgent.exe";
    public const string PATH_CONSOLE = "..\\..\\..\\..\\BridgeOpsConsole\\bin\\Debug\\net6.0\\";
    public const string EXE_CONSOLE = "BridgeOpsConsole.exe";
    public const string PATH_CONFIG_FILES = "..\\..\\..\\..\\BridgeOpsConsole\\bin\\Debug\\net6.0\\Config Files\\";
    public const string CONFIG_TYPE_OVERRIDES = "type-overrides.txt";
    public const string CONFIG_COLUMN_ADDITIONS = "column-additions.txt";
    public const string CONFIG_NETWORK = "network-config.txt";
    public const string CONFIG_COLUMN_RECORD = "column-record";
    public const string CONFIG_FRIENDLY_NAMES = "friendly-names.txt";   
    public const string LOG_ERROR_AGENT = "agent-error-log.txt";

    // Client/Agent Function Specifiers
    public const int CLIENT_PULL_COLUMN_RECORD = 0;
    public const int CLIENT_LOGIN = 1;
    public const int CLIENT_LOGOUT = 2;
    public const int CLIENT_PASSWORD_RESET = 3;
    public const int CLIENT_NEW_LOGIN = 4;
    public const int CLIENT_NEW_ORGANISATION = 5;
    public const int CLIENT_NEW_CONTACT = 6;
    public const int CLIENT_NEW_ASSET = 7;
    public const int CLIENT_NEW_CONFERENCE_TYPE = 8;
    public const int CLIENT_NEW_CONFERENCE = 9;
    public const int CLIENT_NEW_RESOURCE = 10;
    public const int CLIENT_UPDATE_LOGIN = 11;
    public const int CLIENT_UPDATE_ORGANISATION = 12;
    public const int CLIENT_UPDATE_CONTACT = 13;
    public const int CLIENT_UPDATE_ASSET = 14;
    public const int CLIENT_UPDATE_CONFERENCE_TYPE = 15;
    public const int CLIENT_UPDATE_CONFERENCE = 16;
    public const int CLIENT_UPDATE_RESOURCE = 17;
    public const int CLIENT_SELECT_COLUMN_PRIMARY = 30;
    public const int CLIENT_SELECT = 31;
    public const int CLIENT_SELECT_WIDE = 32;
    public const int CLIENT_DELETE = 33;
    public const int CLIENT_LINK_CONTACT = 40;
    public const int CLIENT_LINKED_CONTACT_SELECT = 41;
    public const int CLIENT_SELECT_HISTORY = 42;
    public const int CLIENT_SELECT_HISTORICAL_RECORD = 43;


    // Console/Agent Function Specifiers
    public const int CONSOLE_CLIENT_LIST = 0;
    public const int CONSOLE_LOGOUT_USER = 1;

    // Operational
    public const int CLIENT_SESSION_INVALID = 0;
    public const int CLIENT_INSUFFICIENT_PERMISSIONS = 1;
    public const int CLIENT_REQUEST_FAILED = 2;
    public const int CLIENT_REQUEST_SUCCESS = 3;
    public const int CLIENT_REQUEST_SUCCESS_MORE_TO_FOLLOW = 4;
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
    public const int PERMISSION_CONFERENCE_TYPES = 3;
    public const int PERMISSION_REPORTS = 4;
    public const int PERMISSION_USER_ACC_MGMT = 5;

    public const string CLIENT_LOGOUT_ACCEPT = "Safe travels";

    public const string CLIENT_LOGOUT_SESSION_INVALID = "Session invalid";
    

    // Network Config
    public const int NETWORK_SETTINGS_LENGTH = 15; // Increase if longer settings names are used in network-config.txt
    public const string NETWORK_SETTINGS_PORT_INBOUND = "inbound port:  ";
    public const string NETWORK_SETTINGS_PORT_OUTBOUND = "outbound port: ";


    public const string TEXT = "TEXT";
    public const string INT = "INT";
    public const string SMALLINT = "SMALLINT";
    public const string TINYINT = "TINYINT";

    // Column Names
    public static class Tab
    {
        public const string ORGANISATION_ID = "Organisation_ID";
        public const string PARENT_ID = "Parent_ID";
        public const string DIAL_NO = "Dial_No";

        public const string CHANGE_ID = "Change_ID";
        public const string CHANGE_TIME = "Time";
        public const string CHANGE_REASON = "Reason";
        public const string CHANGE_REGISTER_SUFFIX = "_Register";

        public const string CONTACT_ID = "Contact_ID";

        public const string ASSET_ID = "Asset_ID";

        public const string CONFERENCE_TYPE_ID = "Type_ID";
        public const string ORGANISATION_RESOURCE_ROW = "Resource_Row";
        public const string CONFERENCE_TYPE_NAME = "Type_Name";
        public const string CONFERENCE_ID = "Conference_ID";
        public const string CONFERENCE_TYPE = "Type_ID";
        public const string CONFERENCE_TITLE = "Title";
        public const string CONFERENCE_START = "Start_Time";
        public const string CONFERENCE_END = "End_Time";
        public const string CONFERENCE_BUFFER = "Buffer";

        public const string RESOURCE_ID = "Resource_ID";
        public const string RESOURCE_NAME = "Resource_Name";
        public const string RESOURCE_FROM = "Available_From";
        public const string RESOURCE_TO = "Available_To";
        public const string RESOURCE_CAPACITY = "Capacity";

        public const string RECURRENCE_ID = "Recurrence_ID";

        public const string LOGIN_ID = "Login_ID";
        public const string LOGIN_USERNAME = "Username";
        public const string LOGIN_PASSWORD = "Password";
        public const string LOGIN_ADMIN = "Admin";
        public const string LOGIN_CREATE_PERMISSIONS = "Create_Permissions";
        public const string LOGIN_EDIT_PERMISSIONS = "Edit_Permissions";
        public const string LOGIN_DELETE_PERMISSIONS = "Delete_Permissions";
        public const string LOGIN_ENABLED = "Enabled";

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
    }
}