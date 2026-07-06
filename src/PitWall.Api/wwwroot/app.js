const TRACKS = {
  silverstone: {
    id: 'silverstone',
    name: 'Silverstone',
    country: 'United Kingdom',
    lengthKm: 5.891,
    startFinishProgress: 0.0,
    pitEntryProgress: 0.92,
    pitExitProgress: 0.055,
    pitBox: { x: 0.63, y: 0.82, width: 0.22, height: 0.078 },
    sectors: [
      { label: 'S1', progress: 0.18 },
      { label: 'S2', progress: 0.50 },
      { label: 'S3', progress: 0.78 }
    ],
    points: [
      { x: 0.60, y: 0.78 },
      { x: 0.72, y: 0.73 },
      { x: 0.83, y: 0.66 },
      { x: 0.88, y: 0.55 },
      { x: 0.84, y: 0.45 },
      { x: 0.77, y: 0.39 },
      { x: 0.75, y: 0.30 },
      { x: 0.66, y: 0.22 },
      { x: 0.53, y: 0.19 },
      { x: 0.42, y: 0.22 },
      { x: 0.36, y: 0.31 },
      { x: 0.33, y: 0.42 },
      { x: 0.23, y: 0.40 },
      { x: 0.14, y: 0.47 },
      { x: 0.16, y: 0.58 },
      { x: 0.26, y: 0.63 },
      { x: 0.38, y: 0.58 },
      { x: 0.46, y: 0.64 },
      { x: 0.51, y: 0.73 }
    ]
  },

  monza: {
    id: 'monza',
    name: 'Monza',
    country: 'Italy',
    lengthKm: 5.793,
    startFinishProgress: 0.0,
    pitEntryProgress: 0.93,
    pitExitProgress: 0.06,
    pitBox: { x: 0.58, y: 0.83, width: 0.24, height: 0.078 },
    sectors: [
      { label: 'S1', progress: 0.20 },
      { label: 'S2', progress: 0.53 },
      { label: 'S3', progress: 0.80 }
    ],
    points: [
      { x: 0.54, y: 0.79 },
      { x: 0.70, y: 0.75 },
      { x: 0.82, y: 0.67 },
      { x: 0.88, y: 0.57 },
      { x: 0.84, y: 0.48 },
      { x: 0.73, y: 0.45 },
      { x: 0.64, y: 0.42 },
      { x: 0.67, y: 0.34 },
      { x: 0.78, y: 0.27 },
      { x: 0.84, y: 0.19 },
      { x: 0.76, y: 0.12 },
      { x: 0.58, y: 0.11 },
      { x: 0.43, y: 0.15 },
      { x: 0.30, y: 0.24 },
      { x: 0.22, y: 0.35 },
      { x: 0.21, y: 0.46 },
      { x: 0.30, y: 0.53 },
      { x: 0.44, y: 0.53 },
      { x: 0.55, y: 0.57 },
      { x: 0.49, y: 0.69 }
    ]
  },

  spa: {
    id: 'spa',
    name: 'Spa-Francorchamps',
    country: 'Belgium',
    lengthKm: 7.004,
    startFinishProgress: 0.0,
    pitEntryProgress: 0.91,
    pitExitProgress: 0.065,
    pitBox: { x: 0.54, y: 0.83, width: 0.24, height: 0.078 },
    sectors: [
      { label: 'S1', progress: 0.22 },
      { label: 'S2', progress: 0.55 },
      { label: 'S3', progress: 0.82 }
    ],
    points: [
      { x: 0.50, y: 0.79 },
      { x: 0.62, y: 0.76 },
      { x: 0.73, y: 0.68 },
      { x: 0.82, y: 0.58 },
      { x: 0.82, y: 0.47 },
      { x: 0.73, y: 0.41 },
      { x: 0.62, y: 0.44 },
      { x: 0.56, y: 0.36 },
      { x: 0.63, y: 0.25 },
      { x: 0.56, y: 0.16 },
      { x: 0.43, y: 0.17 },
      { x: 0.33, y: 0.27 },
      { x: 0.25, y: 0.38 },
      { x: 0.16, y: 0.48 },
      { x: 0.19, y: 0.61 },
      { x: 0.31, y: 0.66 },
      { x: 0.41, y: 0.60 },
      { x: 0.47, y: 0.69 }
    ]
  }
};

const state = {
  cars: [],
  previousCars: new Map(),
  currentCars: new Map(),

  displayCars: new Map(),
  lastFrameAt: performance.now(),

  lanes: new Map(),

  pitPlans: new Map(),
  visualProgressOffsets: new Map(),

  selectedTrackId: 'silverstone',
  raceStarted: false,
forceStartLineForNewCars: false,
raceStartVisualProgress: 0,
raceLaunchStartedAt: null,
raceLaunchDurationMs: 6500,
visualSpeedMultipliers: new Map(),
visualRaceProgress: 0,
visualGapOffsets: new Map(),
  snapshotAt: performance.now(),
  telemetryIntervalMs: 900,

  selectedCode: null,
  services: [],
  incidents: [],
  deployment: null,
  events: [],
  strategyRecommendations: [],
strategyRecommendationsLoadedAt: 0,
strategyRecommendationsLoading: false,
  connection: null,
  telemetryPollId: null,
  mode: 'Loading',
  lap: '-'
};
const DEMO_SCENARIO_EXPLAINERS = {
  undercut: {
    title: 'UNDERCUT',
    body: 'ALP1 is close behind a rival. Pit now to attack the car ahead with fresher tyres.',
    watch: 'Watch ALP1 gap to car ahead, pit loss, and projected rejoin position.'
  },
  overcut: {
    title: 'OVERCUT',
    body: 'ALP1 stays out because its current tyres can still perform while the car ahead may lose time.',
    watch: 'Watch tyre age, gap to car ahead, and whether staying out keeps track position.'
  },
  'cover-rival': {
    title: 'COVER RIVAL',
    body: 'A rival behind ALP1 is close enough to threaten an undercut. Pit now to defend.',
    watch: 'Watch ALP1 gap to the car behind and the cover threat score.'
  },
  'bad-pit-window': {
    title: 'EXTEND STINT',
    body: 'ALP1 tyres are poor, but pitting now would rejoin in traffic. Stay out temporarily.',
    watch: 'Watch projected rejoin position and positions lost if pit.'
  },
  'tyre-cliff': {
    title: 'PIT NOW',
    body: 'ALP1 has reached the tyre cliff. Staying out costs more than the pit loss.',
    watch: 'Watch tyre pressure, tyre age, and urgency.'
  }
};
const el = id => document.getElementById(id);
const canvas = el('trackCanvas');
const ctx = canvas.getContext('2d');

