# Amnezia Panel .NET

Minimal `ASP.NET Core 10` backend scaffold for an Amnezia control panel.

Current scope:
- minimal API for servers and jobs
- `EF Core 10` + PostgreSQL model scaffold
- background sync worker
- HTTP contract for a local `amnezia-agent`
- temporary compatibility mode where the current PHP UI can be kept as frontend

Planned next steps:
- add first EF migration
- implement read-only `amnezia-agent`
- wire PHP UI pages to the new API
- move client lifecycle operations from PHP into jobs + agent
