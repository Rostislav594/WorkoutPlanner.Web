const [page] = await fetch('http://127.0.0.1:9222/json').then(response => response.json());
const socket = new WebSocket(page.webSocketDebuggerUrl);
await new Promise((resolve, reject) => {
  socket.addEventListener('open', resolve, { once: true });
  socket.addEventListener('error', reject, { once: true });
});

let nextId = 1;
const pending = new Map();
socket.addEventListener('message', event => {
  const message = JSON.parse(event.data);
  if (!message.id || !pending.has(message.id)) return;
  const { resolve, reject } = pending.get(message.id);
  pending.delete(message.id);
  message.error ? reject(new Error(JSON.stringify(message.error))) : resolve(message.result);
});

function command(method, params = {}) {
  const id = nextId++;
  socket.send(JSON.stringify({ id, method, params }));
  return new Promise((resolve, reject) => pending.set(id, { resolve, reject }));
}

async function evaluate(expression) {
  const response = await command('Runtime.evaluate', { expression, awaitPromise: true, returnByValue: true });
  if (response.exceptionDetails) throw new Error(response.exceptionDetails.text);
  return response.result.value;
}

const wait = milliseconds => new Promise(resolve => setTimeout(resolve, milliseconds));
await command('Runtime.enable');
await evaluate("Blazor.navigateTo('/today')");
await wait(1800);
await evaluate("document.querySelector('.free-workout-button')?.click()");
await wait(800);
await evaluate("document.querySelector('.exercise-header.edit-hold-target')?.click()");
await wait(400);
const beforeDelete = await evaluate(`(() => ({
  freeChip: !!document.querySelector('.free-workout-chip'),
  cards: document.querySelectorAll('.exercise-card').length,
  deleteButton: !![...document.querySelectorAll('button')].find(button => button.innerText.trim() === 'Удалить карточку'),
}))()`);
await evaluate("[...document.querySelectorAll('button')].find(button => button.innerText.trim() === 'Удалить карточку')?.click()");
await wait(500);
const afterDelete = await evaluate(`(() => ({
  freeChip: !!document.querySelector('.free-workout-chip'),
  cards: document.querySelectorAll('.exercise-card').length,
  freeWorkoutButton: !!document.querySelector('.free-workout-button'),
  emptyState: document.body.innerText.includes('На сегодня запланированной тренировки нет'),
}))()`);

console.log(JSON.stringify({ beforeDelete, afterDelete }, null, 2));
if (beforeDelete.freeChip !== true || beforeDelete.cards !== 1 || beforeDelete.deleteButton !== true ||
    afterDelete.freeChip !== false || afterDelete.cards !== 0 || afterDelete.freeWorkoutButton !== true || afterDelete.emptyState !== true) {
  process.exitCode = 1;
}
socket.close();
