# Lists

Demonstrates the native Zaris **list** data structure via `IZaris.Lists` (`IListClient`), using a
work-queue as the running example.

List elements are stored as raw `byte[]`, so the sample encodes and decodes with UTF-8.

## What it shows

| Section | API |
|---|---|
| Enqueue onto the tail / jump the queue at the head | `PushRightAsync`, `PushLeftAsync` |
| Length and full contents | `LengthAsync`, `RangeAsync(0, -1)` |
| Random access and in-place update | `IndexAsync`, `SetAsync` |
| Dequeue FIFO / LIFO | `PopLeftAsync`, `PopRightAsync` |
| Remove by value | `RemoveAsync` |

`left` is the head of the list, `right` is the tail. `RangeAsync(0, -1)` returns the whole list.

## Run

```bash
dotnet run
```

By default the sample connects to an in-process store (`zaris://inproc/teststore` in
`appsettings.json`), so it runs standalone with no cluster. Point `ConnectionStrings:teststore`
at a real cluster (`zaris://host:7861/store`) to run it against a deployment.
