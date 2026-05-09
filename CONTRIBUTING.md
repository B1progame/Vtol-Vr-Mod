# Contributing

Thanks for helping improve VTOL VR Workshop Profile Switcher.

## Good First Contributions

- Bug reports with clear steps to reproduce.
- Small UI and wording improvements.
- Safer handling for edge cases around Workshop folders, profiles, backups, and imports.
- Documentation updates for installation, troubleshooting, and release notes.

## Development Setup

Requirements:

- Windows
- .NET 8 SDK
- Steam with VTOL VR and VTOL VR Mod Loader installed

Build and run:

```powershell
dotnet restore .\VTOLVRWorkshopProfileSwitcher.sln
dotnet build .\VTOLVRWorkshopProfileSwitcher.sln -c Release
dotnet run --project .\src\VTOLVRWorkshopProfileSwitcher\VTOLVRWorkshopProfileSwitcher.csproj
```

## Pull Request Guidelines

- Keep changes focused and easy to review.
- Avoid bundling VTOL VR, VTOL VR Mod Loader, Steam Workshop mod files, or third-party assets without permission.
- Test profile apply, snapshot restore, import/export, and missing-mod flows when your change touches those areas.
- Include screenshots for visible UI changes.

## Project Boundaries

This project is a helper/profile manager for locally installed VTOL VR Mod Loader Workshop content. It should not download, unlock, redistribute, or replace VTOL VR, VTOL VR Mod Loader, or Workshop mods.
