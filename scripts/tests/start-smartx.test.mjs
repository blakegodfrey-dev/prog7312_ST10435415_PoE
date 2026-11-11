import assert from 'node:assert/strict';
import { test } from 'node:test';
import { execFile } from 'node:child_process';
import { createServer } from 'node:net';
import { mkdtemp, rm } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { promisify } from 'node:util';
import { LocalSession, serve } from '../lib/local-session.mjs';
import { connectionServer, freePort } from '../start-smartx.mjs';

const execute = promisify(execFile);
const fixture = fileURLToPath(new URL('fixtures/local-server.mjs', import.meta.url));
const launcher = fileURLToPath(new URL('../start-smartx.mjs', import.meta.url));
const listen = () => new Promise(resolve => {
  const server = createServer();
  server.listen(0, '127.0.0.1', () => resolve(server));
});
const close = server => new Promise(resolve => server.close(resolve));
async function availablePort() {
  const server = await listen(); const port = server.address().port;
  await close(server); return port;
}
function service(label, port, mode = 'ok') {
  return { label, executable: process.execPath, args: [fixture, mode, String(port)],
    url: `http://127.0.0.1:${port}`, healthy: async r => r.ok && (await r.json()).status === 'Healthy' };
}

test('SQL server selection respects common aliases/quotes without interpreting passwords', () => {
  assert.equal(connectionServer('Server=(localdb)\\MSSQLLocalDB;Database=Existing;Password=not-a-real-secret'), '(localdb)\\MSSQLLocalDB');
  assert.equal(connectionServer('Data Source="sql.example,1433";Database=Existing'), 'sql.example,1433');
  assert.equal(connectionServer("Addr='sql-host';Integrated Security=true"), 'sql-host');
  assert.equal(connectionServer('Database=MissingServer'), '');
});

test('An occupied port is rejected without terminating its unrelated listener', async () => {
  const server = await listen();
  try {
    await assert.rejects(freePort(server.address().port), /launcher will not stop another process/);
    assert.equal(server.listening, true);
  } finally { await close(server); }
});

test('A failed preparation command reports its exit code and stops the startup sequence', async () => {
  const session = new LocalSession({ output: () => {} });
  try { await assert.rejects(session.run('prepare', process.execPath, [fixture, 'fail', '0']), /exit 7.*Later startup steps were not run/); }
  finally { await session.stop(); }
});

test('Two real child servers must be healthy before READY, then cancellation releases both ports', async () => {
  const ports = [await availablePort(), await availablePort()];
  const controller = new AbortController();
  const session = new LocalSession({ signal: controller.signal, output: () => {} });
  let ready = 0;
  try {
    await serve(session, ports.map((p, i) => service(i ? 'UI' : 'API', p)), {
      signal: controller.signal, timeoutMs: 5000,
      onReady: () => { ready++; controller.abort(); },
    });
  } finally { await session.stop(); }
  assert.equal(ready, 1);
  for (const port of ports) await freePort(port);
});

test('A failed UI child prevents READY and the owned API is cleaned up', async () => {
  const apiPort = await availablePort(); const uiPort = await availablePort();
  const controller = new AbortController();
  const session = new LocalSession({ signal: controller.signal, output: () => {} });
  let ready = false;
  try {
    await assert.rejects(serve(session, [service('API', apiPort), service('UI', uiPort, 'fail')], {
      signal: controller.signal, timeoutMs: 5000, onReady: () => { ready = true; },
    }), /UI exited before becoming ready/);
  } finally { await session.stop(); }
  assert.equal(ready, false);
  await freePort(apiPort); await freePort(uiPort);
});

test('Cancelling during API readiness never launches the UI and releases the API port', async () => {
  const apiPort = await availablePort(); const uiPort = await availablePort();
  const controller = new AbortController(); const lines = [];
  const session = new LocalSession({ signal: controller.signal, output: line => {
    lines.push(line);
    if (line.startsWith('[API] PID:')) controller.abort();
  } });
  try {
    await assert.rejects(serve(session, [service('API', apiPort, 'unready'), service('UI', uiPort)], {
      signal: controller.signal, timeoutMs: 5000,
    }), error => error.name === 'AbortError');
  } finally { await session.stop(); }
  assert.equal(lines.some(l => l.startsWith('[UI]')), false);
  await freePort(apiPort); await freePort(uiPort);
});

test('Cancelling an in-flight preparation command leaves no owned listener behind', async () => {
  const port = await availablePort();
  const controller = new AbortController();
  const session = new LocalSession({ signal: controller.signal, output: line => {
    if (line.includes('PID:')) controller.abort();
  } });
  try { await assert.rejects(session.run('prepare', process.execPath, [fixture, 'ok', String(port)]), e => e.name === 'AbortError'); }
  finally { await session.stop(); }
  await freePort(port);
});

test('API loss during UI startup is detected before READY and both owned ports are released', async () => {
  const apiPort = await availablePort(); const uiPort = await availablePort();
  const controller = new AbortController(); let stopRequest;
  const session = new LocalSession({ signal: controller.signal, output: line => {
    if (line.startsWith('[UI] PID:')) stopRequest = fetch(`http://127.0.0.1:${apiPort}/exit`);
  } });
  let ready = false;
  try {
    await assert.rejects(serve(session, [service('API', apiPort), service('UI', uiPort, 'unready')], {
      signal: controller.signal, timeoutMs: 5000, onReady: () => { ready = true; },
    }), /API exited before becoming ready/);
    await stopRequest;
  } finally { await session.stop(); }
  assert.equal(ready, false);
  await freePort(apiPort); await freePort(uiPort);
});

test('CLI preflight fails clearly when the SDK is absent, without starting services', async () => {
  const empty = await mkdtemp(join(tmpdir(), 'smartx-empty-path-'));
  try {
    await assert.rejects(execute(process.execPath, [launcher, '--check'], {
      env: { ...process.env, PATH: empty, Path: empty }, timeout: 5000,
    }), error => error.code === 1 && error.stderr.includes('.NET SDK was not found on PATH'));
  } finally { await rm(empty, { recursive: true, force: true }); }
});
