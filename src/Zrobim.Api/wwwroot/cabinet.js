const cabinetStatus = {
  PENDING: 'Профиль ожидает одобрения. Пока можно настроить услуги и график.',
  APPROVED: 'Профиль одобрен. Активные услуги видны в каталоге.',
  REJECTED: 'Профиль отклонён.'
};
const weekdays = ['Понедельник', 'Вторник', 'Среда', 'Четверг', 'Пятница', 'Суббота', 'Воскресенье'];

let adminCabinetSection = 'masters';

function loadCabinet() {
  if (!session || !['MASTER', 'ADMIN'].includes(currentRole)) return;
  const content = document.getElementById('cabinet-content');
  const isAdmin = currentRole === 'ADMIN';
  const managingCategories = isAdmin && adminCabinetSection === 'categories';
  const tabs = document.getElementById('admin-cabinet-tabs');
  tabs.hidden = !isAdmin;
  for (const tab of tabs.querySelectorAll('button')) {
    const selected = tab.dataset.section === adminCabinetSection;
    tab.classList.toggle('ghost', !selected);
    tab.setAttribute('aria-pressed', String(selected));
  }

  let title = 'Кабинет мастера';
  let path = '/api/master/cabinet';
  let render = renderMasterCabinet;
  if (isAdmin) {
    title = managingCategories ? 'Категории' : 'Одобрение мастеров';
    path = managingCategories ? '/api/admin/categories' : '/api/admin/masters/pending';
    render = managingCategories ? renderAdminCategories : renderPendingMasters;
  }
  document.getElementById('cabinet-title').textContent = title;
  screenRequest?.abort();
  return loadScreenContent(path, content, data => render(content, data), session.accessToken);
}

function renderMasterCabinet(content, data) {
  content.append(catalogElement('p', 'cabinet-status', cabinetStatus[data.verificationStatus]));
  content.append(catalogButton('btn sm calendar-launch', 'Календарь на неделю', () => showScreen('calendar')));
  content.append(catalogElement('h3', 'section-title', 'Анкета мастера'));
  const profile = catalogElement('div', 'card cabinet-card');
  profile.append(renderMasterProfileForm(data.profile));
  content.append(profile);
  content.append(catalogElement('h3', 'section-title', 'Мои услуги'));
  for (const service of data.services) {
    const editor = catalogElement('details', 'card cabinet-card');
    const summary = service.name + ' · ' + serviceSummary({ ...service, currency: 'BYN' }) +
      (service.isActive ? '' : ' · скрыта');
    editor.append(catalogElement('summary', 'catalog-name', summary), renderServiceForm(data.categories, service));
    content.append(editor);
  }

  if (data.categories.length) {
    const editor = catalogElement('details', 'card cabinet-card');
    editor.append(catalogElement('summary', 'catalog-name', 'Добавить услугу'), renderServiceForm(data.categories));
    content.append(editor);
  } else {
    content.append(catalogElement('p', 'catalog-description', 'В каталоге пока нет доступных категорий.'));
  }

  content.append(catalogElement('h3', 'section-title', 'Рабочие часы'));
  content.append(renderWorkingHours(data.workingHours));
  content.append(catalogElement('h3', 'section-title', 'Блокировки времени'));
  content.append(renderTimeBlocks(data.timeBlocks));
}

function cabinetField(label, input) {
  const field = catalogElement('label', 'field cabinet-field');
  field.append(catalogElement('span', '', label), input);
  return field;
}

function cabinetInput(name, type, value = '') {
  const input = catalogElement('input', '');
  input.name = name;
  input.type = type;
  input.value = value;
  return input;
}

function cabinetSelect(name, options, value) {
  const select = catalogElement('select', '');
  select.name = name;
  for (const [key, title] of options) {
    const option = catalogElement('option', '', title);
    option.value = key;
    select.append(option);
  }
  if (value) select.value = value;
  return select;
}

function renderMasterProfileForm(profile) {
  const form = catalogElement('form', 'cabinet-form');
  const city = cabinetInput('city', 'text', profile.city ?? '');
  city.maxLength = 100;
  const description = catalogElement('textarea', '');
  description.name = 'description';
  description.rows = 4;
  description.maxLength = 2000;
  description.value = profile.description ?? '';
  const address = cabinetInput('address', 'text', profile.address ?? '');
  address.maxLength = 500;
  const showAddress = cabinetInput('showAddressInCatalog', 'checkbox');
  showAddress.checked = profile.showAddressInCatalog;
  const visibility = catalogElement('label', 'cabinet-check');
  visibility.append(showAddress, catalogElement('span', '', 'Показывать адрес в каталоге'));

  const toggleAddress = () => { address.required = showAddress.checked; };
  showAddress.addEventListener('change', toggleAddress);
  toggleAddress();
  form.append(catalogElement('p', 'catalog-description',
      'Город и описание видны после одобрения. Адрес можно отдельно опубликовать для услуг с приёмом у мастера.'),
    cabinetField('Город', city), cabinetField('О себе и условиях приёма', description),
    cabinetField('Адрес приёма', address), visibility);
  wireCabinetForm(form, 'Сохранить анкету', () => ({
    path: '/api/master/profile', method: 'PUT',
    body: { city: city.value.trim(), description: description.value.trim(),
      address: address.value.trim(), showAddressInCatalog: showAddress.checked }
  }));
  return form;
}


