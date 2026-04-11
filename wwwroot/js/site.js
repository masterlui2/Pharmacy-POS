document.addEventListener("DOMContentLoaded", () => {
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

  let activeIndex = slides.findIndex((slide) => slide.classList.contains("homepage-hero--active"));
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
});
