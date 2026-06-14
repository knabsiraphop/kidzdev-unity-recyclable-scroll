# KidzDev Unity Recyclable Scroll

High-performance recyclable `ScrollRect` for Unity uGUI. Displays arbitrarily
long lists by recycling a small pool of item views instead of instantiating one
GameObject per row.

> ⚠️ **Work in progress.** The package currently ships a compiling scaffold
> (Phase 1 in progress). The scroll engine methods are stubs — see the roadmap
> below.

## Install

Add via Package Manager → *Add package from git URL*, or edit
`Packages/manifest.json`:

```
https://github.com/knabsiraphop/kidzdev-unity-recyclable-scroll.git#v0.1.0
```

## Features

- Object-pooled item recycling for large data sets
- Linear (vertical/horizontal) and loop scrolling modes
- Variable item sizes via the data source
- Synchronous and (planned) asynchronous item loading

### Planned

- Addressables-native async item loading via
  [kidzdev-unity-addressables-toolkit](https://github.com/knabsiraphop/kidzdev-unity-addressables-toolkit)
  (shipped as a separate optional assembly so the core stays dependency-free).

## Roadmap

- **Phase 1** — linear layout, fixed item size, synchronous binding.
- **Phase 2** — async item loading (instantiate / bind across frames).
- **Phase 3** — variable item sizes driven by `IRecyclableDataSource.GetItemSize`.
- **Phase 4** — loop mode (infinite wrap-around scrolling).

## Usage

1. Add a `RecyclableScrollView` to your Scroll View and assign its viewport,
   content, and an item prefab whose root derives from `RecyclableScrollItem`.
2. Implement `IRecyclableDataSource` on your data owner.
3. Call `scrollView.SetDataSource(source)` then `scrollView.Refresh()`.

## Sample

Import the **Demo** sample from the Package Manager to see a basic recyclable
list wired to dummy data.

## License

MIT — see [LICENSE.md](LICENSE.md).
