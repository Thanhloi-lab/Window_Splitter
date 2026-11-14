# Windows Splitter Installer

This folder contains the Inno Setup definition and the Windows Splitter End User License Agreement for the Windows x64 installer. Setup displays the license before installation and copies it to the application folder. The installer requires administrator privileges, matching the application's `requireAdministrator` manifest. Setup also requires users to check an explicit administrator permission checkbox before continuing past the consent page.

## Build

Run the build script from any working directory:

```powershell
installer\build-installer.bat
```

The script publishes a self-contained x64 build and compiles the installer. It requires the .NET 8 SDK and Inno Setup 6. The installer will be written to `installer\output`. The setup requests administrator privileges to install under Program Files. If **Launch Windows Splitter** is selected on the final page, Windows starts the app with the `runas` shell verb so its administrator manifest can request elevation.

If Inno Setup is installed in a custom location, set the `INNO_SETUP_COMPILER` environment variable to the full path of `ISCC.exe` before running the batch file.

To run at sign-in with administrator privileges, launch the app, open **Settings**, and enable **Start with Windows (Administrator Privileges)**. Windows Splitter registers an interactive Task Scheduler task at the highest run level so the app can display its UI and use its window hooks in the signed-in session.

## Hotkey

Press **Win + Ctrl + B** to toggle borderless mode for the active window. Borderless mode removes the title bar and frame, covers the taskbar, and shows a brief translucent hint when enabled.
