document.addEventListener("click", (event) => {
  if (document.body.classList.contains("menu-open") && !event.target.closest(".sidebar") && !event.target.closest(".menu-button")) {
    document.body.classList.remove("menu-open");
  }
});

document.querySelectorAll('.entry-form').forEach((form) => {
  const party = form.querySelector('[name="Input.PartiNo"]');
  const date = form.querySelector('[name="Input.SoymaBaslangici"]');
  if (!party || !date) return;
  const sync = () => {
    if (!date.value || party.dataset.persisted === 'true') return;
    const selected = new Date(`${date.value.slice(0, 10)}T12:00:00`);
    const isoDate = new Date(Date.UTC(selected.getFullYear(), selected.getMonth(), selected.getDate()));
    isoDate.setUTCDate(isoDate.getUTCDate() + 4 - (isoDate.getUTCDay() || 7));
    const isoYear = isoDate.getUTCFullYear();
    const yearStart = new Date(Date.UTC(isoYear, 0, 1));
    const week = Math.ceil((((isoDate - yearStart) / 86400000) + 1) / 7);
    party.value = `${String(isoYear).slice(-2)}${String(week).padStart(2, '0')}01`;
  };
  date.addEventListener('change', sync);
  sync();
});
