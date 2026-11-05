const canvas = document.getElementById('bgCanvas');
const ctx = canvas.getContext('2d');

function resizeCanvas() {
  canvas.width = window.innerWidth;
  canvas.height = window.innerHeight;
}
window.addEventListener('resize', resizeCanvas);
resizeCanvas();

const TRIANGLE_COUNT = window.innerWidth < 500 ? 16 : 24;
const triangles = [];
function rand(a, b) { return a + Math.random() * (b - a); }
function newTriangle() {
  return {
    x: rand(0, canvas.width), y: rand(0, canvas.height),
    vx: rand(-0.3, 0.3), vy: rand(-0.3, 0.3),
    size: rand(14, 45),
    rot: rand(0, Math.PI * 2), vrot: rand(-0.008, 0.008),
    baseOpacity: rand(0.06, 0.22),
    hue: rand(260, 300), phase: rand(0, Math.PI * 2)
  };
}
for (let i = 0; i < TRIANGLE_COUNT; i++) triangles.push(newTriangle());

function drawTri(cx, cy, size, rotation) {
  ctx.beginPath();
  for (let i = 0; i < 3; i++) {
    const a = rotation + (2 * Math.PI * i / 3) - Math.PI / 2;
    const x = cx + size * Math.cos(a);
    const y = cy + size * Math.sin(a);
    if (i === 0) ctx.moveTo(x, y); else ctx.lineTo(x, y);
  }
  ctx.closePath();
}
function animateBg() {
  ctx.clearRect(0, 0, canvas.width, canvas.height);
  for (const t of triangles) {
    t.x += t.vx; t.y += t.vy; t.rot += t.vrot; t.phase += 0.012;
    const pulse = 0.6 + 0.4 * Math.sin(t.phase);
    const opacity = t.baseOpacity * pulse;
    if (t.x < -60) t.x = canvas.width + 60;
    if (t.x > canvas.width + 60) t.x = -60;
    if (t.y < -60) t.y = canvas.height + 60;
    if (t.y > canvas.height + 60) t.y = -60;
    ctx.strokeStyle = `hsla(${t.hue}, 80%, 72%, ${opacity})`;
    ctx.lineWidth = 1.4;
    drawTri(t.x, t.y, t.size, t.rot);
    ctx.stroke();
  }
  requestAnimationFrame(animateBg);
}
animateBg();

const sessionKey = 'zrobim.session';
const cityPreferenceKey = 'zrobim.city';
let selectedCity = readPreference(cityPreferenceKey) ?? '';

function readPreference(key) {
  try { return localStorage.getItem(key); }
  catch { return null; }
}

function savePreference(key, value) {
  try {
    if (value === null) localStorage.removeItem(key);
    else localStorage.setItem(key, value);
  } catch {
    // The current session also works when browser storage is unavailable.
  }
}

function clearSavedSession() {
  savePreference(sessionKey, null);
  try { sessionStorage.removeItem(sessionKey); } catch {}
}

const appScreens = ['home', 'bookings', 'profile', 'cabinet', 'notifications', 'calendar', 'payment'];
const roleNames = { CLIENT: 'Клиент', MASTER: 'Мастер', ADMIN: 'Администратор' };
let session = null;
let currentRole = null;
let signingIn = false;
let toastTimer;

function showScreen(screen, bookingId) {
  document.querySelectorAll('.screen').forEach(element => {
    element.classList.toggle('active', element.id === 'screen-' + screen);
  });
  document.getElementById('bottom-nav').classList.toggle('show', Boolean(session) && appScreens.includes(screen));
  document.getElementById('guest-login').hidden = Boolean(session);
  document.getElementById('catalog-greeting').hidden = !session;
  document.querySelectorAll('.nav-item').forEach(button => {
    const activeScreen = screen === 'calendar' ? 'cabinet' : screen === 'payment' ? 'bookings' : screen;
    const active = button.dataset.screen === activeScreen;
    button.classList.toggle('active', active);
    if (active) button.setAttribute('aria-current', 'page');
    else button.removeAttribute('aria-current');
  });
  window.scrollTo(0, 0);
  screenRequest?.abort();
  if (screen === 'home') {
    document.getElementById('catalog-query').value = '';
    showCategories();
  }
  if (screen === 'bookings') loadBookings(bookingId);
  if (screen === 'notifications') loadNotifications();
  if (appScreens.includes(screen)) refreshNotificationBadge();
  if (screen === 'cabinet') loadCabinet();
  if (screen === 'calendar') loadMasterCalendar();
}

