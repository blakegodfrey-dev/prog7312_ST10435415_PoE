import { spawn } from 'node:child_process';
import { createInterface } from 'node:readline';
import { setTimeout as delay } from 'node:timers/promises';

// A session owns only the processes it creates. It never finds/kills by port,
// executable name, or an old PID file. There is no detached background daemon.
export class LocalSession {
  #children = [];
  constructor({ signal, output = line => console.log(line) } = {}) {
    this.signal = signal;
    this.output = output;
  }

  start(label, executable, args, { cwd, env } = {}) {
    this.signal?.throwIfAborted();
    const child = spawn(executable, args, {
      cwd, env, shell: false, windowsHide: true,
      detached: process.platform !== 'win32', stdio: ['ignore', 'pipe', 'pipe'],
    });
    const record = { child, label, finished: false };
    for (const stream of [child.stdout, child.stderr]) {
      const lines = createInterface({ input: stream });
      lines.on('line', line => this.output(`[${label}] ${line}`));
    }
    record.done = new Promise(resolve => {
      child.once('error', error => {
        record.finished = true;
        resolve({ error });
      });
      child.once('close', (code, signal) => {
        record.finished = true;
        resolve({ code, signal });
      });
    });
    this.#children.push(record);
    return record;
  }

  async run(label, executable, args, options) {
    const record = this.start(label, executable, args, options);
    const result = await Promise.race([record.done, aborted(this.signal)]);
    this.signal?.throwIfAborted();
    if (result.error) throw new Error(`${label} could not start: ${result.error.message}`);
    if (result.code !== 0) throw new Error(`${label} failed (exit ${result.code ?? result.signal}). Later startup steps were not run.`);
  }

  async stop() {
    if (process.platform === 'win32') {
      // A console Ctrl+C can reach the servers as well. Allow that shutdown
      // briefly, then terminate only still-running process trees we created.
      await delay(300);
      await Promise.all(this.#children.filter(r => !r.finished && r.child.pid).map(r =>
        new Promise(resolve => {
          const killer = spawn('taskkill.exe', ['/PID', String(r.child.pid), '/T', '/F'],
            { shell: false, windowsHide: true, stdio: 'ignore' });
          killer.once('error', () => { r.child.kill(); resolve(); });
          killer.once('close', resolve);
        })));
    } else {
      const send = signal => {
        for (const record of this.#children) {
          const { child } = record;
          if (record.finished || !child.pid) continue;
          try { process.kill(-child.pid, signal); }
          catch (error) { if (error.code !== 'ESRCH') throw error; }
        }
      };
      send('SIGTERM');
      await Promise.race([Promise.all(this.#children.map(r => r.done)), delay(1500)]);
      // Force remaining owned server groups if graceful shutdown was ignored.
      send('SIGKILL');
    }
    await Promise.race([Promise.all(this.#children.map(r => r.done)), delay(2000)]);
  }
}

function aborted(signal) {
  return signal ? new Promise(resolve => {
    if (signal.aborted) resolve({ aborted: true });
    else signal.addEventListener('abort', () => resolve({ aborted: true }), { once: true });
  }) : new Promise(() => {});
}

export async function waitForService(url, record, signal, { timeoutMs = 90000, healthy = response => response.ok, companions = [] } = {}) {
  const until = Date.now() + timeoutMs;
  while (Date.now() < until) {
    signal?.throwIfAborted();
    const stopped = [record, ...companions].find(item => item.finished);
    if (stopped) {
      const result = await stopped.done;
      throw new Error(`${stopped.label} exited before becoming ready (exit ${result.code ?? result.signal ?? 'spawn error'}). Review its output above.`);
    }
    try {
      const requestSignal = signal ? AbortSignal.any([signal, AbortSignal.timeout(1500)]) : AbortSignal.timeout(1500);
      const response = await fetch(url, { signal: requestSignal });
      if (await healthy(response)) return;
    } catch { signal?.throwIfAborted(); }
    await delay(150, undefined, { signal });
  }
  throw new Error(`${record.label} did not become ready within ${timeoutMs / 1000}s. Review its output; check SQL connectivity for API startup.`);
}

export async function serve(session, services, { signal, onReady = () => {}, timeoutMs = 90000 } = {}) {
  const running = [];
  for (const service of services) {
    const record = session.start(service.label, service.executable, service.args, service);
    running.push(record);
    await waitForService(service.url, record, signal, { timeoutMs, healthy: service.healthy, companions: running });
  }
  if (running.some(record => record.finished)) throw new Error('A server stopped during startup. READY was not reached.');
  onReady();
  const ended = await Promise.race([
    ...running.map(async record => ({ record, result: await record.done })),
    aborted(signal),
  ]);
  if (signal?.aborted) return;
  throw new Error(`${ended.record.label} stopped unexpectedly (exit ${ended.result.code ?? ended.result.signal ?? 'spawn error'}). Stopping the other owned server.`);
}
