const container = document.getElementById('container');
const minimap = document.getElementById('minimap');
const minimapView = document.getElementById('minimap-view');
const minimapContainer = document.getElementById('minimap-container');
const map = document.getElementById('map');

let loading = false;
function loadSvg(url) {
  loading = true;
  minimap.src = url;
  map.data = url;
}

function getState() {
  let parts = window.location.hash.slice(1).split(';');
  return {
    url: parts[0] || 'hierarchy.svg',
    x: parts[1] || 0,
    y: parts[2] || 0,
    s: parts[3] || null,
  }
}

function load() {
  loadSvg(getState().url);
}

function setScale(s) {
  map.style.scale = s;
  const bounds = map.getBoundingClientRect();
  container.style.width = bounds.width + 'px';
  container.style.height = bounds.height + 'px';
  update();
}

function zoom(amount, pointX, pointY) {
  const currentScale = +map.style.scale;
  const newScale = currentScale * amount;

  const currentScrollX = window.scrollX;
  const currentScrollY = window.scrollY;

  map.style.scale = newScale;
  const bounds = map.getBoundingClientRect();
  container.style.width = bounds.width + 'px';
  container.style.height = bounds.height + 'px';

  window.scrollTo(pointX * amount - (pointX - currentScrollX), pointY * amount - (pointY - currentScrollY));
  update();
}
function scroll(dx, dy) {
  window.scrollBy(dx, dy);
  update();
}
function handleMove(e) {
  if (!drag) return;
  if (drag === minimapContainer) {
    const bounds = minimap.getBoundingClientRect();

    scroll(
      document.documentElement.scrollWidth * e.movementX / bounds.width,
      document.documentElement.scrollHeight * e.movementY / bounds.height
    );
  } else {
    scroll(-e.movementX, -e.movementY);
  }
  e.preventDefault();
}

let debounceTimeout = null;
function update() {
  minimapView.style.top = document.documentElement.scrollTop / document.documentElement.scrollHeight * 100 + '%';
  minimapView.style.left = document.documentElement.scrollLeft / document.documentElement.scrollWidth * 100 + '%';
  minimapView.style.width = document.documentElement.clientWidth / document.documentElement.scrollWidth * 100 + '%';
  minimapView.style.height = document.documentElement.clientHeight / document.documentElement.scrollHeight * 100 + '%';

  clearTimeout(debounceTimeout);
  debounceTimeout = setTimeout(updateHash, 100);
}

function updateHash() {
  if (loading) return;
  const state = getState();

  history.replaceState(null, null, `${document.location.pathname}#${state.url};${window.scrollX};${window.scrollY};${map.style.scale}`);
}

let drag = false;

map.addEventListener('load', () => {
  const base = getState().url.match(/.*\//) || '';

  for (let a of map.getSVGDocument().getElementsByTagName('a')) {
    a.setAttribute('xlink:href', window.location.pathname + '#' + base + a.getAttribute('xlink:href'));
    a.setAttribute('target', '_top');
  }

  const state = getState();
  loading = false;

  if (state.s) {
    setScale(state.s);
  } else {
    setScale(1);
    const bounds = map.getBoundingClientRect();
    const scaleW = document.documentElement.clientWidth / bounds.width;
    const scaleH = document.documentElement.clientHeight / bounds.height;
    const maxAspectRatio = 0.2;
    const scale = Math.min(Math.max(maxAspectRatio, scaleW / scaleH) * scaleH, Math.max(maxAspectRatio, scaleH / scaleW) * scaleW);
    setScale(Math.min(scale, 1));
  }

  window.scrollTo(state.x, state.y);

  const content = map.contentDocument;
  content.addEventListener('wheel', (e) => {
    e.preventDefault();
    const currentScale = +map.style.scale;
    zoom(e.deltaY > 0 ? (1 / 1.1) : 1.1, e.pageX * currentScale, e.pageY * currentScale);
  }, {passive: false});

  content.addEventListener('mousedown', (e) => {
    if (e.target && e.target.tagName === 'text') return;
    if (!(e.buttons & 1)) return;
    drag = e.target || true;
  }, {passive: true});
  content.addEventListener('mouseup', (e) => {
    if (e.buttons & 1) return;
    drag = false;
    updateHash();
  }, {passive: true});
  content.addEventListener('mousemove', e => {
    handleMove(e);
  }, {passive: false});
});

window.addEventListener('wheel', e => {
  e.preventDefault();
  zoom(e.deltaY > 0 ? (1 / 1.1) : 1.1, e.pageX, e.pageY);
}, {passive: false});

window.addEventListener('mousedown', e => {
  if (e.target && e.target.tagName === 'text') return;
  if (!(e.buttons & 1)) return;
  if (drag) return;
  drag = e.target || true;
}, {passive: true});

window.addEventListener('mouseup', e => {
  if (e.buttons & 1) return;
  drag = false;
  updateHash();
}, {passive: true});

window.addEventListener('mousemove', e => {
  handleMove(e);
}, {passive: false});

window.addEventListener('scroll', () => {
  update();
}, {passive: true});

minimapContainer.addEventListener('mousedown', e => {
  e.preventDefault();
}, {passive: false});

window.addEventListener('hashchange', () => {
  load();
}, {passive: true});

load();
