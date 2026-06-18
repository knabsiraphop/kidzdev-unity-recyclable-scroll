# Changelog

All notable changes to this package are documented here.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.2.0] - 2026-06-18

### Added

- Working recycling engine in `RecyclableScrollView`: object-pooled item reuse
  driven by `ScrollRect.onValueChanged`, so only a viewport-worth of item
  GameObjects (plus a configurable `buffer`) ever exist regardless of list length.
- Vertical and horizontal orientations via `RecyclableScrollView.Orientation`.
- Per-index variable item sizes through `IRecyclableDataSource.GetItemSize`,
  backed by prefix-sum offsets and a binary-search visible-index lookup.
- `ScrollToIndex(int)` to align the viewport to a given item.
- `RecyclableScrollItem.OnBind` override hook, called after the data source binds
  a recycled item.
- `buffer` inspector field — extra items kept realized on each side of the viewport.
- EditMode + PlayMode test assemblies covering layout math and recycling behaviour.

### Changed

- `SetDataSource(...)` rebuilds the view immediately; an explicit `Refresh()` call
  is no longer required after assigning the data source.
- Demo sample now binds live `Row N` text through the reused item pool.

## [0.1.0] - 2026-06-14

### Added

- Initial scaffold: `RecyclableScrollView`, `RecyclableScrollItem`, and
  `IRecyclableDataSource` (compiling stubs), asmdef, and a Demo sample.
