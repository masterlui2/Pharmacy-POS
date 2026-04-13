(() => {
  const body = document.body;
  const root = document.querySelector("[data-wishlist-root]");
  const page = document.querySelector("[data-wishlist-page]");
  const countBadges = document.querySelectorAll("[data-wishlist-count]");
  const isAuthenticated = body.dataset.authenticated === "true";
  const loginUrl = body.dataset.loginUrl || "/Auth/Login";
  const wishlistUrl = body.dataset.wishlistUrl || "/Home/Wishlist";
  const itemsUrl = body.dataset.wishlistItemsUrl || "/wishlist/items";
  const toggleUrl = body.dataset.wishlistToggleUrl || "/wishlist/toggle";
  const removeUrl = body.dataset.wishlistRemoveUrl || "/wishlist/remove";
  const antiForgeryToken =
    document.querySelector("[data-app-antiforgery]")?.value || "";
  const core = window.SafeMedCartCore;
  let currentWishlist = new Set();

  if (!core) {
    return;
  }

  const formatMoney = (value) => core.currency.format(value || 0);

  const setCount = (count) => {
    countBadges.forEach((badge) => {
      badge.textContent = String(count);
    });
  };

  const setButtonState = (productId, active) => {
    document
      .querySelectorAll(`[data-cart-product][data-product-id="${productId}"] [data-wishlist-toggle]`)
      .forEach((button) => {
        if (!(button instanceof HTMLElement)) {
          return;
        }

        button.classList.toggle("is-active", active);
        button.setAttribute("aria-pressed", active ? "true" : "false");
        const icon = button.querySelector("i");
        if (icon) {
          icon.className = active ? "bi bi-heart-fill" : "bi bi-heart";
        }
      });
  };

  const syncButtons = () => {
    document.querySelectorAll("[data-cart-product]").forEach((card) => {
      if (!(card instanceof HTMLElement)) {
        return;
      }

      const productId = card.dataset.productId || "";
      setButtonState(productId, currentWishlist.has(productId));
    });
  };

  const postJson = async (url, payload) => {
    const response = await window.fetch(url, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        RequestVerificationToken: antiForgeryToken,
      },
      body: JSON.stringify(payload),
    });

    const result = await response.json();
    if (!response.ok) {
      throw new Error(result?.message || "Request failed.");
    }

    return result;
  };

  const addWishlistItemToCart = (item) => {
    const cart = core.readCart();
    const existing = cart.find((entry) => entry.id === item.productId);
    const tax = Math.round(item.unitPrice * 0.12 * 100) / 100;

    if (existing) {
      existing.quantity += 1;
    } else {
      cart.push({
        id: item.productId,
        name: item.productName,
        brand: item.brandName,
        image: item.imageUrl,
        price: item.unitPrice,
        tax,
        quantity: 1,
        requiresPrescription: item.requiresPrescription,
      });
    }

    core.writeCart(cart);
    core.syncBagCount(cart);
  };

  const renderWishlistPage = (items) => {
    if (!root) {
      return;
    }

    if (!isAuthenticated) {
      root.innerHTML = `
        <div class="wishlist-page__empty">
          <h2>Sign in to view your wishlist</h2>
          <p>Your saved medicines are tied to your SafeMed account.</p>
          <a href="${core.escapeHtml(loginUrl)}">Login / Sign Up</a>
        </div>`;
      return;
    }

    if (items.length === 0) {
      root.innerHTML = `
        <div class="wishlist-page__empty">
          <h2>Your wishlist is empty</h2>
          <p>Save medicines from the homepage and they will appear here.</p>
          <a href="${core.escapeHtml(core.homeUrl)}">Browse medicines</a>
        </div>`;
      return;
    }

    root.innerHTML = `
      <div class="wishlist-grid">
        ${items
          .map(
            (item) => `
              <article class="wishlist-card" data-wishlist-product="${core.escapeHtml(item.productId)}">
                <div class="wishlist-card__media">
                  <img src="${core.escapeHtml(item.imageUrl)}" alt="${core.escapeHtml(item.brandName)}" />
                </div>
                <div class="wishlist-card__body">
                  <div class="wishlist-card__head">
                    <div>
                      <h2>${core.escapeHtml(item.brandName)}</h2>
                      <p>${core.escapeHtml(item.productName)}</p>
                    </div>
                    ${
                      item.requiresPrescription
                        ? '<span class="wishlist-card__tag">Rx required</span>'
                        : ""
                    }
                  </div>
                  <div class="wishlist-card__footer">
                    <strong>${formatMoney(item.unitPrice)}</strong>
                    <div class="wishlist-card__actions">
                      <button type="button" data-wishlist-remove="${core.escapeHtml(item.productId)}">Remove</button>
                      <button type="button" data-wishlist-add-cart="${core.escapeHtml(item.productId)}">Add to Cart</button>
                    </div>
                  </div>
                </div>
              </article>`,
          )
          .join("")}
      </div>`;
  };

  const loadWishlist = async () => {
    if (!isAuthenticated) {
      setCount(0);
      renderWishlistPage([]);
      return;
    }

    try {
      const response = await window.fetch(itemsUrl, { credentials: "same-origin" });
      const result = await response.json();
      if (!response.ok) {
        throw new Error(result?.message || "Unable to load wishlist.");
      }

      currentWishlist = new Set(result.items.map((item) => item.productId));
      setCount(result.count || 0);
      syncButtons();
      renderWishlistPage(result.items || []);
    } catch {
      setCount(0);
      renderWishlistPage([]);
    }
  };

  document.addEventListener("click", async (event) => {
    const target = event.target;
    if (!(target instanceof HTMLElement)) {
      return;
    }

    const toggleButton = target.closest("[data-wishlist-toggle]");
    if (toggleButton instanceof HTMLElement) {
      const card = toggleButton.closest("[data-cart-product]");
      if (!(card instanceof HTMLElement)) {
        return;
      }

      if (!isAuthenticated) {
        window.location.href = loginUrl;
        return;
      }

      try {
        const product = core.parseProduct(card);
        const result = await postJson(toggleUrl, {
          productId: product.id,
          productName: product.name,
          brandName: product.brand,
          imageUrl: product.image,
          unitPrice: product.price,
          requiresPrescription: product.requiresPrescription,
        });

        if (result.isInWishlist) {
          currentWishlist.add(product.id);
        } else {
          currentWishlist.delete(product.id);
        }

        setCount(result.count || currentWishlist.size);
        setButtonState(product.id, result.isInWishlist);
        if (page) {
          loadWishlist();
        }
      } catch {
        window.alert("Unable to update wishlist right now.");
      }
      return;
    }

    const removeButton = target.closest("[data-wishlist-remove]");
    if (removeButton instanceof HTMLElement) {
      try {
        const productId = removeButton.dataset.wishlistRemove || "";
        const result = await postJson(removeUrl, { productId });
        currentWishlist.delete(productId);
        setCount(result.count || currentWishlist.size);
        setButtonState(productId, false);
        loadWishlist();
      } catch {
        window.alert("Unable to remove this item right now.");
      }
      return;
    }

    const addToCartButton = target.closest("[data-wishlist-add-cart]");
    if (addToCartButton instanceof HTMLElement && root) {
      const productId = addToCartButton.dataset.wishlistAddCart || "";
      const response = await window.fetch(itemsUrl, { credentials: "same-origin" });
      const result = await response.json();
      const item = (result.items || []).find((entry) => entry.productId === productId);
      if (item) {
        addWishlistItemToCart(item);
        window.location.href = wishlistUrl;
      }
    }
  });

  document.addEventListener("DOMContentLoaded", loadWishlist);
})();
