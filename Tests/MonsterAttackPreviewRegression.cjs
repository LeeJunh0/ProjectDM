const fs = require('node:fs');
const vm = require('node:vm');
const assert = require('node:assert/strict');
const path = require('node:path');
const root = path.join(__dirname, '../Design/Monsters/AttackPreview-v1');
const html = fs.readFileSync(path.join(root, 'index.html'), 'utf8');
const script = html.match(/<script>([\s\S]*?)<\/script>/)[1];
const embedded = html.match(/id="embedded-sheet"[^>]+src="([^"]+)"/)[1];
assert.deepEqual(Buffer.from(embedded.split(',')[1], 'base64'), fs.readFileSync(path.join(root, 'attack-sheet.png')));
for (const mode of ['async', 'cached', 'error']) {
    const elements = new Map();
    let draws = 0, frames = [];
    const ctx = new Proxy({}, { get: (o, key) => o[key] ?? (() => { draws++; }) });
    const element = key => {
        if (!elements.has(key)) elements.set(key, { checked: true, value: '1', src: key === '#embedded-sheet' ? embedded : '',
            children: [], append(child) { this.children.push(child); }, getContext() { return ctx; } });
        return elements.get(key);
    };
    let loadedImage;
    const sandbox = {
        Image: class {
            constructor() { loadedImage = this; this.width = 2172; this.height = 724; this.naturalWidth = 0; }
            set src(value) {
                assert(value.startsWith('data:image/png;base64,'), 'Preview must not depend on relative image requests');
                assert.equal(typeof this.onload, 'function', 'Install load handler before requesting the image');
                if (mode === 'error') this.onerror();
                if (mode === 'cached') { this.complete = true; this.naturalWidth = this.width; this.onload(); }
            }
        },
        document: { querySelector: element, createElement: () => ({ append() {}, getContext() { return ctx; } }) },
        requestAnimationFrame: callback => frames.push(callback)
    };
    vm.createContext(sandbox);
    vm.runInContext(script, sandbox);
    if (mode === 'error') {
        assert(element('#load-status').textContent.includes('읽지 못했어요'));
        assert.equal(frames.length, 0);
        continue;
    }
    if (mode === 'async') { loadedImage.naturalWidth = loadedImage.width; loadedImage.onload(); }
    assert.equal(frames.length, 1, 'Start exactly one animation loop');
    assert.equal(element('#mushroom-frames').children.length, 6);
    assert.equal(element('#armor-frames').children.length, 6);
    for (let i = 1; i <= 60; i++) frames.shift()(i * 50);
    assert(vm.runInContext('time > 2', sandbox), 'Scheduled frames must advance animation time');
    assert(draws > 1000);
    element('#play').onclick();
    const paused = vm.runInContext('time', sandbox);
    frames.shift()(3050);
    assert.equal(vm.runInContext('time', sandbox), paused);
    element('#restart').onclick();
    assert.equal(vm.runInContext('time', sandbox), 0);
    assert.equal(vm.runInContext('playing', sandbox), true);
}
console.log('Monster attack preview PASS: embedded PNG parity, asynchronous/cached/error loading, scheduled animation, controls.');
