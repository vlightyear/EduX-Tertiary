document.addEventListener('DOMContentLoaded', function () {
    // ── Modal elements ────────────────────────────────────────────────────────
    const createModal = document.getElementById('createAcademicYearModal');
    const updateModal = document.getElementById('updateAcademicYearModal');
    const deleteModal = document.getElementById('deleteAcademicYearModal');

    // ── Form elements ─────────────────────────────────────────────────────────
    const createForm = document.getElementById('createAcademicYearForm');
    const updateForm = document.getElementById('updateAcademicYearForm');
    const deleteForm = document.getElementById('deleteAcademicYearForm');

    // ── Table elements ────────────────────────────────────────────────────────
    const academicYearTable = document.getElementById('academicYearTable');
    const tableBody = academicYearTable?.querySelector('tbody');
    const yearSearch = document.getElementById('yearSearch');
    const statusFilter = document.getElementById('statusFilter');
    const typeFilter = document.getElementById('typeFilter');

    // ── Pagination elements ───────────────────────────────────────────────────
    const paginationButtons = document.getElementById('paginationButtons');
    const prevPageBtn = document.getElementById('prevPage');
    const nextPageBtn = document.getElementById('nextPage');
    const startRowElem = document.getElementById('startRow');
    const endRowElem = document.getElementById('endRow');
    const totalRowsElem = document.getElementById('totalRows');

    // ── Table state ───────────────────────────────────────────────────────────
    let currentPage = 1;
    const rowsPerPage = 10;
    let originalRows = [];
    let filteredRows = [];
    let sortColumn = 'index';
    let sortDirection = 'asc';

    // ── Toast notification ────────────────────────────────────────────────────
    function showNotification(message, type = 'success') {
        const colours = { success: 'bg-green-500', error: 'bg-red-500' };
        const toast = document.createElement('div');
        toast.className = `fixed top-4 right-4 p-4 rounded-lg text-white ${colours[type]} shadow-lg z-50 transition-opacity duration-300`;
        toast.textContent = message;
        document.body.appendChild(toast);
        setTimeout(() => {
            toast.style.opacity = '0';
            setTimeout(() => toast.remove(), 300);
        }, 3000);
    }

    // ── Modal helpers ─────────────────────────────────────────────────────────
    const modalHandler = {
        show(modal) {
            if (!modal) return;
            modal.classList.remove('hidden');
            document.body.style.overflow = 'hidden';
        },
        hide(modal) {
            if (!modal) return;
            modal.classList.add('hidden');
            document.body.style.overflow = 'auto';
        }
    };

    [createModal, updateModal, deleteModal].forEach(modal => {
        if (!modal) return;
        modal.addEventListener('click', e => { if (e.target === modal) modalHandler.hide(modal); });
        modal.querySelectorAll('[data-bs-dismiss="modal"]').forEach(btn => {
            btn.addEventListener('click', () => modalHandler.hide(modal));
        });
    });

    document.addEventListener('keydown', e => {
        if (e.key === 'Escape') {
            [createModal, updateModal, deleteModal].forEach(m => {
                if (m && !m.classList.contains('hidden')) modalHandler.hide(m);
            });
        }
    });

    // ── Form validation ───────────────────────────────────────────────────────
    // NOTE: Semester/term period dates now live on AcademicYearPeriod (managed
    // via the Manage Periods modal), so we only validate the year-level dates here.
    function validateForm(form) {
        const startInput = form.querySelector('input[name="StartDate"]');
        const endInput = form.querySelector('input[name="EndDate"]');

        if (!startInput?.value || !endInput?.value) {
            showNotification('Start date and end date are required.', 'error');
            return false;
        }

        const startDate = new Date(startInput.value);
        const endDate = new Date(endInput.value);

        if (startDate >= endDate) {
            showNotification('Academic Year Start Date must be earlier than End Date.', 'error');
            return false;
        }

        // Optional: year-level registration window (may or may not exist in the form)
        const regStart = form.querySelector('input[name="RegistrationStartDate"]');
        const regEnd = form.querySelector('input[name="RegistrationEndDate"]');

        if (regStart?.value && regEnd?.value) {
            if (new Date(regStart.value) >= new Date(regEnd.value)) {
                showNotification('Registration Start Date must be earlier than Registration End Date.', 'error');
                return false;
            }
        }

        return form.checkValidity();
    }

    // ── AJAX submit ───────────────────────────────────────────────────────────
    async function submitForm(form) {
        try {
            const formData = new FormData(form);

            const isActiveCheckbox = form.querySelector('input[name="IsActive"]');
            if (isActiveCheckbox) {
                formData.set('IsActive', isActiveCheckbox.checked);
            }

            const response = await fetch(form.action, {
                method: 'POST',
                body: formData,
                headers: {
                    'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]')?.value ?? ''
                }
            });

            if (response.redirected) {
                window.location.href = response.url;
                return;
            }

            const result = await response.json();

            if (result.success) {
                showNotification(result.message || 'Operation completed successfully');
                setTimeout(() => location.reload(), 1000);
            } else {
                showNotification(result.message || 'Operation failed', 'error');
            }
        } catch (error) {
            console.error('Form submission error:', error);
            showNotification('An error occurred while processing your request.', 'error');
        }
    }

    // ── CREATE modal ──────────────────────────────────────────────────────────
    window.showCreateAcademicYearModal = function () {
        createForm?.reset();
        // Default to Annual
        const annualRadio = document.getElementById('annualType');
        if (annualRadio) annualRadio.checked = true;
        modalHandler.show(createModal);
    };

    createForm?.addEventListener('submit', async function (e) {
        e.preventDefault();
        if (validateForm(this)) {
            await submitForm(this);
            modalHandler.hide(createModal);
        }
    });

    // ── UPDATE modal ──────────────────────────────────────────────────────────
    window.showUpdateAcademicYearModal = function (academicYear) {
        // Required fields
        const set = (id, val) => { const el = document.getElementById(id); if (el) el.value = val ?? ''; };
        const check = (id, val) => { const el = document.getElementById(id); if (el) el.checked = !!val; };

        set('updateYearId', academicYear.yearId);
        set('updateYearValue', academicYear.yearValue);
        set('updateStartDate', academicYear.startDate);
        set('updateEndDate', academicYear.endDate);
        set('updateMinRegistrationPayment', academicYear.minRegistrationPaymentPercentage);
        set('updateMinExamPayment', academicYear.minExamPaymentPercentage);
        set('updateRegistrationStartDate', academicYear.registrationStartDate);
        set('updateRegistrationEndDate', academicYear.registrationEndDate);
        set('updateNextAcademicYearId', academicYear.nextAcademicYearId);
        check('updateIsActive', academicYear.isActive === true || academicYear.isActive === 'true');

        // Academic type radio — 0=Annual, 1=Semester, 2=Term
        const typeVal = parseInt(academicYear.academicType) || 0;
        const annualRadio = document.getElementById('updateAnnualType');
        const semesterRadio = document.getElementById('updateSemesterType');
        const termRadio = document.getElementById('updateTermType');
        if (annualRadio) annualRadio.checked = typeVal === 0;
        if (semesterRadio) semesterRadio.checked = typeVal === 1;
        if (termRadio) termRadio.checked = typeVal === 2;

        modalHandler.show(updateModal);
    };

    updateForm?.addEventListener('submit', async function (e) {
        e.preventDefault();
        if (validateForm(this)) {
            await submitForm(this);
            modalHandler.hide(updateModal);
        }
    });

    // ── DELETE modal ──────────────────────────────────────────────────────────
    window.showDeleteAcademicYearModal = function (id, yearValue, academicType) {
        const idEl = document.getElementById('deleteYearId');
        const valEl = document.getElementById('deleteYearValue');
        const typeEl = document.getElementById('deleteAcademicType');
        if (idEl) idEl.value = id;
        if (valEl) valEl.textContent = yearValue;
        if (typeEl) typeEl.textContent = academicType;
        modalHandler.show(deleteModal);
    };

    deleteForm?.addEventListener('submit', async function (e) {
        e.preventDefault();
        await submitForm(this);
        modalHandler.hide(deleteModal);
    });

    // ── Table: sorting ────────────────────────────────────────────────────────
    if (tableBody) {
        originalRows = Array.from(tableBody.querySelectorAll('tr'));
        filteredRows = [...originalRows];

        academicYearTable.querySelectorAll('th[data-sort]').forEach(header => {
            header.addEventListener('click', () => {
                const column = header.getAttribute('data-sort');
                sortDirection = (sortColumn === column && sortDirection === 'asc') ? 'desc' : 'asc';
                sortColumn = column;
                sortTable(column, sortDirection);
                renderTable();
            });
        });

        yearSearch?.addEventListener('input', filterTable);
        statusFilter?.addEventListener('change', filterTable);
        typeFilter?.addEventListener('change', filterTable);

        setupPagination();
        renderTable();
    }

    function sortTable(column, direction) {
        filteredRows.sort((a, b) => {
            let aVal, bVal;
            switch (column) {
                case 'index': aVal = parseInt(a.cells[0]?.textContent); bVal = parseInt(b.cells[0]?.textContent); break;
                case 'year': aVal = a.cells[1]?.textContent; bVal = b.cells[1]?.textContent; break;
                case 'type': aVal = a.cells[2]?.textContent; bVal = b.cells[2]?.textContent; break;
                case 'status': aVal = a.cells[3]?.textContent.includes('Active') ? 1 : 0; bVal = b.cells[3]?.textContent.includes('Active') ? 1 : 0; break;
                case 'nextYear': {
                    aVal = a.cells[5]?.textContent.trim() ?? '';
                    bVal = b.cells[5]?.textContent.trim() ?? '';
                    if (aVal === 'Not set') aVal = 'ZZZZ';
                    if (bVal === 'Not set') bVal = 'ZZZZ';
                    break;
                }
                default: aVal = a.cells[0]?.textContent; bVal = b.cells[0]?.textContent;
            }
            if (direction === 'asc') return aVal > bVal ? 1 : -1;
            if (direction === 'desc') return aVal < bVal ? 1 : -1;
            return 0;
        });
    }

    // ── Table: filtering ──────────────────────────────────────────────────────
    function filterTable() {
        const searchTerm = (yearSearch?.value ?? '').toLowerCase();
        const statusVal = statusFilter?.value ?? 'all';
        const typeVal = typeFilter?.value ?? 'all';

        filteredRows = originalRows.filter(row => {
            const yearText = row.cells[1]?.textContent.toLowerCase() ?? '';
            const typeText = row.cells[2]?.textContent ?? '';
            const isActive = row.cells[3]?.textContent.includes('Active') ?? false;

            const matchesSearch = !searchTerm || yearText.includes(searchTerm);
            const matchesStatus = statusVal === 'all'
                || (statusVal === 'active' && isActive)
                || (statusVal === 'inactive' && !isActive);
            const matchesType = typeVal === 'all' || typeText.includes(typeVal);

            return matchesSearch && matchesStatus && matchesType;
        });

        currentPage = 1;
        setupPagination();
        renderTable();
    }

    // ── Table: pagination ─────────────────────────────────────────────────────
    function setupPagination() {
        if (!paginationButtons) return;
        const totalPages = Math.ceil(filteredRows.length / rowsPerPage);
        if (totalRowsElem) totalRowsElem.textContent = filteredRows.length;

        paginationButtons.innerHTML = '';
        for (let i = 1; i <= totalPages; i++) {
            const btn = document.createElement('button');
            btn.textContent = i;
            btn.className = `px-3 py-1 text-sm ${i === currentPage ? 'bg-primary-100 text-primary-600' : 'text-secondary-600 hover:bg-gray-100'} rounded-md`;
            btn.addEventListener('click', () => { currentPage = i; renderTable(); });
            paginationButtons.appendChild(btn);
        }

        if (prevPageBtn) {
            prevPageBtn.disabled = currentPage === 1;
            prevPageBtn.onclick = () => { if (currentPage > 1) { currentPage--; renderTable(); } };
        }
        if (nextPageBtn) {
            nextPageBtn.disabled = currentPage === totalPages || totalPages === 0;
            nextPageBtn.onclick = () => { if (currentPage < totalPages) { currentPage++; renderTable(); } };
        }
    }

    function renderTable() {
        if (!tableBody) return;
        const startIndex = (currentPage - 1) * rowsPerPage;
        const endIndex = Math.min(startIndex + rowsPerPage, filteredRows.length);

        if (startRowElem) startRowElem.textContent = filteredRows.length > 0 ? startIndex + 1 : 0;
        if (endRowElem) endRowElem.textContent = endIndex;

        tableBody.innerHTML = '';

        if (filteredRows.length === 0) {
            tableBody.innerHTML = `<tr><td colspan="7" class="px-6 py-4 text-center text-gray-500">No academic years found matching your criteria.</td></tr>`;
            return;
        }

        filteredRows.slice(startIndex, endIndex).forEach(row => tableBody.appendChild(row.cloneNode(true)));

        // Sync active page button highlight
        paginationButtons?.querySelectorAll('button').forEach((btn, i) => {
            const page = i + 1;
            btn.className = `px-3 py-1 text-sm ${page === currentPage ? 'bg-primary-100 text-primary-600' : 'text-secondary-600 hover:bg-gray-100'} rounded-md`;
        });

        const totalPages = Math.ceil(filteredRows.length / rowsPerPage);
        if (prevPageBtn) prevPageBtn.disabled = currentPage === 1;
        if (nextPageBtn) nextPageBtn.disabled = currentPage === totalPages;
    }

    // ── Next-year dropdown: filter options that start before current year ends ─
    function setupNextYearFiltering() {
        function filterOptions(endDateVal, selectEl, currentYearId = null) {
            if (!endDateVal || !selectEl) return;
            selectEl.querySelectorAll('option').forEach(opt => {
                if (!opt.value) { opt.disabled = false; return; }
                const optStart = opt.getAttribute('data-start-date');
                const isSelf = currentYearId && opt.value === currentYearId;
                if (isSelf) {
                    opt.disabled = true;
                    opt.text = (opt.getAttribute('data-year-value') ?? opt.text) + ' (Cannot link to itself)';
                } else if (optStart && optStart < endDateVal) {
                    opt.disabled = true;
                    opt.text = (opt.getAttribute('data-year-value') ?? opt.text) + ' (Starts too early)';
                } else {
                    opt.disabled = false;
                    opt.text = opt.getAttribute('data-year-value') ?? opt.text;
                }
            });
        }

        const endDateEl = document.getElementById('endDate');
        const nextYearEl = document.getElementById('nextAcademicYearId');
        const upEndDateEl = document.getElementById('updateEndDate');
        const upNextYearEl = document.getElementById('updateNextAcademicYearId');

        endDateEl?.addEventListener('change', () => filterOptions(endDateEl.value, nextYearEl));

        upEndDateEl?.addEventListener('change', () => {
            const yearId = document.getElementById('updateYearId')?.value;
            filterOptions(upEndDateEl.value, upNextYearEl, yearId);
        });

        // Re-filter when update modal opens
        if (updateModal && upEndDateEl && upNextYearEl) {
            new MutationObserver(mutations => {
                mutations.forEach(m => {
                    if (!m.target.classList.contains('hidden')) {
                        const yearId = document.getElementById('updateYearId')?.value;
                        filterOptions(upEndDateEl.value, upNextYearEl, yearId);
                    }
                });
            }).observe(updateModal, { attributes: true, attributeFilter: ['class'] });
        }
    }

    setupNextYearFiltering();

    console.log('Academic Years JS loaded.');
});