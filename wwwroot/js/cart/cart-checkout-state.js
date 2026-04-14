(() => {
  const DRAFT_KEY = "safemed-checkout-draft";
  const getStorageKey = () => {
    const scope = window.SafeMedCartCore?.accountScope || "guest";
    return `${DRAFT_KEY}:${scope}`;
  };

  const getDefaultDraft = () => ({
    step: 1,
    address: {
      fullName: document.body.dataset.userName || "",
      phoneNumber: document.body.dataset.userPhone || "",
      deliveryAddress: "",
      landmark: "",
      addressType: "Home",
      saveAddress: document.body.dataset.authenticated === "true",
      latitude: null,
      longitude: null,
      distanceKm: null,
      coverageStatus: "unselected",
      coverageLabel: "",
      deliveryFee: 0,
    },
    shipping: {
      option: "Standard",
    },
    payment: {
      method: "CashOnDelivery",
    },
    ui: {
      message: "",
      tone: "",
      busy: false,
      orderNumber: "",
    },
  });

  const readDraft = () => {
    try {
      const raw = window.localStorage.getItem(getStorageKey());
      const parsed = raw ? JSON.parse(raw) : null;
      const normalizedPaymentMethod =
        parsed?.payment?.method === "EWallet" ? "GCash" : parsed?.payment?.method;

      return parsed
        ? {
            ...getDefaultDraft(),
            ...parsed,
            address: { ...getDefaultDraft().address, ...parsed.address },
            shipping: { ...getDefaultDraft().shipping, ...parsed.shipping },
            payment: { ...getDefaultDraft().payment, ...parsed.payment, method: normalizedPaymentMethod || getDefaultDraft().payment.method },
            ui: { ...getDefaultDraft().ui, ...parsed.ui },
          }
        : getDefaultDraft();
    } catch {
      return getDefaultDraft();
    }
  };

  const writeDraft = (draft) => {
    window.localStorage.setItem(getStorageKey(), JSON.stringify(draft));
  };

  const clearDraft = () => {
    window.localStorage.removeItem(getStorageKey());
  };

  const resetDraftForNewOrder = () => {
    const current = readDraft();
    const nextDraft = {
      ...getDefaultDraft(),
      address: { ...getDefaultDraft().address, ...current.address },
      shipping: { ...getDefaultDraft().shipping, ...current.shipping },
      payment: { ...getDefaultDraft().payment, ...current.payment },
      ui: { ...getDefaultDraft().ui },
    };

    writeDraft(nextDraft);
    return nextDraft;
  };

  window.SafeMedCheckoutState = {
    getDefaultDraft,
    readDraft,
    writeDraft,
    clearDraft,
    resetDraftForNewOrder,
  };
})();