function showError(message) {
  const toast = document.getElementById('toast');
  toast.textContent = message;
  toast.classList.add('show', 'error');
  clearTimeout(toastTimer);
  toastTimer = setTimeout(() => toast.classList.remove('show'), 5000);
}

async function apiRequest(path, { body, token, signal, method = body ? 'POST' : 'GET' } = {}) {
  const headers = { Accept: 'application/json' };
  if (body) headers['Content-Type'] = 'application/json';
  if (token) headers.Authorization = 'Bearer ' + token;

  let response;
  try {
    response = await fetch(path, {
      method,
      headers,
      body: body ? JSON.stringify(body) : undefined,
      signal
    });
  } catch {
    throw new Error('Не удалось подключиться к серверу. Попробуйте ещё раз.');
  }

  if (response.status === 204) return null;
  if (response.ok) return response.json();

  const problem = await response.json().catch(() => ({}));
  const fieldErrors = Object.values(problem.errors ?? {}).flat();
  const message = response.status === 429
    ? 'Слишком много попыток. Попробуйте через минуту.'
    : fieldErrors[0] || problem.title || 'Сервер не смог обработать запрос.';
  throw Object.assign(new Error(message), { status: response.status });
}

function renderUser(user) {
  currentRole = user.role;
  document.getElementById('favorites-open').hidden = currentRole !== 'CLIENT';
  document.getElementById('cabinet-nav').hidden = !['MASTER', 'ADMIN'].includes(currentRole);
  const name = [user.firstName, user.lastName].filter(Boolean).join(' ');
  document.getElementById('greet-name').textContent = user.firstName;
  document.getElementById('profile-name').textContent = name;
  document.getElementById('profile-role').textContent = roleNames[user.role] ?? user.role;
  document.getElementById('profile-email').textContent = user.email;
  document.getElementById('profile-phone').textContent = user.phone || '—';
}

function logout() {
  currentRole = null;
  session = null;
  document.getElementById('favorites-open').hidden = true;
  refreshNotificationBadge();
  document.getElementById('cabinet-nav').hidden = true;
  clearSavedSession();
  document.getElementById('login-form').reset();
  document.getElementById('register-form').reset();
  showScreen('login');
}

async function loadProfile(activeSession) {
  try {
    const user = await apiRequest('/api/me', { token: activeSession.accessToken });
    if (session !== activeSession) return false;
    renderUser(user);
    return true;
  } catch (error) {
    if (session !== activeSession) return false;
    if (error.status === 401 || error.status === 404) {
      logout();
      showError('Сессия завершилась. Войдите снова.');
    } else {
      showError(error.message);
    }
    return false;
  }
}

async function submitAuth(event) {
  event.preventDefault();
  if (signingIn) return;
  const form = event.currentTarget;
  const submit = form.querySelector('button[type="submit"]');
  const fields = Object.fromEntries(new FormData(form));
  const isRegistration = form.id === 'register-form';
  const body = { email: fields.email.trim(), password: fields.password };

  if (isRegistration) {
    body.firstName = fields.firstName.trim();
    body.lastName = fields.lastName.trim() || null;
    body.phone = fields.phone.trim() || null;
    body.role = fields.role;
  }

  signingIn = true;
  submit.disabled = true;
  form.setAttribute('aria-busy', 'true');
  try {
    const result = await apiRequest(isRegistration ? '/api/auth/register' : '/api/auth/login', { body });
    const { accessToken, expiresAt, user } = result;
    session = { accessToken, expiresAt };
    savePreference(sessionKey, JSON.stringify(session));
    form.reset();
    document.getElementById('toast').classList.remove('show');
    renderUser(user);
    showScreen('home');
  } catch (error) {
    showError(error.message);
  } finally {
    signingIn = false;
    submit.disabled = false;
    form.removeAttribute('aria-busy');
  }
}