const PIT_ANIMATION_MS = 6500;
function roundedRectPath(x, y, width, height, radius) {
  const r = Math.min(radius, Math.abs(width) / 2, Math.abs(height) / 2);

  ctx.moveTo(x + r, y);
  ctx.lineTo(x + width - r, y);
  ctx.quadraticCurveTo(x + width, y, x + width, y + r);
  ctx.lineTo(x + width, y + height - r);
  ctx.quadraticCurveTo(x + width, y + height, x + width - r, y + height);
  ctx.lineTo(x + r, y + height);
  ctx.quadraticCurveTo(x, y + height, x, y + height - r);
  ctx.lineTo(x, y + r);
  ctx.quadraticCurveTo(x, y, x + r, y);
}

function currentTrack() {
  return TRACKS[state.selectedTrackId] || TRACKS.silverstone;
}
function startLineProgress() {
  return normalizeProgress(currentTrack().startFinishProgress ?? 0);
}
function statusClass(value) {
  if (!value) return '';

  if (value.includes('Timing')) return 'Timing';
  if (value.includes('Strategy')) return 'Strategy';
  if (value.includes('Telemetry')) return 'Telemetry';
  if (value.includes('Tyre')) return 'Tyre';
  if (value.includes('Pit')) return 'Pit';

  return value.split(' ')[0];
}

function laneForCode(code) {
  const lanePattern = [-1.5, -0.5, 0.5, 1.5];

  if (!state.lanes.has(code)) {
    const nextLane = lanePattern[state.lanes.size % lanePattern.length];
    state.lanes.set(code, nextLane);
  }

  return state.lanes.get(code);
}

function updateTrackUi() {
  const track = currentTrack();

  const circuitLabel = el('circuitLabel');
  if (circuitLabel) {
    circuitLabel.textContent = track.name;
  }

  const selectedTrackMeta = el('selectedTrackMeta');
  if (selectedTrackMeta) {
    selectedTrackMeta.textContent =
      `${track.name} · ${track.country} · ${track.lengthKm.toFixed(3)} km · circuit-specific pit entry and exit loaded.`;
  }

  const trackSelect = el('trackSelect');
  if (trackSelect) {
    trackSelect.value = track.id;
  }
}

function selectTrack(trackId) {
  if (!TRACKS[trackId]) {
    return;
  }

  state.selectedTrackId = trackId;

  state.pitPlans.clear();
  state.visualProgressOffsets.clear();

  for (const [code, car] of state.displayCars.entries()) {
    state.displayCars.set(code, {
      ...car,
      lapProgress: car.lapProgress ?? 0
    });
  }

  updateTrackUi();
}

function resetFrontendRaceState() {
  state.cars = [];
  state.previousCars = new Map();
  state.currentCars = new Map();
  state.displayCars = new Map();

  state.lanes.clear();
  state.pitPlans.clear();
  state.visualProgressOffsets.clear();
  state.visualSpeedMultipliers.clear();
state.visualGapOffsets.clear();
state.visualRaceProgress = startLineProgress();
  state.selectedCode = null;
  state.snapshotAt = performance.now();
  state.telemetryIntervalMs = 900;
  state.strategyRecommendations = [];
state.strategyRecommendationsLoadedAt = 0;
state.strategyRecommendationsLoading = false;

renderStrategyRecommendations([]);

  state.forceStartLineForNewCars = false;
  state.raceStartVisualProgress = startLineProgress();
  state.raceLaunchStartedAt = null;

  renderTiming();
  renderDriverCard();
}
async function loadRaceSnapshot() {
  const snapshot = await fetch('/api/snapshot').then(r => r.json());

  if (!state.raceStarted) {
    return;
  }

  applySnapshot(snapshot);
}

function startTelemetryPolling() {
  stopTelemetryPolling();

  state.telemetryPollId = window.setInterval(() => {
    if (!state.raceStarted) {
      return;
    }

    loadRaceSnapshot().catch(err => {
      console.error('Failed to poll race snapshot', err);
    });
  }, 650);
}

function stopTelemetryPolling() {
  if (state.telemetryPollId !== null) {
    window.clearInterval(state.telemetryPollId);
    state.telemetryPollId = null;
  }
}

async function startRace() {
  const trackSelect = el('trackSelect');

  if (trackSelect) {
    selectTrack(trackSelect.value);
    trackSelect.disabled = true;
  }

 state.raceStarted = true;
resetFrontendRaceState();

state.forceStartLineForNewCars = true;
state.raceStartVisualProgress = startLineProgress();
state.raceLaunchStartedAt = performance.now();
state.visualRaceProgress = startLineProgress();
state.visualGapOffsets.clear();

  const startRaceBtn = el('startRaceBtn');
  if (startRaceBtn) {
    startRaceBtn.textContent = 'Race running';
    startRaceBtn.disabled = true;
  }

  const resetRaceBtn = el('resetRaceBtn');
  if (resetRaceBtn) {
    resetRaceBtn.disabled = false;
  }

  // Use fetch directly so this does not depend on postAction guard logic.
  const resetResponse = await fetch('/api/session/reset', {
    method: 'POST'
  });

  if (!resetResponse.ok) {
    const text = await resetResponse.text();
    alert(text || `Reset failed: ${resetResponse.status}`);

    state.raceStarted = false;

    if (trackSelect) trackSelect.disabled = false;
    if (startRaceBtn) {
      startRaceBtn.textContent = 'Start race';
      startRaceBtn.disabled = false;
    }
    if (resetRaceBtn) resetRaceBtn.disabled = true;

    return;
  }

 await connectSignalR();

// First snapshot creates the cars.
await loadRaceSnapshot();

// Repeated snapshots calculate progressRatePerMs.
// Without this, animation depends entirely on SignalR.
startTelemetryPolling();

updateTrackUi();
}
async function resetRace() {
  stopTelemetryPolling();
  await disconnectSignalR();

  state.raceStarted = false;
  resetFrontendRaceState();
  state.forceStartLineForNewCars = false;
state.raceStartVisualProgress = startLineProgress();

  // Use fetch directly here, not postAction().
  // postAction blocks requests when raceStarted is false.
  const resetResponse = await fetch('/api/session/reset', {
    method: 'POST'
  });

  if (!resetResponse.ok) {
    const text = await resetResponse.text();
    alert(text || `Reset failed: ${resetResponse.status}`);
    return;
  }

  const trackSelect = el('trackSelect');
  if (trackSelect) {
    trackSelect.disabled = false;
  }

  const startRaceBtn = el('startRaceBtn');
  if (startRaceBtn) {
    startRaceBtn.textContent = 'Start race';
    startRaceBtn.disabled = false;
  }

  const resetRaceBtn = el('resetRaceBtn');
  if (resetRaceBtn) {
    resetRaceBtn.disabled = true;
  }

  const [snapshot, events] = await Promise.all([
    fetch('/api/snapshot').then(r => r.json()),
    fetch('/api/events?limit=80').then(r => r.json())
  ]);

  // Again: do NOT call applySnapshot(snapshot) while race is stopped.
  state.services = snapshot.services || [];
  state.incidents = snapshot.incidents || [];
  state.deployment = snapshot.deployment;
  state.mode = snapshot.mode || 'Normal operations';
  state.lap = '-';

  state.events = events || [];

  renderServices();
  renderIncidents();
  renderDeployment();
  renderEvents();
  renderTiming();
  renderDriverCard();
  updateHeader();
  updateTrackUi();

  el('connectionState').textContent = 'Stopped';
}

