let badgeRequest = null;

async function refreshNotificationBadge() {
  const badge = document.getElementById('notification-badge');
  badgeRequest?.abort();
  if (!session) {
    badge.hidden = true;
    badge.textContent = '';
    return;
  }

  const activeSession = session;
  const request = new AbortController();
  badgeRequest = request;
  try {
    const result = await apiRequest('/api/notifications/unread-count', {
      token: activeSession.accessToken, signal: request.signal
    });
    if (request.signal.aborted || session !== activeSession) return;
    badge.textContent = result.count > 99 ? '99+' : String(result.count);
    badge.hidden = result.count === 0;
  } catch {
    // The next refresh can recover from a transient error.
  } finally {
    if (badgeRequest === request) badgeRequest = null;
  }
}

function loadNotifications(page = 1) {
  if (!session) return;
  const content = document.getElementById('notifications-content');
  screenRequest?.abort();
  return loadScreenContent('/api/notifications?page=' + page, content,
    result => renderNotifications(content, result, page), session.accessToken);
}

function renderNotifications(content, result, page) {
  if (!result.items.length)
    content.append(catalogElement('p', 'empty', 'Уведомлений пока нет.'));

  const dateFormat = new Intl.DateTimeFormat('ru-BY', {
    timeZone: 'Europe/Minsk', day: 'numeric', month: 'long', hour: '2-digit', minute: '2-digit'
  });
  for (const item of result.items) {
    const card = catalogElement('article', 'booking-item' + (item.isRead ? '' : ' notification-unread'));
    card.append(catalogElement('h3', 'client', item.title));
    card.append(catalogElement('p', 'time', dateFormat.format(new Date(item.createdAt))));
    if (!item.isRead) card.append(catalogElement('p', 'info', 'Новое'));
    if (item.message) card.append(catalogElement('p', 'info profile-text', item.message));
    if (item.bookingId || !item.isRead) {
      const label = item.bookingId ? 'Открыть запись' : 'Отметить прочитанным';
      const button = catalogButton('btn sm ghost notification-open', label,
        () => openNotification(item, button, page));
      card.append(button);
    }
    content.append(card);
  }

  if (page > 1 || result.hasMore) {
    const navigation = catalogElement('div', 'catalog-search-actions');
    if (page > 1)
      navigation.append(catalogButton('btn sm ghost', 'Предыдущая', () => loadNotifications(page - 1)));
    navigation.append(catalogElement('span', 'catalog-search-page', 'Страница ' + page));
    if (result.hasMore)
      navigation.append(catalogButton('btn sm ghost', 'Следующая', () => loadNotifications(page + 1)));
    content.append(navigation);
  }
}

async function openNotification(item, button, page) {
  if (!session || button.disabled) return;
  const activeSession = session;
  button.disabled = true;
  try {
    if (!item.isRead)
      await apiRequest('/api/notifications/' + item.id + '/read', { method: 'POST', token: activeSession.accessToken });
    if (session !== activeSession) return;

    if (document.getElementById('screen-notifications').classList.contains('active')) {
      if (item.bookingId) {
        showScreen('bookings', item.bookingId);
        return;
      }
      await loadNotifications(page);
    }
    refreshNotificationBadge();
  } catch (error) {
    if (session !== activeSession) return;
    if (error.status === 401) {
      logout();
      showError('Сессия завершилась. Войдите снова.');
    } else {
      showError(error.message);
    }
  } finally {
    button.disabled = false;
  }
}

function initNotifications() {
  document.getElementById('notifications-refresh').addEventListener('click', () => {
    loadNotifications();
    refreshNotificationBadge();
  });
  setInterval(() => {
    if (session && document.visibilityState !== 'hidden') refreshNotificationBadge();
  }, 30000);
}
