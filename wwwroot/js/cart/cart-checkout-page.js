(() => {
  const page = document.querySelector("[data-cart-page]");
  if (!page) {
    return;
  }

  const core = window.SafeMedCartCore;
  const stateStore = window.SafeMedCheckoutState;
  const renderer = window.SafeMedCheckoutRenderer;
  const mapController = window.SafeMedCheckoutMap;
  const root = page.querySelector("[data-cart-root]");
  const antiForgeryInput = document.querySelector("[data-cart-antiforgery]");
  const placeOrderUrl = page.dataset.placeOrderUrl || "/checkout/place-order";
  const deliverySettings = {
    apiKey: page.dataset.mapsApiKey || "",
    branchName: page.dataset.branchName || "SafeMed Davao Dispatch",
    branchAddress: page.dataset.branchAddress || "Davao City, Philippines",
    branchLatitude: Number.parseFloat(page.dataset.branchLatitude || "7.073056"),
    branchLongitude: Number.parseFloat(page.dataset.branchLongitude || "125.612778"),
    baseDistanceKm: Number.parseFloat(page.dataset.baseDistanceKm || "3"),
    maxRadiusKm: Number.parseFloat(page.dataset.maxRadiusKm || "18"),
    baseFee: Number.parseFloat(page.dataset.baseFee || "59"),
    perKmFee: Number.parseFloat(page.dataset.perKmFee || "12"),
  };

  if (!core || !stateStore || !renderer || !root) {
    return;
  }

  let draft = stateStore.readDraft();

  const normalizeDraftForCurrentCart = () => {
    const cart = core.readCart();
    if (cart.length > 0 && draft.ui.orderNumber) {
      draft = stateStore.resetDraftForNewOrder();
    }
  };

  const syncQueryFeedback = () => {
    const params = new URLSearchParams(window.location.search);
    const paymentState = params.get("payment");
    const orderNumber = params.get("order");
    if (!paymentState) {
      return;
    }

    if (paymentState === "success") {
      draft.ui.message = orderNumber
        ? `Payment completed for order ${orderNumber}.`
        : "Payment completed successfully.";
      draft.ui.tone = "success";
    }

    if (paymentState === "pending") {
      draft.ui.message = orderNumber
        ? `Payment session opened for order ${orderNumber}. Complete payment in PayMongo to finish checkout.`
        : "Payment session opened. Complete payment in PayMongo.";
      draft.ui.tone = "info";
    }

    if (paymentState === "cancelled") {
      draft.ui.message = "PayMongo checkout was cancelled. You can choose another payment method or try again.";
      draft.ui.tone = "warning";
    }

    persistDraft();
    window.history.replaceState({}, document.title, window.location.pathname);
  };

  const clearCheckoutState = () => {
    core.writeCart([]);
    core.writePromo(null);
    core.writeRxUploads({ files: [], submitted: false });
    core.syncBagCount([]);
    stateStore.clearDraft();
  };

  const persistDraft = () => {
    stateStore.writeDraft(draft);
  };

  const setMessage = (message, tone = "info") => {
    draft.ui.message = message;
    draft.ui.tone = tone;
    persistDraft();
  };

  const clearMessage = () => {
    draft.ui.message = "";
    draft.ui.tone = "";
  };

  const syncDeliveryCoverage = () => {
    if (!mapController) {
      return;
    }

    mapController.syncCoverage(draft, deliverySettings);
  };

  const patchAddressLocation = (payload, shouldRender = true) => {
    draft.address.deliveryAddress = payload.deliveryAddress || draft.address.deliveryAddress;
    draft.address.latitude = payload.latitude;
    draft.address.longitude = payload.longitude;
    draft.address.distanceKm = payload.distanceKm;
    draft.address.deliveryFee = payload.deliveryFee;
    draft.address.coverageStatus = payload.coverageStatus;
    draft.address.coverageLabel = payload.coverageLabel;
    clearMessage();
    persistDraft();

    if (shouldRender) {
      renderPage();
    }
  };

  const initializeMapStep = () => {
    if (!mapController) {
      return;
    }

    const mapRoot = page.querySelector("[data-checkout-map]");
    if (!(mapRoot instanceof HTMLElement)) {
      return;
    }

    const searchInput = page.querySelector("[data-map-search]");
    const statusTarget = page.querySelector("[data-map-status]");
    if (!deliverySettings.apiKey) {
      if (statusTarget instanceof HTMLElement) {
        statusTarget.textContent = "Google Maps is not configured yet. Add your API key in appsettings before using map-based delivery.";
      }
      return;
    }

    mapController
      .mount({
        root: mapRoot,
        searchInput: searchInput instanceof HTMLInputElement ? searchInput : null,
        statusTarget: statusTarget instanceof HTMLElement ? statusTarget : null,
        draft,
        settings: deliverySettings,
        onChange: (payload) => patchAddressLocation(payload),
        onError: (message, tone) => {
          setMessage(message, tone);
          if (statusTarget instanceof HTMLElement) {
            statusTarget.textContent = message;
          }
        },
      })
      .catch(() => {
        if (statusTarget instanceof HTMLElement) {
          statusTarget.textContent = "Google Maps failed to load. Check the API key, billing, referrer settings, and enabled APIs.";
        }
      });
  };

  const renderPage = () => {
    draft = stateStore.readDraft();
    normalizeDraftForCurrentCart();
    syncDeliveryCoverage();
    renderer.render(root, {
      core,
      cart: core.readCart(),
      draft,
      deliverySettings,
    });

    if (draft.step === 2) {
      initializeMapStep();
    } else {
      mapController?.teardown();
    }
  };

  const validateAddress = () => {
    if (!draft.address.fullName.trim()) {
      setMessage("Enter the delivery full name.", "danger");
      return false;
    }

    if (!draft.address.phoneNumber.trim()) {
      setMessage("Enter the delivery phone number.", "danger");
      return false;
    }

    if (!draft.address.deliveryAddress.trim()) {
      setMessage("Enter the full delivery address.", "danger");
      return false;
    }

    if (
      typeof draft.address.latitude !== "number" ||
      typeof draft.address.longitude !== "number"
    ) {
      setMessage("Pin the exact delivery location on the map.", "danger");
      return false;
    }

    if (draft.address.coverageStatus !== "covered") {
      setMessage("Delivery is only available for locations within Davao coverage.", "danger");
      return false;
    }

    return true;
  };

  const isPrescriptionBlocked = () => {
    const prescription = renderer.getPrescriptionStatus(
      core.readCart(),
      core.readRxUploads(),
    );

    return (
      prescription.code === "Missing" || prescription.code === "Uploaded"
    );
  };

  const canProceedFromSummary = () => {
    if (!core.isAuthenticated) {
      setMessage("Sign in to continue with checkout.", "danger");
      return false;
    }

    if (isPrescriptionBlocked()) {
      setMessage(
        "Upload and submit the prescription before proceeding.",
        "danger",
      );
      return false;
    }

    return true;
  };

  const updateDraftField = (path, value) => {
    const [section, key] = path.split(".");
    draft[section][key] = value;
    if (section === "shipping" || section === "address") {
      syncDeliveryCoverage();
    }
    clearMessage();
    persistDraft();
  };

  const changeStep = (nextStep) => {
    if (nextStep > draft.step + 1) {
      return;
    }

    if (draft.step === 1 && nextStep > 1 && !canProceedFromSummary()) {
      renderPage();
      return;
    }

    if (nextStep > draft.step && draft.step === 2 && !validateAddress()) {
      renderPage();
      return;
    }

    draft.step = Math.max(1, Math.min(4, nextStep));
    clearMessage();
    persistDraft();
    renderPage();
  };

  const updateCartQuantity = (id, delta) => {
    const cart = core.readCart();
    const item = cart.find((entry) => entry.id === id);
    if (!item) {
      return;
    }

    item.quantity = Math.max(1, item.quantity + delta);
    core.writeCart(cart);
    renderPage();
  };

  const removeItem = (id) => {
    const nextCart = core.readCart().filter((item) => item.id !== id);
    core.writeCart(nextCart);

    if (!nextCart.some((item) => item.requiresPrescription)) {
      core.writeRxUploads({ files: [], submitted: false });
    }

    renderPage();
  };

  const applyPromo = (form) => {
    const input = form.elements.namedItem("promoCode");
    if (!(input instanceof HTMLInputElement)) {
      return;
    }

    const code = input.value.trim().toUpperCase();
    if (!code) {
      core.writePromo(null);
      renderPage();
      return;
    }

    const rate = core.getPromoRate(code);
    if (!rate) {
      setMessage("Promo code not recognized.", "warning");
      renderPage();
      return;
    }

    core.writePromo({ code, rate });
    setMessage(`Promo code ${code} applied.`, "success");
    renderPage();
  };

  const buildPayload = () => {
    const uploads = core.readRxUploads();
    return {
      fullName: draft.address.fullName.trim(),
      phoneNumber: draft.address.phoneNumber.trim(),
      deliveryAddress: draft.address.deliveryAddress.trim(),
      landmark: draft.address.landmark.trim(),
      addressType: draft.address.addressType,
      saveAddress: Boolean(draft.address.saveAddress),
      latitude: draft.address.latitude,
      longitude: draft.address.longitude,
      distanceKm: draft.address.distanceKm,
      deliveryOption: draft.shipping.option,
      paymentMethod: draft.payment.method,
      prescriptionStatus: renderer.getPrescriptionStatus(core.readCart(), uploads).code,
      promoCode: core.readPromo()?.code || "",
      prescriptionFiles: uploads.files,
      items: core.readCart().map((item) => ({
        productId: item.id,
        name: item.name,
        brand: item.brand,
        image: item.image,
        price: item.price,
        tax: item.tax,
        quantity: item.quantity,
        requiresPrescription: item.requiresPrescription,
      })),
    };
  };

  const placeOrder = async () => {
    if (!validateAddress()) {
      renderPage();
      return;
    }

    if (isPrescriptionBlocked()) {
      setMessage(
        "Prescription items must be uploaded and marked valid before checkout.",
        "danger",
      );
      renderPage();
      return;
    }

    draft.ui.busy = true;
    clearMessage();
    persistDraft();
    renderPage();

    try {
      const response = await window.fetch(placeOrderUrl, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          RequestVerificationToken: antiForgeryInput?.value || "",
        },
        body: JSON.stringify(buildPayload()),
      });

      const result = await response.json();
      if (!response.ok) {
        draft.ui.busy = false;
        draft.ui.message = result?.message || "Unable to place order.";
        draft.ui.tone = "danger";
        persistDraft();
        renderPage();
        return;
      }

      draft.ui.busy = false;
      draft.ui.orderNumber = result.orderNumber || "";
      draft.ui.message = result.message || "Order placed successfully.";
      draft.ui.tone = result.checkoutUrl ? "info" : "success";
      persistDraft();

      if (result.checkoutUrl) {
        window.location.href = result.checkoutUrl;
        return;
      }

      clearCheckoutState();
      const separator = core.myOrdersUrl.includes("?") ? "&" : "?";
      window.location.href = `${core.myOrdersUrl}${separator}order=${encodeURIComponent(result.orderNumber || "")}&payment=placed`;
    } catch {
      draft.ui.busy = false;
      draft.ui.message = "Unable to connect to checkout right now.";
      draft.ui.tone = "danger";
      persistDraft();
      renderPage();
    }
  };

  page.addEventListener("click", (event) => {
    const target = event.target;
    if (!(target instanceof HTMLElement)) {
      return;
    }

    const increase = target.closest("[data-cart-increase]");
    if (increase instanceof HTMLElement) {
      updateCartQuantity(increase.dataset.cartIncrease || "", 1);
      return;
    }

    const decrease = target.closest("[data-cart-decrease]");
    if (decrease instanceof HTMLElement) {
      updateCartQuantity(decrease.dataset.cartDecrease || "", -1);
      return;
    }

    const remove = target.closest("[data-cart-remove]");
    if (remove instanceof HTMLElement) {
      removeItem(remove.dataset.cartRemove || "");
      return;
    }

    const stepGo = target.closest("[data-step-go]");
    if (stepGo instanceof HTMLElement) {
      changeStep(Number(stepGo.dataset.stepGo || draft.step));
      return;
    }

    if (target.closest("[data-step-next]")) {
      changeStep(draft.step + 1);
      return;
    }

    if (target.closest("[data-step-prev]")) {
      changeStep(draft.step - 1);
      return;
    }

    if (target.closest("[data-rx-trigger]")) {
      page.querySelector("[data-rx-input]")?.click();
      return;
    }

    const rxRemove = target.closest("[data-rx-remove]");
    if (rxRemove instanceof HTMLElement) {
      const uploads = core.readRxUploads();
      const index = Number.parseInt(rxRemove.dataset.rxRemove || "-1", 10);
      if (index >= 0) {
        uploads.files.splice(index, 1);
        uploads.submitted = false;
        core.writeRxUploads(uploads);
        renderPage();
      }
      return;
    }

    if (target.closest("[data-rx-validate]")) {
      const uploads = core.readRxUploads();
      if (uploads.files.length > 0) {
        uploads.submitted = true;
        core.writeRxUploads(uploads);
        setMessage("Prescription submitted and marked valid for checkout.", "success");
        renderPage();
      }
      return;
    }

    const addressType = target.closest("[data-address-type]");
    if (addressType instanceof HTMLElement) {
      draft.address.addressType = addressType.dataset.addressType || "Home";
      persistDraft();
      renderPage();
      return;
    }

    const shippingOption = target.closest("[data-shipping-option]");
    if (shippingOption instanceof HTMLElement) {
      draft.shipping.option = shippingOption.dataset.shippingOption || "Standard";
      syncDeliveryCoverage();
      persistDraft();
      renderPage();
      return;
    }

    const paymentMethod = target.closest("[data-payment-method]");
    if (paymentMethod instanceof HTMLElement) {
      draft.payment.method = paymentMethod.dataset.paymentMethod || "CashOnDelivery";
      persistDraft();
      renderPage();
      return;
    }

    if (target.closest("[data-place-order]")) {
      placeOrder();
      return;
    }

    if (target.closest("[data-map-locate]")) {
      mapController?.locateUser(
        (location) => {
          const payload = {
            deliveryAddress: draft.address.deliveryAddress,
            latitude: location.lat,
            longitude: location.lng,
            distanceKm: draft.address.distanceKm,
            deliveryFee: draft.address.deliveryFee,
            coverageStatus: draft.address.coverageStatus,
            coverageLabel: draft.address.coverageLabel,
          };
          patchAddressLocation(payload);
        },
        (message, tone) => {
          setMessage(message, tone);
          renderPage();
        },
      );
    }
  });

  page.addEventListener("change", (event) => {
    const target = event.target;

    if (target instanceof HTMLInputElement && target.matches("[data-rx-input]")) {
      const files = Array.from(target.files || [])
        .filter((file) => file.size <= 10 * 1024 * 1024)
        .map((file) => file.name);

      if (files.length > 0) {
        const uploads = core.readRxUploads();
        core.writeRxUploads({
          files: [...uploads.files, ...files],
          submitted: false,
        });
        setMessage("Prescription uploaded.", "warning");
        renderPage();
      }

      return;
    }

    if (target instanceof HTMLInputElement && target.matches("[data-draft-checkbox]")) {
      updateDraftField(target.dataset.draftCheckbox || "", target.checked);
      return;
    }

    if (
      (target instanceof HTMLInputElement || target instanceof HTMLTextAreaElement) &&
      target.matches("[data-draft-field]")
    ) {
      updateDraftField(target.dataset.draftField || "", target.value);
    }
  });

  page.addEventListener("submit", (event) => {
    const form = event.target;
    if (!(form instanceof HTMLFormElement)) {
      return;
    }

    if (form.matches("[data-cart-promo-form]")) {
      event.preventDefault();
      applyPromo(form);
    }
  });

  window.addEventListener("storage", renderPage);
  document.addEventListener("DOMContentLoaded", () => {
    syncQueryFeedback();
    normalizeDraftForCurrentCart();
    renderPage();
  });
})();
