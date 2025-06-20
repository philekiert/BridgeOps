# Bridge Manager

Bridge Manager (originally BridgeOps) is a conference, asset and task management solution developed for internal use by Akkodis.

The solution file consists of three projects:

### BridgeOpsClient
This is the user-facing application. It offers asset, task, conference and report management, among other features.

### BridgeOpsAgent
This is the server-side program responsible for receiving requests from the client and processing them, interacting with SQL Server, data migration and handling user sessions. It runs as a background process only.

### BridgeOpsConsole
The administrator will carry out most of their server-side tasks with this console application. It is responsible for database creation and controlling the agent.

## Setup

To set up your environment, install Visual Studio 2022 and clone this repository using its built-in tools. All dependencies are included in the .csproj files, and should download automatically. The project uses .NET 6.0 (SDK may need installing separately), with WPF for its window management (install as a Visual Studio workload).

Set up SQL Server with the following options applied to your instance:
- Enable Windows Authentication and SQL Server Authentication.
- Enable TCP/IP and Shared Memory protocols in the SQL Server Configuration Manager.
- Set the SQL Server service to use the local built-in account in the SQL Server Configuration Manager.
- Create a reader login for the agent to use to run user-defined SQL statements safely.
 - The server role for the login should be set to public.
 - The login must be mapped to BridgeManager (not possible until after the application has been set up).
 - The login should have only the public and db_datareader roles enabled for the BridgeManager database (not possible until after the application has been set up).

Note that Bridge Manager by default wants the SQL Server instance to be called SQLSERVER. If you called it something else, that's fine. After building the agent, you will find a file in Documents/Bridge Manager/Config Files named sql-server-name.txt. Open it, and update it with your desired server name.

## Build
*(process tested 25/04/2025)*

Once SQL Server is running, carry out the following steps to set up a basic Bridge Manager test environment:

1) Build and run BridgeOpsAgent. It may present you with an error - that's fine as long as it's built.
2) Build and run BridgeOpsConsole. You should see a message stating that the connection to SQL Server has been established. If this isn't the case, it's likely an issue with your SQL Server configuration.
3) Once it's loaded, create the database by typing ``database``, then ``create database``. During database creation, you may encounter errors. Assuming your SQL Server instance is running properly, these errors are SQL Server's, so they should give you enough to troubleshoot on.
4) Once the database has been created and you are returned to the prompt, run the agent from within the console by typing ``agent``, then ``start``.
5) Close the console (the agent will continue running in the background).
6) Build and run BridgeOpsClient.
7) Log in with 'admin':'admin'.

NB: There is a known bug in the client where it can crash while moving windows around. This is specific to the debug environment, and does not occur in the published version.

## Publish

Publish to folder only (don't use ClickOnce).

Make sure that BridgeOpsConsole and BridgeOpsAgent are published to the same directory, ideally named Bridge Manager Server. It doesn't matter which order they're published in.

## Docs

For more information on the application's operation and use, see the Word/PDF docs in the Documentation folder in BridgeOpsClient. This folder and its files are copied to the build directory when building or publishing.
