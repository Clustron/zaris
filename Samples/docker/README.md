# Clustron Zaris — Deployment samples (Docker Compose)

Where the samples in the parent folder show how to *use* Zaris from a .NET client, these show how to
**deploy** it. Each subfolder is a self-contained `docker compose` topology you can bring up with one command,
with a README explaining what it demonstrates, how to run it, and how to verify it.

They all reference the two Zaris images by tag rather than building them:

| Image | Built from |
|---|---|
| `clustron-zaris-node:local` | `docker/Dockerfile.node` (source repo) |
| `clustron-zaris-machine:local` | `docker/Dockerfile.machine` (source repo) |

Build those once before running any sample.

## Available deployments

| Sample | Model | Security | What it shows |
|---|---|---|---|
| [`attach-secured/`](attach-secured/) | Attach | Token auth **+ mutual TLS** | A 4-node cluster + manager that come up **fully secured on the first `up`**, declaratively, via mounted PKI (no runtime "enable security" step). |

_More topologies (open attach, supervisor, single-node) will land here as sibling folders._

## Deployment models in one line

- **Attach** — the orchestrator (Compose/Kubernetes) owns the node processes; the manager only *observes*
  them. Security must be **declared** in node config + mounted certs (it can't be pushed at runtime).
- **Supervisor** — the manager forks and owns the node processes, so it *can* push policy and restart them.
