(() => {
  const DRAFT_KEY = "safemed-checkout-draft";

  const getDefaultDraft = () => ({
    step: 1,
    address: {
      fullName: document.body.dataset.userName || "",
      phoneNumber: document.body.dataset.userPhone || "",
      deliveryAddress: "",
      landmark: "",
      addressType: "Home",
      saveAddress: document.body.dataset.authenticated === "true",
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
      const raw = window.localStorage.getItem(DRAFT_KEY);
      const parsed = raw ? JSON.parse(raw) : null;

      return parsed
        ? {
            ...getDefaultDraft(),
            ...parsed,
            address: { ...getDefaultDraft().address, ...parsed.address },
            shipping: { ...getDefaultDraft().shipping, ...parsed.shipping },
            payment: { ...getDefaultDraft().payment, ...parsed.payment },
            ui: { ...getDefaultDraft().ui, ...parsed.ui },
          }
        : getDefaultDraft();
    } catch {
      return getDefaultDraft();
    }
  };

  const writeDraft = (draft) => {
    window.localStorage.setItem(DRAFT_KEY, JSON.stringify(draft));
  };

  const clearDraft = () => {
    window.localStorage.removeItem(DRAFT_KEY);
  };

  window.SafeMedCheckoutState = {
    getDefaultDraft,
    readDraft,
    writeDraft,
    clearDraft,
  };
})();