const catalog = document.getElementById('catalog-content');
const searchForm = document.getElementById('catalog-search');
let screenRequest = null;

function catalogElement(tag, className, text) {
  const element = document.createElement(tag);
  element.className = className;
  if (text) element.textContent = text;
  return element;
}

function catalogButton(className, label, action) {
  const button = catalogElement('button', className, label);
  button.type = 'button';
  button.addEventListener('click', action);
  return button;
}

function catalogPage(title, back, withSearch = false) {
  searchForm.hidden = !withSearch;
  screenRequest?.abort();
  catalog.replaceChildren();
  if (back) catalog.append(catalogButton('btn sm ghost catalog-back', '← Назад', back));
  catalog.append(catalogElement('h3', 'section-title', title));
  const content = catalogElement('div', 'catalog-list');
  catalog.append(content);
  return content;
}

function loadCatalog(path, title, emptyMessage, back, render, withSearch = false) {
  const content = catalogPage(title, back, withSearch);
  return loadScreenContent(path, content, (items) => {
    if (items.length) render(content, items);
    else content.append(catalogElement('p', 'empty', emptyMessage));
  });
}

async function loadScreenContent(path, content, render, token) {
  const request = new AbortController();
  screenRequest = request;
  const loading = catalogElement('p', 'empty', 'Загрузка…');
  loading.setAttribute('role', 'status');
  content.replaceChildren(loading);

  try {
    const items = await apiRequest(path, { signal: request.signal, token });
    if (request.signal.aborted) return;
    content.replaceChildren();
    render(items);
  } catch (error) {
    if (request.signal.aborted) return;
    if (token && error.status === 401) {
      logout();
      showError('Сессия завершилась. Войдите снова.');
      return;
    }
    const message = catalogElement('p', 'empty', error.message);
    message.setAttribute('role', 'status');
    content.replaceChildren(message, catalogButton('btn sm ghost', 'Повторить', () =>
      loadScreenContent(path, content, render, token)));
  } finally {
    if (screenRequest === request) screenRequest = null;
  }
}

const categoryArtwork = {
  'Уборка и клининг': ['🧹', 'Порядок и свежесть дома', 'cleaning'],
  'Дом и ремонт': ['🛠️', 'Помощь с домашними задачами', 'home'],
  'Маникюр и педикюр': ['💅', 'Уход и любимые оттенки', 'nails'],
  'Красота и уход': ['✨', 'Стрижки, брови и уход', 'beauty'],
  'Сборка мебели': ['🪑', 'Новая мебель без хлопот', 'furniture'],
  'Ремонт техники': ['💻', 'Помощь с устройствами', 'tech'],
  'Уход за питомцами': ['🐾', 'Забота о ваших любимцах', 'pets'],
  'Обучение': ['📚', 'Новые знания и навыки', 'learning']
};

function formatCatalogCount(count, noun) {
  const forms = noun === 'master'
    ? { one: 'мастер', few: 'мастера', many: 'мастеров', other: 'мастера' }
    : { one: 'услуга', few: 'услуги', many: 'услуг', other: 'услуги' };
  return count + ' ' + forms[new Intl.PluralRules('ru').select(count)];
}

