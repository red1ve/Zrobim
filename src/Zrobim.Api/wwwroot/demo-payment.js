const demoPaymentMethods = { CARD: 'Банковская карта', ERIP: 'ЕРИП' };

function openDemoPayment(booking) {
  if (!session || !booking.canPayDemo) return;
  showScreen('payment');
  document.getElementById('payment-title').textContent = 'Оплата';
  const content = document.getElementById('payment-content');
  const form = catalogElement('form', 'card catalog-details demo-payment');
  form.append(catalogElement('h3', 'catalog-name', booking.serviceName));
  form.append(catalogElement('p', 'demo-payment-amount', formatPrice(booking.price, booking.currency)));
  form.append(catalogElement('p', 'catalog-description',
    'Демонстрационный режим. Деньги не списываются.'));
  const methods = catalogElement('fieldset', 'demo-payment-methods');
  methods.append(catalogElement('legend', 'catalog-name', 'Способ оплаты'));
  for (const [key, label] of Object.entries(demoPaymentMethods)) {
    const option = catalogElement('label', 'demo-payment-choice');
    const radio = catalogElement('input', '');
    radio.type = 'radio';
    radio.name = 'method';
    radio.value = key;
    radio.required = true;
    radio.checked = key === 'CARD';
    option.append(radio, catalogElement('span', '', label));
    methods.append(option);
  }
  form.append(methods);
  const status = catalogElement('p', 'catalog-description');
  status.setAttribute('role', 'status');
  const button = catalogElement('button', 'btn full', 'Оплатить');
  button.type = 'submit';
  form.append(status, button);
  form.addEventListener('submit', event => {
    event.preventDefault();
    return submitDemoPayment(booking, form, button, status, content);
  });
  content.replaceChildren(form);
}

async function submitDemoPayment(booking, form, button, status, content) {
  if (!session || button.disabled) return;
  const method = new FormData(form).get('method');
  if (!method) return;
  const activeSession = session;
  const stillViewing = () => session === activeSession && form.isConnected &&
    document.getElementById('screen-payment').classList.contains('active');
  button.disabled = true;
  status.textContent = 'Подтверждаем оплату…';

  try {
    const receipt = await apiRequest('/api/bookings/' + booking.id + '/demo-payment', {
      body: { method }, token: activeSession.accessToken
    });
    if (!stillViewing()) return;
    document.getElementById('payment-title').textContent = 'Оплата прошла успешно';
    const summary = catalogElement('article', 'card catalog-details demo-payment');
    const heading = catalogElement('h3', 'catalog-name', '✓ Запись подтверждена');
    heading.tabIndex = -1;
    summary.append(heading);
    summary.append(catalogElement('p', 'demo-payment-amount', formatPrice(receipt.amount, receipt.currency)));
    summary.append(catalogElement('p', 'catalog-description', demoPaymentMethods[receipt.method]));
    summary.append(catalogElement('p', 'catalog-description', 'Демонстрационная оплата. Списания не было.'));
    summary.append(catalogButton('btn full', 'К записи', () => showScreen('bookings', receipt.bookingId)));
    content.replaceChildren(summary);
    heading.focus();
  } catch (error) {
    if (!stillViewing()) return;
    if (error.status === 401) {
      logout();
      showError('Сессия завершилась. Войдите снова.');
    } else {
      status.textContent = error.message;
      if (error.status === 409 || error.status === 404)
        button.replaceWith(catalogButton('btn full ghost', 'Обновить записи', () => showScreen('bookings', booking.id)));
    }
  } finally {
    button.disabled = false;
  }
}
