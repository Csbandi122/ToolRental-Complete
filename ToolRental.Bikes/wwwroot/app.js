const ROUTES = {
  home: {
    title: 'Főoldal',
    subtitle: 'Innen fog indulni majd a teljes webes rendszer.'
  },
  bikes: {
    title: 'Eszközkezelő',
    subtitle: 'A most elkészült modul mostantól külön menüpontként működik.'
  },
  settings: {
    title: 'Beállítások',
    subtitle: 'Desktop logika, webes feltöltéssel és szerveroldali mentéssel.'
  }
};

const TYPE_ORDER = [
  'Férfi kerékpár',
  'Női kerékpár',
  'Férfi e-bike',
  'Női e-bike',
  'Gyerekbicikli',
  'Gyerekülés',
  'Utánfutó'
];

const TYPE_ICONS = {
  'Férfi kerékpár': '🚲',
  'Női kerékpár': '🚲',
  'Férfi e-bike': '⚡',
  'Női e-bike': '⚡',
  'Gyerekbicikli': '🔵',
  'Gyerekülés': '👶',
  'Utánfutó': '🛻'
};

const FILE_FIELDS = {
  companyLogo: { inputId: 'companyLogoInput', clearField: 'clearCompanyLogo' },
  templateContract: { inputId: 'templateContractInput', clearField: 'clearTemplateContract' },
  aszfFile: { inputId: 'aszfFileInput', clearField: 'clearAszfFile' },
  contractEmailTemplate: { inputId: 'contractEmailTemplateInput', clearField: 'clearContractEmailTemplate' },
  reviewEmailTemplate: { inputId: 'reviewEmailTemplateInput', clearField: 'clearReviewEmailTemplate' },
  invoiceXml: { inputId: 'invoiceXmlInput', clearField: 'clearInvoiceXml' }
};

const state = {
  currentRoute: 'home',
  currentDbMode: localStorage.getItem('toolrental-admin-db-mode') === 'test' ? 'test' : 'prod',
  allBikes: [],
  activeFilter: null,
  activeTypeFilter: null,
  releasing: new Set(),
  reserving: new Set(),
  settingsPayload: null,
  settingsFiles: {},
  clearEmailPassword: false,
  settingsLoaded: false,
  loadingSettings: false,
  bikesLoaded: false,
  companyLogoObjectUrl: null
};

function init() {
  document.querySelectorAll('[data-route]').forEach(button => {
    button.addEventListener('click', () => navigate(button.dataset.route));
  });

  document.getElementById('menuToggle').addEventListener('click', openSidebar);
  document.getElementById('sidebarBackdrop').addEventListener('click', closeSidebar);
  document.getElementById('sidebarDbToggle').addEventListener('click', toggleDatabaseMode);

  document.getElementById('refreshBikesButton').addEventListener('click', () => loadBikes(true));
  document.getElementById('filterFreeBtn').addEventListener('click', () => setFilter('free'));
  document.getElementById('filterReservedBtn').addEventListener('click', () => setFilter('reserved'));
  document.getElementById('filterOccupiedBtn').addEventListener('click', () => setFilter('occupied'));

  document.getElementById('testSqlButton').addEventListener('click', testSqlConnection);
  document.getElementById('settingsForm').addEventListener('submit', saveSettings);
  document.getElementById('clearEmailPasswordButton').addEventListener('click', toggleClearEmailPassword);
  document.getElementById('emailPassword').addEventListener('input', () => {
    if (document.getElementById('emailPassword').value.trim()) {
      state.clearEmailPassword = false;
      renderEmailPasswordHint();
    }
  });

  Object.entries(FILE_FIELDS).forEach(([key, meta]) => {
    document.getElementById(meta.inputId).addEventListener('change', event => handleFileSelection(key, event.target.files?.[0] || null));
  });

  document.querySelectorAll('[data-clear-file]').forEach(button => {
    button.addEventListener('click', () => clearFileSelection(button.dataset.clearFile));
  });

  window.addEventListener('hashchange', handleRouteChange);
  document.addEventListener('visibilitychange', () => {
    if (!document.hidden && state.currentRoute === 'bikes') {
      loadBikes(true);
    }
  });

  setInterval(() => {
    if (!document.hidden && state.currentRoute === 'bikes') {
      loadBikes(true);
    }
  }, 60000);

  updateDbIndicators();
  handleRouteChange();
  loadSettings(true, { silent: true });
}

