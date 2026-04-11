document.addEventListener("DOMContentLoaded", () => {
  document.querySelectorAll("[data-password-toggle]").forEach((button) => {
    button.addEventListener("click", () => {
      const wrap = button.closest(".auth-password-wrap");
      const input = wrap?.querySelector("[data-password-input]");
      const icon = button.querySelector("i");

      if (!(input instanceof HTMLInputElement) || !icon) {
        return;
      }

      const reveal = input.type === "password";
      input.type = reveal ? "text" : "password";
      icon.classList.toggle("bi-eye", !reveal);
      icon.classList.toggle("bi-eye-slash", reveal);
    });
  });

  const registerPasswordInput = document.querySelector('input[name="Password"][data-password-input]');
  const passwordGuide = document.querySelector("[data-password-guide]");

  if (registerPasswordInput instanceof HTMLInputElement && passwordGuide instanceof HTMLElement) {
    const rules = {
      letter: (value) => /[A-Za-z]/.test(value),
      uppercase: (value) => /[A-Z]/.test(value),
      number: (value) => /\d/.test(value),
      special: (value) => /[^A-Za-z0-9]/.test(value),
      length: (value) => value.length >= 6,
    };

    const updatePasswordGuide = () => {
      const value = registerPasswordInput.value;

      Object.entries(rules).forEach(([ruleName, test]) => {
        const item = passwordGuide.querySelector(`[data-password-rule="${ruleName}"]`);
        if (!(item instanceof HTMLElement)) {
          return;
        }

        item.classList.toggle("is-valid", test(value));
      });
    };

    registerPasswordInput.addEventListener("focus", () => {
      passwordGuide.hidden = false;
      updatePasswordGuide();
    });

    registerPasswordInput.addEventListener("input", updatePasswordGuide);

    registerPasswordInput.addEventListener("blur", () => {
      window.setTimeout(() => {
        const activeElement = document.activeElement;
        if (activeElement !== registerPasswordInput && !passwordGuide.contains(activeElement)) {
          passwordGuide.hidden = registerPasswordInput.value.length === 0;
        }
      }, 120);
    });

    updatePasswordGuide();
  }

  document.querySelectorAll("[data-consent-checkbox]").forEach((checkbox) => {
    checkbox.addEventListener("change", () => {
      if (!(checkbox instanceof HTMLInputElement)) {
        return;
      }

      const validation = document.querySelector(`[data-consent-validation="${checkbox.name}"]`);
      if (checkbox.checked && validation instanceof HTMLElement) {
        validation.textContent = "";
      }
    });
  });
});