function showCategories() {
  loadSearchCities();
  const params = new URLSearchParams();
  if (selectedCity) params.set('city', selectedCity);
  const path = '/api/catalog/categories' + (params.size ? '?' + params : '');
  const empty = selectedCity ? 'В этом городе пока нет доступных мастеров. Выберите другой город.' : 'Категорий пока нет.';
  return loadCatalog(path, 'Категории', empty, null, (content, categories) => {
    const grid = catalogElement('div', 'cat-grid');
    for (const category of categories) {
      const [emoji, description, theme] = categoryArtwork[category.name] ?? ['✦', 'Найдите своего специалиста', 'home'];
      const button = catalogButton('cat-card category-tile category-' + theme, '', () => showMasters(category));
      button.append(catalogElement('span', 'category-emoji', emoji));
      button.append(catalogElement('span', 'catalog-name', category.name));
      button.append(catalogElement('span', 'category-description', description));
      const counts = formatCatalogCount(category.masterCount, 'master') + ' · ' +
        formatCatalogCount(category.serviceCount, 'service');
      button.append(catalogElement('span', 'category-count', counts));
      grid.append(button);
    }
    content.append(grid);
  }, true);
}

async function loadSearchCities() {
  const select = document.getElementById('catalog-city');
  const status = document.getElementById('catalog-city-status');
  const renderOptions = cities => {
    const names = selectedCity && !cities.includes(selectedCity) ? [selectedCity, ...cities] : cities;
    const all = catalogElement('option', '', 'Все города');
    all.value = '';
    select.replaceChildren(all, ...names.map(city => {
      const option = catalogElement('option', '', city);
      option.value = city;
      return option;
    }));
    select.value = selectedCity;
  };
  if (selectedCity && !select.value) renderOptions([]);
  try {
    const cities = await apiRequest('/api/catalog/cities');
    renderOptions(cities);
    status.textContent = '';
  } catch {
    status.textContent = 'Не удалось обновить список городов. Попробуйте обновить страницу.';
  }
}

function showSearchResults(filters, page = 1) {
  window.scrollTo(0, 0);
  const content = catalogPage('Результаты поиска', null, true);
  const params = new URLSearchParams({ ...filters, page: String(page) });
  return loadScreenContent('/api/catalog/search?' + params, content, result => {
    if (!result.items.length)
      content.append(catalogElement('p', 'empty', 'По этим условиям ничего не найдено. Попробуйте другой запрос или город.'));

    for (const { categoryName, master, service } of result.items) {
      const name = [master.firstName, master.lastName].filter(Boolean).join(' ');
      const button = catalogButton('master-card', '', () =>
        showService(master, name, service, () => showSearchResults(filters, page)));
      const info = catalogElement('span', 'master-info');
      info.append(catalogElement('span', 'name', service.name));
      info.append(catalogElement('span', 'line', [name, master.city].filter(Boolean).join(' · ')));
      info.append(catalogElement('span', 'line', categoryName + ' · ' + serviceSummary(service)));
      button.append(info, catalogElement('span', 'arrow', '›'));
      content.append(button);
    }

    if (page > 1 || result.hasMore) {
      const pagination = catalogElement('div', 'catalog-search-actions');
      if (page > 1)
        pagination.append(catalogButton('btn sm ghost', 'Предыдущая', () => showSearchResults(filters, page - 1)));
      pagination.append(catalogElement('span', 'catalog-search-page', 'Страница ' + page));
      if (result.hasMore)
        pagination.append(catalogButton('btn sm ghost', 'Следующая', () => showSearchResults(filters, page + 1)));
      content.append(pagination);
    }
  });
}


function showMasters(category) {
  const params = new URLSearchParams();
  if (selectedCity) params.set('city', selectedCity);
  const path = '/api/catalog/categories/' + category.id + '/masters' + (params.size ? '?' + params : '');
  return loadCatalog(path,
    category.name, 'В этой категории пока нет мастеров.', showCategories, (content, masters) => {
      for (const master of masters) {
        const name = [master.firstName, master.lastName].filter(Boolean).join(' ');
        const button = catalogButton('master-card', '', () => showServices(category, master, name));
        const info = catalogElement('span', 'master-info');
        info.append(catalogElement('span', 'name', name));
        if (master.city) info.append(catalogElement('span', 'line', master.city));
        info.append(catalogElement('span', 'line', masterRatingText(master)));
        button.append(info, catalogElement('span', 'arrow', '›'));
        content.append(button);
      }
    });
}

