document.addEventListener("DOMContentLoaded", () => {
  const body = document.body;
  const sidebar = document.querySelector("[data-admin-sidebar]");
  const toggleButtons = Array.from(
    document.querySelectorAll("[data-admin-sidebar-toggle]"),
  );
  const closeButtons = Array.from(
    document.querySelectorAll("[data-admin-sidebar-close]"),
  );
  const desktopQuery = window.matchMedia("(min-width: 1025px)");
  let isOpen = false;

  const applySidebarState = () => {
    const isDesktop = desktopQuery.matches;
    body.classList.toggle("admin-nav-open", !isDesktop && isOpen);

    toggleButtons.forEach((button) => {
      button.setAttribute("aria-expanded", String(!isDesktop && isOpen));
    });

    if (sidebar) {
      sidebar.setAttribute("aria-hidden", String(!isDesktop && !isOpen));
    }
  };

  const setSidebarOpen = (nextState) => {
    isOpen = nextState;
    applySidebarState();
  };

  closeButtons.forEach((button) => {
    button.addEventListener("click", () => {
      setSidebarOpen(false);
    });
  });

  toggleButtons.forEach((button) => {
    button.addEventListener("click", () => {
      setSidebarOpen(!isOpen);
    });
  });

  document.addEventListener("keydown", (event) => {
    if (event.key === "Escape") {
      setSidebarOpen(false);
    }
  });

  const handleViewportChange = (event) => {
    if (event.matches) {
      isOpen = false;
    }

    applySidebarState();
  };

  if (typeof desktopQuery.addEventListener === "function") {
    desktopQuery.addEventListener("change", handleViewportChange);
  } else {
    desktopQuery.addListener(handleViewportChange);
  }

  if (window.bootstrap?.Tooltip) {
    document.querySelectorAll("[data-bs-toggle='tooltip']").forEach((element) => {
      new bootstrap.Tooltip(element);
    });
  }

  applySidebarState();
});
