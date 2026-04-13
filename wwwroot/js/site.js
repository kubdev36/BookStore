// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

(function () {
  var form = document.querySelector('.dc-head-search[data-autocomplete="books"]');
  if (!form) {
    return;
  }

  var input = form.querySelector('input[name="keyword"]');
  var dropdown = form.querySelector('.dc-search-dropdown');
  var suggestUrl = form.getAttribute('data-suggest-url');
  var timerId = 0;
  var currentRequest = null;

  if (!input || !dropdown || !suggestUrl) {
    return;
  }

  function closeDropdown() {
    dropdown.hidden = true;
    dropdown.innerHTML = '';
  }

  function makeFallback(title) {
    var fallback = document.createElement('div');
    fallback.className = 'dc-search-thumb-fallback';
    fallback.textContent = (title || 'Sách').slice(0, 4).toUpperCase();
    return fallback;
  }

  function renderItems(items) {
    dropdown.innerHTML = '';

    if (!items || items.length === 0) {
      var empty = document.createElement('div');
      empty.className = 'dc-search-empty';
      empty.textContent = 'Không tìm thấy sách phù hợp.';
      dropdown.appendChild(empty);
      dropdown.hidden = false;
      return;
    }

    items.forEach(function (item) {
      var link = document.createElement('a');
      link.className = 'dc-search-item';
      link.href = item.detailsUrl;

      if (item.coverUrl) {
        var image = document.createElement('img');
        image.className = 'dc-search-thumb';
        image.src = item.coverUrl;
        image.alt = item.title || 'Sách';

        var fallback = makeFallback(item.title);
        image.addEventListener('error', function () {
          image.style.display = 'none';
          fallback.style.display = 'flex';
        });

        link.appendChild(image);
        link.appendChild(fallback);
      } else {
        var fallbackOnly = makeFallback(item.title);
        fallbackOnly.style.display = 'flex';
        link.appendChild(fallbackOnly);
      }

      var textWrap = document.createElement('div');
      textWrap.className = 'dc-search-text';

      var title = document.createElement('strong');
      title.textContent = item.title || 'Chưa có tiêu đề';

      var author = document.createElement('span');
      author.textContent = item.author || 'Tác giả chưa cập nhật';

      textWrap.appendChild(title);
      textWrap.appendChild(author);
      link.appendChild(textWrap);
      dropdown.appendChild(link);
    });

    dropdown.hidden = false;
  }

  function fetchSuggestions(keyword) {
    if (currentRequest) {
      currentRequest.abort();
    }

    currentRequest = new AbortController();
    var url = suggestUrl + '?keyword=' + encodeURIComponent(keyword);

    fetch(url, { signal: currentRequest.signal })
      .then(function (response) {
        if (!response.ok) {
          throw new Error('Request failed');
        }
        return response.json();
      })
      .then(function (items) {
        renderItems(items);
      })
      .catch(function (error) {
        if (error.name !== 'AbortError') {
          closeDropdown();
        }
      });
  }

  input.addEventListener('input', function () {
    var keyword = input.value.trim();
    window.clearTimeout(timerId);

    if (keyword.length < 2) {
      closeDropdown();
      return;
    }

    timerId = window.setTimeout(function () {
      fetchSuggestions(keyword);
    }, 180);
  });

  input.addEventListener('focus', function () {
    if (dropdown.innerHTML.trim()) {
      dropdown.hidden = false;
    }
  });

  document.addEventListener('click', function (event) {
    if (!form.contains(event.target)) {
      closeDropdown();
    }
  });

  form.addEventListener('submit', function () {
    closeDropdown();
  });
})();

(function () {
  var menus = document.querySelectorAll('[data-user-menu]');
  if (!menus.length) {
    return;
  }

  function closeMenu(menu) {
    var trigger = menu.querySelector('.dc-user-trigger');
    var dropdown = menu.querySelector('.dc-user-dropdown');
    if (!trigger || !dropdown) {
      return;
    }

    trigger.setAttribute('aria-expanded', 'false');
    dropdown.hidden = true;
  }

  menus.forEach(function (menu) {
    var trigger = menu.querySelector('.dc-user-trigger');
    var dropdown = menu.querySelector('.dc-user-dropdown');
    if (!trigger || !dropdown) {
      return;
    }

    trigger.addEventListener('click', function (event) {
      event.preventDefault();
      var willOpen = dropdown.hidden;

      menus.forEach(function (otherMenu) {
        if (otherMenu !== menu) {
          closeMenu(otherMenu);
        }
      });

      dropdown.hidden = !willOpen;
      trigger.setAttribute('aria-expanded', willOpen ? 'true' : 'false');
    });
  });

  document.addEventListener('click', function (event) {
    menus.forEach(function (menu) {
      if (!menu.contains(event.target)) {
        closeMenu(menu);
      }
    });
  });

  document.addEventListener('keydown', function (event) {
    if (event.key === 'Escape') {
      menus.forEach(closeMenu);
    }
  });
})();


