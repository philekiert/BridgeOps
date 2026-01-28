# Changelog

## 1.2.0
### Major
- Bulk reporting feature added.
### Changed
- Modified behaviour when failing to load a preset. It now falls back to the previous selection instead of ambiguously selecting the broken preset and resetting the view.

## 1.1.10
- Prevented the application from defaulting to OneDrive's Documents folder in some instances.
- Fixed a bug where the client would crash on login, resulting from a stored non-default vertical zoom while the schedule pane was hidden.

## 1.1.9
- Changed the application to prefer the local Documents folder in cases where Windows might refer the application to a sync'd OneDrive Documents folder.

## 1.1.8
- Fixed a bug preventing new blank visits and documents from being saved.

## 1.1.7
- Fixed a bug preventing conference edits from being detected properly, leaving the Save button disabled.

## 1.1.6
### Fixed
- Fixed a bug where the an organisation's task reference wasn't being recorded on initial load, leading to edit detection providing false positives.
- Fixed a bug where the Document window's Type field was using the Visit window's Type list.

## 1.1.5
### Added
- Added multi-select, update and deletion functionality to the asset and contact lists in the organisation window.
- Added edit detection and warnings about unsaved changes to the following windows:
	- Organisation
	- Asset
	- Contact
	- Conference
	- Recurrence
	- Task
	- Document
	- Visit
### Fixed
- Fixed a bug preventing organisation references from displaying in connections if the name was set to null.
- Fixed a bug preventing date pickers reverting to null when the textbox is cleared.

## 1.1.4
- Fixed a bug where the parent organisation dropdown list wasn't populating when creating an organisation from the task window.

## 1.1.3
### Added
- Added multi-line text support to added column input fields.
### Changed
- Query builder window now remembers its size.
### Fixed
- Fixed an issue where presets with special characters in their name failed to save.
- Fixed a layout issue in the query builder window.

## 1.1.2
### Added
- An option to replicate visits and documents into broken out tasks.
- An option to update all associated visits and documents, as well as the attached organisation if present, when updating a task reference.
### Changed
- Task window size is now remembered.
### Fixed
- Visit & document task references are now updated with the new reference if the task is broken out.
- Fixed a bug where you could cause the application to crash by removing a task breakout row.

## 1.1.1
### Changed
- Unique key violation errors are now given in plain English.
### Fixed
- Bug fixed where visit & document type fields were being saved as empty strings rather than null.
- Visit & document type constraint names corrected in the database creation code.

## 1.1.0
### Major
- Task management features added.
### Minor
#### Changed
- Improved the time field UX.
#### Fixed
- Stopped the date field from being focusable when hidden in connections.
- Fixed a bug preventing conference updates from the query builder window.
- Conference duplications now omit the closure field as intended.
- Fixed a bug causing the program to crash when exporting data from the query builder if there was an invalid Excel tab name.
- Fixed a bug where the data pane's update context menu button was disabled if the user lacked delete permissions.
  
## 1.0.2
### Changed
- The application now remembers the Recurrence window size.
- The schedule view now remembers its vertical zoom amount between sessions.
### Fixed
- Resolved a bug preventing some user settings from loading in some situations.
- Minor changes to layout for consistency.
  
## 1.0.1
### Fixed
  - Resolved a crash that occurred on most conference searches.
  
## 1.0.0
### Added
- Client
  - Day of week filtering on conference searches in the database pane.
  - Conference dial number searches in the data pane now also bring up results for unmanaged connections.
  - The ability to hide, show or reorder resources.
  - Added a Help menu to the title bar with an About page and links to docs.
### Changed
- Server
  - Ordered the command lists in the console help summaries more sensibly.
- Client
  - Included recurrence notes in data pane search results.
  - Clarified some button names in the Select Query Builder window.