function getRouteFromHash() {
  const hash = window.location.hash.replace(/^#\/?/, '').trim();
  return ROUTES[hash] ? hash : 'home';
}

function navigate(route) {
  window.location.hash = `#/${route}`;
}

function handleRouteChange() {
  state.currentRoute = getRouteFromHash();
  closeSidebar();
  renderRoute();

  if (state.currentRoute === 'bikes' && !state.bikesLoaded) {
    loadBikes();
  }

  if (state.currentRoute === 'settings') {
    loadSettings(true);
  }
}

function renderRoute() {
  const routeMeta = ROUTES[state.currentRoute];
  document.getElementById('pageTitle').textContent = routeMeta.title;
  document.getElementById('pageSubtitle').textContent = routeMeta.subtitle;

  document.querySelectorAll('.page-view').forEach(view => {
    view.hidden = view.dataset.view !== state.currentRoute;
  });

  document.querySelectorAll('.nav-item').forEach(item => {
    item.classList.toggle('active', item.dataset.route === state.currentRoute);
  });
}

function openSidebar() {
  document.getElementById('sidebar').classList.add('open');
  document.getElementById('sidebarBackdrop').classList.add('visible');
}

function closeSidebar() {
  document.getElementById('sidebar').classList.remove('open');
  document.getElementById('sidebarBackdrop').classList.remove('visible');
}

function toggleDatabaseMode() {
  state.currentDbMode = state.currentDbMode === 'prod' ? 'test' : 'prod';
  localStorage.setItem('toolrental-admin-db-mode', state.currentDbMode);
  updateDbIndicators();

  if (state.currentRoute === 'bikes') {
    loadBikes();
  } else if (state.currentRoute === 'settings') {
    loadSettings(true);
  } else {
    loadSettings(true, { silent: true });
  }

  showToast(state.currentDbMode === 'test' ? 'TEST adatbázis aktív' : 'PROD adatbázis aktív');
}

function updateDbIndicators() {
  const label = state.currentDbMode.toUpperCase();
  const sidebarButton = document.getElementById('sidebarDbToggle');
  const topbarIndicator = document.getElementById('topbarDbIndicator');

  sidebarButton.textContent = label;
  topbarIndicator.textContent = label;

  sidebarButton.classList.toggle('prod', state.currentDbMode === 'prod');
  sidebarButton.classList.toggle('test', state.currentDbMode === 'test');
  topbarIndicator.classList.toggle('prod', state.currentDbMode === 'prod');
  topbarIndicator.classList.toggle('test', state.currentDbMode === 'test');

  document.getElementById('settingsModeBanner').textContent =
    `A beállítások most a ${label} adatbázishoz töltődnek be és oda is mentődnek.`;
}

function apiUrl(path) {
  const separator = path.includes('?') ? '&' : '?';
  return `${path}${separator}db=${state.currentDbMode}`;
}

function showToast(message, type = 'success') {
  const toast = document.getElementById('toast');
  toast.textContent = message;
  toast.className = `toast ${type}`;
  window.clearTimeout(showToast._timeout);
  requestAnimationFrame(() => toast.classList.add('show'));
  showToast._timeout = window.setTimeout(() => toast.classList.remove('show'), 2800);
}

function showBanner(id, message, variant = 'info') {
  const banner = document.getElementById(id);
  banner.hidden = false;
  banner.textContent = message;
  banner.className = `status-banner ${variant}`;
}

function hideBanner(id) {
  const banner = document.getElementById(id);
  banner.hidden = true;
  banner.textContent = '';
}

function formatMoney(amount) {
  return Math.round(amount).toLocaleString('hu-HU') + ' Ft';
}

function escHtml(value) {
  return String(value ?? '')
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;');
}

async function loadBikes(silent = false) {
  if (!silent) {
    document.getElementById('bikesLoadingPanel').hidden = false;
  }

  hideBanner('bikesErrorBanner');

  try {
    const response = await fetch(apiUrl('/api/bikes/status'));
    if (!response.ok) {
      throw new Error(`HTTP ${response.status}`);
    }

    const data = await response.json();
    state.allBikes = Array.isArray(data.items) ? data.items : [];
    state.bikesLoaded = true;
    renderBikes();
  } catch (error) {
    showBanner('bikesErrorBanner', 'Nem sikerült betölteni az eszközöket. Ellenőrizd az SQL kapcsolatot a Beállítások menüben.', 'error');
    document.getElementById('bikesContent').innerHTML = `
      <div class="empty-state">
        <p>Az eszközlista jelenleg nem érhető el.</p>
      </div>`;
  } finally {
    document.getElementById('bikesLoadingPanel').hidden = true;
  }
}

function bikeState(bike) {
  if (bike.isOccupied) return 'occupied';
  if (bike.isReserved) return 'reserved';
  return 'free';
}

function setFilter(filterName) {
  state.activeFilter = state.activeFilter === filterName ? null : filterName;
  state.activeTypeFilter = null;
  renderBikes();
}

function setTypeFilter(typeName) {
  state.activeTypeFilter = state.activeTypeFilter === typeName ? null : typeName;
  renderBikes();
}

function renderBikes() {
  const bikes = state.allBikes;
  const freeCount = bikes.filter(bike => bikeState(bike) === 'free').length;
  const reservedCount = bikes.filter(bike => bikeState(bike) === 'reserved').length;
  const occupiedCount = bikes.filter(bike => bikeState(bike) === 'occupied').length;

  document.getElementById('freeCount').textContent = freeCount.toLocaleString('hu-HU');
  document.getElementById('reservedCount').textContent = reservedCount.toLocaleString('hu-HU');
  document.getElementById('occupiedCount').textContent = occupiedCount.toLocaleString('hu-HU');
  document.getElementById('statsBar').hidden = false;

  document.getElementById('filterFreeBtn').classList.toggle('active-filter', state.activeFilter === 'free');
  document.getElementById('filterReservedBtn').classList.toggle('active-filter', state.activeFilter === 'reserved');
  document.getElementById('filterOccupiedBtn').classList.toggle('active-filter', state.activeFilter === 'occupied');
  document.getElementById('filterHint').textContent = state.activeFilter
    ? 'szűrő aktív — újra kattintva törlődik'
    : 'szűréshez kattints';

  let filtered = state.activeFilter
    ? bikes.filter(bike => bikeState(bike) === state.activeFilter)
    : bikes.slice();

  if (state.activeTypeFilter) {
    filtered = filtered.filter(bike => bike.typeName === state.activeTypeFilter);
  }

  renderTypeSummary(filtered, bikes);

  if (filtered.length === 0) {
    document.getElementById('bikesContent').innerHTML = `
      <div class="empty-state">
        <p>${state.activeFilter ? 'Nincs ilyen állapotú eszköz.' : 'Nincs megjeleníthető eszköz.'}</p>
      </div>`;
    return;
  }

  const grouped = {};
  filtered.forEach(bike => {
    grouped[bike.typeName] ??= [];
    grouped[bike.typeName].push(bike);
  });

  const orderedTypes = TYPE_ORDER.filter(typeName => grouped[typeName]);
  let html = '';

  orderedTypes.forEach(typeName => {
    html += `
      <div class="type-section">
        <div class="type-header">${TYPE_ICONS[typeName] || '🧰'} ${escHtml(typeName)}</div>
        <div class="bikes-grid">
          ${grouped[typeName].map(renderBikeCard).join('')}
        </div>
      </div>`;
  });

  document.getElementById('bikesContent').innerHTML = html;

  document.querySelectorAll('[data-release-id]').forEach(button => {
    button.addEventListener('click', () => handleRelease(Number(button.dataset.releaseId)));
  });
  document.querySelectorAll('[data-reserve-id]').forEach(button => {
    button.addEventListener('click', () => handleReserve(Number(button.dataset.reserveId)));
  });
  document.querySelectorAll('[data-unreserve-id]').forEach(button => {
    button.addEventListener('click', () => handleUnreserve(Number(button.dataset.unreserveId)));
  });
}

function renderTypeSummary(filtered, allBikes) {
  const summary = document.getElementById('typeSummary');
  const title = document.getElementById('typeSummaryTitle');
  const grid = document.getElementById('typeSummaryGrid');

  if (!state.activeFilter) {
    summary.hidden = true;
    grid.innerHTML = '';
    return;
  }

  const source = allBikes.filter(bike => bikeState(bike) === state.activeFilter);
  const counts = {};
  source.forEach(bike => {
    counts[bike.typeName] = (counts[bike.typeName] || 0) + 1;
  });

  const total = source.length;
  title.textContent = state.activeFilter === 'free'
    ? `${total} szabad eszköz típusonként`
    : state.activeFilter === 'reserved'
      ? `${total} foglalt eszköz típusonként`
      : `${total} kiadott eszköz típusonként`;

  grid.innerHTML = TYPE_ORDER
    .filter(typeName => counts[typeName])
    .map(typeName => {
      const chipClass = state.activeFilter === 'free'
        ? 'chip-free'
        : state.activeFilter === 'reserved'
          ? 'chip-reserved'
          : 'chip-occupied';

      return `
        <button type="button" class="type-chip ${chipClass}${state.activeTypeFilter === typeName ? ' chip-active' : ''}" data-type-filter="${escHtml(typeName)}">
          <span class="chip-count">${counts[typeName]} db</span>
          <span class="chip-label">${escHtml(typeName)}</span>
        </button>`;
    })
    .join('');

  summary.hidden = false;

  document.querySelectorAll('[data-type-filter]').forEach(button => {
    button.addEventListener('click', () => setTypeFilter(button.dataset.typeFilter));
  });
}

function renderBikeCard(bike) {
  const stateName = bikeState(bike);
  const imageHtml = bike.hasImage
    ? `<img src="${apiUrl(`/api/bikes/image/${bike.id}`)}" alt="${escHtml(bike.name)}" loading="lazy" onerror="this.parentElement.innerHTML='<div class=&quot;no-image&quot;>🧰</div>'">`
    : `<div class="no-image">🧰</div>`;

  const overlayHtml = stateName === 'occupied'
    ? `<div class="occupied-overlay"><span class="occupied-badge">Kiadva</span></div>`
    : stateName === 'reserved'
      ? `<div class="occupied-overlay"><span class="reserved-badge">Foglalva</span></div>`
      : '';

  const metaHtml = stateName === 'occupied'
    ? `
      <div class="bike-meta">
        <div class="bike-meta-row"><strong>Bérlés:</strong> ${escHtml(bike.currentTicketNr || '')}</div>
        <div class="bike-meta-row"><strong>Ügyfél:</strong> ${escHtml(bike.currentCustomerName || '')}</div>
        <div class="bike-meta-row"><strong>Lejárat:</strong> ${escHtml(bike.plannedEndDate || '')}</div>
      </div>`
    : '';

  const buttonHtml = stateName === 'occupied'
    ? `<button class="release-btn btn-release" type="button" data-release-id="${bike.id}">Bérlés lezárása</button>`
    : stateName === 'reserved'
      ? `<button class="release-btn btn-unreserve" type="button" data-unreserve-id="${bike.id}">Foglalás törlése</button>`
      : `<button class="release-btn btn-reserve" type="button" data-reserve-id="${bike.id}">Foglalás</button>`;

  return `
    <article class="bike-card ${stateName}">
      <div class="bike-image-wrap">
        ${imageHtml}
        ${overlayHtml}
      </div>
      <div class="bike-info">
        <div class="bike-name">${escHtml(bike.name)}</div>
        ${metaHtml}
      </div>
      <div class="bike-actions">${buttonHtml}</div>
    </article>`;
}

async function handleRelease(id) {
  if (state.releasing.has(id)) return;
  state.releasing.add(id);
  const button = document.querySelector(`[data-release-id="${id}"]`);
  if (button) {
    button.disabled = true;
    button.textContent = 'Lezárás...';
  }

  try {
    const response = await fetch(apiUrl(`/api/bikes/${id}/release`), { method: 'POST' });
    const data = await response.json();
    if (!response.ok) {
      throw new Error(data.error || 'A lezárás nem sikerült.');
    }
    await loadBikes(true);
    showToast('Bérlés sikeresen lezárva.');
  } catch (error) {
    showToast(error.message || 'A lezárás nem sikerült.', 'error');
  } finally {
    state.releasing.delete(id);
  }
}

async function handleReserve(id) {
  if (state.reserving.has(id)) return;
  state.reserving.add(id);
  const button = document.querySelector(`[data-reserve-id="${id}"]`);
  if (button) {
    button.disabled = true;
    button.textContent = 'Foglalás...';
  }

  try {
    const response = await fetch(apiUrl(`/api/bikes/${id}/reserve`), { method: 'POST' });
    const data = await response.json();
    if (!response.ok) {
      throw new Error(data.error || 'A foglalás nem sikerült.');
    }
    await loadBikes(true);
    showToast('Az eszköz foglaltra állítva.');
  } catch (error) {
    showToast(error.message || 'A foglalás nem sikerült.', 'error');
  } finally {
    state.reserving.delete(id);
  }
}

async function handleUnreserve(id) {
  if (state.reserving.has(id)) return;
  state.reserving.add(id);
  const button = document.querySelector(`[data-unreserve-id="${id}"]`);
  if (button) {
    button.disabled = true;
    button.textContent = 'Törlés...';
  }

  try {
    const response = await fetch(apiUrl(`/api/bikes/${id}/unreserve`), { method: 'POST' });
    const data = await response.json();
    if (!response.ok) {
      throw new Error(data.error || 'A foglalás törlése nem sikerült.');
    }
    await loadBikes(true);
    showToast('A foglalás törölve.');
  } catch (error) {
    showToast(error.message || 'A foglalás törlése nem sikerült.', 'error');
  } finally {
    state.reserving.delete(id);
  }
}

async function loadSettings(force = false, options = {}) {
  if (state.loadingSettings && !force) return;

  state.loadingSettings = true;
  if (!options.silent) {
    showBanner('settingsStatusBanner', 'Beállítások betöltése...', 'info');
  }

  try {
    const response = await fetch(apiUrl('/api/settings'));
    if (!response.ok) {
      throw new Error(`HTTP ${response.status}`);
    }

    const data = await response.json();
    state.settingsPayload = data;
    state.settingsLoaded = true;
    state.clearEmailPassword = false;

    fillSettingsForm(data);
    renderEmailPasswordHint();
    updateCompanyBadge(data);

    if (data.databaseStatus?.canConnect) {
      showBanner('settingsStatusBanner', data.databaseStatus.message, 'success');
      if (options.silent) hideBanner('settingsStatusBanner');
    } else {
      showBanner('settingsStatusBanner', data.databaseStatus?.message || 'Az adatbázis jelenleg nem érhető el.', 'error');
    }
  } catch (error) {
    showBanner('settingsStatusBanner', 'Nem sikerült betölteni a beállításokat.', 'error');
  } finally {
    state.loadingSettings = false;
  }
}

function fillSettingsForm(data) {
  const sql = data.sql || {};
  const application = data.application || {};
  const files = data.files || {};

  setInputValue('sqlServer', sql.server || '');
  setInputValue('sqlPort', sql.port || 1433);
  setInputValue('sqlDatabase', sql.database || '');
  setInputValue('sqlUserId', sql.userId || '');
  setInputValue('sqlPassword', sql.password || '');
  setInputValue('testDatabaseName', sql.testDatabaseName || 'ToolRentalDB_Test');
  document.getElementById('sqlTrustServerCertificate').checked = Boolean(sql.trustServerCertificate);

  setInputValue('companyName', application.companyName || 'Kerékpár Bérlő Kft.');
  setInputValue('emailSmtp', application.emailSmtp || '');
  setInputValue('smtpPort', application.smtpPort || 587);
  setInputValue('senderEmail', application.senderEmail || '');
  setInputValue('emailPassword', application.emailPassword || '');
  setInputValue('senderName', application.senderName || '');
  setInputValue('ccAddress', application.ccAddress || '');
  setInputValue('emailSubject', application.emailSubject || 'Bérlési szerződés');
  setInputValue('reviewEmailSubject', application.reviewEmailSubject || 'Értékelje szolgáltatásunkat!');
  setInputValue('googleReview', application.googleReview || '');
  setInputValue('defaultRentalDays', application.defaultRentalDays || 1);
  setInputValue('reviewEmailDelayDays', application.reviewEmailDelayDays || 3);

  state.settingsFiles = {};
  Object.keys(FILE_FIELDS).forEach(key => {
    state.settingsFiles[key] = {
      descriptor: files[key] || null,
      clear: false,
      selectedFile: null
    };
    document.getElementById(FILE_FIELDS[key].inputId).value = '';
    renderFileCard(key);
  });
}

function setInputValue(id, value) {
  const input = document.getElementById(id);
  if (input) {
    input.value = value ?? '';
  }
}

function renderEmailPasswordHint() {
  const hint = document.getElementById('emailPasswordHint');
  const application = state.settingsPayload?.application;
  const typedPassword = document.getElementById('emailPassword').value.trim();

  if (typedPassword) {
    hint.textContent = 'Új email jelszó lesz mentve.';
    return;
  }

  if (state.clearEmailPassword) {
    hint.textContent = 'Az email jelszó a következő mentéskor törlődni fog.';
    return;
  }

  if (application?.emailPasswordNeedsReset) {
    hint.textContent = 'A jelenlegi jelszó a Windows app DPAPI titkosításával van mentve, ezért itt nem olvasható vissza. Újra megadható webes használathoz.';
    return;
  }

  if (application?.emailPasswordConfigured) {
    hint.textContent = 'Jelenleg van mentett email jelszó. Ha nem írsz be újat, a rendszer megtartja.';
    return;
  }

  hint.textContent = 'Nincs mentett email jelszó.';
}

function toggleClearEmailPassword() {
  const input = document.getElementById('emailPassword');
  if (input.value.trim()) {
    input.value = '';
  }

  state.clearEmailPassword = !state.clearEmailPassword;
  renderEmailPasswordHint();
}

function handleFileSelection(key, file) {
  state.settingsFiles[key] ??= { descriptor: null, clear: false, selectedFile: null };
  state.settingsFiles[key].selectedFile = file;
  state.settingsFiles[key].clear = false;
  renderFileCard(key);
}

function clearFileSelection(key) {
  state.settingsFiles[key] ??= { descriptor: null, clear: false, selectedFile: null };
  state.settingsFiles[key].selectedFile = null;
  state.settingsFiles[key].clear = true;
  document.getElementById(FILE_FIELDS[key].inputId).value = '';
  renderFileCard(key);
}

function renderFileCard(key) {
  const current = state.settingsFiles[key];
  if (!current) return;

  const descriptor = current.descriptor;
  const statusElement = document.getElementById(`${key}Status`);
  const pathElement = document.getElementById(`${key}Path`);
  const selectedElement = document.getElementById(`${key}Selected`);

  if (current.clear) {
    statusElement.textContent = 'A fájl a következő mentéskor törlődik.';
    pathElement.textContent = '';
  } else if (descriptor) {
    statusElement.textContent = descriptor.status || 'Nincs fájl feltöltve.';
    pathElement.textContent = descriptor.storedPath || '';
  } else {
    statusElement.textContent = 'Nincs fájl feltöltve.';
    pathElement.textContent = '';
  }

  selectedElement.textContent = current.selectedFile
    ? `Kiválasztva: ${current.selectedFile.name}`
    : (current.clear ? 'A mentés után ez az érték kiürül.' : '');

  if (key === 'companyLogo') {
    renderCompanyLogoPreview(descriptor, current.selectedFile, current.clear);
  }
}

function renderCompanyLogoPreview(descriptor, selectedFile, isCleared) {
  const preview = document.getElementById('companyLogoPreview');

  if (state.companyLogoObjectUrl) {
    URL.revokeObjectURL(state.companyLogoObjectUrl);
    state.companyLogoObjectUrl = null;
  }

  if (selectedFile) {
    state.companyLogoObjectUrl = URL.createObjectURL(selectedFile);
    preview.src = state.companyLogoObjectUrl;
    preview.hidden = false;
    return;
  }

  if (!isCleared && descriptor?.previewUrl) {
    preview.src = descriptor.previewUrl;
    preview.hidden = false;
    return;
  }

  preview.hidden = true;
  preview.removeAttribute('src');
}

async function testSqlConnection() {
  const payload = collectSqlPayload();
  const button = document.getElementById('testSqlButton');
  button.disabled = true;
  button.textContent = 'Tesztelés...';

  try {
    const response = await fetch(apiUrl('/api/settings/test-sql'), {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload)
    });
    const data = await response.json();
    if (!response.ok) {
      throw new Error(data.error || 'A kapcsolat tesztelése nem sikerült.');
    }
    showBanner('settingsStatusBanner', data.message || 'Kapcsolat sikeres.', 'success');
    showToast('SQL kapcsolat rendben.');
  } catch (error) {
    showBanner('settingsStatusBanner', error.message || 'A kapcsolat tesztelése nem sikerült.', 'error');
    showToast(error.message || 'A kapcsolat tesztelése nem sikerült.', 'error');
  } finally {
    button.disabled = false;
    button.textContent = 'SQL kapcsolat tesztelése';
  }
}

