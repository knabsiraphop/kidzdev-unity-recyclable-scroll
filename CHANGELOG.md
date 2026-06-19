# Changelog

All notable changes to this package are documented here.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.3.0] - 2026-06-19

### Added

- `IScrollLayout` / `LayoutMetrics`: internal layout-engine seam between the recycler and each
  layout strategy; keeps the recycler orientation-agnostic and layouts unit-testable in isolation.
- `GridLayout`: K-column grid layout (`columns > 1`) — row height is the max item size in that
  row; cells share the usable cross extent (viewport minus cross padding) evenly, separated by
  cross spacing.
- `IAsyncRecyclableDataSource`: optional interface for async item binding via
  `BindItemAsync(index, item, CancellationToken)`. Detected at runtime; in-flight binds are
  cancelled automatically when an item scrolls out of view before binding finishes.
- Loop mode (`loop` inspector field): infinite wrap-around scrolling driven by virtual-index
  arithmetic that maps any scroll offset back to real data indices.
- `columns` inspector field on `RecyclableScrollView` — set to `1` for a linear list, `>1` for
  a grid.
- `spacing` (`Vector2`) and `padding` (`RectOffset`) inspector fields — main-axis and cross-axis
  spacing/padding split per orientation.
- Owned scroll engine: `RecyclableScrollView` now drives drag, inertia, mouse-wheel, and
  elastic-edge scrolling itself without delegating to `ScrollRect.onValueChanged`. Configurable
  via `inertia`, `decelerationRate`, `elasticity`, and `scrollSensitivity` inspector fields.
- `ScrollToOffset(float mainOffset)` public method for jumping to an absolute scroll position.
- `TotalContentLength` and `ScrollPosition` read-only public properties.
- `GridLayoutTests` (EditMode) and `RecyclableScrollAsyncPlayTests` / `RecyclableScrollLoopPlayTests`
  (PlayMode) test suites.

### Changed

- Content rect is now kept viewport-sized in all modes; items are positioned at local
  scroll-relative coordinates so coordinates stay small regardless of list length.
- `LinearLayout` refactored to implement `IScrollLayout` and receive `LayoutMetrics` from the
  view instead of raw parameters.
- Demo sample replaced with a new `RecyclableScrollShowcase` scene and updated prefabs.

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
