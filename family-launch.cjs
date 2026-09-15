// 一次性启动器：dotenv 解析 .env.example（不能直接 source，DATABASE_URL 带 `;`）后拉起子进程。
// 用法: node family-launch.cjs <envfile> <cmd> [args...]
const fs = require('fs');
const { spawn } = require('child_process');

const [envFile, cmd, ...args] = process.argv.slice(2);
if (!envFile || !cmd) {
  console.error('usage: node family-launch.cjs <envfile> <cmd> [args...]');
  process.exit(2);
}
for (const line of fs.readFileSync(envFile, 'utf8').split(/\r?\n/)) {
  const m = line.match(/^\s*(?:export\s+)?([A-Za-z_][A-Za-z0-9_]*)\s*=\s*(.*)\s*$/);
  if (!m || line.trim().startsWith('#')) continue;
  let v = m[2].trim();
  if ((v.startsWith('"') && v.endsWith('"')) || (v.startsWith("'") && v.endsWith("'"))) v = v.slice(1, -1);
  process.env[m[1]] = v;
}
const child = spawn(cmd, args, { stdio: 'inherit', env: process.env, shell: false });
child.on('exit', (c) => process.exit(c ?? 0));
