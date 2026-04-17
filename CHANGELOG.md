# Changelog

## [1.3.0] - 2026-04-17

### Added
- Pattern detection infrastructure with IPatternDetector interface
- Pattern detectors for different artifact types (Dump, GcDump, Trace, Counters, Stacks)
- Live integration tests via MemoryPressureApp
- Heap parser timeout regression test
- Separate CI job for live integration tests (Linux only, master/release branches only)

### Changed
- Refactored SummaryTools.cs (1421 lines) into modular components:
  - PatternDetectionTools.cs - DetectPatterns tool
  - HeapAnalysisTools.cs - AnalyzeHeap and FindRetainerPaths tools
  - ArtifactContentAnalyzer.cs - helper class for artifact content analysis
- Updated DI registration to include new pattern detectors and analysis tools
- CI configuration to filter out Live tests from main test job
- Heap adapters rework (completed in 3075a6d)
- ReverseBFS fix in DominatorTreeCalculator

### Fixed
- SummaryTools.cs reduced from 1421 lines to 61 lines
- Process killing on heap parser timeout properly tested

### Technical Details
- All pattern detection logic extracted into dedicated detector classes
- Live tests marked with [Trait("Category", "Live")]
- CI split ensures live tests don't run on every PR to avoid flakiness
