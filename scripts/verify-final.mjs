import assert from 'node:assert/strict';
import { spawn } from 'node:child_process';
import { createWriteStream } from 'node:fs';
import { access, mkdir, readFile, writeFile } from 'node:fs/promises';
import { join, relative } from 'node:path';
import { fileURLToPath } from 'node:url';

// Offline application checks: no database changes, API start/restart or demo
// records. npm/NuGet restore and vulnerability audits require network access.
const root = fileURLToPath(new URL('../', import.meta.url));
const client = join(root, 'src', 'SmartX.Client');
if (process.argv.includes('--help')) {
  console.log('Usage: node scripts/verify-final.mjs\nRun from any directory. Stop API/Vite/watch first and install Playwright Chromium once.\nRequires .NET 10, Node 22.13+ (22.x) or 24+, npm and registry access.\nWrites timestamped logs, test counts, audits and AVL CSV under artifacts/pillar4-checks/.\nLive SQL restart/demo checks remain a separate step in START_HERE_PILLAR_4.md.');
  process.exit(0);
}
if (process.argv.length > 2) throw new Error('Unknown arguments. Use --help.');
const stamp = new Date().toISOString().replace(/[:.]/g, '-') + '-' + process.pid;
const output = join(root, 'artifacts', 'pillar4-checks', stamp);
await mkdir(output, { recursive: true });
const record = { startedAtUtc: new Date().toISOString(), node: process.version,
  platform: process.platform, architecture: process.arch, status: 'running',
  scope: 'Builds, automated suites, dependency audits and algorithm-only benchmark; live SQL/browser sign-off is separate.',
  steps: [] };
const save = () => writeFile(join(output, 'summary.json'), JSON.stringify(record, null, 2) + '\n');
const exists = path => access(path).then(() => true, () => false);

function nodeTestCounts(stdout) {
  const read = label => {
    const match = stdout.match(new RegExp(`(?:^|\\n)(?:#|ℹ)\\s+${label}\\s+(\\d+)`));
    assert.ok(match, `Missing ${label} test counter.`);
    return Number(match[1]);
  };
  const counts = Object.fromEntries(['tests', 'pass', 'fail', 'cancelled', 'skipped', 'todo'].map(label => [label, read(label)]));
  assert.ok(counts.tests > 0 && counts.pass === counts.tests && counts.fail + counts.cancelled + counts.skipped + counts.todo === 0,
    'The test suite must discover tests and pass without skipped/cancelled/todo cases.');
  return counts;
}

async function run(name, executable, args, cwd = root, validate) {
  const index = String(record.steps.length + 1).padStart(2, '0');
  const slug = `${index}-${name}`;
  const step = { name, command: [executable, ...args], cwd: relative(root, cwd) || '.', status: 'running' };
  record.steps.push(step); await save();
  console.log(`\n[${index}] ${name}`);
  const stdoutFile = createWriteStream(join(output, slug + '.stdout.log'));
  const stderrFile = createWriteStream(join(output, slug + '.stderr.log'));
  let stdout = '';
  const started = performance.now();
  try {
    // npm.cmd needs a Windows command shell. Its arguments below are fixed
    // constants; user input, paths and environment values are never interpolated.
    const windowsNpm = process.platform === 'win32' && executable === 'npm';
    const exit = await new Promise((resolve, reject) => {
      const child = spawn(windowsNpm ? 'npm.cmd' : executable, args, {
        cwd, shell: windowsNpm, stdio: ['ignore', 'pipe', 'pipe'],
      });
      child.stdout.on('data', chunk => { stdout += chunk; stdoutFile.write(chunk); process.stdout.write(chunk); });
      child.stderr.on('data', chunk => { stderrFile.write(chunk); process.stderr.write(chunk); });
      child.once('error', reject);
      child.once('close', (code, signal) => resolve({ code, signal }));
    });
    step.exitCode = exit.code; step.signal = exit.signal;
    assert.equal(exit.code, 0, `${name} failed; see ${slug} logs.`);
    if (validate) step.result = await validate(stdout);
    step.status = 'passed';
  } catch (error) {
    step.status = 'failed'; step.error = error.message;
    throw error;
  } finally {
    step.durationMs = Math.round(performance.now() - started);
    await Promise.all([new Promise(resolve => stdoutFile.end(resolve)), new Promise(resolve => stderrFile.end(resolve))]);
    await save();
  }
}