function queuePitStop(code) {
  if (!state.raceStarted) {
    alert('Start the race first.');
    return;
  }

  const car = state.displayCars.get(code) || state.currentCars.get(code);

  if (!car) {
    alert('Selected car is not available.');
    return;
  }

  if (state.pitPlans.has(code)) {
    return;
  }

  state.pitPlans.set(code, {
    phase: 'queued',
    requestedAt: performance.now(),
    lastProgress: normalizeProgress(car.lapProgress ?? 0)
  });

  state.selectedCode = code;
  renderDriverCard();
}

async function postAction(url) {
  if (!state.raceStarted && !url.startsWith('/api/report')) {
    alert('Start the race first.');
    return null;
  }

  const res = await fetch(url, { method: 'POST' });

  if (!res.ok) {
    const text = await res.text();
    alert(text || `Request failed: ${res.status}`);
    return null;
  }

  const contentType = res.headers.get('content-type') || '';

  let payload = null;

if (contentType.includes('application/json')) {
  payload = await res.json();
}

void loadStrategyRecommendations(true);

return payload;
}

document.querySelectorAll('[data-action]').forEach(button => {
  button.addEventListener('click', () => {
    const demoScenario = button.dataset.demoScenario;

    if (demoScenario) {
      renderScenarioExplainer(demoScenario);
    }

    postAction(button.dataset.action);
  });
});

const trackSelect = el('trackSelect');
if (trackSelect) {
  trackSelect.addEventListener('change', () => {
    if (state.raceStarted) {
      return;
    }

    selectTrack(trackSelect.value);
  });
}

const startRaceBtn = el('startRaceBtn');
if (startRaceBtn) {
  startRaceBtn.addEventListener('click', () => {
    void startRace();
  });
}

const resetRaceBtn = el('resetRaceBtn');
if (resetRaceBtn) {
  resetRaceBtn.addEventListener('click', () => {
    void resetRace();
  });
}

const reportBtn = el('reportBtn');
if (reportBtn) {
  reportBtn.addEventListener('click', async () => {
    const res = await fetch('/api/report');
    el('reportOutput').textContent = await res.text();
  });
}

const pitSelectedBtn = el('pitSelectedBtn');
if (pitSelectedBtn) {
  pitSelectedBtn.addEventListener('click', () => {
    if (!state.selectedCode) {
      alert('Select a car first.');
      return;
    }

    queuePitStop(state.selectedCode);
  });
}

async function bootstrap() {
  updateTrackUi();

  const [snapshot, events] = await Promise.all([
    fetch('/api/snapshot').then(r => r.json()),
    fetch('/api/events?limit=80').then(r => r.json())
  ]);

  // Do NOT call applySnapshot(snapshot) here.
  // That would load live car data before the race starts.
  state.services = snapshot.services || [];
  state.incidents = snapshot.incidents || [];
  state.deployment = snapshot.deployment;
  state.mode = snapshot.mode || 'Normal operations';
  state.lap = '-';

  state.events = events || [];

  renderServices();
  renderIncidents();
  renderDeployment();
  renderEvents();
  renderTiming();
  renderDriverCard();
  updateHeader();

  el('connectionState').textContent = 'Stopped';

  requestAnimationFrame(draw);
}

function applySnapshot(snapshot) {
  state.services = snapshot.services || [];
  state.incidents = snapshot.incidents || [];
  state.deployment = snapshot.deployment;
  state.mode = snapshot.mode || 'Normal operations';
  state.lap = snapshot.lap || '-';

  receiveCars(snapshot.cars || []);

  renderServices();
renderIncidents();
renderDeployment();
renderTiming();
updateHeader();

void loadStrategyRecommendations(true);
}
function progressRateFromSpeedKph(speedKph) {
  const speed = Number(speedKph);

  if (!Number.isFinite(speed) || speed <= 0) {
    return 0.00002;
  }

  // speed km/h -> km/ms -> laps/ms
  return (speed / 3_600_000) / currentTrack().lengthKm;
}
function startLineProgress() {
  return normalizeProgress(currentTrack().startFinishProgress ?? 0);
}

function launchAccelerationFactor() {
  if (!state.raceLaunchStartedAt) {
    return 1;
  }

  const elapsed = performance.now() - state.raceLaunchStartedAt;
  const t = Math.max(0, Math.min(1, elapsed / state.raceLaunchDurationMs));

  // Smooth F1-style launch: not instant full speed.
  const eased = t * t * (3 - 2 * t);

  // Small minimum so cars visibly leave the line immediately.
  return 0.08 + eased * 0.92;
}

function progressRateFromSpeedKph(speedKph) {
  const speed = Number(speedKph);
  const trackLengthKm = currentTrack().lengthKm;

  if (!Number.isFinite(speed) || speed <= 0 || !Number.isFinite(trackLengthKm) || trackLengthKm <= 0) {
    return 0.00001;
  }

  // km/h -> km/ms -> laps/ms
  return (speed / 3_600_000) / trackLengthKm;
}