function renderServiceForm(categories, service) {
  const form = catalogElement('form', 'cabinet-form');
  const name = cabinetInput('name', 'text', service?.name);
  name.required = true;
  name.maxLength = 150;
  const options = categories.map(category => [category.id, category.name]);
  if (service && !categories.some(category => category.id === service.categoryId))
    options.unshift([service.categoryId, service.categoryName + ' (скрыта)']);
  const category = cabinetSelect('categoryId', options, service?.categoryId);

  const description = catalogElement('textarea', '');
  description.name = 'description';
  description.rows = 2;
  description.maxLength = 2000;
  description.value = service?.description ?? '';
  const price = cabinetInput('price', 'number', service?.price);
  price.min = '0.01';
  price.max = '9999999999.99';
  price.step = '0.01';
  price.required = true;
  const duration = cabinetInput('durationMinutes', 'number', service?.durationMinutes ?? 60);
  duration.min = '1';
  duration.max = '1440';
  duration.required = true;
  const location = cabinetSelect('locationType',
    [['MASTER_PLACE', 'У мастера'], ['CLIENT_PLACE', 'У клиента']], service?.locationType);
  const active = cabinetInput('isActive', 'checkbox');
  active.checked = service?.isActive ?? true;
  const activeLabel = catalogElement('label', 'cabinet-check');
  activeLabel.append(active, catalogElement('span', '', 'Активна в каталоге'));

  form.append(cabinetField('Название', name), cabinetField('Категория', category),
    cabinetField('Описание', description), cabinetField('Цена, BYN', price),
    cabinetField('Длительность, мин', duration), cabinetField('Место оказания', location), activeLabel);
  const path = '/api/master/services' + (service ? '/' + service.id : '');
  wireCabinetForm(form, service ? 'Сохранить услугу' : 'Создать услугу', () => ({
    path, method: service ? 'PUT' : 'POST',
    body: { name: name.value.trim(), categoryId: category.value, description: description.value.trim(),
      price: Number(price.value), durationMinutes: Number(duration.value),
      locationType: location.value, isActive: active.checked }
  }));
  return form;
}

function renderWorkingHours(hours) {
  const form = catalogElement('form', 'card cabinet-card cabinet-form');
  form.append(catalogElement('p', 'catalog-description',
    'Время по Минску. Выберите рабочие дни. Изменения графика сохраняют существующие записи.'));
  const rows = weekdays.map((dayName, index) => {
    const dayOfWeek = index + 1;
    const existing = hours.find(day => day.dayOfWeek === dayOfWeek);
    const enabled = cabinetInput('enabled-' + dayOfWeek, 'checkbox');
    enabled.checked = Boolean(existing);
    const label = catalogElement('label', 'cabinet-check');
    label.append(enabled, catalogElement('span', '', dayName));
    const start = cabinetInput('start-' + dayOfWeek, 'time', existing?.startTime ?? '09:00');
    const end = cabinetInput('end-' + dayOfWeek, 'time', existing?.endTime ?? '18:00');
    start.step = end.step = '1';
    const toggleTimes = () => { start.disabled = end.disabled = !enabled.checked; };
    start.required = end.required = true;
    enabled.addEventListener('change', toggleTimes);
    toggleTimes();

    const row = catalogElement('div', 'cabinet-day');
    const times = catalogElement('div', 'cabinet-times');
    times.append(cabinetField('Начало', start), cabinetField('Конец', end));
    row.append(label, times);
    form.append(row);
    return { dayOfWeek, enabled, start, end };
  });
  const seconds = value => value.length === 5 ? value + ':00' : value;
  wireCabinetForm(form, 'Сохранить график', () => ({
    path: '/api/master/working-hours', method: 'PUT',
    body: { days: rows.filter(row => row.enabled.checked).map(row => ({
      dayOfWeek: row.dayOfWeek, startTime: seconds(row.start.value), endTime: seconds(row.end.value)
    })) }
  }));
  return form;
}

