document.addEventListener("DOMContentLoaded", () => {
  initializeStorefrontNavigation();
  initializeHeroSlider();
});

function initializeStorefrontNavigation() {
  const body = document.body;
  const panel = document.querySelector("[data-storefront-nav-panel]");
  const drawer = document.querySelector("[data-storefront-nav-drawer]");
  const toggle = document.querySelector("[data-storefront-nav-toggle]");
  const closeButtons = Array.from(
    document.querySelectorAll("[data-storefront-nav-close]"),
  );
  const desktopQuery = window.matchMedia("(min-width: 993px)");
  let isOpen = false;

  if (!panel || !drawer || !toggle) {
    return;
  }

  const applyState = () => {
    const isDesktop = desktopQuery.matches;
    body.classList.toggle("storefront-nav-open", !isDesktop && isOpen);
    toggle.setAttribute("aria-expanded", String(!isDesktop && isOpen));
    panel.setAttribute("aria-hidden", String(isDesktop || !isOpen));
  };

  const setOpen = (nextState) => {
    isOpen = nextState;
    applyState();
  };

  toggle.addEventListener("click", () => {
    setOpen(!isOpen);
  });

  closeButtons.forEach((button) => {
    button.addEventListener("click", () => {
      setOpen(false);
    });
  });

  drawer.addEventListener("click", (event) => {
    const target = event.target;
    if (!(target instanceof HTMLElement)) {
      return;
    }

    if (target.closest("a")) {
      setOpen(false);
    }
  });

  document.addEventListener("keydown", (event) => {
    if (event.key === "Escape") {
      setOpen(false);
    }
  });

  const handleViewportChange = (event) => {
    if (event.matches) {
      isOpen = false;
    }

    applyState();
  };

  if (typeof desktopQuery.addEventListener === "function") {
    desktopQuery.addEventListener("change", handleViewportChange);
  } else {
    desktopQuery.addListener(handleViewportChange);
  }

  applyState();
}

function initializeHeroSlider() {
  const slider = document.querySelector("[data-hero-slider]");
  if (!slider) {
    return;
  }

  const slides = Array.from(slider.querySelectorAll("[data-hero-slide]"));
  const dots = Array.from(slider.querySelectorAll("[data-hero-dot]"));
  const prevButton = slider.querySelector("[data-hero-prev]");
  const nextButton = slider.querySelector("[data-hero-next]");

  if (slides.length <= 1) {
    return;
  }

  let activeIndex = slides.findIndex((slide) =>
    slide.classList.contains("homepage-hero--active"),
  );
  if (activeIndex < 0) {
    activeIndex = 0;
  }

  let autoplayHandle;

  const render = (nextIndex) => {
    activeIndex = (nextIndex + slides.length) % slides.length;

    slides.forEach((slide, index) => {
      const isActive = index === activeIndex;
      slide.classList.toggle("homepage-hero--active", isActive);
      slide.setAttribute("aria-hidden", String(!isActive));
    });

    dots.forEach((dot, index) => {
      dot.setAttribute("aria-selected", String(index === activeIndex));
    });
  };

  const startAutoplay = () => {
    window.clearInterval(autoplayHandle);
    autoplayHandle = window.setInterval(() => {
      render(activeIndex + 1);
    }, 6000);
  };

  prevButton?.addEventListener("click", () => {
    render(activeIndex - 1);
    startAutoplay();
  });

  nextButton?.addEventListener("click", () => {
    render(activeIndex + 1);
    startAutoplay();
  });

  dots.forEach((dot, index) => {
    dot.addEventListener("click", () => {
      render(index);
      startAutoplay();
    });
  });

  slider.addEventListener("mouseenter", () => {
    window.clearInterval(autoplayHandle);
  });

  slider.addEventListener("mouseleave", startAutoplay);

  render(activeIndex);
  startAutoplay();
}
