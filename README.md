# KidzDev Unity Recyclable Scroll

High-performance recyclable scroll view for Unity uGUI. Displays arbitrarily long lists by recycling a small pool of item views — only a viewport-worth of GameObjects ever exist regardless of data-set size.

## Install

Add via Package Manager → *Add package from git URL*, or edit `Packages/manifest.json`:

```
https://github.com/knabsiraphop/kidzdev-unity-recyclable-scroll.git#v0.4.0
```

**Dependency**: [UniTask](https://openupm.com/packages/com.cysharp.unitask/) via OpenUPM.

## Features

- **Owned scroll engine** — no `ScrollRect` dependency; `RecyclableScrollView` drives drag, inertia, mouse-wheel, and elastic-edge overscroll itself via pointer event interfaces.
- **Linear and grid layouts** — single-column/row lists or K-column grids; set `columns` in the inspector.
- **Vertical and horizontal orientations.**
- **Variable item sizes** — each item can report its own main-axis size via `IRecyclableDataSource.GetItemSize`.
- **Loop mode** — infinite wrap-around scrolling via virtual-index arithmetic; no content duplication.
- **Async binding** — implement `IAsyncRecyclableDataSource` to bind items across frame boundaries (e.g. load a texture); in-flight binds are cancelled automatically when an item scrolls out of view.
- **Custom instantiation seam** — `IItemInstantiator` replaces the default `Instantiate(prefab)` call with any async strategy: Addressables, `Resources.LoadAsync`, a custom object pool, etc.
- **Windowed content rect** — content stays viewport-sized; items carry the scroll offset, so coordinates stay small regardless of list length.

## Quick start

1. Add a `RecyclableScrollView` component to a UI object. Assign its **Viewport** and **Content** `RectTransform` fields, and set an **Item Prefab** whose root derives from `RecyclableScrollItem`.
2. Add a raycast-target `Graphic` (e.g. an `Image`) somewhere over the viewport so the component can receive pointer events.
3. Create a data source and assign it:

```csharp
// Synchronous — uniform size
var source = new RecyclableDataSource<MyItemView>(
    count:    items.Count,
    itemSize: 80f,
    bind:     (view, index) => view.SetData(items[index])
);
scrollView.SetDataSource(source);
```

```csharp
// Async bind — cancels in-flight work when items scroll out
var source = new AsyncRecyclableDataSource<MyItemView>(
    count:    items.Count,
    itemSize: 80f,
    bind:     async (view, index, ct) =>
    {
        var sprite = await Addressables.LoadAssetAsync<Sprite>(items[index].key).WithCancellation(ct);
        view.SetIcon(sprite);
    }
);
scrollView.SetDataSource(source);
```

```csharp
// Custom instantiator (e.g. Addressables prefab)
var source = new RecyclableDataSource<MyItemView>(
    count:    items.Count,
    itemSize: 80f,
    bind:     (view, index) => view.SetData(items[index]),
    instantiate: ct => Addressables.InstantiateAsync(prefabRef).WithCancellation(ct)
                           .ContinueWith(go => go.GetComponent<MyItemView>()),
    destroy:  go => Addressables.ReleaseInstance(go)
);
scrollView.SetDataSource(source);
```

Alternatively, implement `IRecyclableDataSource` (and optionally `IAsyncRecyclableDataSource`) directly on your own class.

## Inspector reference

| Field | Description |
|---|---|
| `orientation` | Vertical or Horizontal scroll direction |
| `columns` | `1` = linear list, `>1` = grid |
| `loop` | Infinite wrap-around scrolling |
| `spacing` | Gap between items: X = horizontal, Y = vertical |
| `padding` | Inner edge padding (pixels) |
| `buffer` | Extra items kept realized on each side of the viewport |
| `inertia` | Keep moving after a flick |
| `decelerationRate` | Velocity falloff per second (uGUI default 0.135) |
| `elasticity` | Rubber-band stiffness at edges; 0 = hard stop |
| `scrollSensitivity` | Pixels per mouse-wheel notch |

## API

```csharp
scrollView.SetDataSource(IRecyclableDataSource source);
scrollView.SetDataSource(IRecyclableDataSource source, IItemInstantiator instantiator);
scrollView.SetInstantiator(IItemInstantiator instantiator);
scrollView.Refresh();                    // rebuild from current data source
scrollView.ScrollToIndex(int index);     // align viewport to item
scrollView.ScrollToOffset(float offset); // jump to absolute main-axis offset

float scrollView.TotalContentLength;     // full virtual length of the data set
float scrollView.ScrollPosition;         // current scroll offset
```

## Interfaces

| Interface | Purpose |
|---|---|
| `IRecyclableDataSource` | Item count, sync bind, per-index size |
| `IAsyncRecyclableDataSource` | Extends `IRecyclableDataSource` with an async bind path |
| `IItemInstantiator` | Custom async instantiation and destruction |

`RecyclableDataSource<TItem>` and `AsyncRecyclableDataSource<TItem>` are concrete helpers that cover the common delegate-based pattern without subclassing.

## Sample

Import the **Demo** sample from the Package Manager to see a live list and grid wired to dummy data.

## License

MIT — see [LICENSE.md](LICENSE.md).