try {
  const [major, minor] = process.versions.node.split('.').map(Number);
  assert.ok((major === 22 && minor >= 13) || major >= 24, 'Use Node 22.13+ in the 22.x line or Node 24+.');
  for (const name of ['SmartX.sln', 'src/SmartX.Api/Program.cs', 'src/SmartX.Api/Controllers/MeshController.cs', 'src/SmartX.Client/package-lock.json'])
    assert.ok(await exists(join(root, name)), `Missing ${name}; apply the full patch at the repository root.`);
  for (const name of ['Configuration', 'Contracts', 'Controllers', 'Filters', 'Properties', 'Program.cs', 'WeatherForecast.cs', 'SmartX.Api.csproj'])
    assert.ok(!(await exists(join(root, 'src', 'SmartX.Api', 'Controllers', name))),
      `Misplaced API entry under Controllers/${name}. Correct the copy location before rebuilding; see the setup guide.`);
  record.layout = 'passed';
  await run('dotnet-version', 'dotnet', ['--version']);
  await run('npm-version', 'npm', ['--version'], client);
  await run('restore', 'dotnet', ['restore', 'SmartX.sln', '--disable-parallel', '-m:1', '-nr:false']);
  await run('release-rebuild', 'dotnet', ['build', 'SmartX.sln', '-c', 'Release', '--no-restore', '-t:Rebuild', '-warnaserror', '--disable-build-servers', '-m:1']);
  await run('backend-tests', 'dotnet', ['test', 'SmartX.sln', '-c', 'Release', '--no-build', '--no-restore', '-m:1', '--logger', 'trx;LogFileName=backend-tests.trx', '--results-directory', output], root, async () => {
    const xml = await readFile(join(output, 'backend-tests.trx'), 'utf8');
    const counters = xml.match(/<Counters\s+([^>]+)\/?\s*>/);
    assert.ok(counters, 'Missing backend TRX counters.');
    const values = Object.fromEntries([...counters[1].matchAll(/(\w+)="(\d+)"/g)].map(([, key, value]) => [key, Number(value)]));
    assert.ok(values.total > 0 && values.passed === values.total && values.executed === values.total && values.failed === 0,
      'Backend tests must be discovered and all pass without skips.');
    return values;
  });
  await run('nuget-audit', 'dotnet', ['list', 'SmartX.sln', 'package', '--vulnerable', '--include-transitive', '--format', 'json', '--no-restore'], root, stdout => {
    const report = JSON.parse(stdout);
    assert.ok(Array.isArray(report.projects) && report.projects.length > 0, 'NuGet audit returned no project results.');
    function findings(value) {
      if (!value || typeof value !== 'object') return 0;
      return Object.entries(value).reduce((sum, [key, item]) => sum +
        (['vulnerabilities', 'errors', 'problems'].includes(key) ? (Array.isArray(item) ? item.length : item ? 1 : 0) : findings(item)), 0);
    }
    const count = findings(report);
    assert.equal(count, 0, 'NuGet audit has findings/errors; review its log.');
    return { findings: count };
  });
  await run('npm-ci', 'npm', ['ci'], client);
  await run('npm-audit', 'npm', ['audit', '--json'], client, stdout => {
    const report = JSON.parse(stdout);
    assert.equal(report.metadata?.vulnerabilities?.total, 0, 'npm audit has findings or no valid result.');
    return report.metadata.vulnerabilities;
  });
  await run('client-unit', 'npm', ['test'], client, nodeTestCounts);
  await run('client-integration', 'npm', ['run', 'test:integration'], client, nodeTestCounts);
  await run('client-browser', 'npm', ['run', 'test:browser'], client, nodeTestCounts);
  await run('client-lint', 'npm', ['run', 'lint'], client);
  await run('client-build', 'npm', ['run', 'build'], client);
  const benchmark = 'tools/SmartX.HistoryBenchmarks/SmartX.HistoryBenchmarks.csproj';
  await run('benchmark-restore', 'dotnet', ['restore', benchmark, '--disable-parallel', '-m:1']);
  await run('benchmark-build', 'dotnet', ['build', benchmark, '-c', 'Release', '--no-restore', '-warnaserror', '--disable-build-servers', '-m:1']);
  await run('avl-benchmark', 'dotnet', ['run', '--project', benchmark, '-c', 'Release', '--no-build', '--no-restore'], root, async stdout => {
    const lines = stdout.trim().split(/\r?\n/);
    assert.equal(lines[0], 'readings,height,build_ms,matched,visited_nodes,median_query_us,linear_scan_us');
    const rows = lines.slice(1).map(line => line.split(',').map(Number));
    assert.deepEqual(rows.map(row => row[0]), [10000, 50000, 100000]);
    assert.ok(rows.every(row => row.length === 7 && row.every(Number.isFinite) && row[3] === 121), 'Unexpected benchmark output.');
    await writeFile(join(output, 'avl-benchmark.csv'), lines.join('\n') + '\n');
    return { sizes: rows.map(row => row[0]), matchesPerQuery: 121, scope: 'Algorithm-only; excludes SQL/HTTP/rendering.' };
  });
  record.status = 'passed';
  console.log('\nPASS automated final checks. Complete the separate live SQL/restart checks before signing off Pillar 4.');
} catch (error) {
  record.status = 'failed'; record.error = error.message; process.exitCode = 1;
  console.error(`\nFAIL: ${error.message}\nStopped at the first failed check; later steps were not run.`);
} finally {
  record.finishedAtUtc = new Date().toISOString(); await save();
  console.log(`Evidence: ${output}`);
}