### Fixed
- Client
  - Hosts are now correctly reported in the Adjust Connections and Set Host windows.
  - Text values inserted as parameters in select statements are now enclosed in single quotes as they should have been previously.
  - Fixed a bug preventing conference notes from being explicitly searched.
  - Fixed a conference duplication bug where clash and overflow detection was happening on the conference being duplicated rather than the duplication.
  - Added the ConferenceAdditional view to the join options in the Select Query Builder.
  - The dialog windows for linking records, conferences to recurrences or setting conference hosts now display the correct title for each.
  - The recurrence title field now wraps text as intended.
  - The conference and recurrence title and notes fields now set their max lengths correctly according to their column restrictions in the database.
  - Corrected the default name of new headers in the column order settings from "New Column" to "New Header".
- Server
  - Fixed multiple bugs regarding alterations to core columns.
  - Corrected max VARCHAR and CHAR length in the database creation functions from 65535 to 8000.
  - Corrected some command descriptions in the console.
  
  
## 0.6.7
### Fixed
- Fixed issue where sites could not be selected for removal in the Adjust Connections window.
  
  
## 0.6.6
### Added
- Added a feature for historical searches on specific columns in addition to the current historical wide search.
### Changed
- Reworked the search bar in the data pane to make wide, narrow and historical search options more clear.
### Fixed
- User Settings windows no longer read "New User" in the title bar.
- The Settings window now correctly reports whether users accounts are enabled or disabled.
  
  
## 0.6.5
### Fixed
- Fixed an issue where the application was crashing after closing some windows.
- Fixed a bug preventing conference IDs from being searched in the data pane.
  
  
## 0.6.4
### Added
- The ability to add a conference to an existing recurrence from the conference booking window.
- Added the following search options:
  - Conference by conference ID
  - Recurrence by organisation information such as dial number or reference
  - Recurrence by conference ID
  - Recurrence by recurrence ID
- A logout warning on closing the application when there are other windows open other than the main window.
- Conference searches in the data pane will now also display user-added columns.
- A tool has been added to both the client and console application to enable the admin to remotely close client applications.
### Changed
- Switching to a dialog window or a window that is the parent of a dialog window now re-focuses the whole tree in sequence.
- Added a very mild background to the schedule view to stop overlayed windows blending into it so much.
- Removed the restriction against making recurrings in the past, as this makes some fixes impossible.
### Fixed
- Disallowed renaming of recurrence columns, as this stopped some application functions working properly.
- Columns in the recurrence window can now be hidden.
- Conference search results are now sorted by start date and time.
- Ctrl-clicking in multi-select data tables now selects multiple items.
  
  
## 0.6.3
### Added
- Client
  - An 8px drag threshold to prevent accidental moves and resizes while selecting conferences or double clicking.
  - Custom syntax for inserting user-friendly parameters into SQL statements.
  - A 'Run All Pages' button on the query builder to run all pages simultaneously.
  - A dedicated button for duplicating a page in the select query builder.
  - Conference searches in the database and recurrence views now include the resource name and row.
  - Added new icons to icon set.
  - A dedicated colour for cancelled conferences, currently pink.
### Changed
- Client
  - Reworked the button layout in the select query builder window.
- Agent
  - Conference moves now also move the connection dates if the move exceeds one day.
### Fixed
- Client
  - Fixed the horizontal positioning of date inputs in the update window.
  - Fixed an issue where the title bar was not clickable in the bottom 5 pixels of non-resizable window title bars.
  - Stopped the schedule clash and overflow overlays appearing when the main window is in the background.
  - Stopped the query builder window hiding when viewing a conference opened from one of its data tables.
  - The application no longer crashes when attempting to duplicate a select statement page.
  - Selection count is now reported correctly in the query builder output table.
  - Loading query builder presets now correctly updates the tab name textbox.
  - Resolved an issue where connection dates couldn't be set if they differed from a single-day conference's date.
  - Fixed a bug where date pickers were expecting typed-in dates in the American format of mm/dd/yyyy.
- Agent
  - Fixed a bug that would sometimes prevent updates on organisations that were in multiple conferences.
  
  