function visualSpeedMultiplier(code) {
  if (state.visualSpeedMultipliers.has(code)) {
    return state.visualSpeedMultipliers.get(code);
  }

  let hash = 0;

  for (const char of code) {
    hash = (hash * 31 + char.charCodeAt(0)) >>> 0;
  }

  // Deterministic spread: cars can separate and overtake visually.
  const multiplier = (0.96 + (hash % 13) * 0.008) * 2.1;

  state.visualSpeedMultipliers.set(code, multiplier);
  return multiplier;
}
function receiveCars(cars) {
  if (!state.raceStarted) {
    return;
  }

  state.previousCars = new Map(state.currentCars);
  state.currentCars = new Map(cars.map(car => [car.code, car]));
  state.cars = cars;
  state.snapshotAt = performance.now();

  for (const car of cars) {
    if (!state.displayCars.has(car.code)) {
      state.displayCars.set(car.code, {
        ...car,
        lapProgress: state.visualRaceProgress,
        progressRatePerMs: 0
      });

      state.visualGapOffsets.set(car.code, 0);
    }

    laneForCode(car.code);
  }

  for (const code of state.displayCars.keys()) {
    if (!state.currentCars.has(code)) {
      state.displayCars.delete(code);
      state.lanes.delete(code);
      state.pitPlans.delete(code);
      state.visualProgressOffsets.delete(code);
      state.visualSpeedMultipliers.delete(code);
      state.visualGapOffsets.delete(code);
    }
  }

  if (!state.selectedCode && cars.length) {
    state.selectedCode = cars[0].code;
  }

  state.lap = cars.length
    ? Math.max(...cars.map(c => c.lap || 1))
    : '-';

  if (state.forceStartLineForNewCars && cars.length) {
    state.forceStartLineForNewCars = false;
  }

  renderTiming();
renderDriverCard();
updateHeader();

void loadStrategyRecommendations();
}

function updateHeader() {
  el('modeLabel').textContent = state.mode;
  el('lapLabel').textContent = state.lap;
  updateTrackUi();
}

function renderServices() {
  el('serviceGrid').innerHTML = state.services.map(s => `
    <div class="service-card">
      <strong>${s.name}</strong>
      <span class="badge ${s.status}">${s.status}</span>
      <small>${s.message}</small>
      <small>${s.latencyMs} ms · ${Number(s.packetLossPercent).toFixed(1)}% packet loss</small>
    </div>
  `).join('');
}

function renderIncidents() {
  const list = el('incidentList');

  if (!state.incidents.length) {
    list.className = 'incident-list empty';
    list.textContent = 'No incidents yet.';
    return;
  }

  list.className = 'incident-list';
  list.innerHTML = state.incidents.map(i => `
    <div class="incident">
      <strong>${i.id} — ${i.title}</strong>
      <span>${i.service} · ${i.status}</span>
      <p>${i.impact}</p>
    </div>
  `).join('');
}

function renderDeployment() {
  const d = state.deployment;
  if (!d) return;

  el('deploymentCard').innerHTML = `
    <strong>${d.service}</strong><br/>
    <span class="badge ${d.status}">${d.status}</span>
    <p>Stable: ${d.currentVersion} · Candidate: ${d.candidateVersion}</p>
    <p>Canary: ${d.canaryPercent}%</p>
    <p>${d.message}</p>
  `;
}

function renderEvents() {
  const recent = state.events.slice(-40).reverse();

  el('eventLog').innerHTML = recent.map(e => `
    <div class="event">
      <strong>${new Date(e.timestamp).toLocaleTimeString()} · ${e.eventType}</strong>
      <span>${e.service || 'system'}</span>
      <div>${e.message}</div>
    </div>
  `).join('');
}

function renderTiming() {

    if (!state.raceStarted) {
    el('timingBody').innerHTML = `
      <tr>
        <td colspan="6">Start the race to load live timing.</td>
      </tr>
    `;
    return;
  }
  const rows = [...state.cars].sort((a, b) => a.position - b.position);

  el('timingBody').innerHTML = rows.map(car => `
    <tr data-code="${car.code}">
      <td>${car.position}</td>
      <td><strong>${car.code}</strong></td>
      <td>${car.position === 1 ? 'LEAD' : '+' + car.gapToLeaderSeconds.toFixed(1)}</td>
      <td>${car.tyre}</td>
      <td>${car.tyreAge}</td>
      <td><span class="badge ${statusClass(car.status)}">${car.status}</span></td>
    </tr>
  `).join('');

  document.querySelectorAll('#timingBody tr').forEach(row => {
    row.addEventListener('click', () => {
      state.selectedCode = row.dataset.code;
      renderDriverCard();
    });
  });
}

