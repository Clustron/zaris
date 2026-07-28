# secrets/ — the PKI this sample mounts (generate once, do not commit)

`docker-compose.yml` mounts this directory read-only so the cluster comes up secured on the first `up`:

| Path | What it is | Mounted into | Secret? |
|---|---|---|---|
| `manager-security/` | the manager's security dir — the **Keyvus signing key** (`keyvus-signing-key.secret`), the **cluster CA** (`tls/`), the admin user (`users.json`), RBAC policy, and `enabled.flag` | `demostore-manager:/var/lib/clustron/security` | **yes — private keys** |
| `demostore-n<0-3>.pfx` | each node's **leaf certificate** (signed by the CA) | `demostore-n<i>:/var/lib/clustron/certs/demostore-n<i>.pfx` | **yes — private keys** |
| `ca.pem` | the cluster CA **public** root, PEM — also pasted into `../config/demostore.json` (`security.tls.trustAnchors`) and handed to clients | (reference copy) | no (public) |

> ⚠ **Everything except `ca.pem` is a private key.** This directory is `.gitignore`d and must never be
> committed. What's here is throwaway **demo** material for a localhost cluster — regenerate per environment
> and never reuse it for anything real. In Kubernetes these become `Secret` objects / a secrets manager.

## Generate it (one time)

The manager mints the CA + signing key when security is first initialized, and issues a leaf per node. The
easiest way to produce a matched set is to bootstrap the cluster **open** once, secure it, then harvest.
Run from the **source repo's** `docker/` directory (it has the open attach compose):

```bash
cd docker

# 1. bring the cluster up OPEN
docker compose -f docker-compose.demostore.yml -p demostore up -d

# 2. initialize + enable security on the manager (generates the CA + signing key + admin)
CONSOLE=http://localhost:18101 ; HM='X-Zaris-Managers: demostore-manager:7801'
curl -s -X POST $CONSOLE/admin/v1/security/init   -H "$HM" -H 'Content-Type: application/json' \
     -d '{"adminSubject":"admin","adminPassword":"admin"}'
curl -s -X POST $CONSOLE/admin/v1/security/enable -H "$HM"
curl -s -X POST $CONSOLE/admin/v1/security/tls-provision -H "$HM" \
     -H "X-Zaris-Token: $(curl -s -X POST localhost:17801/security/login -H 'Content-Type: application/json' \
        -d '{"username":"admin","password":"admin"}' | grep -oE '"token":"[^"]*"' | head -1 | cut -d'"' -f4)"
#    (enroll each node — see the source repo's secure walkthrough — so a leaf is written per node)

# 3. harvest the material into THIS sample's secrets/ (adjust the destination path)
DST=/path/to/Samples/docker/attach-secured/secrets
docker cp demostore-manager:/var/lib/clustron/security/. "$DST/manager-security/"
for i in 0 1 2 3; do docker cp demostore-n$i:/var/lib/clustron/certs/demostore-n$i.pfx "$DST/demostore-n$i.pfx"; done

# 4. export the CA public root and stamp it (plus the token public key) into ../config/demostore.json:
#      security.tls.trustAnchors[0].pem   <- ca.pem
#      security.publicKeys[0]             <- the manager's /admin/v1/security/public-key {keyId, spkiBase64}
```

Then tear the open cluster down and start the secured one:

```bash
docker compose -f docker-compose.demostore.yml -p demostore down
cd /path/to/Samples/docker/attach-secured
docker compose -p demostore-secure up -d
```