function renderTimeBlocks(blocks) {
  const section = catalogElement('div', 'card cabinet-card');
  const formatter = new Intl.DateTimeFormat('ru-BY', {
    timeZone: 'Europe/Minsk', day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit'
  });
  for (const block of blocks) {
    const item = catalogElement('div', 'cabinet-block');
    const when = formatter.format(new Date(block.startAt)) + ' — ' + formatter.format(new Date(block.endAt));
    item.append(catalogElement('p', 'catalog-name', when));
    if (block.reason) item.append(catalogElement('p', 'catalog-description', block.reason));
    const remove = catalogButton('btn sm ghost', 'Снять блокировку', () => {
      if (window.confirm('Снять блокировку времени?'))
        return submitCabinetChange(remove, { path: '/api/master/time-blocks/' + block.id, method: 'DELETE' });
    });
    item.append(remove);
    section.append(item);
  }
  if (!blocks.length) section.append(catalogElement('p', 'catalog-description', 'Будущих блокировок нет.'));

  const form = catalogElement('form', 'cabinet-form');
  const start = cabinetInput('localStart', 'datetime-local');
  const end = cabinetInput('localEnd', 'datetime-local');
  start.required = end.required = true;
  const reason = cabinetInput('reason', 'text');
  reason.maxLength = 255;
  form.append(cabinetField('Начало по Минску', start), cabinetField('Конец по Минску', end),
    cabinetField('Причина (необязательно)', reason));
  wireCabinetForm(form, 'Заблокировать время', () => ({
    path: '/api/master/time-blocks', method: 'POST',
    body: { localStart: start.value, localEnd: end.value, reason: reason.value.trim() }
  }));
  section.append(form);
  return section;
}

function renderAdminCategories(content, categories) {
  content.append(catalogElement('p', 'catalog-description',
    'Скрытая категория и её услуги исчезают из каталога. Созданные заявки сохраняются.'));
  const create = catalogElement('details', 'card cabinet-card');
  create.append(catalogElement('summary', 'catalog-name', 'Новая категория'), renderCategoryForm());
  content.append(create);

  if (!categories.length) content.append(catalogElement('p', 'empty', 'Категорий пока нет.'));
  for (const category of categories) {
    const card = catalogElement('details', 'card cabinet-card');
    const summary = category.name + (category.isActive ? '' : ' · скрыта');
    card.append(catalogElement('summary', 'catalog-name', summary));
    card.append(catalogElement('p', 'catalog-description category-service-count', 'Услуг в категории: ' + category.serviceCount));
    card.append(renderCategoryForm(category));
    content.append(card);
  }
}

function renderCategoryForm(category) {
  const form = catalogElement('form', 'cabinet-form');
  const name = cabinetInput('name', 'text', category?.name);
  name.maxLength = 100;
  name.required = true;
  const active = cabinetInput('isActive', 'checkbox');
  active.checked = category?.isActive ?? true;
  const visibility = catalogElement('label', 'cabinet-check');
  visibility.append(active, catalogElement('span', '', 'Показывать в каталоге'));
  form.append(cabinetField('Название категории', name), visibility);
  wireCabinetForm(form, category ? 'Сохранить категорию' : 'Создать категорию', () => ({
    path: '/api/admin/categories' + (category ? '/' + category.id : ''),
    method: category ? 'PUT' : 'POST',
    body: { name: name.value.trim(), isActive: active.checked }
  }));
  return form;
}


function renderPendingMasters(content, masters) {
  if (!masters.length) content.append(catalogElement('p', 'empty', 'Профилей на проверке пока нет.'));
  for (const master of masters) {
    const card = catalogElement('article', 'card cabinet-card');
    card.append(catalogElement('h3', 'catalog-name', [master.firstName, master.lastName].filter(Boolean).join(' ')));
    card.append(catalogElement('p', 'catalog-description', master.email));
    const { city, description, address, showAddressInCatalog } = master.profile;
    card.append(catalogElement('p', 'catalog-description', 'Город: ' + (city || 'не указан')));
    card.append(catalogElement('p', 'catalog-description profile-text', description || 'Описание не заполнено.'));
    card.append(catalogElement('p', 'catalog-description', 'Адрес приёма: ' + (address || 'не указан')));
    if (address)
      card.append(catalogElement('p', 'catalog-description',
        showAddressInCatalog ? 'Мастер разрешил показывать адрес в каталоге.' : 'Адрес виден только мастеру и администратору.'));
    card.append(catalogElement('p', 'catalog-description',
      'Активных услуг: ' + master.activeServices + ' · рабочих дней: ' + master.workingDays));
    const approve = catalogButton('btn sm', 'Одобрить', () => submitCabinetChange(approve, {
      path: '/api/admin/masters/' + master.id + '/approve', method: 'POST'
    }));
    card.append(approve);
    content.append(card);
  }
}

function wireCabinetForm(form, label, request) {
  const status = catalogElement('p', 'catalog-description');
  status.setAttribute('role', 'status');
  const button = catalogElement('button', 'btn full', label);
  button.type = 'submit';
  form.append(status, button);
  form.addEventListener('submit', event => {
    event.preventDefault();
    return submitCabinetChange(button, request(), status);
  });
}

