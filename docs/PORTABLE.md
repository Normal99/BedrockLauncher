# Running without admin rights (portable)

The normal release is *framework-dependent*: it needs the .NET 8 Desktop Runtime to be installed, and the regular installer for that needs admin rights. There are two ways to avoid that.

## Option 1: Portable (self-contained) build (recommended)
A self-contained build carries its own copy of the .NET runtime in the launcher folder, so nothing has to be installed.

**Get a build**
- From GitHub Actions: open the **Build Portable** workflow, pick a successful run (or start one with **Run workflow**) and download the `BedrockLauncher-portable-win-x64` artifact.
- Or build it yourself on any machine with the .NET 8 SDK:
  ```
  BedrockLauncher\Properties\PublishProfiles\PublishPortable.bat
  ```
  or `dotnet publish BedrockLauncher/BedrockLauncher.csproj -c Release -r win-x64 --self-contained true`.
  The output ends up in `BedrockLauncher\bin\Portable\win-x64\`.

**Use it**
1. Copy the whole folder somewhere you can write to, e.g. `Documents\BedrockLauncher` or a USB stick.
2. Run `BedrockLauncher.exe`.

Only `win-x64` is supported, because the bundled native launcher libraries are 64-bit only.

## Option 2: Install .NET for your user only
You can keep using the normal release and install the .NET 8 Desktop Runtime into your own profile, which does not need admin rights. In PowerShell:

```powershell
$dotnet = "$env:LOCALAPPDATA\Microsoft\dotnet"
& ([scriptblock]::Create((Invoke-WebRequest 'https://dot.net/v1/dotnet-install.ps1' -UseBasicParsing))) -Channel 8.0 -Runtime windowsdesktop -InstallDir $dotnet
[Environment]::SetEnvironmentVariable('DOTNET_ROOT', $dotnet, 'User')
```

Sign out and back in (or restart Explorer) so the new `DOTNET_ROOT` is picked up, then run `BedrockLauncher.exe`.

If scripts are blocked on your PC, download the **.NET Desktop Runtime 8 Binaries (x64 zip)** from https://dotnet.microsoft.com/en-us/download/dotnet/8.0, extract it to `%LOCALAPPDATA%\Microsoft\dotnet` and set the `DOTNET_ROOT` user environment variable to that folder (Start → "Edit environment variables for your account").

## What a portable build can't fix
- **Developer Mode.** Installing non-GDK Minecraft versions registers the game from loose files, and Windows only allows that with Developer Mode turned on. The launcher also uses it to create the save-redirection link without admin rights. Turning Developer Mode on needs admin rights once. To check whether it's already on, run:
  ```
  reg query HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock /v AllowDevelopmentWithoutDevLicense
  ```
  `0x1` means it's on.
- **Managed PCs.** School and work PCs can block app sideloading or unknown executables through group policy (for example AppLocker). The launcher can't work around those.
