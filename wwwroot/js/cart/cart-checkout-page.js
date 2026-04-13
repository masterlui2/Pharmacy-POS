(() => {
  const page = document.querySelector("[data-cart-page]");
  if (!page) {
    return;
  }

  const core = window.SafeMedCartCore;
  const stateStore = window.SafeMedCheckoutState;
  const renderer = window.SafeMedCheckoutRenderer;
  const root = page.querySelector("[data-cart-root]");
  const antiForgeryInput = document.querySelector("[data-cart-antiforgery]");
  const placeOrderUrl = page.dataset.placeOrderUrl || "/checkout/place-order";

  if (!core || !stateStore || !renderer || !root) {
    return;
  }

  let draft = stateStore.readDraft();

  const persistDraft = () => {
    stateStore.writeDraft(draft);
  };

  const renderPage = () => {
    draft = stateStore.readDraft();
    renderer.render(root, {
      core,
      cart: core.readCart(),
      uploads: core.readRxUploads(),
      draft,
    });
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

  const updateDraftField = (path, value) => {
    const [section, key] = path.split(".");
    draft[section][key] = value;
    clearMessage();
    persistDraft();
  };

  const changeStep = (nextStep) => {
    if (nextStep > draft.step + 1) {
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
      draft.ui.message = "Order placed successfully.";
      draft.ui.tone = "success";
      persistDraft();

      core.writeCart([]);
      core.writePromo(null);
      core.writeRxUploads({ files: [], submitted: false });
      core.syncBagCount([]);
      renderPage();
      stateStore.clearDraft();
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
        setMessage("Prescription marked as valid for checkout.", "success");
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
  document.addEventListener("DOMContentLoaded", renderPage);
})();
