# Recyclable Scroll — Demo

A basic recyclable list backed by ~1000 dummy string rows.

> ⚠️ The scroll engine is still in progress (**Phase 1**). The view's
> `SetDataSource` / `Refresh` / `ScrollToIndex` methods are stubs, so this demo
> shows the intended *wiring* — `BindItem` calls are logged rather than rendered.

## Setup

1. Create a UI **ScrollRect** (`GameObject > UI > Scroll View`).
2. Add a **RecyclableScrollView** component to the Scroll View (or its Viewport),
   and assign its **Viewport** and **Content** RectTransforms.
3. Build an item prefab whose root has a component deriving from
   **RecyclableScrollItem**, and assign it to the view's **Item Prefab** field.
4. Add the **RecyclableScrollDemo** component to a GameObject in the scene and
   assign the **RecyclableScrollView** reference.
5. Enter Play mode — the demo binds 1000 rows through the data source.

## Item views

Derive from `RecyclableScrollItem` and override `OnBind()` to refresh your
visuals when the item is recycled for a new index.
