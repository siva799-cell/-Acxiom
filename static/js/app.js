document.addEventListener("DOMContentLoaded", () => {
  const sidebar = document.getElementById("crm-sidebar");
  if (!sidebar) return;

  sidebar.querySelectorAll("a.sidebar-link").forEach((link) => {
    link.addEventListener("click", () => {
      if (window.matchMedia("(max-width: 991.98px)").matches) {
        bootstrap.Offcanvas.getInstance(sidebar)?.hide();
      }
    });
  });
});