function renderDriverCard() {
    if (!state.raceStarted) {
    el('driverCard').innerHTML = `
      <h3>Selected car</h3>
      <p class="muted">Start the race, then click a moving car or timing row.</p>
    `;
    return;
  }
  const car = state.currentCars.get(state.selectedCode) || state.cars[0];

  if (!car) {
    el('driverCard').innerHTML = '<p>No car selected.</p>';
    return;
  }

  const pitPlan = state.pitPlans.get(car.code);
  const pitInfo = pitPlan
    ? `<p>Pit request: <span class="badge Pit">${pitPlan.phase === 'queued' ? 'Queued' : 'Active'}</span></p>`
    : '';

  el('driverCard').innerHTML = `
    <h3>${car.code}</h3>
    <p><strong>${car.driver}</strong></p>
    <p>Position: P${car.position}</p>
    <p>Lap: ${car.lap} · Speed: ${car.speedKph} km/h</p>
    <p>Tyre: ${car.tyre}, age ${car.tyreAge}</p>
    <p>Gap to leader: ${car.position === 1 ? 'Leader' : '+' + car.gapToLeaderSeconds.toFixed(1) + 's'}</p>
    <p>Status: <span class="badge ${statusClass(car.status)}">${car.status}</span></p>
    ${pitInfo}
  `;
}
async function loadStrategyRecommendations(force = false) {
  const panel = el('strategyRecommendations');

  if (!panel) {
    return;
  }

  if (!state.raceStarted) {
    state.strategyRecommendations = [];
    renderStrategyRecommendations([]);
    return;
  }

  const now = performance.now();

  if (!force && now - state.strategyRecommendationsLoadedAt < 2000) {
    return;
  }

  if (state.strategyRecommendationsLoading) {
    return;
  }

  state.strategyRecommendationsLoading = true;

  try {
    const response = await fetch('/api/strategy/recommendations');

    if (!response.ok) {
      throw new Error(`Strategy request failed: ${response.status}`);
    }

    const recommendations = await response.json();

    state.strategyRecommendations = recommendations;
    state.strategyRecommendationsLoadedAt = now;

    renderStrategyRecommendations(recommendations);
  } catch (err) {
    console.error('Failed to load strategy recommendations', err);
    panel.textContent = 'Strategy recommendations unavailable.';
  } finally {
    state.strategyRecommendationsLoading = false;
  }
}
function renderScenarioExplainer(scenarioKey) {
  const panel = el('scenarioExplainer');

  if (!panel) {
    return;
  }

  const scenario = DEMO_SCENARIO_EXPLAINERS[scenarioKey];

  if (!scenario) {
    panel.innerHTML = `
      <strong>No demo scenario selected</strong>
      <span>Use the compact scenario buttons to force a backend strategy state.</span>
    `;
    return;
  }

  panel.innerHTML = `
    <strong>${scenario.title}</strong>
    <span>${scenario.body}</span>
    <small>${scenario.watch}</small>
  `;
}
function renderStrategyRecommendations(recommendations) {
  const panel = el('strategyRecommendations');

  if (!panel) {
    return;
  }

  if (!state.raceStarted) {
    panel.textContent = 'Start the race to load strategy recommendations.';
    return;
  }

  if (!recommendations.length) {
    panel.textContent = 'No strategy recommendations available.';
    return;
  }

  panel.innerHTML = recommendations.map(item => {
    const factors = Array.isArray(item.factors) ? item.factors : [];

    return `
      <div class="strategy-card">
        <div class="strategy-card-head">
          <div>
            <strong>${item.carCode} — ${item.action}</strong>
            <small>${item.racePhase} · confidence ${Math.round(Number(item.confidence) * 100)}%</small>
          </div>
          <span class="badge ${item.urgency}">${item.urgency}</span>
        </div>

        <p>${item.reason}</p>

        <div class="strategy-metrics">
          <span>Lap ${item.currentLap}</span>
          <span>${item.currentTyre} age ${item.tyreAge}</span>
          <span>Gap ${Number(item.gapToLeaderSeconds).toFixed(1)}s</span>
          <span>Rejoin P${item.projectedRejoinPosition}</span>
          <span>Lose ${item.positionsLostIfPit} pos</span>
          <span>Pit loss ${Number(item.pitLossSeconds).toFixed(0)}s</span>
          <span>Tyre pressure ${Math.round(Number(item.tyreLifePressure) * 100)}%</span>
          <span>Undercut ${Math.round(Number(item.undercutScore) * 100)}%</span>
          <span>Overcut ${Math.round(Number(item.overcutScore) * 100)}%</span>
          <span>Cover ${Math.round(Number(item.coverThreatScore) * 100)}%</span>
          <span>Next tyre: ${item.recommendedTyre}</span>
        </div>

        <div class="strategy-factors">
          ${factors.map(factor => `<span>${factor}</span>`).join('')}
        </div>
      </div>
    `;
  }).join('');
}
async function connectSignalR() {
  if (!window.signalR) {
    el('connectionState').textContent = 'SignalR client not loaded';
    return;
  }

  if (state.connection) {
    return;
  }

  const connection = new signalR.HubConnectionBuilder()
    .withUrl('/opsHub')
    .withAutomaticReconnect()
    .build();

  state.connection = connection;

  connection.onreconnecting(() => {
    el('connectionState').textContent = 'Reconnecting';
  });

  connection.onreconnected(() => {
    el('connectionState').textContent = state.raceStarted ? 'Connected' : 'Stopped';
  });

  connection.onclose(() => {
    el('connectionState').textContent = state.raceStarted ? 'Disconnected' : 'Stopped';
    state.connection = null;
  });

  connection.on('TelemetryUpdated', cars => {
    if (!state.raceStarted) return;
    receiveCars(cars);
  });

  connection.on('ServicesUpdated', services => {
  if (!state.raceStarted) return;
  state.services = services;
  renderServices();
  void loadStrategyRecommendations(true);
});

  connection.on('IncidentsUpdated', incidents => {
    if (!state.raceStarted) return;
    state.incidents = incidents;
    renderIncidents();
  });

  connection.on('DeploymentUpdated', deployment => {
    if (!state.raceStarted) return;
    state.deployment = deployment;
    renderDeployment();
  });

  connection.on('EventLogged', event => {
    if (!state.raceStarted) return;
    state.events.push(event);
    renderEvents();
  });

  connection.on('AlertRaised', alert => {
    if (!state.raceStarted) return;

    state.events.push({
      timestamp: alert.timestamp,
      eventType: 'AlertRaised',
      service: 'Strategy/Operations',
      message: `${alert.title}: ${alert.message}`
    });

    renderEvents();
  });

  try {
    await connection.start();
    el('connectionState').textContent = 'Connected';
  } catch (err) {
    el('connectionState').textContent = 'Failed';
    console.error(err);
    state.connection = null;
  }
}
async function disconnectSignalR() {
  const connection = state.connection;

  if (!connection) {
    el('connectionState').textContent = 'Stopped';
    return;
  }

  state.connection = null;

  try {
    await connection.stop();
  } catch (err) {
    console.error(err);
  }

  el('connectionState').textContent = 'Stopped';
}
function normalizeProgress(progress) {
  return ((progress % 1) + 1) % 1;
}

function canvasPoint(point) {
  return {
    x: point.x * canvas.width,
    y: point.y * canvas.height
  };
}

function currentTrackPoints() {
  return currentTrack().points.map(canvasPoint);
}

function trackMetrics() {
  const points = currentTrackPoints();
  const segments = [];
  let totalLength = 0;

  for (let i = 0; i < points.length; i++) {
    const a = points[i];
    const b = points[(i + 1) % points.length];
    const length = Math.hypot(b.x - a.x, b.y - a.y);

    segments.push({
      a,
      b,
      length,
      startDistance: totalLength,
      endDistance: totalLength + length
    });

    totalLength += length;
  }

  return {
    points,
    segments,
    totalLength
  };
}

