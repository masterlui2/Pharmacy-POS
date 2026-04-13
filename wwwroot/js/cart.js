(() => {
  const CART_KEY = "safemed-cart";
  const PROMO_KEY = "safemed-cart-promo";
  const RX_UPLOADS_KEY = "safemed-cart-rx-uploads";
  const LEGACY_KEYS = [CART_KEY, PROMO_KEY, RX_UPLOADS_KEY, "safemed-checkout-draft"];
  const currency = new Intl.NumberFormat("en-PH", {
    style: "currency",
    currency: "PHP",
  });

  const bagCount = document.querySelector("[data-cart-count]");
  const cartModal = document.querySelector("[data-cart-modal]");
  const antiForgeryToken =
    document.querySelector("[data-app-antiforgery]")?.value || "";
  const body = document.body;
  const homeUrl =
    document.querySelector(".storefront-logo")?.getAttribute("href") || "/";
  const loginUrl = body.dataset.loginUrl || "/Auth/Login";
  const myOrdersUrl = body.dataset.myOrdersUrl || "/Orders";
  const isAuthenticated = body.dataset.authenticated === "true";
  const accountScope = (() => {
    const email = (body.dataset.userEmail || "").trim().toLowerCase();
    if (isAuthenticated && email) {
      return `account:${email}`;
    }

    return "guest";
  })();

  const promoCatalog = {
    SAFEMED10: 0.1,
    RXLESS5: 0.05,
  };

  const getScopedKey = (baseKey) => `${baseKey}:${accountScope}`;
  const getCheckoutDraftKey = () => `safemed-checkout-draft:${accountScope}`;

  const purgeLegacyKeys = () => {
    LEGACY_KEYS.forEach((key) => window.localStorage.removeItem(key));
  };

  const escapeHtml = (value) =>
    String(value)
      .replaceAll("&", "&amp;")
      .replaceAll("<", "&lt;")
      .replaceAll(">", "&gt;")
      .replaceAll('"', "&quot;")
      .replaceAll("'", "&#39;");

  const readJson = (key, fallback) => {
    try {
      const raw = window.localStorage.getItem(key);
      return raw ? JSON.parse(raw) : fallback;
    } catch {
      return fallback;
    }
  };

  const readCart = () => {
    const parsed = readJson(getScopedKey(CART_KEY), []);
    return Array.isArray(parsed) ? parsed : [];
  };

  const writeCart = (cart) => {
    window.localStorage.setItem(getScopedKey(CART_KEY), JSON.stringify(cart));
    syncBagCount(cart);
  };

  const readPromo = () => {
    const parsed = readJson(getScopedKey(PROMO_KEY), null);
    return parsed &&
      typeof parsed.code === "string" &&
      typeof parsed.rate === "number"
      ? parsed
      : null;
  };

  const writePromo = (promo) => {
    if (!promo) {
      window.localStorage.removeItem(getScopedKey(PROMO_KEY));
      return;
    }

    window.localStorage.setItem(getScopedKey(PROMO_KEY), JSON.stringify(promo));
  };

  const readRxUploads = () => {
    const parsed = readJson(getScopedKey(RX_UPLOADS_KEY), { files: [], submitted: false });
    return {
      files: Array.isArray(parsed?.files)
        ? parsed.files.filter((name) => typeof name === "string")
        : [],
      submitted: Boolean(parsed?.submitted),
    };
  };

  const writeRxUploads = (payload) => {
    window.localStorage.setItem(getScopedKey(RX_UPLOADS_KEY), JSON.stringify(payload));
  };

  const getPromoRate = (code) => promoCatalog[code] || 0;

  const getCartTotals = (cart, promo) => {
    const itemCount = cart.reduce((sum, item) => sum + item.quantity, 0);
    const subtotal = cart.reduce(
      (sum, item) => sum + item.price * item.quantity,
      0,
    );
    const taxes = cart.reduce((sum, item) => sum + item.tax * item.quantity, 0);
    const discount = promo ? subtotal * promo.rate : 0;

    return {
      itemCount,
      subtotal,
      taxes,
      discount,
    };
  };

  const syncBagCount = (cart = readCart()) => {
    if (!bagCount) {
      return;
    }

    const totalItems = cart.reduce((sum, item) => sum + item.quantity, 0);
    bagCount.textContent = String(totalItems);
  };

  const parseProduct = (card) => ({
    id: card.dataset.productId,
    name: card.dataset.productName,
    brand: card.dataset.productBrand,
    image: card.dataset.productImage,
    price: Number.parseFloat(card.dataset.productPrice || "0"),
    tax: Number.parseFloat(card.dataset.productTax || "0"),
    requiresPrescription: card.dataset.productRx === "true",
  });

  const getDisplayName = (item) =>
    item.requiresPrescription ? `Rx: ${item.name}` : item.name;

  const showModal = (item, quantityAdded) => {
    if (!cartModal) {
      return;
    }

    const image = cartModal.querySelector("[data-cart-modal-image]");
    const name = cartModal.querySelector("[data-cart-modal-name]");
    const price = cartModal.querySelector("[data-cart-modal-price]");
    const qty = cartModal.querySelector("[data-cart-modal-quantity]");
    const tax = cartModal.querySelector("[data-cart-modal-tax]");
    const rxNotice = cartModal.querySelector("[data-cart-modal-rx]");

    if (!image || !name || !price || !qty || !tax || !rxNotice) {
      return;
    }

    image.src = item.image;
    image.alt = item.name;
    name.textContent = getDisplayName(item);
    price.textContent = currency.format(item.price);
    qty.textContent = `Quantity: ${quantityAdded}`;
    tax.textContent = currency.format(item.tax * quantityAdded);
    rxNotice.hidden = !item.requiresPrescription;

    cartModal.hidden = false;
    document.body.style.overflow = "hidden";
  };

  const hideModal = () => {
    if (!cartModal) {
      return;
    }

    cartModal.hidden = true;
    document.body.style.overflow = "";
  };

  const addToCart = (product, quantity) => {
    const cart = readCart();
    const existing = cart.find((item) => item.id === product.id);

    if (existing) {
      existing.quantity += quantity;
    } else {
      cart.push({ ...product, quantity });
    }

    try {
      const rawDraft = window.localStorage.getItem(getCheckoutDraftKey());
      if (rawDraft) {
        const parsedDraft = JSON.parse(rawDraft);
        if (parsedDraft?.ui?.orderNumber) {
          parsedDraft.step = 1;
          parsedDraft.ui = {
            message: "",
            tone: "",
            busy: false,
            orderNumber: "",
          };
          window.localStorage.setItem(getCheckoutDraftKey(), JSON.stringify(parsedDraft));
        }
      }
    } catch {
      window.localStorage.removeItem(getCheckoutDraftKey());
    }

    writeCart(cart);
    showModal(product, quantity);
  };

  const initializeMedicineCards = () => {
    document.querySelectorAll("[data-cart-product]").forEach((card) => {
      const qtyValue = card.querySelector("[data-qty-value]");
      const minusButton = card.querySelector("[data-qty-decrease]");
      const plusButton = card.querySelector("[data-qty-increase]");
      const addButton = card.querySelector("[data-add-to-cart]");

      if (!qtyValue || !minusButton || !plusButton || !addButton) {
        return;
      }

      const setQuantity = (next) => {
        const safeValue = Math.min(99, Math.max(1, next));
        qtyValue.textContent = String(safeValue);
      };

      minusButton.addEventListener("click", () => {
        setQuantity(Number.parseInt(qtyValue.textContent || "1", 10) - 1);
      });

      plusButton.addEventListener("click", () => {
        setQuantity(Number.parseInt(qtyValue.textContent || "1", 10) + 1);
      });

      addButton.addEventListener("click", () => {
        const quantity = Number.parseInt(qtyValue.textContent || "1", 10);
        addToCart(parseProduct(card), quantity);
      });
    });
  };

  const initializeModal = () => {
    if (!cartModal) {
      return;
    }

    cartModal.addEventListener("click", (event) => {
      const target = event.target;
      if (!(target instanceof HTMLElement)) {
        return;
      }

      if (target.closest("[data-cart-modal-close]")) {
        hideModal();
      }
    });

    document.addEventListener("keydown", (event) => {
      if (event.key === "Escape") {
        hideModal();
      }
    });
  };

  window.SafeMedCartCore = {
    currency,
    homeUrl,
    loginUrl,
    myOrdersUrl,
    isAuthenticated,
    accountScope,
    antiForgeryToken,
    escapeHtml,
    parseProduct,
    readCart,
    writeCart,
    readPromo,
    writePromo,
    readRxUploads,
    writeRxUploads,
    getCartTotals,
    getPromoRate,
    syncBagCount,
  };

  document.addEventListener("DOMContentLoaded", () => {
    purgeLegacyKeys();
    syncBagCount();
    initializeMedicineCards();
    initializeModal();
  });

  window.addEventListener("storage", () => {
    syncBagCount();
  });
})();
