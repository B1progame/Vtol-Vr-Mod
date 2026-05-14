# Full Feature List

`VTOL VR Workshop Profile Switcher` is a Windows desktop helper for managing local VTOL VR Mod Loader Workshop content with reusable profiles, safer switching workflows, and server-driven modpack discovery tools.

## Core Workshop Management

- Auto-detects the VTOL VR Mod Loader Workshop folder from Steam libraries
- Supports manual Workshop path override
- Scans installed Workshop items and reads local metadata when available
- Keeps Workshop folders in canonical numeric ID naming
- Detects enabled and disabled items from folder state
- Includes duplicate folder cleanup and rename safety helpers
- Supports live rescan and workshop watcher refresh

## Profiles

- Create named profiles from locally installed mods
- Save enabled and included Workshop IDs separately
- Apply profiles with one click
- Rename profiles
- Delete profiles
- Search and filter saved profiles
- Store notes with profiles
- Assign profile icons from the integrated icon library
- Open profile details for editing and review
- Toggle individual profile mods on or off without removing them
- Create server-based profiles from exposed required server mods

## Profile Import / Export

- Export a single profile as a portable JSON package
- Export multiple profiles together
- Import profile packages from other users
- Conflict handling for import:
  - Rename
  - Overwrite
  - Skip
- Preserves profile icon selection in package files

## Missing Mod Helper

- Detects missing Workshop IDs required by a selected profile
- Shows a missing mod context panel
- Opens missing mods one by one in Steam Workshop
- Copies missing IDs for manual sharing or lookup
- Supports rescan after Steam downloads complete
- Supports apply-again flow after missing items are installed

## Server Browser (Beta)

- Reads public VTOL VR lobby data through Steam
- Shows open servers by default
- Optional inclusion of password-protected servers
- Optional filter for modded servers only
- Search by server name, creator, or scenario
- Server cards open from full-card click
- Shows creator, player count, join code, access type, version, and scenario
- Shows whether a server exposes extra required mod items
- Shows scenario requirement when available
- Opens server scenarios and required items directly in Steam Workshop
- Creates a profile from exposed server-required mods
- Copies server join code
- Automatically rechecks missing server scenario and item state while server details are open
- Reuses cached lobby results when switching away from and back to the server tab

## Mod Browser UI

- Search installed mods by name or Workshop ID
- Bulk enable all
- Bulk disable all
- Add mod flow into the active profile workflow
- Full-card click on mod cards to open details
- Right-side details panel for selected mods
- Shows Workshop ID, versions, size, and update state
- Open selected mod in Steam Workshop

## Backups

- Creates a snapshot backup before profile apply
- Lists available backups
- Supports restore workflow after a bad switch

## Logs

- Operation logging
- Warning and error visibility filters
- Recent logs view
- Crash log location separation

## Update and Release Channel Tools

- Stable and beta-aware release handling
- Stable mode only checks stable releases
- Beta mode can include prereleases
- Beta mode can expose downgrade tooling
- Beta installs still require confirmation before install
- Optional automatic installer preparation flow
- Separate stable and beta installer build scripts
- Beta build window branding and sidebar badge support

## Launch and Runtime Helpers

- Launch VTOL VR in modded mode
- Launch VTOL VR in vanilla mode
- Tracks VTOL running state
- Supports SteamVR, Oculus, and OpenXR launch target preference
- Preserves mod switching workflows around launch flow
- Includes helper cleanup when needed after VTOL exits

## Performance and UX Improvements

- Virtualized mods grid with `ItemsRepeater`
- Lazy thumbnail loading
- Thumbnail decode-to-size for lower memory use
- Bounded thumbnail cache
- Bitmap disposal and release-aware thumbnail lifecycle
- Cleaner modernized navigation and list styling
- Real icon glyphs in the sidebar

## What It Does Not Do

- It does not install VTOL VR
- It does not bundle the VTOL VR Mod Loader
- It does not automatically download Workshop mods
- It does not automatically install Workshop updates
- It does not bypass Steam, Steam Workshop, DLC ownership, or mod licensing
