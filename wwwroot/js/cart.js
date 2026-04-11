(() => {
  const CART_KEY = "safemed-cart";
  const PROMO_KEY = "safemed-cart-promo";
  const RX_UPLOADS_KEY = "safemed-cart-rx-uploads";
  const currency = new Intl.NumberFormat("en-PH", {
    style: "currency",
    currency: "PHP",
  });

  const bagCount = document.querySelector("[data-cart-count]");
  const cartModal = document.querySelector("[data-cart-modal]");
  const cartPage = document.querySelector("[data-cart-page]");
  const body = document.body;
  const homeUrl =
    document.querySelector(".storefront-logo")?.getAttribute("href") || "/";
  const loginUrl = body.dataset.loginUrl || "/Auth/Login";
  const isAuthenticated = body.dataset.authenticated === "true";

  const promoCatalog = {
    SAFEMED10: 0.1,
    RXLESS5: 0.05,
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
    const parsed = readJson(CART_KEY, []);
    return Array.isArray(parsed) ? parsed : [];
  };

  const writeCart = (cart) => {
    window.localStorage.setItem(CART_KEY, JSON.stringify(cart));
    syncBagCount(cart);
  };

  const readPromo = () => {
    const parsed = readJson(PROMO_KEY, null);
    return parsed &&
      typeof parsed.code === "string" &&
      typeof parsed.rate === "number"
      ? parsed
      : null;
  };

  const writePromo = (promo) => {
    if (!promo) {
      window.localStorage.removeItem(PROMO_KEY);
      return;
    }

    window.localStorage.setItem(PROMO_KEY, JSON.stringify(promo));
  };

  const readRxUploads = () => {
    const parsed = readJson(RX_UPLOADS_KEY, { files: [], submitted: false });
    return {
      files: Array.isArray(parsed?.files)
        ? parsed.files.filter((name) => typeof name === "string")
        : [],
      submitted: Boolean(parsed?.submitted),
    };
  };

  const writeRxUploads = (payload) => {
    window.localStorage.setItem(RX_UPLOADS_KEY, JSON.stringify(payload));
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

  const updateItemQuantity = (id, delta) => {
    const cart = readCart();
    const item = cart.find((entry) => entry.id === id);
    if (!item) {
      return;
    }

    item.quantity = Math.max(1, item.quantity + delta);
    writeCart(cart);
    renderCartPage();
  };

  const removeItem = (id) => {
    const nextCart = readCart().filter((item) => item.id !== id);
    writeCart(nextCart);
    renderCartPage();
  };

  const getCartTotals = (cart, promo) => {
    const itemCount = cart.reduce((sum, item) => sum + item.quantity, 0);
    const subtotal = cart.reduce(
      (sum, item) => sum + item.price * item.quantity,
      0,
    );
    const taxes = cart.reduce((sum, item) => sum + item.tax * item.quantity, 0);
    const discount = promo ? subtotal * promo.rate : 0;
    const total = Math.max(0, subtotal - discount);

    return {
      itemCount,
      subtotal,
      taxes,
      discount,
      total,
    };
  };

  const getPrescriptionSectionMarkup = (uploads) => `
    <div class="cart-rx-stack">
      <div class="cart-rx-reminder">
        <i class="bi bi-exclamation-triangle-fill"></i>
        <p>You've selected a prescription medicine. Please upload your prescription below to proceed. If you don't have one, remove the item from your cart to check out.</p>
      </div>

      <section class="cart-rx-upload">
        <h2>Upload Prescription</h2>
        <p>Upload a photo of your prescription with the doctor's license no. visible. Accepted files: pdf, png, jpg, jpeg, gif. Maximum file size is 10MB.</p>

        <input type="file" class="cart-rx-upload__input" data-rx-upload-input accept=".pdf,.png,.jpg,.jpeg,.gif" multiple />
        <button type="button" class="cart-rx-upload__add" data-rx-upload-trigger>Add More Prescriptions</button>

        <div class="cart-rx-upload__files">
          <h3>Uploaded files</h3>
          ${
            uploads.files.length > 0
              ? uploads.files
                  .map(
                    (fileName, index) => `
                      <div class="cart-rx-file">
                        <div class="cart-rx-file__meta">
                          <i class="bi bi-file-earmark-arrow-up-fill"></i>
                          <span>${escapeHtml(fileName)}</span>
                        </div>
                        <button type="button" aria-label="Remove uploaded prescription" data-rx-file-remove data-file-index="${index}">x</button>
                      </div>`,
                  )
                  .join("")
              : `<div class="cart-rx-file cart-rx-file--empty">
                   <div class="cart-rx-file__meta">
                     <i class="bi bi-file-earmark-arrow-up-fill"></i>
                     <span>No file selected</span>
                   </div>
                 </div>`
          }
        </div>

        <div class="cart-rx-upload__footer">
          <p>Once you have uploaded your Prescription, click the Submit to proceed to checkout.</p>
          <button type="button" class="cart-rx-upload__submit" data-rx-submit ${uploads.files.length === 0 ? "disabled" : ""}>
            Submit All Prescriptions
          </button>
        </div>
        <p class="cart-rx-upload__status">${uploads.submitted ? "Prescription files marked as submitted for checkout." : ""}</p>
      </section>
    </div>`;

  const renderCartPage = () => {
    if (!cartPage) {
      return;
    }

    const root = cartPage.querySelector("[data-cart-root]");
    if (!root) {
      return;
    }

    const cart = readCart();
    const promo = readPromo();
    const totals = getCartTotals(cart, promo);
    const hasRxItems = cart.some((item) => item.requiresPrescription);
    const loginNotice = hasRxItems && !isAuthenticated;
    const showUploadCard = hasRxItems && isAuthenticated;
    const uploads = readRxUploads();
    const canCheckout = !hasRxItems || loginNotice || uploads.submitted;

    if (cart.length === 0) {
      writeRxUploads({ files: [], submitted: false });
      root.innerHTML = `
        <div class="cart-page__empty-state">
          <h2>Your cart is empty</h2>
          <p>Add medicines from the homepage and they will appear here.</p>
          <a class="cart-page__continue-link" href="${escapeHtml(homeUrl)}">Continue Shopping</a>
        </div>`;
      return;
    }

    if (!hasRxItems && (uploads.files.length > 0 || uploads.submitted)) {
      writeRxUploads({ files: [], submitted: false });
    }

    const itemsMarkup = cart
      .map((item) => {
        const lineTotal = item.price * item.quantity;
        const rxBadge = item.requiresPrescription
          ? '<span class="cart-item__rx-badge">Rx</span>'
          : "";

        return `
          <article class="cart-item">
            <div class="cart-item__product">
              <div class="cart-item__image">
                <img src="${escapeHtml(item.image)}" alt="${escapeHtml(item.name)}" />
              </div>
              <div class="cart-item__details">
                <h2>${rxBadge}<span>${escapeHtml(item.name)}</span></h2>
                <div class="cart-item__unit-price">${currency.format(item.price)}</div>
                ${
                  item.requiresPrescription
                    ? '<p class="cart-item__upload">Prescription upload required at checkout</p>'
                    : ""
                }
              </div>
            </div>
            <div class="cart-item__qty">
              <div class="cart-item__qty-controls">
                <button type="button" aria-label="Decrease quantity" data-cart-decrease data-id="${escapeHtml(item.id)}">-</button>
                <span>${item.quantity}</span>
                <button type="button" aria-label="Increase quantity" data-cart-increase data-id="${escapeHtml(item.id)}">+</button>
              </div>
            </div>
            <div class="cart-item__line-total">${currency.format(lineTotal)}</div>
            <div class="cart-item__remove-wrap">
              <button type="button" class="cart-item__remove" aria-label="Remove item" data-cart-remove data-id="${escapeHtml(item.id)}">
                <i class="bi bi-trash-fill"></i>
              </button>
            </div>
          </article>`;
      })
      .join("");

    root.innerHTML = `
      <div class="cart-checkout">
        <div class="cart-checkout__main">
          <div class="cart-steps" aria-label="Checkout steps">
            <div class="cart-step cart-step--active">1. Summary</div>
            <div class="cart-step">2. Address</div>
            <div class="cart-step">3. Shipping</div>
            <div class="cart-step">4. Payment</div>
          </div>

          <div class="cart-headline">
            <h1>Your Cart</h1>
            <p>Your shopping cart contains: <strong>${totals.itemCount} product${totals.itemCount === 1 ? "" : "s"}</strong></p>
          </div>

          <div class="cart-table-head">
            <span>Product</span>
            <span>Qty</span>
            <span>Total Price</span>
            <span></span>
          </div>

          <div class="cart-items">${itemsMarkup}</div>
          ${showUploadCard ? getPrescriptionSectionMarkup(uploads) : ""}

          <a class="cart-page__back-link" href="${escapeHtml(homeUrl)}">
            <i class="bi bi-arrow-left"></i>
            <span>Continue shopping</span>
          </a>
        </div>

        <aside class="cart-checkout__side">
          ${
            loginNotice
              ? `<div class="cart-alert">
                  <i class="bi bi-exclamation-triangle-fill"></i>
                  <span>Log in to process prescription products</span>
                </div>`
              : ""
          }

          <div class="cart-summary-card">
            <div class="cart-summary-card__title">Order Summary</div>
            <div class="cart-summary-card__rows">
              <div class="cart-summary-card__row">
                <span>${totals.itemCount} item${totals.itemCount === 1 ? "" : "s"}</span>
                <strong>${currency.format(totals.subtotal)}</strong>
              </div>
              <div class="cart-summary-card__row">
                <span>Shipping</span>
                <strong>--</strong>
              </div>
              <div class="cart-summary-card__row cart-summary-card__row--total">
                <span>Total (tax incl.)</span>
                <strong>${currency.format(totals.total)}</strong>
              </div>
              <div class="cart-summary-card__row">
                <span>Included taxes:</span>
                <strong>${currency.format(totals.taxes)}</strong>
              </div>
              ${
                promo
                  ? `<div class="cart-summary-card__row">
                      <span>Promo (${escapeHtml(promo.code)})</span>
                      <strong>-${currency.format(totals.discount)}</strong>
                    </div>`
                  : ""
              }
            </div>

            <form class="cart-promo" data-cart-promo-form>
              <input type="text" name="promoCode" placeholder="Enter Promo Code Here" value="${escapeHtml(
                promo?.code || "",
              )}" />
              <button type="submit">Submit</button>
            </form>
            <p class="cart-promo__message" data-cart-promo-message></p>
          </div>

          <div class="cart-processing-note">
            <strong>Your order will be processed by the SafeMed franchise THE GENERICS PHARMACY INC.</strong>
          </div>

          ${
            loginNotice
              ? `<a class="cart-login-callout" href="${escapeHtml(loginUrl)}">Click here to Log in/Sign up</a>`
              : `<button type="button" class="cart-checkout-btn" ${canCheckout ? "" : "disabled"}>
                   ${canCheckout ? "Checkout" : "Submit prescriptions first"}
                 </button>`
          }
        </aside>
      </div>`;
  };

  const initializeCartPage = () => {
    if (!cartPage) {
      return;
    }

    renderCartPage();

    cartPage.addEventListener("click", (event) => {
      const target = event.target;
      if (!(target instanceof HTMLElement)) {
        return;
      }

      const decrease = target.closest("[data-cart-decrease]");
      if (decrease instanceof HTMLElement) {
        updateItemQuantity(decrease.dataset.id || "", -1);
        return;
      }

      const increase = target.closest("[data-cart-increase]");
      if (increase instanceof HTMLElement) {
        updateItemQuantity(increase.dataset.id || "", 1);
        return;
      }

      const remove = target.closest("[data-cart-remove]");
      if (remove instanceof HTMLElement) {
        removeItem(remove.dataset.id || "");
        return;
      }

      const uploadTrigger = target.closest("[data-rx-upload-trigger]");
      if (uploadTrigger instanceof HTMLElement) {
        cartPage.querySelector("[data-rx-upload-input]")?.click();
        return;
      }

      const uploadRemove = target.closest("[data-rx-file-remove]");
      if (uploadRemove instanceof HTMLElement) {
        const uploads = readRxUploads();
        const index = Number.parseInt(
          uploadRemove.dataset.fileIndex || "-1",
          10,
        );
        if (index >= 0) {
          uploads.files.splice(index, 1);
          uploads.submitted = false;
          writeRxUploads(uploads);
          renderCartPage();
        }
        return;
      }

      if (target.closest("[data-rx-submit]")) {
        const uploads = readRxUploads();
        if (uploads.files.length > 0) {
          uploads.submitted = true;
          writeRxUploads(uploads);
          renderCartPage();
        }
      }
    });

    cartPage.addEventListener("submit", (event) => {
      const form = event.target;
      if (
        !(form instanceof HTMLFormElement) ||
        !form.matches("[data-cart-promo-form]")
      ) {
        return;
      }

      event.preventDefault();
      const message = cartPage.querySelector("[data-cart-promo-message]");
      const input = form.elements.namedItem("promoCode");

      if (
        !(message instanceof HTMLElement) ||
        !(input instanceof HTMLInputElement)
      ) {
        return;
      }

      const code = input.value.trim().toUpperCase();
      if (!code) {
        writePromo(null);
        message.textContent = "";
        renderCartPage();
        return;
      }

      const rate = promoCatalog[code];
      if (!rate) {
        writePromo(null);
        message.textContent = "Promo code not recognized.";
        return;
      }

      writePromo({ code, rate });
      renderCartPage();

      const nextMessage = cartPage.querySelector("[data-cart-promo-message]");
      if (nextMessage instanceof HTMLElement) {
        nextMessage.textContent = `Promo code ${code} applied.`;
      }
    });

    cartPage.addEventListener("change", (event) => {
      const target = event.target;
      if (
        !(target instanceof HTMLInputElement) ||
        !target.matches("[data-rx-upload-input]")
      ) {
        return;
      }

      const selectedFiles = Array.from(target.files || [])
        .filter((file) => file.size <= 10 * 1024 * 1024)
        .map((file) => file.name);

      if (selectedFiles.length === 0) {
        return;
      }

      const uploads = readRxUploads();
      uploads.files = [...uploads.files, ...selectedFiles];
      uploads.submitted = false;
      writeRxUploads(uploads);
      renderCartPage();
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

  document.addEventListener("DOMContentLoaded", () => {
    syncBagCount();
    initializeMedicineCards();
    initializeModal();
    initializeCartPage();
  });

  window.addEventListener("storage", () => {
    syncBagCount();
    renderCartPage();
  });
})();
