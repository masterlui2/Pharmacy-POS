(() => {
  const shippingProfiles = {
    Standard: {
      label: "Standard delivery",
      etaLabel: "45-75 minutes",
      fee: 79,
      branch: "SafeMed Main Branch",
      detail: "Balanced speed for regular medicine orders.",
    },
    Express: {
      label: "Express delivery",
      etaLabel: "20-35 minutes",
      fee: 149,
      branch: "SafeMed Express Hub - Main Branch",
      detail: "Priority dispatch for urgent medicine needs.",
    },
  };

  const paymentLabels = {
    CashOnDelivery: "Cash on Delivery",
    EWallet: "E-wallet",
    Card: "Credit / Debit Card",
  };

  const getPrescriptionStatus = (cart, uploads) => {
    const hasRx = cart.some((item) => item.requiresPrescription);

    if (!hasRx) {
      return { code: "NotRequired", label: "Not required", tone: "success" };
    }

    if (uploads.submitted) {
      return { code: "Valid", label: "Valid", tone: "success" };
    }

    if (uploads.files.length > 0) {
      return { code: "Uploaded", label: "Uploaded", tone: "warning" };
    }

    return { code: "Missing", label: "Missing", tone: "danger" };
  };

  const renderStepPill = (step, currentStep, label) => {
    const state =
      currentStep === step
        ? "active"
        : currentStep > step
          ? "complete"
          : "upcoming";

    return `<button type="button" class="cart-step-pill cart-step-pill--${state}" data-step-go="${step}">
      <span class="cart-step-pill__number">${step}</span>
      <span>${label}</span>
    </button>`;
  };

  const renderSummaryStep = (ctx) => {
    const { core, cart, uploads, prescription, deliveryProfile, promo } = ctx;

    const itemsMarkup = cart
      .map(
        (item) => `
          <article class="cart-line">
            <div class="cart-line__media">
              <img src="${core.escapeHtml(item.image)}" alt="${core.escapeHtml(item.name)}" />
            </div>
            <div class="cart-line__body">
              <div class="cart-line__meta">
                <h3>${core.escapeHtml(item.name)}</h3>
                <span class="cart-status-badge cart-status-badge--${item.requiresPrescription ? "danger" : "success"}">
                  ${item.requiresPrescription ? "Prescription required" : "Ready"}
                </span>
              </div>
              <p>${core.escapeHtml(item.brand || "SafeMed")}</p>
              <div class="cart-line__actions">
                <button type="button" data-cart-decrease="${core.escapeHtml(item.id)}">-</button>
                <span>${item.quantity}</span>
                <button type="button" data-cart-increase="${core.escapeHtml(item.id)}">+</button>
                <button type="button" class="cart-line__remove" data-cart-remove="${core.escapeHtml(item.id)}">Remove</button>
              </div>
            </div>
            <div class="cart-line__price">
              <strong>${core.currency.format(item.price * item.quantity)}</strong>
              <span>${core.currency.format(item.tax * item.quantity)} tax</span>
            </div>
          </article>`,
      )
      .join("");

    const uploadsMarkup =
      uploads.files.length > 0
        ? uploads.files
            .map(
              (file, index) => `
                <div class="cart-upload-file">
                  <span>${core.escapeHtml(file)}</span>
                  <button type="button" data-rx-remove="${index}"><i class="bi bi-x-lg"></i></button>
                </div>`,
            )
            .join("")
        : `<div class="cart-upload-file cart-upload-file--empty">No prescription uploaded yet.</div>`;

    const warningMarkup =
      prescription.code === "Missing"
        ? `<div class="cart-banner cart-banner--danger">
             <i class="bi bi-exclamation-triangle-fill"></i>
             <div>
               <strong>Prescription missing</strong>
               <span>Upload and validate the prescription before placing an order with Rx items.</span>
             </div>
           </div>`
        : prescription.code === "Uploaded"
          ? `<div class="cart-banner cart-banner--warning">
               <i class="bi bi-file-earmark-medical-fill"></i>
               <div>
                 <strong>Prescription uploaded</strong>
                 <span>Mark the upload as valid when the prescription details are ready.</span>
               </div>
             </div>`
          : "";

    return `
      <section class="cart-panel">
        <div class="cart-panel__head">
          <div>
            <p class="cart-eyebrow">Step 1</p>
            <h1>Cart Summary</h1>
          </div>
          <div class="cart-estimate-mini">
            <span>Estimated delivery</span>
            <strong>${deliveryProfile.etaLabel}</strong>
          </div>
        </div>
        ${warningMarkup}
        <div class="cart-line-list">${itemsMarkup}</div>
        <div class="cart-summary-grid">
          <div class="cart-panel cart-panel--nested">
            <div class="cart-panel__subhead">
              <h2>Prescription Status</h2>
              <span class="cart-status-badge cart-status-badge--${prescription.tone}">${prescription.label}</span>
            </div>
            <p class="cart-copy">Missing, Uploaded, and Valid states stay visible through checkout so medical compliance is obvious.</p>
            <div class="cart-upload-list">${uploadsMarkup}</div>
            <div class="cart-upload-actions">
              <input type="file" accept=".pdf,.png,.jpg,.jpeg,.gif" multiple hidden data-rx-input />
              <button type="button" class="cart-secondary-btn" data-rx-trigger>Upload Prescription</button>
              <button type="button" class="cart-primary-btn" data-rx-validate ${uploads.files.length === 0 ? "disabled" : ""}>Mark as Valid</button>
            </div>
          </div>
          <div class="cart-panel cart-panel--nested">
            <div class="cart-panel__subhead">
              <h2>Delivery Preview</h2>
              <span class="cart-status-badge cart-status-badge--neutral">${deliveryProfile.label}</span>
            </div>
            <div class="cart-kv"><span>ETA</span><strong>${deliveryProfile.etaLabel}</strong></div>
            <div class="cart-kv"><span>Delivery fee</span><strong>${core.currency.format(deliveryProfile.fee)}</strong></div>
            <div class="cart-kv"><span>Fulfillment branch</span><strong>${deliveryProfile.branch}</strong></div>
            <form class="cart-promo" data-cart-promo-form>
              <input type="text" name="promoCode" value="${core.escapeHtml(promo?.code || "")}" placeholder="Promo code" />
              <button type="submit">Apply</button>
            </form>
            <p class="cart-field-hint" data-cart-promo-message>${promo ? `Promo ${core.escapeHtml(promo.code)} applied.` : ""}</p>
          </div>
        </div>
        <a class="cart-page__back-link" href="${core.escapeHtml(core.homeUrl)}">
          <i class="bi bi-arrow-left"></i>
          <span>Continue shopping</span>
        </a>
      </section>`;
  };

  const renderAddressStep = (ctx) => {
    const { core, draft } = ctx;
    return `
      <section class="cart-panel">
        <div class="cart-panel__head">
          <div>
            <p class="cart-eyebrow">Step 2</p>
            <h1>Address Information</h1>
          </div>
        </div>
        <div class="checkout-form-grid">
          <label class="checkout-field">
            <span>Full name</span>
            <input type="text" value="${core.escapeHtml(draft.address.fullName)}" data-draft-field="address.fullName" />
          </label>
          <label class="checkout-field">
            <span>Phone number</span>
            <input type="tel" value="${core.escapeHtml(draft.address.phoneNumber)}" data-draft-field="address.phoneNumber" />
          </label>
          <label class="checkout-field checkout-field--wide">
            <span>Full delivery address</span>
            <textarea rows="4" data-draft-field="address.deliveryAddress">${core.escapeHtml(draft.address.deliveryAddress)}</textarea>
          </label>
          <label class="checkout-field checkout-field--wide">
            <span>Landmark <small>(optional)</small></span>
            <input type="text" value="${core.escapeHtml(draft.address.landmark)}" data-draft-field="address.landmark" />
          </label>
        </div>
        <div class="checkout-choice-group">
          <span class="checkout-choice-group__label">Address type</span>
          <div class="checkout-choice-row">
            ${["Home", "Work", "Other"]
              .map(
                (value) => `
                  <button type="button" class="checkout-choice ${draft.address.addressType === value ? "checkout-choice--active" : ""}" data-address-type="${value}">
                    ${value}
                  </button>`,
              )
              .join("")}
          </div>
        </div>
        <label class="checkout-toggle">
          <input type="checkbox" ${draft.address.saveAddress ? "checked" : ""} data-draft-checkbox="address.saveAddress" />
          <span>Save this address for future checkout</span>
        </label>
      </section>`;
  };

  const renderShippingStep = (ctx) => {
    const { draft, core } = ctx;
    return `
      <section class="cart-panel">
        <div class="cart-panel__head">
          <div>
            <p class="cart-eyebrow">Step 3</p>
            <h1>Shipping & Delivery</h1>
          </div>
        </div>
        <div class="shipping-option-grid">
          ${Object.entries(shippingProfiles)
            .map(
              ([key, profile]) => `
                <button type="button" class="shipping-card ${draft.shipping.option === key ? "shipping-card--active" : ""}" data-shipping-option="${key}">
                  <div class="shipping-card__top">
                    <strong>${profile.label}</strong>
                    <span>${core.currency.format(profile.fee)}</span>
                  </div>
                  <p>${profile.detail}</p>
                  <div class="shipping-card__meta">
                    <span>${profile.etaLabel}</span>
                    <span>${profile.branch}</span>
                  </div>
                </button>`,
            )
            .join("")}
        </div>
        <div class="cart-banner cart-banner--info">
          <i class="bi bi-shop"></i>
          <div>
            <strong>Pharmacy fulfillment</strong>
            <span>${shippingProfiles[draft.shipping.option].branch} will prepare and process this order.</span>
          </div>
        </div>
      </section>`;
  };

  const renderPaymentStep = (ctx) => {
    const { core, cart, draft, deliveryProfile, totals, promo, prescription } = ctx;
    const finalTotal =
      totals.subtotal + totals.taxes + deliveryProfile.fee - totals.discount;

    return `
      <section class="cart-panel">
        <div class="cart-panel__head">
          <div>
            <p class="cart-eyebrow">Step 4</p>
            <h1>Payment</h1>
          </div>
        </div>
        <div class="payment-option-grid">
          ${Object.entries(paymentLabels)
            .map(
              ([key, label]) => `
                <button type="button" class="payment-card ${draft.payment.method === key ? "payment-card--active" : ""}" data-payment-method="${key}">
                  <strong>${label}</strong>
                  <span>${
                    key === "CashOnDelivery"
                      ? "Pay upon arrival"
                      : key === "EWallet"
                        ? "GCash and similar wallets"
                        : "Visa and Mastercard"
                  }</span>
                </button>`,
            )
            .join("")}
        </div>
        <div class="cart-summary-repeat">
          <div class="cart-panel__subhead">
            <h2>Order Summary</h2>
            <span class="cart-status-badge cart-status-badge--${prescription.tone}">${prescription.label}</span>
          </div>
          ${cart
            .map(
              (item) => `
                <div class="cart-kv">
                  <span>${item.quantity} x ${core.escapeHtml(item.name)}</span>
                  <strong>${core.currency.format(item.price * item.quantity)}</strong>
                </div>`,
            )
            .join("")}
          <div class="cart-kv"><span>Subtotal</span><strong>${core.currency.format(totals.subtotal)}</strong></div>
          <div class="cart-kv"><span>Shipping</span><strong>${core.currency.format(deliveryProfile.fee)}</strong></div>
          <div class="cart-kv"><span>Taxes</span><strong>${core.currency.format(totals.taxes)}</strong></div>
          ${
            promo
              ? `<div class="cart-kv"><span>Discount (${core.escapeHtml(promo.code)})</span><strong>- ${core.currency.format(totals.discount)}</strong></div>`
              : ""
          }
          <div class="cart-kv cart-kv--total"><span>Final total</span><strong>${core.currency.format(finalTotal)}</strong></div>
        </div>
      </section>`;
  };

  const renderSidebar = (ctx) => {
    const { core, draft, totals, prescription } = ctx;
    const deliveryProfile = shippingProfiles[draft.shipping.option];
    const finalTotal =
      totals.subtotal + totals.taxes + deliveryProfile.fee - totals.discount;
    const blocked =
      prescription.code === "Missing" || prescription.code === "Uploaded";

    return `
      <aside class="checkout-sidebar">
        <div class="cart-panel cart-panel--sidebar">
          <div class="cart-panel__subhead">
            <h2>Order Summary</h2>
            <span>${totals.itemCount} item${totals.itemCount === 1 ? "" : "s"}</span>
          </div>
          <div class="cart-kv"><span>Subtotal</span><strong>${core.currency.format(totals.subtotal)}</strong></div>
          <div class="cart-kv"><span>Shipping</span><strong>${core.currency.format(deliveryProfile.fee)}</strong></div>
          <div class="cart-kv"><span>Taxes</span><strong>${core.currency.format(totals.taxes)}</strong></div>
          ${
            totals.discount > 0
              ? `<div class="cart-kv"><span>Discount</span><strong>- ${core.currency.format(totals.discount)}</strong></div>`
              : ""
          }
          <div class="cart-kv cart-kv--total"><span>Final total</span><strong>${core.currency.format(finalTotal)}</strong></div>
        </div>
        <div class="cart-panel cart-panel--sidebar">
          <div class="cart-panel__subhead">
            <h2>Compliance</h2>
            <span class="cart-status-badge cart-status-badge--${prescription.tone}">${prescription.label}</span>
          </div>
          <p class="cart-copy">Warnings stay red until the prescription requirement is cleared.</p>
          ${
            blocked
              ? `<div class="cart-banner cart-banner--danger cart-banner--compact">
                   <i class="bi bi-shield-exclamation"></i>
                   <div>
                     <strong>Checkout blocked</strong>
                     <span>Prescription medicines cannot be placed yet.</span>
                   </div>
                 </div>`
              : ""
          }
        </div>
        <div class="cart-panel cart-panel--sidebar">
          <div class="cart-panel__subhead">
            <h2>Delivery</h2>
            <span>${deliveryProfile.label}</span>
          </div>
          <div class="cart-kv"><span>ETA</span><strong>${deliveryProfile.etaLabel}</strong></div>
          <div class="cart-kv"><span>Branch</span><strong>${deliveryProfile.branch}</strong></div>
        </div>
        ${
          draft.ui.message
            ? `<div class="cart-banner cart-banner--${draft.ui.tone || "info"}">
                 <i class="bi bi-info-circle-fill"></i>
                 <div><strong>${core.escapeHtml(draft.ui.message)}</strong></div>
               </div>`
            : ""
        }
        <div class="checkout-nav">
          ${
            draft.step > 1
              ? '<button type="button" class="cart-secondary-btn" data-step-prev>Back</button>'
              : ""
          }
          ${
            draft.step < 4
              ? '<button type="button" class="cart-primary-btn" data-step-next>Next Step</button>'
              : `<button type="button" class="cart-primary-btn" data-place-order ${draft.ui.busy || blocked ? "disabled" : ""}>
                   ${draft.ui.busy ? "Placing Order..." : "Place Order"}
                 </button>`
          }
        </div>
      </aside>`;
  };

  const render = (root, ctx) => {
    const { core, cart, uploads, draft } = ctx;

    if (cart.length === 0 && !draft.ui.orderNumber) {
      root.innerHTML = `
        <div class="cart-page__empty-state">
          <h2>Your cart is empty</h2>
          <p>Add medicines from the homepage and they will appear here.</p>
          <a class="cart-page__continue-link" href="${core.escapeHtml(core.homeUrl)}">Continue Shopping</a>
        </div>`;
      return;
    }

    const promo = core.readPromo();
    const totals = core.getCartTotals(cart, promo);
    const prescription = getPrescriptionStatus(cart, uploads);
    const deliveryProfile = shippingProfiles[draft.shipping.option];
    const context = { ...ctx, promo, totals, prescription, deliveryProfile };

    const successMarkup = `
      <section class="cart-panel cart-panel--success">
        <div class="cart-banner cart-banner--success">
          <i class="bi bi-check-circle-fill"></i>
          <div>
            <strong>Order ${core.escapeHtml(draft.ui.orderNumber)} confirmed</strong>
            <span>Your order is queued at ${deliveryProfile.branch}.</span>
          </div>
        </div>
        <a class="cart-page__continue-link" href="${core.escapeHtml(core.homeUrl)}">Continue Shopping</a>
      </section>`;

    const stepMarkup =
      draft.ui.orderNumber
        ? successMarkup
        : draft.step === 1
          ? renderSummaryStep(context)
          : draft.step === 2
            ? renderAddressStep(context)
            : draft.step === 3
              ? renderShippingStep(context)
              : renderPaymentStep(context);

    root.innerHTML = `
      <div class="checkout-shell">
        <div class="checkout-main">
          <div class="cart-step-row">
            ${renderStepPill(1, draft.step, "Summary")}
            ${renderStepPill(2, draft.step, "Address")}
            ${renderStepPill(3, draft.step, "Shipping")}
            ${renderStepPill(4, draft.step, "Payment")}
          </div>
          ${stepMarkup}
        </div>
        ${draft.ui.orderNumber ? "" : renderSidebar(context)}
      </div>`;
  };

  window.SafeMedCheckoutRenderer = {
    render,
    shippingProfiles,
    getPrescriptionStatus,
  };
})();