function formatPrice(price, currency) {
  return new Intl.NumberFormat('ru-BY', { style: 'currency', currency }).format(price);
}

function serviceSummary(service) {
  return formatPrice(service.price, service.currency) + ' · ' + service.durationMinutes + ' мин';
}

function masterRatingText(master) {
  return master.reviewsCount
    ? '★ ' + master.rating.toLocaleString('ru-BY') + ' · отзывов: ' + master.reviewsCount
    : 'Пока без отзывов';
}

function renderPublicMasterProfile(master) {
  const profile = catalogElement('div', 'card catalog-details master-profile');
  profile.append(catalogElement('h3', 'catalog-name', 'О мастере'));
  if (master.city) profile.append(catalogElement('p', 'catalog-description', 'Город: ' + master.city));
  if (master.publicAddress)
    profile.append(catalogElement('p', 'catalog-description', 'Адрес приёма: ' + master.publicAddress));
  if (master.description)
    profile.append(catalogElement('p', 'catalog-description profile-text', master.description));
  profile.append(catalogElement('p', 'catalog-description', masterRatingText(master)));
  if (currentRole === 'CLIENT') profile.append(favoriteButton(master.id));
  return profile;
}

function serviceLocation(master, service) {
  if (service.locationType === 'CLIENT_PLACE') return 'У клиента';
  return master.publicAddress ? 'У мастера · ' + master.publicAddress : 'У мастера';
}


function showServices(category, master, name) {
  const path = '/api/catalog/categories/' + category.id + '/masters/' + master.id + '/services';
  return showMasterServices(master, name, path, () => showMasters(category));
}

function showMasterServices(master, name, path, back) {
  const reopen = () => showMasterServices(master, name, path, back);
  return loadCatalog(path, name, 'У мастера пока нет доступных услуг.', back, (content, services) => {
      content.append(renderPublicMasterProfile(master));
      for (const service of services) {
        const button = catalogButton('master-card', '', () => showService(master, name, service, reopen));
        const info = catalogElement('span', 'master-info');
        info.append(catalogElement('span', 'name', service.name));
        info.append(catalogElement('span', 'line', serviceSummary(service)));
        button.append(info, catalogElement('span', 'arrow', '›'));
        content.append(button);
      }
    });
}

function showService(master, name, service, back) {
  const content = catalogPage(service.name, back);
  const details = catalogElement('div', 'card catalog-details');
  details.append(catalogElement('p', 'catalog-name', serviceSummary(service)));
  details.append(catalogElement('p', 'catalog-description', 'Мастер: ' + name));
  details.append(catalogElement('p', 'catalog-description', serviceLocation(master, service)));
  if (service.description) details.append(catalogElement('p', 'catalog-description', service.description));
  if (currentRole === 'CLIENT') details.append(favoriteButton(master.id));
  content.append(details);
  content.append(catalogElement('h3', 'section-title', 'Свободное время на 7 дней'));
  const availability = catalogElement('div', 'catalog-availability');
  content.append(availability);

  const refreshTimes = () => showService(master, name, service, back);
  return loadScreenContent('/api/catalog/services/' + service.id + '/availability', availability,
    result => renderAvailability(availability, result, currentRole === 'CLIENT'
      ? (slot, when) => showBookingForm(master, name, service, slot, when, refreshTimes)
      : null));
}

