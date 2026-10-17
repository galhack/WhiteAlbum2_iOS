# v0.4.1 subtitles and release plan

User scope: integrate the supplied WA2-CHS-Subtitle-Patch on all Apple clients, update both READMEs with subtitle instructions and latest asset links, and publish v0.4.1. Preserve user README edits, resources and saves. Earlier UI/performance plan archived in build/plans/.

## Stage 1: Subtitle data and parsing
**Goal**: Bundle the separate upstream main/Special Contents YAML with provenance and notices; support upstream and legacy schemas without reflection.
**Success Criteria**: All entries load; invalid timings fail descriptively; editions and languages stay isolated; external overrides work.
**Tests**: Parser/timeline fixtures and supplied dataset metadata checks.
**Status**: Complete (all supplied entries parse; 13 new schema/timeline fixtures pass)

## Stage 2: Playback integration
**Goal**: Synchronize subtitles with SE/voice and Special Contents drama/song playback.
**Success Criteria**: Correct display/clear/seek/stop behavior; simultaneous drama and song text; controls work with touch, mouse and remote.
**Tests**: Rendered synthetic-audio integration and original-resource playback smoke.
**Status**: Complete (main captions, SC dialogue/song concurrency, pause/seek/stop and Japanese isolation rendered checks pass)

## Stage 3: Documentation and version
**Goal**: Update Chinese/English README, source/license notices and metadata to v0.4.1/build 6.
**Success Criteria**: Latest Release asset links, subtitle scope/upstream gaps and generated notices are accurate.
**Tests**: Version consistency, notices generation and link checks.
**Status**: Complete (both READMEs, latest asset links, provenance/license notices and v0.4.1/build 6 metadata updated)

## Stage 4: Build and publication
**Goal**: Build optimized iOS/tvOS/macOS packages; publish v0.4.1 with FFmpeg source, notices and SHA256 sums.
**Success Criteria**: Builds/packages pass; exported subtitle files verified; release tag/source/assets match; latest URLs resolve.
**Tests**: C#/Python suites, rendered smoke, native platform/signature checks, uploaded SHA256 comparison.
**Status**: In Progress (optimized all-platform builds running)

Publishing uses the user-authorized GALHACK environment credential without printing or persisting it. Repository write access is verified.
