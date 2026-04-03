# Amnezia Panel .NET

Minimal `ASP.NET Core 10` backend scaffold for an Amnezia control panel.

Current scope:
- minimal API for servers and jobs
- `EF Core 10` + PostgreSQL storage
- background sync worker
- local `amnezia-agent` for read-only AWG discovery and snapshots
- import flow for existing `amnezia-awg*` runtimes
- client lifecycle through `.NET API` and local agent:
  - create client
  - revoke / restore / delete
  - download config
  - QR code generation
- temporary compatibility mode where the current PHP UI can be kept as frontend

Planned next steps:
- wire PHP UI pages to the new API
- add backups and restore flows
- move long-running mutate operations into jobs where needed

## Local stack

`compose.yaml` defines:
- `postgres` on `5432`
- `api` on `8083`
- `agent` on `9180`

Bootstrap:

```bash
cp .env.example .env
docker compose up -d postgres agent api
```

Run API locally:

```bash
export PATH=/root/.dotnet:$PATH
dotnet run --project src/Amnezia.Panel.Api
```

Run in Docker:

```bash
docker compose up -d --build
```

## API flow

List discovered AWG runtimes from the local host:

```bash
curl http://127.0.0.1:9180/v1/runtimes/awgs
```

Import an existing runtime into the new panel database:

```bash
curl -X POST http://127.0.0.1:8083/api/servers/import-existing \
  -H 'Content-Type: application/json' \
  -d '{
    "name": "Amnesia",
    "host": "89.125.86.92",
    "containerName": "amnezia-awg2"
  }'
```

Queue a live sync for an imported server:

```bash
curl -X POST http://127.0.0.1:8083/api/servers/<server-id>/sync
```

Create a client:

```bash
curl -X POST http://127.0.0.1:8083/api/servers/<server-id>/clients \
  -H 'Content-Type: application/json' \
  -d '{
    "name": "My Laptop",
    "expiresInDays": 30
  }'
```

Download a generated config:

```bash
curl -OJ http://127.0.0.1:8083/api/clients/<client-id>/download
```

Revoke or restore a client:

```bash
curl -X POST http://127.0.0.1:8083/api/clients/<client-id>/revoke
curl -X POST http://127.0.0.1:8083/api/clients/<client-id>/restore
```