function renderAvailability(content, { timeZone, slots }, selectSlot) {
  if (!slots.length) {
    content.append(catalogElement('p', 'empty', 'На ближайшие 7 дней свободного времени нет.'));
    return;
  }

  const dayFormat = new Intl.DateTimeFormat('ru-BY', {
    timeZone, weekday: 'long', day: 'numeric', month: 'long'
  });
  const timeFormat = new Intl.DateTimeFormat('ru-BY', {
    timeZone, hour: '2-digit', minute: '2-digit'
  });
  content.append(catalogElement('p', 'catalog-description', 'Время по Минску'));

  let currentDay = '';
  let daySlots;
  for (const slot of slots) {
    const start = new Date(slot.startAt);
    const day = dayFormat.format(start);
    if (day !== currentDay) {
      currentDay = day;
      const group = catalogElement('div', 'catalog-slot-day');
      daySlots = catalogElement('ul', 'catalog-slots');
      group.append(catalogElement('h4', 'catalog-description', day), daySlots);
      content.append(group);
    }
    const interval = timeFormat.format(start) + '–' + timeFormat.format(new Date(slot.endAt));
    const item = catalogElement('li', '');
    item.append(selectSlot
      ? catalogButton('catalog-slot', interval, () => selectSlot(slot, day + ', ' + interval))
      : catalogElement('span', 'catalog-slot', interval));
    daySlots.append(item);
  }
}

function showBookingForm(master, name, service, slot, when, refreshTimes) {

  const content = catalogPage('Новая заявка', refreshTimes);
  const form = catalogElement('form', 'card catalog-details');
  form.append(catalogElement('p', 'catalog-name', service.name));
  form.append(catalogElement('p', 'catalog-description', 'Мастер: ' + name));
  form.append(catalogElement('p', 'catalog-description', when + ' · время по Минску'));
  form.append(catalogElement('p', 'catalog-description', serviceSummary(service)));
  form.append(catalogElement('p', 'catalog-description', serviceLocation(master, service)));

  if (service.locationType === 'CLIENT_PLACE') {
    const address = bookingField('input', 'address', 'Адрес оказания услуги', 500);
    address.querySelector('input').required = true;
    form.append(address);
  }
  form.append(bookingField('textarea', 'comment', 'Комментарий (необязательно)', 1000));

  const status = catalogElement('div', 'catalog-description');
  status.setAttribute('role', 'status');
  const submit = catalogElement('button', 'btn full', 'Отправить заявку');
  submit.type = 'submit';
  form.append(status, submit);
  form.addEventListener('submit', async event => {
    event.preventDefault();
    if (submit.disabled || !session) return;

    const fields = new FormData(form);
    const address = fields.get('address')?.trim() || null;
    if (service.locationType === 'CLIENT_PLACE' && !address) {
      status.textContent = 'Укажите адрес оказания услуги.';
      return;
    }

    const activeSession = session;
    const formIsVisible = () => form.isConnected &&
      document.getElementById('screen-home').classList.contains('active');
    submit.disabled = true;
    status.textContent = 'Отправляем заявку…';
    try {
      await apiRequest('/api/bookings', {
        token: activeSession.accessToken,
        body: { serviceId: service.id, startAt: slot.startAt, address, comment: fields.get('comment').trim() || null }
      });
      if (session !== activeSession) return;
      if (formIsVisible() || document.getElementById('screen-bookings').classList.contains('active'))
        showScreen('bookings');
    } catch (error) {
      if (session !== activeSession) return;
      if (error.status === 401) {
        logout();
        showError('Сессия завершилась. Войдите снова.');
        return;
      }
      if (!formIsVisible()) return;
      status.textContent = error.message;
      if (error.status === 409 || error.status === 404) {
        submit.replaceWith(catalogButton('btn full ghost', 'Обновить свободное время', refreshTimes));
      }
    } finally {
      submit.disabled = false;
    }
  });
  content.append(form);
}

function bookingField(tag, name, label, maxLength) {
  const field = catalogElement('div', 'field');
  const caption = catalogElement('label', '', label);
  caption.htmlFor = 'booking-' + name;
  const input = catalogElement(tag, '');
  input.id = caption.htmlFor;
  input.name = name;
  input.maxLength = maxLength;
  if (tag === 'textarea') input.rows = 3;
  field.append(caption, input);
  return field;
}

