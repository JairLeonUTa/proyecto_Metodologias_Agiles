// Gateway Ocelot real; upstream simulado y aislado. No llama a servicios ni bases existentes.
const assert = require('node:assert/strict');
const crypto = require('node:crypto');
const fs = require('node:fs/promises');
const http = require('node:http');
const os = require('node:os');
const path = require('node:path');
const { spawn } = require('node:child_process');
const { once } = require('node:events');

const root = path.resolve(__dirname, '..');
const roleClaim = 'http://schemas.microsoft.com/ws/2008/06/identity/claims/role';
const key = crypto.randomBytes(32).toString('hex');
const token = (role, extra = {}) => {
  const b64 = x => Buffer.from(JSON.stringify(x)).toString('base64url');
  const body = `${b64({ alg: 'HS256', typ: 'JWT' })}.${b64({
    iss: 'SIHA-IA', aud: 'SIHA-IA-Clients', sub: 'qa-aislado',
    exp: Math.floor(Date.now() / 1000) + 300, ...extra, [roleClaim]: role,
  })}`;
  return `${body}.${crypto.createHmac('sha256', key).update(body).digest('base64url')}`;
};

(async () => {
  const upstream = http.createServer((req, res) => {
    res.writeHead(200, { 'Content-Type': 'application/json' });
    res.end(JSON.stringify({ metodo: req.method, ruta: req.url }));
  });
  upstream.listen(0, '127.0.0.1');
  await once(upstream, 'listening');
  const tempRoot = path.join(os.tmpdir(), 'opencode');
  await fs.mkdir(tempRoot, { recursive: true });
  const temp = await fs.mkdtemp(path.join(tempRoot, 'siha-gateway-test-'));
  let gateway;
  let salida = '';
  try {
    const config = JSON.parse(await fs.readFile(path.join(root, 'src/Services/Siha.Gateway/ocelot.Development.json'), 'utf8'));
    for (const ruta of config.Routes) ruta.DownstreamHostAndPorts = [{ Host: '127.0.0.1', Port: upstream.address().port }];
    await fs.writeFile(path.join(temp, 'ocelot.Development.json'), JSON.stringify(config));
    gateway = spawn('dotnet', [path.join(root, 'src/Services/Siha.Gateway/bin/Debug/net8.0/Siha.Gateway.dll'),
      '--contentRoot', temp, '--urls', 'http://127.0.0.1:0'], {
      cwd: temp, env: { ...process.env, ASPNETCORE_ENVIRONMENT: 'Development',
        Jwt__Key: key, Jwt__Issuer: 'SIHA-IA', Jwt__Audience: 'SIHA-IA-Clients' },
      stdio: ['ignore', 'pipe', 'pipe'],
    });
    gateway.stdout.on('data', data => { salida += data; });
    gateway.stderr.on('data', data => { salida += data; });
    gateway.on('error', error => { salida += error.message; });
    const limite = Date.now() + 20000;
    while (!/Now listening on: (http:\/\/127\.0\.0\.1:\d+)/.test(salida)) {
      if (gateway.exitCode !== null || Date.now() > limite) throw new Error(`Gateway no arrancó: ${salida}`);
      await new Promise(resolve => setTimeout(resolve, 50));
    }
    const base = salida.match(/Now listening on: (http:\/\/127\.0\.0\.1:\d+)/)[1];
    let checks = 0;
    for (const [method, resource] of [
      ['DELETE', '/api/carreras/1'], ['PUT', '/api/mallas/1'],
      ['DELETE', '/api/mallas/1'], ['DELETE', '/api/asignaturas/1'], ['PUT', '/api/reglas/1'],
    ]) {
      for (const [role, status] of [[null, 401], ['OPERADOR', 403], ['DOCENTE', 403], ['ADMINISTRADOR', 200]]) {
        const response = await fetch(base + resource, { method, headers: role ? { Authorization: `Bearer ${token(role)}` } : {} });
        assert.equal(response.status, status, `${method} ${resource}: ${role || 'anónimo'}`);
        if (status === 200) assert.deepEqual(await response.json(), { metodo: method, ruta: resource });
        checks++;
      }
    }
    for (const resource of ['/api/carreras/1', '/api/mallas/1', '/api/asignaturas/1', '/api/reglas']) {
      const response = await fetch(base + resource, { headers: { Authorization: `Bearer ${token('DOCENTE')}` } });
      assert.equal(response.status, 200, `GET ${resource}: DOCENTE`);
      checks++;
    }
    for (const role of ['OPERADOR', undefined]) {
      const response = await fetch(base + '/api/mallas/1', { method: 'DELETE',
        headers: { Authorization: `Bearer ${token(role, { rolGateway: 'ADMINISTRADOR' })}` } });
      assert.equal(response.status, 403, 'El alias no puede reemplazar el rol firmado por MsA');
      checks++;
    }
    console.log(`Gateway HTTP real: ${checks} comprobaciones; upstream simulado, SQL/RabbitMQ no probados`);
  } finally {
    if (gateway && gateway.exitCode === null) {
      const cierre = once(gateway, 'exit');
      gateway.kill();
      await cierre;
    }
    await new Promise(resolve => upstream.close(resolve));
    await fs.rm(temp, { recursive: true, force: true });
  }
})().catch(error => { console.error(error.message); process.exitCode = 1; });