function collectSqlPayload() {
  return {
    server: document.getElementById('sqlServer').value.trim(),
    port: Number(document.getElementById('sqlPort').value || 1433),
    database: document.getElementById('sqlDatabase').value.trim(),
    userId: document.getElementById('sqlUserId').value.trim(),
    password: document.getElementById('sqlPassword').value,
    trustServerCertificate: document.getElementById('sqlTrustServerCertificate').checked,
    testDatabaseName: document.getElementById('testDatabaseName').value.trim()
  };
}

async function saveSettings(event) {
  event.preventDefault();

  const formData = new FormData();
  const scalarFields = [
    'sqlServer',
    'sqlPort',
    'sqlDatabase',
    'sqlUserId',
    'sqlPassword',
    'testDatabaseName',
    'companyName',
    'emailSmtp',
    'smtpPort',
    'senderEmail',
    'emailPassword',
    'senderName',
    'ccAddress',
    'emailSubject',
    'reviewEmailSubject',
    'googleReview',
    'defaultRentalDays',
    'reviewEmailDelayDays'
  ];

  scalarFields.forEach(id => {
    const element = document.getElementById(id);
    formData.append(id, element?.value ?? '');
  });

  formData.append('sqlTrustServerCertificate', document.getElementById('sqlTrustServerCertificate').checked ? 'true' : 'false');
  formData.append('clearEmailPassword', state.clearEmailPassword ? 'true' : 'false');

  Object.entries(FILE_FIELDS).forEach(([key, meta]) => {
    const fileState = state.settingsFiles[key];
    formData.append(meta.clearField, fileState?.clear ? 'true' : 'false');
    if (fileState?.selectedFile) {
      formData.append(key, fileState.selectedFile);
    }
  });

  const saveButton = document.getElementById('saveSettingsButton');
  saveButton.disabled = true;
  saveButton.textContent = 'Mentés...';

  try {
    const response = await fetch(apiUrl('/api/settings'), {
      method: 'POST',
      body: formData
    });
    const data = await response.json();
    if (!response.ok) {
      throw new Error(data.error || 'A mentés nem sikerült.');
    }

    showBanner('settingsStatusBanner', data.message || 'A beállítások sikeresen elmentve.', 'success');
    showToast('Beállítások elmentve.');
    await loadSettings(true);
    if (state.currentRoute === 'bikes') {
      await loadBikes(true);
    }
  } catch (error) {
    showBanner('settingsStatusBanner', error.message || 'A mentés nem sikerült.', 'error');
    showToast(error.message || 'A mentés nem sikerült.', 'error');
  } finally {
    saveButton.disabled = false;
    saveButton.textContent = 'Mentés';
  }
}

function updateCompanyBadge(data) {
  const name = data?.application?.companyName || 'ToolRental';
  const logo = data?.files?.companyLogo?.previewUrl || null;

  document.getElementById('companyBadgeName').textContent = name;
  document.getElementById('companyBadgeHint').textContent = state.currentDbMode === 'test' ? 'Teszt környezet' : 'Éles környezet';

  const logoContainer = document.getElementById('companyBadgeLogo');
  if (logo) {
    logoContainer.innerHTML = `<img src="${logo}" alt="${escHtml(name)}">`;
  } else {
    logoContainer.textContent = (name || 'TR').trim().slice(0, 2).toUpperCase();
  }
}

init();
