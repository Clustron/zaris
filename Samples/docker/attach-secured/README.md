# Attach mode, secured (token + mutual TLS) — declarative

A 4-node **demostore** cluster plus a management manager that come up **already secured — token
authentication *and* mutual TLS — on the very first `docker compose up`**. No "enable security" click,
no runtime enrollment step. Security is *declared*: it lives in the node config and in mounted certificates.

This is the pattern to copy when you want a reproducible, secured cluster from `git clone` → `up`.

```
docker compose -p demostore-secure up -d
```

| | |
|---|---|
| **Deployment model** | Attach (the orchestrator owns the nodes; the manager only observes) |
| **Auth** | Keyvus tokens — every data + admin call needs a signed token |
| **Transport** | Mutual TLS on the data plane, peer verification `CaAndIdentity` |
| **Topology** | 4 nodes, replication factor 2, 2 partitions (one primary + one replica each) |
| **Console** | http://localhost:18101 |
| **Management API** | http://localhost:17801 |
| **Nodes** | localhost:17861 … 17864 |

---

## Why this is "attach-shaped"

In **supervisor** mode the manager forks and owns the node processes, so it can push a security policy
to them and restart them. In **attach** mode Docker (this compose) owns the nodes — the manager can only
*watch* them. It therefore **cannot turn on security at runtime**; trying to do so from the console reports
`0 nodes updated`.

So in attach mode security has to be **baked in declaratively**, which is exactly what this sample shows:

- **The node config** (`config/demostore.json`) turns on token auth and TLS, and pins the cluster CA as a
  trust anchor:
  ```jsonc
  "security": {
    "enabled": true,
    "issuer": "keyvus://clustron-zaris",
    "publicKeys": [ { "keyId": "…", "spkiBase64": "…" } ],   // verifies tokens
    "tls": {
      "enabled": true,
      "mode": "MutualTls",
      "trustAnchors": [ { "keyId": "cluster-ca", "pem": "-----BEGIN CERTIFICATE----- …" } ],
      "nodeCertificate": { "path": "/var/lib/clustron/certs/${CLUSTRON_NODE_ID}.pfx" }
    }
  }
  ```
- **Each node** mounts its own pre-issued **leaf certificate** at that path. On boot the node logs
  `TLS enrollment evaluated: UsedExisting` → `TLS ENABLED (data plane, server-auth)`.
- **The manager** mounts its whole **security directory** (`secrets/manager-security/`) — the fixed Keyvus
  signing key, the cluster CA, the admin user, and the enforcement flag — so it boots enforced instead of open.
- **A one-shot** (`demostore-attach`) logs in, binds the workspace, and registers the store as `tlsEnabled`
  so the manager and console themselves speak token + TLS to it.

---

## Prerequisites

**1. Images.** This sample references two images by tag and does **not** build them:

| Image | Built from (source repo) |
|---|---|
| `clustron-zaris-node:local` | `docker/Dockerfile.node` |
| `clustron-zaris-machine:local` | `docker/Dockerfile.machine` |

Build them in the source repo, e.g.:
```
docker build -t clustron-zaris-node:local    -f docker/Dockerfile.node    .
docker build -t clustron-zaris-machine:local -f docker/Dockerfile.machine .
```

**2. Secrets.** The `secrets/` directory holds **private keys** and is intentionally **not committed**.
Generate it once — see [`secrets/README.md`](secrets/README.md). Until it exists the nodes will fail to
find their leaf certs.

---

## Run it

```
docker compose -p demostore-secure up -d
docker compose -p demostore-secure ps          # all healthy; demostore-attach Exited (0)
```

Open the console at **http://localhost:18101** — the *demostore* workspace is already bound, the store is
registered, and Security shows **enabled**.

## Verify it's actually secured

```bash
# Get an admin token
TOK=$(curl -s -X POST localhost:17801/security/login \
  -H 'Content-Type: application/json' -d '{"username":"admin","password":"admin"}' \
  | grep -oE '"token":"[^"]*"' | head -1 | cut -d'"' -f4)

# A node boots with TLS on:
docker logs demostore-n0 2>&1 | grep -i "TLS ENABLED"
#   -> TLS ENABLED (data plane, server-auth) for node demostore-n0 — peer verification CaAndIdentity

# The console reads store data over TLS with the token -> 200:
curl -s "localhost:18101/admin/v1/stores/demostore/data/items?limit=3" \
  -H 'X-Zaris-Managers: demostore-manager:7801' -H "X-Zaris-Token: $TOK" \
  -o /dev/null -w "console over TLS: %{http_code}\n"

# A tokenless / plaintext client is rejected (that's the point).
```

Connecting a client by hand from the idle client box:
```
docker exec -it demostore-client bash
# then, with a token you minted above and the CA at secrets/ca.pem copied in:
#   Connect-ZrStore -StoreName demostore -Endpoints demostore-n0:7861,demostore-n1:7861 \
#                   -Token <token> -Tls -TlsCaCert /path/to/ca.pem
```

## Tear down

```
docker compose -p demostore-secure down          # keep secrets
docker compose -p demostore-secure down -v        # also drop volumes/data
```

---

## Notes

- **`TLS server handshake failed; rejecting connection` during startup is normal** — it's the nodes probing
  each other before every leaf is loaded, and it clears within a few seconds. Real clients connect fine.
- **Rotating the PKI** means regenerating `secrets/` *and* re-stamping the matching public key + CA into
  `config/demostore.json`; the two are a matched set (recipe in `secrets/README.md`).
- **Prefer self-generated PKI?** If you don't want to manage certs at all, the alternative is
  *self-enrollment* (nodes enroll against a manager-generated CA on first boot via `ZARIS_TLS_ENROLL`).
  That's simpler to run but gives you less control over the trust root — this sample deliberately shows the
  mounted-secrets model because it's what you'd use in production.