function trackPoint(progress, lane = 0) {
  const metrics = trackMetrics();
  const distance = normalizeProgress(progress) * metrics.totalLength;

  let segment = metrics.segments[metrics.segments.length - 1];

  for (const candidate of metrics.segments) {
    if (distance >= candidate.startDistance && distance <= candidate.endDistance) {
      segment = candidate;
      break;
    }
  }

  const segmentDistance = distance - segment.startDistance;
  const t = segment.length === 0 ? 0 : segmentDistance / segment.length;

  const x = segment.a.x + (segment.b.x - segment.a.x) * t;
  const y = segment.a.y + (segment.b.y - segment.a.y) * t;

  const dx = segment.b.x - segment.a.x;
  const dy = segment.b.y - segment.a.y;
  const angle = Math.atan2(dy, dx);

  const laneOffsetPx = lane * 8;
  const nx = -Math.sin(angle);
  const ny = Math.cos(angle);

  return {
    x: x + nx * laneOffsetPx,
    y: y + ny * laneOffsetPx,
    angle
  };
}

function easeInOut(t) {
  const clamped = Math.max(0, Math.min(1, t));
  return clamped * clamped * (3 - 2 * clamped);
}

function lerp(a, b, t) {
  return a + (b - a) * t;
}

function progressPassed(from, to, target) {
  const start = normalizeProgress(from);
  const end = normalizeProgress(to);
  const point = normalizeProgress(target);

  if (start <= end) {
    return point >= start && point <= end;
  }

  return point >= start || point <= end;
}

function quadraticBezier(a, control, b, t) {
  const oneMinusT = 1 - t;

  return {
    x: oneMinusT * oneMinusT * a.x + 2 * oneMinusT * t * control.x + t * t * b.x,
    y: oneMinusT * oneMinusT * a.y + 2 * oneMinusT * t * control.y + t * t * b.y,
    angle: Math.atan2(b.y - a.y, b.x - a.x)
  };
}

function pitEntryProgress() {
  return currentTrack().pitEntryProgress;
}

function pitExitProgress() {
  return currentTrack().pitExitProgress;
}

function pitLaneBox() {
  const box = currentTrack().pitBox;

  return {
    x: box.x * canvas.width,
    y: box.y * canvas.height,
    width: box.width * canvas.width,
    height: box.height * canvas.height
  };
}

function pitStallPoint() {
  const box = pitLaneBox();

  return {
    x: box.x + box.width / 2,
    y: box.y + box.height / 2,
    angle: 0
  };
}

function startPitAnimation(car) {
  const code = car.code;
  const lane = laneForCode(code);

  const entryPoint = trackPoint(pitEntryProgress(), lane);
  const exitPoint = trackPoint(pitExitProgress(), lane);
  const stall = pitStallPoint();

  const plan = {
    phase: 'active',
    startedAt: performance.now(),
    durationMs: PIT_ANIMATION_MS,
    entryFrom: entryPoint,
    stall,
    exitTo: exitPoint,
    trackId: currentTrack().id
  };

  state.pitPlans.set(code, plan);

  void postAction(`/api/cars/${encodeURIComponent(code)}/pit`);

  renderDriverCard();

  return plan;
}

function pitPlanForCar(car) {
  const code = car.code;
  const plan = state.pitPlans.get(code);

  if (!plan) {
    return null;
  }

  const currentProgress = normalizeProgress(car.lapProgress ?? 0);

  if (plan.phase === 'queued') {
    const hasReachedPitEntry = progressPassed(
      plan.lastProgress,
      currentProgress,
      pitEntryProgress()
    );

    plan.lastProgress = currentProgress;

    if (!hasReachedPitEntry) {
      return null;
    }

    return startPitAnimation(car);
  }

  return plan;
}

function visualPointForCar(car) {
  const lane = laneForCode(car.code);
  const normalTrackPoint = trackPoint(car.lapProgress, lane);
  const plan = pitPlanForCar(car);

  if (!plan || plan.phase !== 'active') {
    return {
      ...normalTrackPoint,
      isPitAnimating: false,
      pitPhase: state.pitPlans.has(car.code) ? 'queued' : 'none'
    };
  }

  const elapsed = performance.now() - plan.startedAt;
  const t = Math.max(0, Math.min(1, elapsed / plan.durationMs));
  const stall = pitStallPoint();

  if (t < 0.32) {
    const phaseT = easeInOut(t / 0.32);

    const control = {
      x: lerp(plan.entryFrom.x, stall.x, 0.52),
      y: Math.min(plan.entryFrom.y, stall.y) - 85
    };

    return {
      ...quadraticBezier(plan.entryFrom, control, stall, phaseT),
      isPitAnimating: true,
      pitPhase: 'entry'
    };
  }

  if (t < 0.68) {
    return {
      ...stall,
      angle: 0,
      isPitAnimating: true,
      pitPhase: 'service'
    };
  }

  if (t < 1) {
    const phaseT = easeInOut((t - 0.68) / 0.32);

    const control = {
      x: lerp(stall.x, plan.exitTo.x, 0.52),
      y: Math.min(stall.y, plan.exitTo.y) - 85
    };

    return {
      ...quadraticBezier(stall, control, plan.exitTo, phaseT),
      isPitAnimating: true,
      pitPhase: 'exit'
    };
  }

  const backendProgress = normalizeProgress(car.lapProgress ?? pitExitProgress());
  const offset = shortestProgressDelta(backendProgress, pitExitProgress());

  state.visualProgressOffsets.set(car.code, offset);
  state.pitPlans.delete(car.code);

  const displayCar = state.displayCars.get(car.code);

  if (displayCar) {
    state.displayCars.set(car.code, {
      ...displayCar,
      lapProgress: pitExitProgress()
    });
  }

  renderDriverCard();

  return {
    ...trackPoint(pitExitProgress(), lane),
    isPitAnimating: false,
    pitPhase: 'none'
  };
}

function drawPolyline(points, lineWidth, strokeStyle, dashed = false) {
  ctx.strokeStyle = strokeStyle;
  ctx.lineWidth = lineWidth;

  if (dashed) {
    ctx.setLineDash([18, 18]);
  } else {
    ctx.setLineDash([]);
  }

  ctx.beginPath();

  points.forEach((point, index) => {
    if (index === 0) {
      ctx.moveTo(point.x, point.y);
    } else {
      ctx.lineTo(point.x, point.y);
    }
  });

  ctx.closePath();
  ctx.stroke();
  ctx.setLineDash([]);
}

