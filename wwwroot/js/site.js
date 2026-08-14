document.addEventListener("click", (event) => {
  if (document.body.classList.contains("menu-open") && !event.target.closest(".sidebar") && !event.target.closest(".menu-button")) {
    document.body.classList.remove("menu-open");
  }
});

document.addEventListener("submit", (event) => {
  const submitter = event.submitter;
  if (!(submitter instanceof HTMLElement) || !submitter.classList.contains("action-delete")) return;
  if (!window.confirm("Bu işlem geri alınamaz. Kaydı silmek istediğinizden emin misiniz?"))
    event.preventDefault();
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

document.querySelectorAll('[data-grid-editor]').forEach((editor) => {
  const body = editor.querySelector('[data-grid-rows]');
  const template = editor.querySelector('[data-grid-template]');
  const addButton = editor.querySelector('[data-grid-add]');
  if (!body || !template || !addButton) return;

  const rows = () => [...body.querySelectorAll('[data-grid-row]')];
  const reindex = () => {
    rows().forEach((row, index) => {
      const number = row.querySelector('.row-number');
      if (number) number.textContent = String(index + 1);
      row.querySelectorAll('[name]').forEach((field) => {
        field.name = field.name.replace(/Satirlar\[\d+\]/, `Satirlar[${index}]`);
        if (field.id) field.id = field.id.replace(/Satirlar_\d+__/, `Satirlar_${index}__`);
      });
    });
  };
  const updateTotals = () => {
    editor.querySelectorAll('[data-sum-column]').forEach((cell) => {
      const column = cell.dataset.sumColumn;
      const total = rows().reduce((sum, row) => {
        const input = [...row.querySelectorAll('input')].find((x) => x.name.endsWith(`.${column}`));
        return sum + (Number.parseFloat(input?.value || '0') || 0);
      }, 0);
      cell.textContent = total.toLocaleString('tr-TR', { maximumFractionDigits: 3 });
    });
  };
  const addRow = () => {
    const index = rows().length;
    body.insertAdjacentHTML('beforeend', template.innerHTML
      .replaceAll('__index__', String(index))
      .replaceAll('__number__', String(index + 1)));
    reindex();
    updateTotals();
    rows().at(-1)?.querySelector('input,select')?.focus();
  };

  addButton.addEventListener('click', addRow);
  body.addEventListener('click', (event) => {
    const button = event.target.closest('[data-grid-remove]');
    if (!button) return;
    const currentRows = rows();
    if (currentRows.length === 1) {
      currentRows[0].querySelectorAll('input').forEach((input) => input.value = '');
      currentRows[0].querySelectorAll('select').forEach((select) => select.selectedIndex = 0);
    } else {
      button.closest('[data-grid-row]')?.remove();
      reindex();
    }
    updateTotals();
  });
  body.addEventListener('input', updateTotals);
  body.addEventListener('keydown', (event) => {
    if (event.key !== 'Enter' || event.target.tagName === 'TEXTAREA') return;
    event.preventDefault();
    const activeRow = event.target.closest('[data-grid-row]');
    if (activeRow === rows().at(-1)) {
      addRow();
      return;
    }
    const fields = [...body.querySelectorAll('input,select')];
    const current = fields.indexOf(event.target);
    fields[current + 1]?.focus();
  });
  reindex();
  updateTotals();
});

document.querySelectorAll('form input[data-val-required], form select[data-val-required]').forEach((field) => {
  field.required = true;
});

document.addEventListener('invalid', (event) => {
  const field = event.target;
  if (!(field instanceof HTMLInputElement || field instanceof HTMLSelectElement)) return;
  if (field.validity.valueMissing) field.setCustomValidity('Bu alan bo\u015F ge\u00E7ilemez.');
}, true);

document.addEventListener('input', (event) => {
  const field = event.target;
  if (field instanceof HTMLInputElement || field instanceof HTMLSelectElement)
    field.setCustomValidity('');
});

document.addEventListener('change', (event) => {
  const field = event.target;
  if (field instanceof HTMLInputElement || field instanceof HTMLSelectElement)
    field.setCustomValidity('');
});
