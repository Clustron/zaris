# Pub/Sub Sample

Demonstrates Clustron Zaris **publish/subscribe** (`IZaris.PubSub`, `IPubSubClient`):
cluster-wide fan-out of messages to every subscriber of a channel.

## What it shows

| Step | API |
|------|-----|
| Subscribe to a channel | `PubSub.SubscribeAsync(channels, onMessage)` |
| Publish messages | `PubSub.PublishAsync(channel, byte[])` → delivery count |
| Pattern (glob) subscribe | `PubSub.PSubscribeAsync(patterns, onMessage)` |
| Unsubscribe | `await subscription.DisposeAsync()` |

Message payloads are `byte[]` — encode/decode with `Encoding.UTF8`. A
`ChannelMessage` carries `Channel`, `Pattern` (null for an exact subscribe), and
`Payload`. Delivery is asynchronous, so the sample polls briefly for the
callbacks rather than sleeping for a fixed interval.

## Run it

```bash
dotnet run
```

By default it connects to an **in-process** store (`zaris://inproc/teststore`
in `appsettings.json`), so pub/sub fans out within this single process — no
cluster required. Point `ConnectionStrings:teststore` at a real cluster
(`zaris://host:7861/store`) to fan out across nodes.
