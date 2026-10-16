# UI and performance optimization plan (2026-10-16)

Scope: take over the working four-client Apple port, improve interface usability on touch, Siri Remote and Vision Pro, remove measurable runtime waste, then publish installation docs. Earlier stages (local progress, chapter progression, remote navigation, first tvOS build) are complete and recorded in APPLE_PORT.md, APPLE_TV.md and APPLE_PORT_HISTORY.md.

## Stage 1: Hot-path performance fixes
**Goal**: Remove redundant decoding and per-frame work identified by code audit.
**Success Criteria**: Character/background textures come from a bounded LRU cache; each BGM is read once; dialogue waiting for a click no longer re-lays out and redraws every frame; font atlases are not decoded twice at launch; CK-GAL decoding uses a lookup table; Apple TV snapshot is skipped when nothing changed; spectrum analysers only run in the music room.
**Tests**: New fixtures for the texture cache budget/eviction, CK-GAL table decoder equivalence against the old decoder, and the snapshot change detector. Existing fixtures and desktop smoke tests stay green.
**Status**: Complete (15 new fixture cases; zh-Hans IC and ja CC rendered smoke + save round trip pass on macOS. The video queue length was left at 18 because the decoder blocks on a full video queue while playback waits for one second of audio.)

## Stage 2: Touch and Vision Pro interface
**Goal**: Every core action reachable by touch with comfortable targets.
**Success Criteria**: A visible menu button and two-finger tap open the pause menu; pause menu offers Auto, Skip, Hide and Title; long-press skip works; small buttons get enlarged hit areas without changing art; backlog scrolls by drag/wheel; launcher text and buttons scale per device; Quit hidden on iOS/tvOS; both landscape orientations allowed.
**Tests**: Fixture for the hit-area expansion and long-press state; rendered GUI smoke for pause menu access.
**Status**: In Progress (code complete: 11 touch fixtures pass; rendered menu smoke covers the pause grid, Auto/Skip entries and tap-outside resume on macOS. Two-finger tap was dropped because the first finger already advances text; the visible menu button replaces it. Device layout check pending in Stage 4.)

## Stage 3: Siri Remote
**Goal**: Remote reaches Auto/Skip/Hide, scrolls the backlog, shows focus from launch, and Menu at the root returns to the Home screen.
**Success Criteria**: Engine patch exposes menu passthrough; C# enables it on root screens only.
**Tests**: Rendered remote smoke updated; physical Apple TV check.
**Status**: In Progress (engine patch regenerated against the pristine pinned source and applies cleanly; release engine exports wa2_set_menu_passthrough; macOS remote smoke passes. Physical Apple TV check pending.)

## Stage 4: Release builds and devices
**Goal**: Ship release (optimized) engine templates, release NativeAOT and app icons for macOS, iPhone/iPad/Vision Pro and Apple TV.
**Success Criteria**: Release packages validate; installed on iPhone 16 Pro Max, iPad Pro, Vision Pro and Apple TV 4K; launch, dialogue, save/load verified.
**Tests**: package_ipa validation, device launch logs, user visual confirmation.
**Status**: Not Started

## Stage 5: Documentation and publication
**Goal**: README with user installation guide; repository pushed to galhack/WhiteAlbum2_iOS after user confirmation.
**Success Criteria**: .gitignore covers credentials, Claude.md and game data; no game data, lyrics or secrets in the pushed tree or history.
**Tests**: Pre-push audit of tracked files and history.
**Status**: Not Started