async function submitCabinetChange(button, { path, method, body }, status) {
  if (!session || button.disabled) return;
  const activeSession = session;
  const viewingCabinet = () => session === activeSession &&
    document.getElementById('screen-cabinet').classList.contains('active');
  button.disabled = true;
  if (status) status.textContent = 'Сохраняем…';
  try {
    await apiRequest(path, { method, body, token: activeSession.accessToken });
    if (viewingCabinet()) await loadCabinet();
  } catch (error) {
    if (session !== activeSession) return;
    if (error.status === 401) {
      logout();
      showError('Сессия завершилась. Войдите снова.');
    } else if (viewingCabinet()) {
      if (status) status.textContent = error.message;
      else showError(error.message);
    }
  } finally {
    button.disabled = false;
  }
}

document.getElementById('cabinet-refresh').addEventListener('click', loadCabinet);
for (const tab of document.getElementById('admin-cabinet-tabs').querySelectorAll('button')) {
  tab.addEventListener('click', () => {
    adminCabinetSection = tab.dataset.section;
    return loadCabinet();
  });
}

let calendarWeekOffset = 0;

function loadMasterCalendar(weekOffset = 0) {
  if (!session || currentRole !== 'MASTER') return;
  calendarWeekOffset = weekOffset;
  const content = document.getElementById('calendar-content');
  screenRequest?.abort();
  return loadScreenContent('/api/master/calendar?weekOffset=' + weekOffset, content,
    result => renderMasterCalendar(content, result, weekOffset), session.accessToken);
}

function renderMasterCalendar(content, { today, days }, weekOffset) {
  const dateFormat = new Intl.DateTimeFormat('ru-BY', {
    timeZone: 'UTC', weekday: 'long', day: 'numeric', month: 'long'
  });
  const timeFormat = new Intl.DateTimeFormat('ru-BY', {
    timeZone: 'Europe/Minsk', hour: '2-digit', minute: '2-digit', hourCycle: 'h23'
  });
  const navigation = catalogElement('div', 'catalog-search-actions calendar-navigation');
  const goTo = offset => {
    window.scrollTo(0, 0);
    return loadMasterCalendar(offset);
  };
  if (weekOffset > -52)
    navigation.append(catalogButton('btn sm ghost', '← Неделя', () => goTo(weekOffset - 1)));
  if (weekOffset !== 0)
    navigation.append(catalogButton('btn sm ghost', 'Текущая', () => goTo(0)));
  if (weekOffset < 52)
    navigation.append(catalogButton('btn sm ghost', 'Неделя →', () => goTo(weekOffset + 1)));
  content.append(navigation);
  const range = days[0].date.slice(8) + '.' + days[0].date.slice(5, 7) + ' – ' +
    days.at(-1).date.slice(8) + '.' + days.at(-1).date.slice(5, 7) + '.' + days.at(-1).date.slice(0, 4);
  content.append(catalogElement('h3', 'section-title', range));
  content.append(catalogElement('p', 'catalog-description', 'Время по Минску. Рабочие часы взяты из текущего шаблона.'));

  for (const day of days) {
    const current = day.date === today;
    const card = catalogElement('article', 'card cabinet-card calendar-day' + (current ? ' calendar-today' : ''));
    const title = dateFormat.format(new Date(day.date + 'T00:00:00Z')) + (current ? ' · Сегодня' : '');
    card.append(catalogElement('h4', 'catalog-name', title));
    const workingHours = day.workStart && day.workEnd
      ? 'Рабочие часы: ' + day.workStart.slice(0, 5) + '–' + day.workEnd.slice(0, 5)
      : 'Выходной по графику';
    card.append(catalogElement('p', 'catalog-description', workingHours));
    if (!day.entries.length) card.append(catalogElement('p', 'catalog-description', 'Записей и блокировок нет.'));

    for (const entry of day.entries) {
      const item = catalogElement('div', 'calendar-entry');
      const end = entry.endAt === day.endAt ? '24:00' : timeFormat.format(new Date(entry.endAt));
      const when = timeFormat.format(new Date(entry.startAt)) + '–' + end;
      item.append(catalogElement('p', 'catalog-name', when + ' · ' + entry.title));
      if (entry.detail) item.append(catalogElement('p', 'catalog-description', entry.detail));
      if (entry.bookingId) {
        item.append(catalogElement('p', 'catalog-description', bookingStatusLabels[entry.status] ?? entry.status));
        item.append(catalogButton('btn sm ghost', 'Открыть заявку', () => showScreen('bookings', entry.bookingId)));
      }
      card.append(item);
    }
    content.append(card);
  }
}
document.getElementById('calendar-refresh').addEventListener('click', () => loadMasterCalendar(calendarWeekOffset));
