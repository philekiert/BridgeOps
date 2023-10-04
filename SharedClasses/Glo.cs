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
    public const int CLIENT_NEW_LOGIN = 3;
    public const int CLIENT_NEW_ORGANISATION = 4;
    public const int CLIENT_NEW_CONTACT = 5;
    public const int CLIENT_NEW_ASSET = 6;
    public const int CLIENT_NEW_CONFERENCE_TYPE = 7;
    public const int CLIENT_NEW_CONFERENCE = 8;
    public const int CLIENT_NEW_RESOURCE = 9;
    public const int CLIENT_SELECT_COLUMN_PRIMARY = 30;
    public const int CLIENT_SELECT_ALL = 31;

    // Console/Agent Function Specifiers
    public const int CONSOLE_CLIENT_LIST = 0;
    public const int CONSOLE_LOGOUT_USER = 1;

    // Operational
    public const int CLIENT_SESSION_INVALID = 0;
    public const int CLIENT_REQUEST_FAILED = 1;
    public const int CLIENT_REQUEST_SUCCESS = 2;
    public const string CLIENT_LOGIN_ACCEPT = "Welcome";
    public const string CLIENT_LOGIN_REJECT_USER_INVALID = "Must have been the wind";
    public const string CLIENT_LOGIN_REJECT_USER_DUPLICATE = "User already logged in"; // Currently unused by agent
    public const string CLIENT_LOGIN_REJECT_IP_DUPLICATE = "IP already connected"; // Currently unused by agent
    public const string CLIENT_LOGIN_REJECT_IP_UNKNOWN = "IP unknown";

    public const string CLIENT_LOGOUT_ACCEPT = "Safe travels";

    public const string CLIENT_LOGOUT_SESSION_INVALID = "Session invalid";
    

    // Network Config
    public const int NETWORK_SETTINGS_LENGTH = 15; // Increase if longer settings names are used in network-config.txt
    public const string NETWORK_SETTINGS_PORT_INBOUND = "inbound port:  ";
    public const string NETWORK_SETTINGS_PORT_OUTBOUND = "outbound port: ";


    // Column Names
    public static class Tab
    {
        public const string ORGANISATION_ID = "Organisation_ID";
        public const string PARENT_ID = "Parent_ID";
        public const string DIAL_NO = "Dial_No";

        public const string CONTACT_ID = "Contact_ID";

        public const string ASSET_ID = "Asset_ID";

        public const string CONFERENCE_TYPE_ID = "Type_ID";
        public const string CONFERENCE_TYPE_NAME = "Type_Name";

        public const string CONFERENCE_ID = "Conference_ID";
        public const string CONFERENCE_TYPE = "Type";
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
        public const string LOGIN_TYPE = "Type";
    }
}