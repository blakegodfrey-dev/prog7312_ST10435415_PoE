import { createServer } from 'node:http';

const [mode, rawPort] = process.argv.slice(2);
if (mode === 'fail') process.exit(7);
const server = createServer((request, response) => {
  if (request.url === '/exit') {
    response.end('Stopping fixture');
    setTimeout(() => process.exit(9), 10);
    return;
  }
  if (mode === 'unready') {
    response.writeHead(503); response.end('Not ready');
  } else {
    response.setHeader('content-type', 'application/json');
    response.end(JSON.stringify({ status: 'Healthy' }));
  }
});
server.listen(Number(rawPort), '127.0.0.1', () => console.log(`PID:${process.pid}`));
process.on('SIGTERM', () => server.close(() => process.exit(0)));