function drawPitLaneMarker() {
  const box = pitLaneBox();

  ctx.save();

  ctx.strokeStyle = 'rgba(114,199,255,0.55)';
  ctx.fillStyle = 'rgba(114,199,255,0.08)';
  ctx.lineWidth = 2;

  ctx.beginPath();
  roundedRectPath(box.x, box.y, box.width, box.height, 10);
  ctx.fill();
  ctx.stroke();

  ctx.fillStyle = 'rgba(255,255,255,0.82)';
  ctx.font = '900 13px system-ui';
  ctx.fillText('PIT LANE', box.x + 18, box.y + box.height / 2 + 5);

  ctx.restore();
}

function drawTrack() {
  ctx.clearRect(0, 0, canvas.width, canvas.height);
  ctx.save();

  ctx.lineCap = 'round';
  ctx.lineJoin = 'round';

  ctx.strokeStyle = 'rgba(255,255,255,0.035)';
  ctx.lineWidth = 1;

  for (let x = 40; x < canvas.width; x += 40) {
    ctx.beginPath();
    ctx.moveTo(x, 30);
    ctx.lineTo(x, canvas.height - 30);
    ctx.stroke();
  }

  for (let y = 40; y < canvas.height; y += 40) {
    ctx.beginPath();
    ctx.moveTo(30, y);
    ctx.lineTo(canvas.width - 30, y);
    ctx.stroke();
  }

  const points = currentTrackPoints();

  drawPolyline(points, 64, 'rgba(255,255,255,0.18)');
  drawPolyline(points, 48, 'rgba(10,14,22,0.96)');
  drawPolyline(points, 3, 'rgba(255,255,255,0.28)', true);

  const track = currentTrack();
  const start = trackPoint(track.startFinishProgress);
  const normalAngle = start.angle + Math.PI / 2;
  const halfLineLength = 42;

  const sx1 = start.x + Math.cos(normalAngle) * halfLineLength;
  const sy1 = start.y + Math.sin(normalAngle) * halfLineLength;
  const sx2 = start.x - Math.cos(normalAngle) * halfLineLength;
  const sy2 = start.y - Math.sin(normalAngle) * halfLineLength;

  ctx.strokeStyle = 'rgba(255,255,255,0.9)';
  ctx.lineWidth = 5;
  ctx.beginPath();
  ctx.moveTo(sx1, sy1);
  ctx.lineTo(sx2, sy2);
  ctx.stroke();

  ctx.fillStyle = 'rgba(255,255,255,0.78)';
  ctx.font = '800 13px system-ui';
  ctx.fillText('START / FINISH', start.x + 42, start.y - 24);

  for (const sector of track.sectors) {
    const pt = trackPoint(sector.progress);

    ctx.fillStyle = 'rgba(114,199,255,0.9)';
    ctx.font = '800 14px system-ui';
    ctx.fillText(sector.label, pt.x + 14, pt.y - 14);
  }

  drawPitLaneMarker();

  ctx.fillStyle = 'rgba(255,255,255,0.72)';
  ctx.font = '700 13px system-ui';
  ctx.fillText(`${track.name} · ${track.lengthKm.toFixed(3)} km`, 72, 86);

  if (!state.raceStarted) {
    ctx.fillStyle = 'rgba(255,255,255,0.72)';
    ctx.font = '900 18px system-ui';
    ctx.fillText('SELECT CIRCUIT, THEN START RACE', 72, 120);
  }

  ctx.restore();
}

function shortestProgressDelta(from, to) {
  let delta = to - from;

  if (delta > 0.5) {
    delta -= 1;
  }

  if (delta < -0.5) {
    delta += 1;
  }

  return delta;
}
function progressRateFromSpeedKph(speedKph) {
  const speed = Number(speedKph);
  const trackLengthKm = currentTrack().lengthKm;

  if (!Number.isFinite(speed) || speed <= 0 || !Number.isFinite(trackLengthKm) || trackLengthKm <= 0) {
    return 0.00001;
  }

  // km/h -> km/ms -> laps/ms
  return (speed / 3_600_000) / trackLengthKm;
}

function clamp(value, min, max) {
  return Math.max(min, Math.min(max, value));
}

function targetGapOffsetForCar(car) {
  const position = Number(car.position);
  const gapSeconds = Number(car.gapToLeaderSeconds);

  if (position === 1) {
    return 0;
  }

  const positionSpacing = Number.isFinite(position) && position > 1
    ? (position - 1) * 0.018
    : 0.04;

  if (Number.isFinite(gapSeconds) && gapSeconds > 0) {
    // Convert race seconds behind the leader into track distance.
    // Use a wider clamp so pitted cars do not all collapse into the same visual point.
    const gapOffset = gapSeconds / 92;

    return Math.max(
      0.012,
      Math.min(0.78, Math.max(gapOffset, positionSpacing))
    );
  }

  return Math.max(0.012, Math.min(0.78, positionSpacing));
}

