import assert from "node:assert/strict";
import { spawn } from "node:child_process";
import { once } from "node:events";
import { copyFile, mkdir, mkdtemp, readFile, rm, writeFile } from "node:fs/promises";
import net from "node:net";
import { tmpdir } from "node:os";
import path from "node:path";
import { test } from "node:test";

async function fixture(t) {
  const root = await mkdtemp(path.join(tmpdir(), "trading-dev-test-"));
  t.after(() => rm(root, { recursive: true, force: true }));
  for (const directory of ["scripts", "bin", "backend/src/Api/obj", "frontend/node_modules/next"]) {
    await mkdir(path.join(root, directory), { recursive: true });
  }
  await copyFile(new URL("./dev.sh", import.meta.url), path.join(root, "scripts/dev.sh"));
  const sdk = JSON.parse(await readFile(new URL("../global.json", import.meta.url), "utf8")).sdk.version;
  for (const [file, contents] of Object.entries({
    ".nvmrc": process.versions.node,
    "global.json": JSON.stringify({ sdk: { version: sdk } }),
    ".env": "LOCAL_TEST=true\n",
    "backend/.env": "ASPNETCORE_URLS=http://localhost:5080\nConnectionStrings__TradingAssistant=dummy_test_only\n",
    "frontend/.env.local": "NEXT_PUBLIC_API_BASE_URL=http://localhost:5080\n",
    "frontend/node_modules/next/package.json": "{}",
    "backend/src/Api/Api.csproj": "",
    "backend/src/Api/obj/project.assets.json": "{}",
  })) {
    await writeFile(path.join(root, file), contents);
  }
  for (const [command, contents] of Object.entries({
    dotnet: `if [[ "$1" == --version ]]; then echo '${sdk}'; exit; fi
[[ "\${ConnectionStrings__TradingAssistant:-}" == dummy_test_only ]] || exit 8
echo $$ > "$TEST_ROOT/api.pid"
sleep 600 & wait $!
`,
    npm: `[[ -z "\${ConnectionStrings__TradingAssistant:-}" ]] || exit 9
echo $$ > "$TEST_ROOT/web.pid"
sleep 600 &
echo $! > "$TEST_ROOT/web-child.pid"
wait $!
`,
    docker: `echo "$*" >> "$TEST_ROOT/docker.calls"
[[ "\${TEST_DOCKER_FAIL:-}" != true ]]
`,
    curl: `case "$*" in
  */api/v1/health*) printf '{"status":"healthy"}' ;;
  */api/v1/ready*) printf '{"status":"ready"}' ;;
  *) printf '<html>Test frontend</html>' ;;
esac
`,
  })) {
    await writeFile(path.join(root, "bin", command), `#!/usr/bin/env bash\nset -eu\n${contents}`, { mode: 0o755 });
  }
  return {
    root,
    launch(extraEnv = {}) {
      const child = spawn("bash", ["scripts/dev.sh"], {
        cwd: root,
        env: {
          ...process.env,
          PATH: `${path.join(root, "bin")}:${path.dirname(process.execPath)}:${process.env.PATH}`,
          NEXT_PUBLIC_API_BASE_URL: "http://localhost:5080",
          TEST_ROOT: root,
          ...extraEnv,
        },
        stdio: ["ignore", "pipe", "pipe"],
      });
      let output = "";
      child.stdout.on("data", data => { output += data; });
      child.stderr.on("data", data => { output += data; });
      const exited = once(child, "close").then(([code, signal]) => ({ code, signal }));
      t.after(async () => {
        if (child.exitCode === null && child.signalCode === null) child.kill("SIGTERM");
        await exited;
      });
      return { child, exited, output: () => output };
    },
  };
}

async function waitForOutput(run, expected) {
  const deadline = Date.now() + 10_000;
  while (!run.output().includes(expected)) {
    assert.ok(Date.now() < deadline && run.child.exitCode === null, run.output());
    await new Promise(resolve => setTimeout(resolve, 50));
  }
}

test("missing settings fail before Docker and are not recreated", { timeout: 15_000 }, async t => {
  const app = await fixture(t);
  await rm(path.join(app.root, "backend/.env"));
  const run = app.launch();
  assert.equal((await run.exited).code, 1);
  assert.match(run.output(), /Missing backend\/.env/);
  await assert.rejects(readFile(path.join(app.root, "docker.calls")), { code: "ENOENT" });
  await assert.rejects(readFile(path.join(app.root, "backend/.env")), { code: "ENOENT" });
});

test("occupied frontend port fails without stopping its owner", { timeout: 15_000 }, async t => {
  const server = net.createServer();
  server.listen(3000, "127.0.0.1");
  await once(server, "listening");
  t.after(() => new Promise(resolve => server.close(resolve)));
  const app = await fixture(t);
  const run = app.launch();
  assert.equal((await run.exited).code, 1);
  assert.match(run.output(), /Cannot use local port 3000/);
  assert.equal(server.listening, true);
  await assert.rejects(readFile(path.join(app.root, "docker.calls")), { code: "ENOENT" });
});

test("unreachable Docker fails before starting application processes", { timeout: 15_000 }, async t => {
  const app = await fixture(t);
  const run = app.launch({ TEST_DOCKER_FAIL: "true" });
  assert.equal((await run.exited).code, 1);
  assert.match(run.output(), /Docker is not reachable/);
  await assert.rejects(readFile(path.join(app.root, "api.pid")), { code: "ENOENT" });
});

test("mismatched API URLs fail before starting services", { timeout: 15_000 }, async t => {
  const app = await fixture(t);
  const run = app.launch({ NEXT_PUBLIC_API_BASE_URL: "http://localhost:5099" });
  assert.equal((await run.exited).code, 1);
  assert.match(run.output(), /same loopback HTTP origin/);
  await assert.rejects(readFile(path.join(app.root, "docker.calls")), { code: "ENOENT" });
});

for (const event of ["interrupt", "service exit"]) {
  test(`${event} stops both process groups without stopping PostgreSQL`, { timeout: 20_000 }, async t => {
    const app = await fixture(t);
    const run = app.launch();
    await waitForOutput(run, "Ready - press Ctrl+C");
    const pids = await Promise.all(["api.pid", "web.pid", "web-child.pid"].map(async file =>
      Number(await readFile(path.join(app.root, file), "utf8"))));
    if (event === "interrupt") run.child.kill("SIGINT");
    else process.kill(pids[0], "SIGTERM");
    assert.equal((await run.exited).code, event === "interrupt" ? 130 : 1);
    for (const pid of pids) {
      assert.throws(() => process.kill(pid, 0), { code: "ESRCH" });
    }
    const calls = await readFile(path.join(app.root, "docker.calls"), "utf8");
    assert.match(calls, /compose up -d --wait --wait-timeout 60 postgres/);
    assert.doesNotMatch(calls, /\b(stop|down)\b/);
    assert.match(run.output(), /PostgreSQL is left running/);
  });
}
