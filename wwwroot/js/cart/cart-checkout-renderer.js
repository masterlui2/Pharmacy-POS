(() => {
  const shippingProfiles = {
    Standard: {
      label: "Standard delivery",
      etaLabel: "45-75 minutes",
      surcharge: 0,
      detail: "Balanced speed for regular medicine orders within Davao City coverage.",
    },
  };

  const paymentLabels = {
    CashOnDelivery: "Cash on Delivery",
    GCash: "GCash",
    Card: "Credit / Debit Card",
  };

  const getPrescriptionStatus = (cart, uploads) => {
    const hasRx = cart.some((item) => item.requiresPrescription);
    if (!hasRx) {
      return { code: "NotRequired", label: "Not required", tone: "success" };
    }

    if (uploads.submitted) {
      return { code: "PendingReview", label: "Pending pharmacist review", tone: "warning" };
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

  const renderPrescriptionUpload = (core, uploads, prescription) => {
    if (prescription.code === "NotRequired") {
      return `<div class="cart-banner cart-banner--success cart-banner--compact">
        <i class="bi bi-check-circle-fill"></i>
        <div>
          <strong>No prescription required</strong>
          <span>This cart can continue directly to the next step.</span>
        </div>
      </div>`;
    }

    const filesMarkup =
      uploads.files.length > 0
        ? uploads.files
            .map(
              (file, index) => `
                <div class="cart-rx-upload-card__file">
                  <div class="cart-rx-upload-card__file-meta">
                    <i class="bi bi-image"></i>
                    <span>${core.escapeHtml(file.name || "Prescription file")}</span>
                  </div>
                  <button type="button" data-rx-remove="${index}" aria-label="Remove uploaded file">
                    <i class="bi bi-x-lg"></i>
                  </button>
                </div>`,
            )
            .join("")
        : `<div class="cart-rx-upload-card__file cart-rx-upload-card__file--empty">
             <div class="cart-rx-upload-card__file-meta">
               <i class="bi bi-image"></i>
               <span>No file selected</span>
             </div>
           </div>`;

    return `
      <section class="cart-rx-upload-card">
        <div class="cart-rx-upload-card__head">
          <h2>Upload Prescription</h2>
          <span class="cart-status-badge cart-status-badge--${prescription.tone}">${prescription.label}</span>
        </div>
        <p class="cart-rx-upload-card__copy">
          Upload a clear photo or PDF. Accepted formats: pdf, png, jpg, jpeg, gif. Maximum file size: 10MB.
        </p>
        <input type="file" accept=".pdf,.png,.jpg,.jpeg,.gif" multiple hidden data-rx-input />
        <button type="button" class="cart-rx-upload-card__add" data-rx-trigger>Add More Prescriptions</button>
        <div class="cart-rx-upload-card__files">
          <h3>Uploaded files</h3>
          ${filesMarkup}
        </div>
        <div class="cart-rx-upload-card__footer">
          <p>Submit the uploaded prescription so the pharmacist can review it before payment.</p>
          <button type="button" class="cart-rx-upload-card__submit" data-rx-validate ${uploads.files.length === 0 ? "disabled" : ""}>
            Submit for Review
          </button>
        </div>
      </section>`;
  };

  const renderSummaryStep = (ctx) => {
    const { core, cart, uploads, prescription, deliveryProfile } = ctx;

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

    let accessNotice = "";
    if (!core.isAuthenticated) {
      accessNotice = `<div class="cart-banner cart-banner--danger">
        <i class="bi bi-person-lock"></i>
        <div>
          <strong>Sign in required</strong>
          <span>You must be signed in to proceed with address, payment, and order placement.</span>
        </div>
      </div>`;
    }

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
        ${accessNotice}
        <div class="cart-line-list">${itemsMarkup}</div>
        <div class="cart-panel cart-panel--nested cart-panel--rx-only">
          <div class="cart-panel__subhead">
            <h2>Prescription</h2>
            ${
              prescription.code === "NotRequired"
                ? ""
                : `<span class="cart-status-badge cart-status-badge--${prescription.tone}">${prescription.label}</span>`
            }
          </div>
          <p class="cart-copy">Upload is only needed for prescription items. Pharmacist approval is required before payment.</p>
          ${renderPrescriptionUpload(core, uploads, prescription)}
        </div>
        <a class="cart-page__back-link" href="${core.escapeHtml(core.homeUrl)}">
          <i class="bi bi-arrow-left"></i>
          <span>Continue shopping</span>
        </a>
      </section>`;
  };

  const renderAddressStep = (ctx) => {
    const { core, draft, deliverySettings } = ctx;
    const hasPin =
      typeof draft.address.latitude === "number" &&
      typeof draft.address.longitude === "number";
    const coverageTone =
      draft.address.coverageStatus === "covered"
        ? "success"
        : draft.address.coverageStatus === "blocked"
          ? "danger"
          : "warning";

    return `
      <section class="cart-panel">
        <div class="cart-panel__head">
          <div>
            <p class="cart-eyebrow">Step 2</p>
            <h1>Address Information</h1>
          </div>
          <div class="cart-estimate-mini">
            <span>Delivery zone</span>
            <strong>Davao City Only</strong>
          </div>
        </div>
        ${
          !deliverySettings.apiKey
            ? `<div class="cart-banner cart-banner--warning">
                 <i class="bi bi-geo-alt-fill"></i>
                 <div>
                   <strong>Google Maps setup required</strong>
                   <span>Add your Google Maps API key in configuration before enabling address pinning.</span>
                 </div>
               </div>`
            : ""
        }
        <div class="checkout-address-layout">
          <div class="checkout-address-layout__form">
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
                <span>Search location</span>
                <input type="text" value="${core.escapeHtml(draft.address.deliveryAddress)}" placeholder="Search within Davao City" data-map-search />
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
          </div>
          <div class="checkout-map-card">
            <div class="checkout-map-card__head">
              <div>
                <h2>Pin Your Delivery Location</h2>
                <p>Search, click, or drag the marker to confirm a Davao delivery point.</p>
              </div>
              <button type="button" class="cart-secondary-btn checkout-map-card__locate" data-map-locate>Use My Location</button>
            </div>
            <div class="checkout-map-canvas" data-checkout-map></div>
            <div class="checkout-map-card__status">
              <span class="cart-status-badge cart-status-badge--${coverageTone}">
                ${hasPin ? "Location selected" : "Pin required"}
              </span>
              <p data-map-status>${core.escapeHtml(draft.address.coverageLabel || "Pick a location within Davao City to continue.")}</p>
            </div>
            <div class="checkout-map-card__meta">
              <div class="cart-kv">
                <span>Dispatch branch</span>
                <strong>${core.escapeHtml(deliverySettings.branchName)}</strong>
              </div>
              <div class="cart-kv">
                <span>Coverage radius</span>
                <strong>${deliverySettings.maxRadiusKm.toFixed(0)} km</strong>
              </div>
              <div class="cart-kv">
                <span>Current distance</span>
                <strong>${draft.address.distanceKm ? `${draft.address.distanceKm.toFixed(1)} km` : "Not set"}</strong>
              </div>
              <div class="cart-kv">
                <span>Estimated fee</span>
                <strong>${core.currency.format(draft.address.deliveryFee || 0)}</strong>
              </div>
            </div>
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
            <p class="cart-eyebrow">Step 3</p>
            <h1>Payment</h1>
          </div>
        </div>
        <div class="payment-option-grid">
          ${Object.entries(paymentLabels)
            .map(
              ([key, label]) => `
                <button type="button" class="payment-card ${draft.payment.method === key ? "payment-card--active" : ""}" data-payment-method="${key}">
                  <div class="payment-card__top">
                    <div class="payment-card__logos">
                      ${
                        key === "CashOnDelivery"
                          ? '<span class="payment-logo payment-logo--cash"><i class="bi bi-cash-coin"></i></span>'
                          : key === "GCash"
                            ? '<span class="payment-logo payment-logo--image"><img src="/images/Payments/gcash.png" alt="GCash" /></span>'
                            : '<span class="payment-logo payment-logo--image payment-logo--card-image"><img src="/images/Payments/card.png" alt="Card payment" /></span>'
                      }
                    </div>
                    <strong>${label}</strong>
                  </div>
                  <span>${
                    key === "CashOnDelivery"
                      ? "Pay upon arrival"
                      : key === "GCash"
                        ? "Secure wallet checkout with PayMongo"
                        : "Visa and Mastercard via PayMongo"
                  }</span>
                </button>`,
            )
            .join("")}
        </div>
        ${prescription.code === "PendingReview"
          ? `<div class="cart-banner cart-banner--warning">
               <i class="bi bi-hourglass-split"></i>
               <div>
                 <strong>Waiting for pharmacist approval</strong>
                 <span>Your order will be submitted for review first. Online payment will only open after the pharmacist approves the prescription.</span>
               </div>
             </div>`
          : draft.payment.method === "CashOnDelivery"
            ? ""
            : `<div class="cart-banner cart-banner--info">
                 <i class="bi bi-shield-lock-fill"></i>
                 <div>
                   <strong>Secure PayMongo checkout</strong>
                   <span>You will be redirected to PayMongo to complete payment safely.</span>
                 </div>
               </div>`}
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
          <div class="cart-kv"><span>Delivery Fee</span><strong>${core.currency.format(deliveryProfile.fee)}</strong></div>
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
    const selectedProfile = shippingProfiles.Standard;
    const shippingFee = (draft.address.deliveryFee || 0) + selectedProfile.surcharge;
    const finalTotal = totals.subtotal + totals.taxes + shippingFee - totals.discount;
    const finalActionLabel =
      draft.ui.busy
        ? "Placing Order..."
        : prescription.code === "PendingReview"
          ? "Submit Order for Review"
          : "Place Order";
    const placeOrderBlocked =
      !core.isAuthenticated ||
      prescription.code === "Missing" ||
      prescription.code === "Uploaded" ||
      draft.address.coverageStatus !== "covered";

    return `
      <aside class="checkout-sidebar">
        <div class="cart-panel cart-panel--sidebar">
          <div class="cart-panel__subhead">
            <h2>Order Summary</h2>
            <span>${totals.itemCount} item${totals.itemCount === 1 ? "" : "s"}</span>
          </div>
          <div class="cart-kv"><span>Subtotal</span><strong>${core.currency.format(totals.subtotal)}</strong></div>
          <div class="cart-kv"><span>Delivery Fee</span><strong>${core.currency.format(shippingFee)}</strong></div>
          <div class="cart-kv"><span>Taxes</span><strong>${core.currency.format(totals.taxes)}</strong></div>
          ${
            totals.discount > 0
              ? `<div class="cart-kv"><span>Discount</span><strong>- ${core.currency.format(totals.discount)}</strong></div>`
              : ""
          }
          <div class="cart-kv cart-kv--total"><span>Final total</span><strong>${core.currency.format(finalTotal)}</strong></div>
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
            !core.isAuthenticated
              ? `<a class="cart-primary-btn cart-primary-btn--link" href="${core.escapeHtml(core.loginUrl)}">Log in to Continue</a>`
              : draft.step < 3
                ? `<button type="button" class="cart-primary-btn" data-step-next>Next Step</button>`
                : `<button type="button" class="cart-primary-btn" data-place-order ${draft.ui.busy || placeOrderBlocked ? "disabled" : ""}>
                     ${finalActionLabel}
                   </button>`
          }
        </div>
      </aside>`;
  };

  const render = (root, ctx) => {
    const { core, cart, draft } = ctx;

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
    const uploads = core.readRxUploads();
    const prescription = getPrescriptionStatus(cart, uploads);
    const selectedProfile = shippingProfiles.Standard;
    const deliveryProfile = {
      ...selectedProfile,
      fee: (draft.address.deliveryFee || 0) + selectedProfile.surcharge,
    };
    const context = { ...ctx, uploads, promo, totals, prescription, deliveryProfile };

    const successMarkup = `
      <section class="cart-panel cart-panel--success">
        <div class="cart-banner cart-banner--success">
          <i class="bi bi-check-circle-fill"></i>
          <div>
            <strong>Order ${core.escapeHtml(draft.ui.orderNumber)} confirmed</strong>
            <span>Your order is queued for dispatch.</span>
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
            : renderPaymentStep(context);

    root.innerHTML = `
      <div class="checkout-shell">
        <div class="checkout-main">
          <div class="cart-step-row">
            ${renderStepPill(1, draft.step, "Summary")}
            ${renderStepPill(2, draft.step, "Address")}
            ${renderStepPill(3, draft.step, "Payment")}
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