function leaderSpeedRateFromCars() {
  const leader = [...state.currentCars.values()]
    .find(car => Number(car.position) === 1);

  if (leader) {
    return progressRateFromSpeedKph(leader.speedKph) * 1.5;
  }

  const firstCar = [...state.currentCars.values()][0];

  if (firstCar) {
    return progressRateFromSpeedKph(firstCar.speedKph) * 1.5;
  }

  return 0.000015;
}
function updateDisplayCars(deltaMs) {
  if (!state.raceStarted) {
    return;
  }

  const dt = Math.max(0, Math.min(deltaMs, 50));
  const acceleration = launchAccelerationFactor();

  const leaderRate = leaderSpeedRateFromCars() * acceleration;
  const baseAdvance = leaderRate * dt;

  state.visualRaceProgress = normalizeProgress(
    state.visualRaceProgress + baseAdvance
  );

  for (const [code, targetCar] of state.currentCars.entries()) {
    const existing = state.displayCars.get(code);

    const targetGap = targetGapOffsetForCar(targetCar);
    const currentGap = state.visualGapOffsets.get(code) ?? 0;

    const gapDelta = targetGap - currentGap;

    const currentStatus = String(targetCar.status || '').toLowerCase();
    const previousStatus = String(existing?.status || '').toLowerCase();

    const pitStateChanged =
      currentStatus.includes('pit') ||
      previousStatus.includes('pit');

    const maxGapStep = Math.max(baseAdvance * 0.75, 0.00004);

    const nextGap = pitStateChanged
      ? targetGap
      : currentGap + clamp(gapDelta, -maxGapStep, maxGapStep);

    state.visualGapOffsets.set(code, nextGap);

    const nextProgress = normalizeProgress(
      state.visualRaceProgress - nextGap
    );

    state.displayCars.set(code, {
      ...targetCar,
      lapProgress: nextProgress,
      progressRatePerMs: leaderRate
    });

    laneForCode(code);
  }

  for (const code of state.displayCars.keys()) {
    if (!state.currentCars.has(code)) {
      state.displayCars.delete(code);
      state.lanes.delete(code);
      state.pitPlans.delete(code);
      state.visualProgressOffsets.delete(code);
      state.visualSpeedMultipliers.delete(code);
      state.visualGapOffsets.delete(code);
    }
  }
}
function drawCars() {

  if (!state.raceStarted) {
    return;
  }

  const cars = [...state.displayCars.values()].sort((a, b) => b.position - a.position);
  const now = performance.now();

  for (const car of cars) {
    const p = visualPointForCar(car);

    const isSelected = car.code === state.selectedCode;
    const cls = statusClass(car.status);
    const isPitAnimating = p.isPitAnimating;
    const isPitQueued = p.pitPhase === 'queued';

    const colour =
      cls === 'Strategy' ? '#ff5c7a' :
      cls === 'Timing' ? '#ffd166' :
      cls === 'Telemetry' ? '#ff9b5c' :
      cls === 'Tyre' ? '#ffd166' :
      cls === 'Pit' || isPitAnimating || isPitQueued ? '#72c7ff' :
      car.team === 'Alpine' ? '#ff7f2a' :
      '#72c7ff';

    ctx.save();
    ctx.translate(p.x, p.y);

    if (isPitQueued) {
      ctx.save();
      ctx.fillStyle = 'rgba(114,199,255,0.16)';
      ctx.strokeStyle = 'rgba(114,199,255,0.55)';
      ctx.lineWidth = 1.5;
      ctx.beginPath();
      roundedRectPath(-34, -42, 68, 22, 6);
      ctx.fill();
      ctx.stroke();

      ctx.fillStyle = '#ffffff';
      ctx.font = '900 10px system-ui';
      ctx.textAlign = 'center';
      ctx.fillText('PIT QUEUED', 0, -27);
      ctx.restore();
    }

    if (isPitAnimating) {
      const pulse = 20 + Math.sin(now / 120) * 5;

      ctx.save();
      ctx.setLineDash([4, 5]);
      ctx.strokeStyle = 'rgba(114,199,255,0.95)';
      ctx.lineWidth = 3;
      ctx.beginPath();
      ctx.arc(0, 0, pulse, 0, Math.PI * 2);
      ctx.stroke();
      ctx.setLineDash([]);
      ctx.restore();

      ctx.save();
      ctx.fillStyle = 'rgba(114,199,255,0.18)';
      ctx.strokeStyle = 'rgba(114,199,255,0.7)';
      ctx.lineWidth = 1.5;
      ctx.beginPath();
      roundedRectPath(-28, -42, 56, 22, 6);
      ctx.fill();
      ctx.stroke();

      ctx.fillStyle = '#ffffff';
      ctx.font = '900 11px system-ui';
      ctx.textAlign = 'center';

      const label =
        p.pitPhase === 'entry' ? 'IN' :
        p.pitPhase === 'service' ? 'PIT' :
        p.pitPhase === 'exit' ? 'OUT' :
        'PIT';

      ctx.fillText(label, 0, -27);
      ctx.restore();
    }

    ctx.rotate(p.angle + Math.PI / 2);

    ctx.shadowColor = colour;
    ctx.shadowBlur = isSelected || isPitAnimating || isPitQueued ? 24 : 12;
    ctx.fillStyle = colour;
    ctx.strokeStyle = isSelected ? '#ffffff' : 'rgba(255,255,255,0.35)';
    ctx.lineWidth = isSelected ? 3 : 1.5;

    ctx.beginPath();
    ctx.moveTo(0, -12);
    ctx.lineTo(10, 9);
    ctx.lineTo(0, 5);
    ctx.lineTo(-10, 9);
    ctx.closePath();
    ctx.fill();
    ctx.stroke();

    if ((cls === 'Timing' || cls === 'Tyre') && !isPitAnimating && !isPitQueued) {
      ctx.setLineDash([2, 4]);
      ctx.strokeStyle = colour;
      ctx.lineWidth = 2;
      ctx.beginPath();
      ctx.arc(0, 0, 18, 0, Math.PI * 2);
      ctx.stroke();
      ctx.setLineDash([]);
    }

    ctx.restore();

    ctx.save();
    ctx.font = isSelected ? '900 14px system-ui' : '800 12px system-ui';
    ctx.fillStyle = isSelected ? '#ffffff' : 'rgba(255,255,255,0.82)';
    ctx.fillText(car.code, p.x + 14, p.y - 12);
    ctx.restore();
  }
}

canvas.addEventListener('click', event => {
  if (!state.raceStarted) {
    return;
  }

  const rect = canvas.getBoundingClientRect();

  const x = (event.clientX - rect.left) * (canvas.width / rect.width);
  const y = (event.clientY - rect.top) * (canvas.height / rect.height);

  let nearest = null;
  let nearestDist = Infinity;

  for (const car of state.displayCars.values()) {
    const p = visualPointForCar(car);
    const d = Math.hypot(p.x - x, p.y - y);

    if (d < nearestDist) {
      nearestDist = d;
      nearest = car;
    }
  }

  if (nearest && nearestDist < 40) {
    state.selectedCode = nearest.code;
    renderDriverCard();
  }
});

function draw(now) {
  const deltaMs = now - state.lastFrameAt;
  state.lastFrameAt = now;

  try {
    updateDisplayCars(deltaMs);
    drawTrack();
    drawCars();
  } catch (err) {
    console.error('Canvas draw failed', err);
  }

  requestAnimationFrame(draw);
}

bootstrap().catch(err => {
  console.error(err);
  el('connectionState').textContent = 'Failed';
});