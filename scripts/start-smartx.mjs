import { execFile } from 'node:child_process';
import { access, readFile, realpath } from 'node:fs/promises';
import { createServer } from 'node:net';
import { delimiter, dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { promisify } from 'node:util';
import { LocalSession, serve } from './lib/local-session.mjs';

const execute = promisify(execFile);
const root = fileURLToPath(new URL('../', import.meta.url));
const client = join(root, 'src', 'SmartX.Client');
const api = join(root, 'src', 'SmartX.Api');
const apiProject = join(api, 'SmartX.Api.csproj');
const apiDll = join(api, 'bin', 'Release', 'net10.0', 'SmartX.Api.dll');
const vite = join(client, 'node_modules', 'vite', 'bin', 'vite.js');
const apiUrl = 'http://localhost:5075';
const clientUrl = 'http://localhost:5173';
const exists = path => access(path).then(() => true, () => false);

export async function freePort(port) {
  for (const host of ['127.0.0.1', '::1']) {
    await new Promise((resolve, reject) => {
      const server = createServer();
      server.once('error', error => {
        if (['EADDRNOTAVAIL', 'EAFNOSUPPORT'].includes(error.code)) resolve();
        else reject(new Error(`Port ${port} is unavailable. Stop your existing API/Vite session first; the launcher will not stop another process.`));
      });
      server.listen({ host, port, exclusive: true }, () => server.close(resolve));
    });
  }
}

async function findProgram(name, env = process.env) {
  for (const folder of (env.PATH ?? env.Path ?? '').split(delimiter)) {
    if (!folder) continue;
    const candidate = join(folder.replace(/^"|"$/g, ''), name);
    if (await exists(candidate)) return candidate;
  }
  return null;
}

async function npmCli() {
  const npm = await findProgram(process.platform === 'win32' ? 'npm.cmd' : 'npm');
  const candidates = [join(dirname(process.execPath), 'node_modules', 'npm', 'bin', 'npm-cli.js')];
  if (npm) {
    candidates.push(join(dirname(npm), 'node_modules', 'npm', 'bin', 'npm-cli.js'));
    // Unix/npm symlinks already resolve directly to npm-cli.js.
    const actual = await realpath(npm);
    if (actual.endsWith('npm-cli.js')) candidates.push(actual);
  }
  for (const path of candidates) if (await exists(path)) return path;
  throw new Error('npm CLI was not found beside Node or on PATH. Install Node with npm and open a new terminal.');
}

export function connectionServer(connection) {
  const match = connection.match(/(?:^|;)\s*(?:Server|Data\s+Source|Address|Addr|Network\s+Address)\s*=\s*(?:"([^"]*)"|'([^']*)'|([^;]*))/i);
  return (match?.[1] ?? match?.[2] ?? match?.[3] ?? '').trim();
}

async function checkSql(signal) {
  const configuration = JSON.parse((await readFile(join(api, 'appsettings.Development.json'), 'utf8')).replace(/^\uFEFF/, ''));
  const overridden = Object.hasOwn(process.env, 'ConnectionStrings__SmartXDatabase');
  const connection = overridden ? process.env.ConnectionStrings__SmartXDatabase : configuration.ConnectionStrings?.SmartXDatabase;
  if (!connection || !connectionServer(connection)) throw new Error('Configure ConnectionStrings__SmartXDatabase with a SQL Server connection before starting.');
  console.log(`SQL configuration: ${overridden ? 'existing environment override' : 'existing Development settings'} (connection details are not printed).`);
  const local = /^\(localdb\)\\(.+)$/i.exec(connectionServer(connection));
  if (!local) {
    console.log('Configured SQL Server will be checked by API startup.');
    return;
  }
  if (process.platform !== 'win32') throw new Error('LocalDB requires Windows. Configure ConnectionStrings__SmartXDatabase for an available SQL Server on this platform.');
  let utility = await findProgram('SqlLocalDB.exe');
  // SQL installers do not always put their tools directory on PATH.
  for (const version of ['180', '170', '160', '150', '140', '130']) {
    const candidate = join(process.env.ProgramFiles ?? 'C:\\Program Files', 'Microsoft SQL Server', version, 'Tools', 'Binn', 'SqlLocalDB.exe');
    if (!utility && await exists(candidate)) utility = candidate;
  }
  if (!utility) throw new Error('SQL Server LocalDB tools were not found. Install LocalDB or configure an existing SQL Server connection.');
  try { await execute(utility, ['info', local[1]], { signal, windowsHide: true, timeout: 15000 }); }
  catch {
    signal.throwIfAborted();
    throw new Error('The configured LocalDB instance was not found. Check it with SqlLocalDB info or configure an existing SQL Server.');
  }
  console.log('Configured LocalDB instance exists. API startup will open it; preflight does not create or delete an instance.');
}

async function main(args) {
  if (args.includes('--help')) {
    console.log('Smart-X local launcher\nUsage: .\\Start-SmartX.ps1 [-CheckOnly] [-SkipPrepare]\nOr: node scripts/start-smartx.mjs [--check] [--skip-prepare]\nDefault: check tools/configuration/ports, restore + Release build API, npm ci, start both servers.\nCheckOnly changes nothing. SkipPrepare requires an already current API build and client dependencies.\nUses existing SQL configuration. Development startup applies existing migrations/empty-database seed.\nCtrl+C stops only the servers started by this launcher; SQL Server is not stopped.');
    return;
  }
  if (args.some(arg => !['--check', '--skip-prepare'].includes(arg))) throw new Error('Unknown option. Use --help.');
  const controller = new AbortController();
  const stop = () => controller.abort();
  process.on('SIGINT', stop); process.on('SIGTERM', stop);
  const session = new LocalSession({ signal: controller.signal });
  try {
    const [major, minor] = process.versions.node.split('.').map(Number);
    if (!((major === 22 && minor >= 13) || major >= 24)) throw new Error('Use Node 22.13+ in the 22.x line or Node 24+.');
    for (const path of [join(root, 'SmartX.sln'), apiProject, join(client, 'package-lock.json')])
      if (!(await exists(path))) throw new Error('Source layout is incomplete. Copy the patch into the existing SmartX.sln repository root.');
    const dotnet = await findProgram(process.platform === 'win32' ? 'dotnet.exe' : 'dotnet');
    if (!dotnet) throw new Error('.NET SDK was not found on PATH. Install the .NET 10 SDK and open a new terminal.');
    const sdk = await execute(dotnet, ['--list-sdks'], { signal: controller.signal, timeout: 15000, windowsHide: true });
    if (!/^10\.\d+\.\d+/m.test(sdk.stdout)) throw new Error('Install the .NET 10 SDK; a runtime-only installation is insufficient.');
    const npm = await npmCli();
    await execute(process.execPath, [npm, '--version'], { signal: controller.signal, timeout: 15000, windowsHide: true });
    await checkSql(controller.signal);
    await freePort(5075); await freePort(5173);
    controller.signal.throwIfAborted();
    console.log('PASS preflight: Node/npm, .NET 10 SDK, source/configuration and local ports. SQL connectivity is verified when the API starts.');
    if (args.includes('--check')) return;
    if (!args.includes('--skip-prepare')) {
      await session.run('restore', dotnet, ['restore', apiProject], { cwd: root, env: process.env });
      await session.run('build', dotnet, ['build', apiProject, '-c', 'Release', '--no-restore', '-warnaserror'], { cwd: root, env: process.env });
      // Invoke npm through its JS CLI, so Windows paths/arguments never pass through cmd.exe.
      await session.run('npm ci', process.execPath, [npm, 'ci'], { cwd: client, env: process.env });
    }
    if (!(await exists(apiDll)) || !(await exists(vite))) throw new Error('Prepared API/client files are missing. Run again without -SkipPrepare.');
    await serve(session, [
      { label: 'API', executable: dotnet, args: [apiDll], cwd: api,
        env: { ...process.env, ASPNETCORE_ENVIRONMENT: 'Development', ASPNETCORE_URLS: apiUrl },
        url: apiUrl + '/api/health', healthy: async r => r.ok && (await r.json()).status === 'Healthy' },
      { label: 'UI', executable: process.execPath, args: [vite, '--host', 'localhost', '--port', '5173', '--strictPort'], cwd: client,
        env: { ...process.env, VITE_API_BASE_URL: apiUrl }, url: clientUrl },
    ], { signal: controller.signal, onReady: () => console.log(`\nREADY Smart-X: ${clientUrl}\nAPI: ${apiUrl}\nKeep this console open. Ctrl+C stops both owned servers.`) });
  } catch (error) {
    if (!controller.signal.aborted) throw error;
  } finally {
    await session.stop();
    process.off('SIGINT', stop); process.off('SIGTERM', stop);
  }
}

const entry = process.argv[1] && resolve(process.argv[1]);
const current = fileURLToPath(import.meta.url);
if (entry && (process.platform === 'win32' ? entry.toLowerCase() === current.toLowerCase() : entry === current)) {
  main(process.argv.slice(2)).catch(error => {
    console.error(`FAIL: ${error.message}`);
    process.exitCode = 1;
  });
}