const bookingStatusLabels = {
  PENDING: 'Ожидает ответа мастера',
  AWAITING_PAYMENT: 'Мастер принял заявку',
  CONFIRMED: 'Подтверждена',
  COMPLETED: 'Выполнена',
  CLIENT_NO_SHOW: 'Неявка клиента',
  CANCELLED: 'Отменена',
  DISPUTED: 'Открыт спор'
};

const bookingCancellationLabels = {
  MASTER_RESPONSE_TIMEOUT: 'Мастер не ответил вовремя',
  PAYMENT_TIMEOUT: 'Истёк срок ожидания оплаты',
  CLIENT_CANCELLED: 'Клиент отозвал заявку',
  MASTER_DECLINED: 'Мастер отклонил заявку'
};


function loadBookings(bookingId) {
  const content = document.getElementById('bookings-content');
  const isMaster = currentRole === 'MASTER';
  document.getElementById('bookings-title').textContent = isMaster ? 'Входящие заявки' : 'Мои записи';
  screenRequest?.abort();
  if (!isMaster && currentRole !== 'CLIENT') {
    content.replaceChildren(catalogElement('p', 'empty', 'Заявки доступны клиентам и мастерам.'));
    return;
  }

  const path = isMaster ? '/api/master/bookings' : '/api/bookings';
  return loadScreenContent(path, content, bookings => renderBookings(content, bookings, isMaster, bookingId),
    session.accessToken);
}

function renderBookings(content, bookings, isMaster, bookingId) {
  if (!bookings.length) {
    const message = isMaster ? 'Входящих заявок пока нет.' : 'У вас пока нет заявок.';
    content.append(catalogElement('p', 'empty', message));
    return;
  }
  const formatter = new Intl.DateTimeFormat('ru-BY', {
    timeZone: 'Europe/Minsk', day: 'numeric', month: 'long', hour: '2-digit', minute: '2-digit'
  });
  const now = Date.now();
  for (const booking of bookings) {
    const card = catalogElement('article', 'booking-item');
    const person = isMaster
      ? [booking.clientFirstName, booking.clientLastName]
      : [booking.masterFirstName, booking.masterLastName];
    const name = person.filter(Boolean).join(' ');
    const price = formatPrice(booking.price, booking.currency);
    const pending = booking.status === 'PENDING';
    const expired = pending && Date.parse(booking.responseDeadlineAt) <= now;
    const status = expired ? 'Срок ответа истёк' : bookingStatusLabels[booking.status] ?? booking.status;

    card.append(catalogElement('h3', 'client', booking.serviceName));
    card.append(catalogElement('p', 'time', formatter.format(new Date(booking.startAt)) + ' · время по Минску'));
    card.append(catalogElement('p', 'info', name + ' · ' + price));
    card.append(catalogElement('p', 'info', status));
    const reason = bookingCancellationLabels[booking.cancellationReason];
    if (booking.status === 'CANCELLED' && reason)
      card.append(catalogElement('p', 'info', reason));
    if (isMaster) {
      if (booking.address) card.append(catalogElement('p', 'info', 'Адрес: ' + booking.address));
      if (booking.comment) card.append(catalogElement('p', 'info', 'Комментарий: ' + booking.comment));
      if (pending && !expired) {
        const actions = catalogElement('div', 'booking-actions');
        const path = '/api/master/bookings/' + booking.id;
        actions.append(
          catalogButton('btn sm', 'Принять', () => runBookingAction(path + '/accept', actions)),
          catalogButton('btn sm ghost', 'Отклонить', () => {
            if (window.confirm('Отклонить заявку?')) return runBookingAction(path + '/decline', actions);
          })
        );
        card.append(actions);
      }
    } else if (booking.canPayDemo || booking.canCancel) {
      const actions = catalogElement('div', 'booking-actions');
      if (booking.canPayDemo)
        actions.append(catalogButton('btn sm', 'Оплатить', () => openDemoPayment(booking)));
      if (booking.canCancel) {
        actions.append(catalogButton('btn sm ghost', 'Отозвать заявку', () => {
          if (window.confirm('Отозвать заявку?'))
            return runBookingAction('/api/bookings/' + booking.id + '/cancel', actions);
        }));
      }
      card.append(actions);
    }
    content.append(card);
    if (booking.id === bookingId) {
      card.classList.add('booking-highlighted');
      card.tabIndex = -1;
      card.focus({ preventScroll: true });
      card.scrollIntoView({ block: 'center' });
    }
  }
}

