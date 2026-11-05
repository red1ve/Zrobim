document.getElementById('profile-subscription').addEventListener('click', openMasterSubscription);
document.getElementById('payment-back').addEventListener('click', () =>
  showScreen(currentRole === 'MASTER' ? 'cabinet' : 'bookings'));

function subscriptionUntil(value) {
  return new Intl.DateTimeFormat('ru-BY', { dateStyle: 'long', timeStyle: 'short' }).format(new Date(value));
}

function renderSubscriptionCard(data) {
  const card = catalogElement('article', 'card catalog-details subscription-card');
  card.append(catalogElement('h3', 'catalog-name', 'Подписка мастера'));
  if (data.priceMonthly != null)
    card.append(catalogElement('p', 'demo-payment-amount', formatPrice(data.priceMonthly, data.currency) + ' / месяц'));
  if (data.isActive) {
    card.append(catalogElement('p', 'subscription-active', '✓ Подписка активна'));
    card.append(catalogElement('p', 'catalog-description', 'До ' + subscriptionUntil(data.expiresAt)));
  } else {
    card.append(catalogElement('p', 'catalog-description', 'Подписка пока не оформлена. Один платёж — один месяц.'));
    if (data.canPayDemo)
      card.append(catalogButton('btn full', 'Оформить подписку', openMasterSubscription));
    else
      card.append(catalogElement('p', 'catalog-description', 'Оформление временно недоступно.'));
  }
  card.append(catalogElement('p', 'catalog-description', 'Без автоматического продления. Демонстрационная оплата, деньги не списываются.'));
  return card;
}

function openMasterSubscription() {
  if (!session || currentRole !== 'MASTER') return;
  showScreen('payment');
  document.getElementById('payment-title').textContent = 'Подписка мастера';
  const content = document.getElementById('payment-content');
  return loadScreenContent('/api/master/subscription', content, data => {
    if (!data.canPayDemo) {
      content.append(renderSubscriptionCard(data));
      content.append(catalogButton('btn full ghost', 'В кабинет', () => showScreen('cabinet')));
      return;
    }
    const form = catalogElement('form', 'card catalog-details demo-payment');
    form.append(catalogElement('h3', 'catalog-name', 'Оформить подписку'));
    form.append(catalogElement('p', 'demo-payment-amount', formatPrice(data.priceMonthly, data.currency) + ' / месяц'));
    form.append(catalogElement('p', 'catalog-description', 'Срок действия — один месяц с момента оплаты. Без автоматического продления.'));
    form.append(catalogElement('p', 'catalog-description', 'Демонстрационный режим. Деньги не списываются.'));
    const methods = catalogElement('fieldset', 'demo-payment-methods');
    methods.append(catalogElement('legend', 'catalog-name', 'Способ оплаты'));
    for (const [key, label] of Object.entries(demoPaymentMethods)) {
      const option = catalogElement('label', 'demo-payment-choice');
      const radio = catalogElement('input', '');
      Object.assign(radio, { type: 'radio', name: 'method', value: key, required: true, checked: key === 'CARD' });
      option.append(radio, catalogElement('span', '', label));
      methods.append(option);
    }
    const status = catalogElement('p', 'catalog-description');
    status.setAttribute('role', 'status');
    const button = catalogElement('button', 'btn full', 'Оплатить ' + formatPrice(data.priceMonthly, data.currency));
    button.type = 'submit';
    form.append(methods, status, button);
    form.addEventListener('submit', event => {
      event.preventDefault();
      return submitSubscriptionPayment(form, button, status, content);
    });
    content.append(form, catalogButton('btn full ghost', 'В кабинет', () => showScreen('cabinet')));
  }, session.accessToken);
}

async function submitSubscriptionPayment(form, button, status, content) {
  if (!session || currentRole !== 'MASTER' || button.disabled) return;
  const method = new FormData(form).get('method');
  if (!method) return;
  const activeSession = session;
  const stillViewing = () => session === activeSession && form.isConnected &&
    document.getElementById('screen-payment').classList.contains('active');
  button.disabled = true;
  status.textContent = 'Оформляем подписку…';
  try {
    const receipt = await apiRequest('/api/master/subscription/demo-payment', {
      body: { method }, token: activeSession.accessToken
    });
    if (!stillViewing()) return;
    document.getElementById('payment-title').textContent = 'Оплата прошла успешно';
    const card = renderSubscriptionCard(receipt);
    const heading = card.querySelector('h3');
    heading.tabIndex = -1;
    content.replaceChildren(card, catalogButton('btn full', 'В кабинет', () => showScreen('cabinet')));
    heading.focus();
  } catch (error) {
    if (!stillViewing()) return;
    if (error.status === 401) {
      logout();
      showError('Сессия завершилась. Войдите снова.');
    } else {
      status.textContent = error.message;
      if (error.status === 404 || error.status === 409)
        button.replaceWith(catalogButton('btn full ghost', 'Обновить', openMasterSubscription));
    }
  } finally {
    button.disabled = false;
  }
}
