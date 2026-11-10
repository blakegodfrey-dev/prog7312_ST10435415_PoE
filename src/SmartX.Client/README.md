# Smart-X React client

The client contains Telemetry, Command Stream and Network Topology workspaces. It uses React/Vite and communicates with the .NET 10 API; SQL access stays on the server.

Use the [repository setup guide](../../README.md#setup-and-use) for complete database/API preparation and [implementation guide](../../docs/Implementation_Guide.md#5-apiui-integration-and-concurrency) for request/state behaviour. Required Node: **22.13+ in 22.x or 24+**.

From this directory in PowerShell:

```powershell
npm.cmd ci
if (-not (Test-Path .env)) { Copy-Item .env.example .env }
npm.cmd run dev -- --host localhost --port 5173 --strictPort
```

Set `VITE_API_BASE_URL=http://localhost:5075` in local `.env` and restart Vite after changes. Open **http://localhost:5173**, the API's configured CORS origin. Keep local configuration out of source commits.

```powershell
npm.cmd test
npm.cmd run test:integration
npx.cmd playwright install chromium
npm.cmd run test:browser
npm.cmd run lint
npm.cmd run build
npm.cmd audit
```

The verified counts are **28 unit / 22 integration / 13 browser** with no failures/skips. Integration/browser suites use controlled responses; browser tests start Vite on `127.0.0.1:4174` and need that port free. For the full solution, stop API/Vite/watch and run `node scripts/verify-final.mjs` from the repository root; see the [verification summary](../../docs/Verification_Summary.md).

Network graph edges/routes come from the mesh API. Selected sensor diagnostics reuse Telemetry state; Command Stream retains separate choices. Late/aborted reads cannot replace a newer selection; pending mutations retain feedback through navigation. Reload starts fresh workspace state. History times use explicit SAST conversion and equal recorded instants share a chart X coordinate. Positions are schematic and energy costs simulated.

The demo video is **pending recording after Pillar 7 work**; no recording or URL is claimed by the client tests.
