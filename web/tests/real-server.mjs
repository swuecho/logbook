// Own the entire test database lifecycle; never accept a deployment DATABASE_URL.
import { spawn, execFileSync } from 'node:child_process';
import { randomUUID } from 'node:crypto';

const name = `logbook-sync-test-${randomUUID()}`;
const password = randomUUID();
let api;
let stopping = false;
function cleanup() {
  if (stopping) return;
  stopping = true;
  api?.kill('SIGTERM');
  try { execFileSync('docker', ['rm', '-f', '-v', name], { stdio: 'ignore' }); } catch {}
}
process.on('SIGINT', () => { cleanup(); process.exit(0); });
process.on('SIGTERM', () => { cleanup(); process.exit(0); });
process.on('exit', cleanup);
function run(command, args, options = {}) {
  execFileSync(command, args, { stdio: 'inherit', ...options });
}
try {
  run('docker', ['run', '-d', '--name', name, '-e', `POSTGRES_PASSWORD=${password}`,
    '-e', 'POSTGRES_DB=logbook_sync_test', '-p', '127.0.0.1::5432', 'postgres:16-alpine']);
  let ready = false;
  for (let attempt = 0; attempt < 60; attempt++) {
    try {
      execFileSync('docker', ['exec', name, 'pg_isready', '-U', 'postgres', '-d', 'logbook_sync_test'], { stdio: 'ignore' });
      ready = true;
      break;
    } catch { await new Promise(resolve => setTimeout(resolve, 1000)); }
  }
  if (!ready) throw new Error('Test PostgreSQL did not become ready.');
  const binding = execFileSync('docker', ['port', name, '5432'], { encoding: 'utf8' }).trim();
  const port = binding.split(':').pop();
  const env = { ...process.env, JWT_SECRET: randomUUID() + randomUUID(), JWT_AUDIENCE: 'logbook-sync-test', DATABASE_URL: `postgresql://postgres:${password}@127.0.0.1:${port}/logbook_sync_test` };
  const migrationArgs = ['run', '--project', '../api/Migrations/Migrations.fsproj'];
  run('dotnet', migrationArgs, { env });
  const journalBefore = execFileSync('docker', ['exec', name, 'psql', '-U', 'postgres', '-d', 'logbook_sync_test', '-Atc', 'SELECT count(*) FROM schemaversions'], { encoding: 'utf8' }).trim();
  run('dotnet', migrationArgs, { env });
  const journalAfter = execFileSync('docker', ['exec', name, 'psql', '-U', 'postgres', '-d', 'logbook_sync_test', '-Atc', 'SELECT count(*) FROM schemaversions'], { encoding: 'utf8' }).trim();
  if (Number(journalBefore) < 4 || journalBefore !== journalAfter) throw new Error('Migration journal was not stable on rerun.');
  api = spawn('dotnet', ['run', '--project', 'Logbook.fsproj', '--no-launch-profile'], {
    cwd: '../api', env: { ...env, ASPNETCORE_URLS: 'http://127.0.0.1:9197', ASPNETCORE_ENVIRONMENT: 'Production' }, stdio: 'inherit',
  });
  api.on('exit', code => { cleanup(); process.exit(code || 0); });
} catch (error) {
  console.error(error.message);
  cleanup();
  process.exit(1);
}
