function favoriteButton(masterId, initialState, onChange) {
  const activeSession = session;
  let saved = initialState;
  const path = '/api/favorites/' + masterId;
  const isCurrent = () => activeSession === session && currentRole === 'CLIENT';
  const button = catalogButton('btn sm ghost favorite-action', '', async () => {
    if (!isCurrent() || button.disabled) return;
    if (saved === undefined) return loadFavoriteStatus();

    button.disabled = true;
    try {
      await apiRequest(path, { method: saved ? 'DELETE' : 'PUT', token: activeSession.accessToken });
      if (!isCurrent()) return;
      saved = !saved;
      updateButton();
      if (button.isConnected && onChange) await onChange();
    } catch (error) {
      if (isCurrent() && button.isConnected) handleFavoriteError(error);
    } finally {
      button.disabled = false;
    }
  });

  function updateButton() {
    if (saved === undefined) {
      button.textContent = 'Проверить избранное';
      return;
    }
    button.setAttribute('aria-pressed', String(saved));
    button.textContent = saved ? '★ Убрать из избранного' : '☆ В избранное';
  }

  async function loadFavoriteStatus() {
    button.disabled = true;
    button.textContent = 'Проверяем избранное…';
    try {
      const result = await apiRequest(path, { token: activeSession.accessToken });
      if (isCurrent()) saved = result.isFavorite;
    } catch (error) {
      if (isCurrent() && button.isConnected) handleFavoriteError(error);
    } finally {
      button.disabled = false;
      updateButton();
    }
  }

  if (saved === undefined) loadFavoriteStatus();
  else updateButton();
  return button;
}

function handleFavoriteError(error) {
  if (error.status === 401) {
    logout();
    showError('Сессия завершилась. Войдите снова.');
  } else {
    showError(error.message);
  }
}

function showFavorites(page = 1) {
  if (!session || currentRole !== 'CLIENT') return;
  window.scrollTo(0, 0);
  const content = catalogPage('Избранные мастера', showCategories);
  return loadScreenContent('/api/favorites?page=' + page, content, result => {
    if (!result.items.length)
      content.append(catalogElement('p', 'empty', 'Здесь будут мастера, которых вы сохранили.'));

    for (const { masterId, master } of result.items) {
      const card = catalogElement('article', 'card catalog-details favorite-card');
      if (master) {
        const name = [master.firstName, master.lastName].filter(Boolean).join(' ');
        card.append(catalogElement('h4', 'catalog-name', name));
        if (master.city) card.append(catalogElement('p', 'catalog-description', master.city));
        card.append(catalogElement('p', 'catalog-description', masterRatingText(master)));
        const servicesPath = '/api/catalog/masters/' + masterId + '/services';
        card.append(catalogButton('btn sm', 'Открыть услуги', () =>
          showMasterServices(master, name, servicesPath, () => showFavorites(page))));
      } else {
        card.append(catalogElement('p', 'catalog-description', 'Мастер сейчас недоступен.'));
      }
      card.append(favoriteButton(masterId, true, () =>
        showFavorites(page > 1 && result.items.length === 1 ? page - 1 : page)));
      content.append(card);
    }

    if (page > 1 || result.hasMore) {
      const pagination = catalogElement('div', 'catalog-search-actions');
      if (page > 1)
        pagination.append(catalogButton('btn sm ghost', 'Предыдущая', () => showFavorites(page - 1)));
      pagination.append(catalogElement('span', 'catalog-search-page', 'Страница ' + page));
      if (result.hasMore)
        pagination.append(catalogButton('btn sm ghost', 'Следующая', () => showFavorites(page + 1)));
      content.append(pagination);
    }
  }, session.accessToken);
}