## 0.6.2
### Added
- Skip buttons for weeks and months in addition to days.
- Light grey shading for for days that fall on weekends.
- A dedicated button for adding a code page to the query builder window.
- The application now remembers the desired conference window size.
### Changed
- Fresh icon set for the schedule view and some other elements.
- The dial clash warning regions are now hidden behind the D key.
- Increased the maximum zoom out value in the scheduler.
### Fixed
- Fixed error where row clashes were not resolving themselves when making conference time adjustments using the 'Adjust Time' tool.
- Fixed a bug preventing the select data button in the schedule view from working correctly in some cases.
  
  
## 0.6.1
### Added
- A function to launch an RPT exporter with the name "./RPT Exporter/RPT Exporter.exe", relative to Bridge Manager's executable.
- The user may hold shift while clicking the forward/backward buttons in the schedule view to skip by weeks rather than days.
### Changed
- The 'go-to conference' feature now scrolls vertically to the conference as well as horizontally.
### Fixed
- Conference adjustments using the 'Adjust Time' or 'Adjust Connections' menus now record the editing user.
  
  
## 0.6.0
### Added
- Conference scheduling now enabled for users.
- Users can now query all necessary tables from the query builder.
- Added a feature to enable typing in a completely custom SQL statement in addition to the select query builder.
- Added syntax highlighting to all the SQL code spaces.
### Changed
- Time entry fields now display as a single textbox rather than two to improve usability.
### Fixed
- Number entry fields now correctly disable their increment/decrement buttons if the user lacks edit permissions.
- Data table rows will no longer expand to fit multi-line contents.
- Fixed != operator not working for fields with dropdown lists in the select query builder.
- The program will no longer crash if you attempt to export multiple query pages to a spreadsheet with identical tab names.
- Date fields will now correctly display without the time of 00:00 in the query builder window.
- AND/OR selection now saves and loads in presets instead of reverting to their default state of 'AND'.
  
  
## 0.5.4
### Added
- Each user's database view pane configuration is now persistent across sessions.
- Added "!=" and "IS NOT" operators to the select query builder.
- Holding Ctrl while clicking the add tab button in the select query builder now clones the current tab.
- The select builder now makes use of VARCHAR columns' allow lists for conditions where either the = or the != operators are being used.
### Adjusted
- Boolean values in data tables now display as "Yes" or "No" in line with the rest of the application, rather than "True" or "False".
- Amended double-clicking on the query builder output table to also load records based on change table IDs.
- When adding existing assets or contacts to an organisation on the Assets & Contacts tab, records that are already linked are now hidden.
### Fixed
- The default ports in the network settings are now the correct way around.
- Removed the redundant first set of brackets from automatically nested conditions generated by the query builder.
- Increased the double-clickable area of the maximised titlebar to the top of the screen. Same for clicking minimise, maximise or close.
  
  
## 0.5.3
### Added
- Added a button to the asset window to open the currently selected organisation reference.
- Added an icon.
### Adjusted
- Record windows slightly restructured for improved window positioning and better visibility of their data tables.
- Uncapped the maximum width for columns in data tables.
- Client network settings are now stored in "Documents/Bridge Manager" rather than Windows' built-in settings to allow the network config to carry across between versions.
### Fixed
- Stopped the program crashing when trying to clear selection from data tables where only single-row selection is enabled.
- DateTime objects now display correctly in the query builder results table.
- Changing the organisation reference in an asset now correctly un-greys out the save button when editing.
  
  
## 0.5.2
### Added
- Selection count now displayed in the search result status bars.
- Push Esc to clear selection in any data table.
### Fixed
- Context menus no longer display on blank data tables.
- Organisation and asset references are now selectable without edit permissions.
  
  
## 0.5.1
### Added
- Status bars added to the database search area to summarise search results.
### Adjusted
- Organisation name field expanded in the organisation window.
- Organisation and asset references no longer locked behind full admin rights for editing.
- Minor visual improvements.
### Fixed
- Conference type permissions removed from the user account settings menu as these are now redundant.
- Fixed a bug causing historical searches to return erroneous results.
  
  
## 0.5.0
- Initial release of the application.
