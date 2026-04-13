(() => {
  const page = document.querySelector("[data-orders-page]");
  if (!page) {
    return;
  }

  const shouldClearCart = page.dataset.clearCartOnLoad === "true";
  if (!shouldClearCart) {
    return;
  }

  const core = window.SafeMedCartCore;
  if (core) {
    core.writeCart([]);
    core.writePromo(null);
    core.writeRxUploads({ files: [], submitted: false });
    core.syncBagCount([]);

    const draftKey = `safemed-checkout-draft:${core.accountScope || "guest"}`;
    window.localStorage.removeItem(draftKey);
  }

  window.history.replaceState({}, document.title, window.location.pathname);
})();
