const targets = process.env.SMOKE_ROUTES?.split(',').filter(Boolean) ?? [
  '/', '/today', '/workouts', '/history', '/progress', '/progress/exercises',
  '/progress/workouts', '/calendar', '/notebook', '/profile',
  '/profile/personal-information', '/profile/email', '/profile/rest-timers',
];

const pages = await fetch('http://127.0.0.1:9222/json').then(response => response.json());
const socket = new WebSocket(pages[0].webSocketDebuggerUrl);
await new Promise((resolve, reject) => {
  socket.addEventListener('open', resolve, { once: true });
  socket.addEventListener('error', reject, { once: true });
});

let nextId = 1;
const pending = new Map();
const runtimeErrors = [];
socket.addEventListener('message', event => {
  const message = JSON.parse(event.data);
  if (message.id && pending.has(message.id)) {
    const { resolve, reject } = pending.get(message.id);
    pending.delete(message.id);
    message.error ? reject(new Error(JSON.stringify(message.error))) : resolve(message.result);
  }
  if (message.method === 'Runtime.exceptionThrown') {
    runtimeErrors.push(message.params.exceptionDetails.text);
  }
});

function command(method, params = {}) {
  const id = nextId++;
  socket.send(JSON.stringify({ id, method, params }));
  return new Promise((resolve, reject) => pending.set(id, { resolve, reject }));
}

async function evaluate(expression) {
  const response = await command('Runtime.evaluate', {
    expression,
    awaitPromise: true,
    returnByValue: true,
  });
  if (response.exceptionDetails) {
    throw new Error(response.exceptionDetails.text);
  }
  return response.result.value;
}

await command('Runtime.enable');
await command('Log.enable');

const results = [];
for (const route of targets) {
  const navigation = await evaluate(`(() => {
    const route = ${JSON.stringify(route)};
    const links = [...document.querySelectorAll('a[href]')];
    const link = links.find(item => new URL(item.href).pathname === route);
    if (link) { link.click(); return 'link'; }
    if (globalThis.Blazor?.navigateTo) { Blazor.navigateTo(route); return 'Blazor.navigateTo'; }
    return 'missing';
  })()`);
  await new Promise(resolve => setTimeout(resolve, 1800));
  const state = await evaluate(`(() => ({
    path: location.pathname,
    title: document.querySelector('h1, h2, .page-title')?.innerText?.trim() ?? '',
    text: document.body.innerText.replace(/\\s+/g, ' ').trim().slice(0, 260),
    links: [...document.querySelectorAll('a[href]')].map(item => new URL(item.href).pathname).filter((item, index, all) => all.indexOf(item) === index),
    errorUi: (() => {
      const element = document.querySelector('#blazor-error-ui');
      return !!element && getComputedStyle(element).display !== 'none' && getComputedStyle(element).visibility !== 'hidden';
    })(),
  }))()`);
  results.push({ requested: route, navigation, ...state });
}

console.log(JSON.stringify({ results, runtimeErrors }, null, 2));
socket.close();
