# Changelog

All notable changes to this package are documented here.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.5.0] - 2026-07-12

### Added

- **Item kinds**: `IKindedDataSource` (extends `IRecyclableDataSource` with `GetItemKind(int index)`)
  and `IKindedItemInstantiator` (kind-aware counterpart to `IItemInstantiator`) let a single view mix
  multiple item shapes — e.g. a chat feed with text/sticker/system rows — instead of one shared
  `itemPrefab`. `RecyclableScrollView` gains a `kindPrefabs` inspector array (indexed by
  `GetItemKind`; kind `0` always resolves to `itemPrefab`, so existing non-kinded consumers are
  unaffected) and pools recycled items per kind. `RecyclableScrollItem.Kind` reports which kind an
  item was instantiated for. New `SetDataSource(IRecyclableDataSource, IKindedItemInstantiator)`
  overload assigns an explicit kind-aware instantiator; a missing `kindPrefabs` entry for a kind
  logs once and falls back to `itemPrefab` rather than hard-failing.
- **Prepend/append with scroll anchor**: `RecyclableScrollView.AppendData()` re-layouts after items
  are appended to the end of the data source without disturbing already-realized items — for
  live-appended content (e.g. new chat messages). `RecyclableScrollView.PrependData(int count)`
  re-layouts after items are inserted at the front and shifts the scroll position so already-visible
  content stays pinned on screen — no visual jump when older content (e.g. paged-in chat history) is
  loaded and prepended. `PrependData` falls back to `Refresh()` in loop mode or when there was no
  prior content to anchor against. Both are also exposed as passthroughs on
  `RecyclableScrollController` (`AppendData()`, `PrependData(int count)`).
- `RecyclableScrollKindedPlayTests` and `RecyclableScrollPrependAppendPlayTests` (PlayMode, 6 tests
  each): coverage for per-kind pooling/instantiation and for prepend/append re-layout and scroll
  anchoring.

## [0.4.1] - 2026-06-20

### Fixed

- `RecyclableScrollView.Refresh()` now has a re-entrancy guard (`_refreshing` flag with
  `try/finally`) that prevents duplicate item sets when a sizer's `OnSizeChanged` fires
  during `Canvas.ForceUpdateCanvases()`. The inner call is detected and returns immediately;
  the outer call continues to completion with the fully flushed viewport metrics.

## [0.4.0] - 2026-06-19

### Added

- `IItemInstantiator`: optional seam for custom async item instantiation (`InstantiateAsync` /
  `Destroy`). Detected automatically by `RecyclableScrollView.SetDataSource` when the data source
  implements it. Can also be passed explicitly to
  `SetDataSource(IRecyclableDataSource, IItemInstantiator)` or set independently via
  `SetInstantiator(IItemInstantiator)`.
- `RecyclableDataSource<TItem>` and `AsyncRecyclableDataSource<TItem>`: concrete generic helpers that
  wire a delegate-based data source without subclassing. Both implement `IItemInstantiator` and are
  detected automatically when an `instantiate` delegate is supplied. `AsyncRecyclableDataSource` also
  implements `IAsyncRecyclableDataSource`.
- `RecyclableScrollController` (MonoBehaviour): high-level bridge that exposes `Init`, `Clear`,
  `Refill`, `UpdateData`, `Refresh`, `ScrollToStart`, `ScrollToEnd`, and `JumpToIndex` operations.
  Optionally syncs a uGUI `Scrollbar` and fires `OnScrolledToStart` / `OnScrolledToEnd` UnityEvents.
- `RecyclableScrollView.MaxScrollPosition` read-only property: maximum valid scroll offset
  (clamp target for UI controls).
- `RecyclableScrollView.IsAtStart` / `IsAtEnd` read-only properties: edge-detection helpers used
  by `RecyclableScrollController`.
- `RecyclableScrollView.SetDataSource(IRecyclableDataSource, IItemInstantiator)` overload: assign
  data source and an explicit instantiator in a single call.
- `RecyclableScrollView.SetInstantiator(IItemInstantiator)`: update the instantiator without
  replacing the data source. Pass `null` to revert to the default `Instantiate(itemPrefab)`.
- `_pendingBinds` guard: slow async instantiators can no longer double-spawn or orphan a
  `GameObject` for the same index when the visible window is re-driven while instantiation is
  in flight.
- `RecyclableScrollLifecyclePlayTests` (PlayMode): regression suite covering `OnBind` timing (sync
  and async paths), duplicate-spawn prevention under a slow instantiator, and one-shot instantiator
  failure recovery.
- Demo sample restructured into three scenes: `01_ControllerShowcase` (controller API),
  `02_LinearScrolls` (multi-orientation list), `03_GridScrolls` (grid layouts). New item components:
  `AchievementCard`, `BadgeCard`, `ContactItem`, `GalleryCard`, `LeaderboardItem`, `NewsCard`,
  `ProductCard`, `ProfileCard`.

### Changed

- `RecyclableScrollView.SetDataSource(IRecyclableDataSource)` now auto-detects `IItemInstantiator`
  on the data source (when `IsEnabled` is true) and installs it as the active instantiator.
- Existing tests updated to cover the new `MaxScrollPosition`, `IsAtStart`, and `IsAtEnd` surface.

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