async function runBookingAction(path, actions) {
  if (!session) return;
  const buttons = [...actions.querySelectorAll('button')];
  if (buttons.some(button => button.disabled)) return;
  const activeSession = session;
  const isViewingBookings = () => session === activeSession &&
    document.getElementById('screen-bookings').classList.contains('active');

  buttons.forEach(button => { button.disabled = true; });
  try {
    await apiRequest(path, { body: {}, token: activeSession.accessToken });
    if (isViewingBookings()) await loadBookings();
  } catch (error) {
    if (session !== activeSession) return;
    if (error.status === 401) {
      logout();
      showError('Сессия завершилась. Войдите снова.');
    } else if (isViewingBookings()) {
      showError(error.message);
      if (error.status === 409 || error.status === 404) await loadBookings();
    }
  } finally {
    buttons.forEach(button => { button.disabled = false; });
  }
}

async function restoreSession() {
  document.getElementById('restore-retry').hidden = true;
  let saved = readPreference(sessionKey);
  if (!saved) {
    try { saved = sessionStorage.getItem(sessionKey); } catch {}
  }
  try { session = JSON.parse(saved); }
  catch { session = null; }
  if (!session?.accessToken || !(Date.parse(session.expiresAt) > Date.now())) {
    session = null;
    clearSavedSession();
    showScreen('splash');
    return;
  }

  savePreference(sessionKey, JSON.stringify(session));
  try { sessionStorage.removeItem(sessionKey); } catch {}
  const activeSession = session;
  if (await loadProfile(activeSession)) {
    showScreen('home');
  } else if (session === activeSession) {
    showScreen('splash');
    document.getElementById('restore-retry').hidden = false;
  }
}

window.addEventListener('storage', event => {
  if (event.key !== sessionKey && event.key !== null) return;
  if (!readPreference(sessionKey)) logout();
  else restoreSession();
});
document.getElementById('restore-retry').addEventListener('click', restoreSession);

document.querySelectorAll('[data-screen]').forEach(button => {
  button.addEventListener('click', () => {
    if (signingIn) return;
    const screen = button.dataset.screen;
    if (screen !== 'home' && appScreens.includes(screen) && !session) return;
    if (screen === 'cabinet' && !['MASTER', 'ADMIN'].includes(currentRole)) return;
    showScreen(screen);
    if (screen === 'profile') loadProfile(session);
  });
});
function runCatalogSearch() {
  const q = document.getElementById('catalog-query').value.trim();
  if (q) return showSearchResults({ q, city: selectedCity });
  return showCategories();
}

searchForm.addEventListener('submit', event => {
  event.preventDefault();
  return runCatalogSearch();
});
document.getElementById('catalog-city').addEventListener('change', event => {
  selectedCity = event.currentTarget.value;
  savePreference(cityPreferenceKey, selectedCity);
  return runCatalogSearch();
});
document.getElementById('catalog-search-reset').addEventListener('click', () => {
  document.getElementById('catalog-query').value = '';
  return showCategories();
});
document.getElementById('favorites-open').addEventListener('click', () => showFavorites());
document.getElementById('bookings-refresh').addEventListener('click', () => loadBookings());
document.getElementById('logout').addEventListener('click', logout);
document.getElementById('login-form').addEventListener('submit', submitAuth);
document.getElementById('register-form').addEventListener('submit', submitAuth);
initNotifications();
restoreSession();
