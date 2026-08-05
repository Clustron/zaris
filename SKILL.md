---
name: zaris-install
description: Install and set up Clustron Zaris in the user's environment — in-process (.NET), Docker Compose, Kubernetes (Helm), or Windows. Detects the environment, runs the correct path, and verifies the install by connecting to the store.
license: Apache-2.0
homepage: https://clustron.io
---

# Install Clustron Zaris

Your job: get a working Clustron Zaris store running in the user's environment and prove it works by connecting to it. Zaris is a distributed key-value + coordination platform. The same client API works in every deployment mode, so pick the mode that fits where the user wants to run it.

## Step 0 — Pick the path (do this first)

Detect the environment; ask only if it's genuinely ambiguous. Choose ONE:

- **In-process (.NET)** — the user has a .NET app and wants Zaris embedded with no infrastructure. Best for dev/test and single-instance apps. Look for a `.csproj`.
- **Docker Compose** — the user has Docker and wants a real multi-node cluster on their machine in one command. Best default for "just let me try a cluster." Check `docker version`.
- **Kubernetes (Helm)** — the user has `kubectl` + `helm` and a cluster context. Best for deploying to a real cluster. Check `kubectl config current-context` and `helm version`.
- **Windows installer** — the user is on Windows and wants a local Management Service + Web Console + PowerShell modules on the host (not in a container). Check `$IsWindows` / the OS.

State which path you chose and why, in one line, before running anything.

## Ports (reference)

management `7801` · health `7802` · cluster base `7811` · client base `7861` · web console `7810` · external TLS (advertised) `7863`.

---

## Path A — In-process (.NET)

No infrastructure. Add the SDK and run Zaris inside the app process.

```bash
dotnet add package Clustron.Zaris.SDK
```

In-process is just a connection string — `zaris://inproc/demo` — registered the same way as a cluster; only the string differs. Put it in `appsettings.json`:

```json
{ "ConnectionStrings": { "demo": "zaris://inproc/demo" } }
```

Wire it up (adjust to the user's DI/host style):

```csharp
using Clustron.Zaris.Client.DependencyInjection;

var services = new ServiceCollection();
services.AddClustronZaris(configuration, "demo");  // reads ConnectionStrings:demo
var sp = services.BuildServiceProvider();

var provider = sp.GetRequiredService<IZarisClientProvider>();
var store = await provider.GetAsync("demo");

await store.PutAsync("greeting", "hello zaris");
var value = await store.GetAsync<string>("greeting");   // -> "hello zaris"
```

**Verify:** the round-tripped value equals what was put. Done.

---

## Path B — Docker Compose (multi-node cluster on this machine)

Brings up a 3-node cluster + Management Service + Web Console, and auto-creates a store named `demo`.

```bash
curl -fL https://clustron.io/quick-start.yml -o quick-start.yml
docker compose -f quick-start.yml up -d
```

Wait for the nodes to report healthy, then:

- **Web Console:** open http://localhost:7810 — the `demo` store should be listed and green.
- **Connect an app on the host:** `zaris://localhost:7861/demo`
- **Connect from another container on the same compose network:** `zaris://zaris-0:7861,zaris-1:7861,zaris-2:7861/demo`

**Verify:** in the Web Console the store shows 3 nodes / Stable, OR connect a client and do a put/get on the `demo` store.

**Tear down** (only if the user asks): `docker compose -f quick-start.yml down -v` (the `-v` deletes data — confirm first).

> If `https://clustron.io/quick-start.yml` is not reachable yet, use the `compose/quick-start.yml` file from the Zaris release bundle instead.

---

## Path C — Kubernetes (Helm)

Deploys a StatefulSet-backed cluster. The chart serves a store whose name **equals the cluster id** (`zaris-k8s` by default) — there is **no separate store-creation step**.

```bash
helm install zaris oci://registry-1.docker.io/clustron/zaris \
  --namespace zaris --create-namespace \
  --set node.replicas=3 --set node.replicationFactor=2

kubectl -n zaris rollout status statefulset/zaris
```

- **Connect from inside the cluster:** `zaris://zaris-client.zaris.svc.cluster.local:7861/zaris-k8s`
- **Connect from outside the cluster:** requires the external listener + a CA; use TLS on the advertised port: `zariss://<external-host>:7863/zaris-k8s?ca=/path/to/ca.pem`. See the Deployment → Kubernetes and Security docs for enabling external access.

**Verify:** `kubectl -n zaris get pods` shows all node pods Ready, and `rollout status` returned success.

**Tear down** (only if asked): `helm uninstall zaris -n zaris` (confirm; PVCs may persist).

> If the OCI registry path isn't published yet, install from the local chart in the release bundle: `helm install zaris ./zaris-0.1.0.tgz -n zaris --create-namespace`.

---

## Path D — Windows installer

Installs the Management Service (`:7801`) + Web Console (`:7810`) + the PowerShell modules on the Windows host.

1. Download `clustron-zaris-0.4.0-win-x64.zip` and extract it.
2. In the extracted folder, run the installer **as Administrator**: right-click `install.cmd` → **Run as administrator** (or run `.\install.cmd` from an Administrator terminal). *Do not run it from a non-elevated shell.*
3. Create a store. A workspace must be pointed at the manager and activated **before** creating a store:

   ```powershell
   # 1) Point a workspace at the local Management Service and activate it
   New-ZrWorkspace -Name local -Managers "localhost:7801" -Activate

   # 2) Create the store. BaseClusterPort/BaseClientPort are REQUIRED.
   #    BaseClientPort is the port your app connects to.
   New-ZrStore -Name orders -ReplicationFactor 2 `
     -BaseClusterPort 7811 -BaseClientPort 7861
   ```

   > Or do the same in the Web Console at http://localhost:7810 — add the manager in the UI, then create the store. No cmdlets needed.

4. **Connect:** `zaris://localhost:7861/orders`

**Verify:** `Get-ZrStore` lists `orders` as healthy, or connect a client and put/get a key.

---

## Safety rules (apply to every path)

- Only install into the environment the user asked for. Never change system/security settings, firewalls, or cluster RBAC beyond what the documented commands do.
- Never run a teardown / `down -v` / `uninstall` / delete without confirming first — those destroy data.
- Do not invent cmdlets, flags, or ports beyond what's in this skill. If something the user needs isn't covered here, point them to the docs at https://clustron.io/docs rather than guessing.
- Do not enter secrets, tokens, or CA private keys on the user's behalf — show where they go and let the user supply them.

## When you're done

Report, in a few lines: which path you used, the connection string the user's app should use, the Web Console URL if applicable, and the one command to tear it down. That's the finish line.
