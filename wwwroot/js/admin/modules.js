document.addEventListener("DOMContentLoaded", () => {
  initializePosSaleModal();
  initializePaymentStatusModal();
  initializePreviewModal(
    "receiptPreviewModal",
    "[data-receipt-preview-content]",
    "Receipt preview could not be loaded.",
  );
  initializePreviewModal(
    "prescriptionPreviewModal",
    "[data-prescription-preview-content]",
    "Prescription preview could not be loaded.",
  );
});

function initializePosSaleModal() {
  const saleModal = document.getElementById("posSaleModal");
  if (!saleModal) {
    return;
  }

  saleModal.addEventListener("show.bs.modal", (event) => {
    const trigger = event.relatedTarget;
    if (!(trigger instanceof HTMLElement)) {
      return;
    }

    saleModal.querySelector("[data-pos-title]").textContent =
      trigger.getAttribute("data-medicine-name") || "Medicine";
    saleModal.querySelector("[data-pos-caption]").textContent =
      trigger.getAttribute("data-medicine-caption") || "";
    saleModal.querySelector("input[name='MedicineId']").value =
      trigger.getAttribute("data-medicine-id") || "";
    saleModal.querySelector("[data-pos-price]").textContent =
      trigger.getAttribute("data-medicine-price") || "PHP 0.00";
    saleModal.querySelector("[data-pos-stock]").textContent =
      trigger.getAttribute("data-medicine-stock") || "0";

    const quantityInput = saleModal.querySelector("input[name='Quantity']");
    const maxStock = trigger.getAttribute("data-medicine-stock") || "1";
    const requiresPrescription =
      trigger.getAttribute("data-medicine-rx") === "true";

    quantityInput.max = maxStock;
    quantityInput.value = "1";

    saleModal.querySelector("input[name='DiscountPercent']").value = "0";
    saleModal.querySelector("input[name='CustomerName']").value =
      "Walk-in Customer";
    saleModal.querySelector("input[name='PhoneNumber']").value = "";
    saleModal.querySelector("select[name='PaymentMethod']").value = "Cash";

    const rxAlert = saleModal.querySelector("[data-pos-rx-alert]");
    const rxField = saleModal.querySelector("[data-pos-rx-field]");
    const rxCheckbox = saleModal.querySelector("[data-pos-rx-checkbox]");
    const rxHidden = saleModal.querySelector("[data-pos-rx-hidden]");

    rxCheckbox.checked = false;
    rxHidden.disabled = requiresPrescription;
    rxHidden.value = requiresPrescription ? "false" : "true";
    rxAlert.classList.toggle("d-none", !requiresPrescription);
    rxField.classList.toggle("d-none", !requiresPrescription);
  });
}

function initializePaymentStatusModal() {
  const statusModal = document.getElementById("paymentStatusModal");
  if (!statusModal) {
    return;
  }

  statusModal.addEventListener("show.bs.modal", (event) => {
    const trigger = event.relatedTarget;
    if (!(trigger instanceof HTMLElement)) {
      return;
    }

    const paymentIdInput = statusModal.querySelector("[data-payment-status-id]");
    const statusSelect = statusModal.querySelector("[data-payment-status-select]");
    const caption = statusModal.querySelector("[data-payment-status-caption]");

    if (paymentIdInput instanceof HTMLInputElement) {
      paymentIdInput.value = trigger.getAttribute("data-payment-id") || "";
    }

    if (statusSelect instanceof HTMLSelectElement) {
      statusSelect.value = trigger.getAttribute("data-payment-status") || "Paid";
    }

    if (caption) {
      const orderNumber = trigger.getAttribute("data-payment-order") || "selected order";
      const customerName = trigger.getAttribute("data-payment-customer") || "customer";
      caption.textContent = `${orderNumber} · ${customerName}`;
    }
  });
}

function initializePreviewModal(modalId, contentSelector, errorText) {
  const previewModal = document.getElementById(modalId);
  if (!previewModal) {
    return;
  }

  const previewContent = previewModal.querySelector(contentSelector);
  if (!previewContent) {
    return;
  }

  const loadingMarkup = previewContent.innerHTML;

  previewModal.addEventListener("show.bs.modal", async (event) => {
    const trigger = event.relatedTarget;
    if (!(trigger instanceof HTMLElement)) {
      return;
    }

    const previewUrl = trigger.getAttribute("data-preview-url");
    if (!previewUrl) {
      return;
    }

    previewContent.innerHTML = loadingMarkup;

    try {
      const response = await window.fetch(previewUrl, {
        headers: {
          "X-Requested-With": "XMLHttpRequest",
        },
      });

      if (!response.ok) {
        throw new Error("Preview request failed.");
      }

      previewContent.innerHTML = await response.text();
    } catch {
      previewContent.innerHTML = `<div class="admin-empty-state">${errorText}</div>`;
    }
  });
}
