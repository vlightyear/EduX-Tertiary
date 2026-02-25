// timeSlotConfig.js

document.addEventListener('DOMContentLoaded', function () {
    const form = document.getElementById('timeSlotForm');
    const periodsTableBody = document.getElementById('periodsTableBody');
    const periodsDataInput = document.getElementById('periodsData');
    const addPeriodBtn = document.getElementById('addPeriodBtn');
    const validationSummary = document.getElementById('validationSummary');
    const deletePeriodModal = document.getElementById('deletePeriodModal');
    const deletePeriodNumberEl = document.getElementById('deletePeriodNumber');
    const deletePeriodTimeEl = document.getElementById('deletePeriodTime');
    const confirmDeleteBtn = document.getElementById('confirmDeletePeriod');

    let deletingPeriodIndex = null;
    let periods = [];

    // Initialize data if available
    if (typeof initialPeriodsData !== 'undefined' && initialPeriodsData.length > 0) {
        periods = initialPeriodsData;
        renderPeriodsTable();
        updateSummary();
    }

    // Add new period
    addPeriodBtn.addEventListener('click', () => {
        const newPeriod = {
            periodNumber: periods.length + 1,
            startTime: '',
            endTime: '',
            type: 'Regular',
            description: ''
        };
        periods.push(newPeriod);
        renderPeriodsTable();
        updateSummary();
        markAsChanged();
    });

    // Modal functions
    window.showDeletePeriodModal = function () {
        if (deletePeriodModal) {
            deletePeriodModal.classList.remove('hidden');
            document.body.style.overflow = 'hidden';
        }
    };

    window.hideDeletePeriodModal = function () {
        if (deletePeriodModal) {
            deletePeriodModal.classList.add('hidden');
            document.body.style.overflow = 'auto';
            deletingPeriodIndex = null;
        }
    };

    // Event delegation for period table interactions
    periodsTableBody.addEventListener('click', (e) => {
        const deleteButton = e.target.closest('button');
        if (!deleteButton) return;

        const row = deleteButton.closest('tr');
        if (!row) return;

        const index = parseInt(row.dataset.index);
        if (isNaN(index)) return;

        deletingPeriodIndex = index;
        const period = periods[index];

        if (deletePeriodNumberEl && deletePeriodTimeEl) {
            deletePeriodNumberEl.textContent = period.periodNumber;
            deletePeriodTimeEl.textContent = `${period.startTime} - ${period.endTime}`;
        }

        window.showDeletePeriodModal();
    });

    // Handle all form control changes
    periodsTableBody.addEventListener('change', (e) => {
        const field = e.target.dataset.field;
        if (!field) return;

        const row = e.target.closest('tr');
        if (!row) return;

        const index = parseInt(row.dataset.index);
        if (isNaN(index)) return;

        const value = e.target.value;

        switch (field) {
            case 'startTime':
            case 'endTime':
                periods[index][field] = value;
                validatePeriods();
                updatePeriodsData();
                markAsChanged();
                break;
            case 'type':
                periods[index].type = value;
                renderPeriodsTable();
                updateSummary();
                updatePeriodsData();
                markAsChanged();
                break;
            case 'description':
                periods[index].description = value;
                updatePeriodsData();
                markAsChanged();
                break;
        }
    });

    // Delete confirmation handler
    if (confirmDeleteBtn) {
        confirmDeleteBtn.addEventListener('click', () => {
            if (deletingPeriodIndex !== null) {
                periods.splice(deletingPeriodIndex, 1);
                periods.forEach((period, i) => {
                    period.periodNumber = i + 1;
                });
                renderPeriodsTable();
                updateSummary();
                validatePeriods();
                updatePeriodsData();
                markAsChanged();
                window.hideDeletePeriodModal();
            }
        });
    }

    // Modal close handlers
    if (deletePeriodModal) {
        deletePeriodModal.addEventListener('click', (e) => {
            if (e.target === deletePeriodModal) {
                window.hideDeletePeriodModal();
            }
        });
    }

    document.addEventListener('keydown', (e) => {
        if (e.key === 'Escape' && deletePeriodModal && !deletePeriodModal.classList.contains('hidden')) {
            window.hideDeletePeriodModal();
        }
    });

    // Render periods table
    function renderPeriodsTable() {
        periodsTableBody.innerHTML = periods.map((period, index) => `
            <tr class="border-b ${period.type === 'Break' ? 'bg-orange-50' : ''}" data-index="${index}" draggable="true">
                <td class="px-4 py-3">Period ${period.periodNumber}</td>
                <td class="px-4 py-3">
                    <input type="time" 
                           class="px-3 py-1.5 border border-gray-300 rounded-lg focus:ring-blue-500 focus:border-blue-500" 
                           value="${period.startTime}"
                           data-field="startTime">
                </td>
                <td class="px-4 py-3">
                    <input type="time" 
                           class="px-3 py-1.5 border border-gray-300 rounded-lg focus:ring-blue-500 focus:border-blue-500" 
                           value="${period.endTime}"
                           data-field="endTime">
                </td>
                <td class="px-4 py-3">
                    <select class="px-3 py-1.5 border border-gray-300 rounded-lg focus:ring-blue-500 focus:border-blue-500"
                            data-field="type">
                        <option value="Regular" ${period.type === 'Regular' ? 'selected' : ''}>Regular</option>
                        <option value="Break" ${period.type === 'Break' ? 'selected' : ''}>Break</option>
                    </select>
                </td>
                <td class="px-4 py-3">
                    <input type="text" 
                           class="w-full px-3 py-1.5 border border-gray-300 rounded-lg focus:ring-blue-500 focus:border-blue-500" 
                           value="${period.description}"
                           placeholder="Optional description"
                           data-field="description">
                </td>
                <td class="px-4 py-3 text-center">
                    <button type="button" 
                            class="text-red-600 hover:text-red-800 transition-colors">
                        <i class="material-icons">delete</i>
                    </button>
                </td>
            </tr>
        `).join('');
    }

    // Utility functions
    function timeToMinutes(timeStr) {
        if (!timeStr) return -1;
        const [hours, minutes] = timeStr.split(':').map(Number);
        return hours * 60 + minutes;
    }

    function minutesToTime(minutes) {
        const hours = Math.floor(minutes / 60);
        const mins = minutes % 60;
        return `${hours.toString().padStart(2, '0')}:${mins.toString().padStart(2, '0')}`;
    }

    function updateSummary() {
        document.getElementById('totalPeriods').textContent = periods.length;
        document.getElementById('regularPeriods').textContent = periods.filter(p => p.type === 'Regular').length;
        document.getElementById('breakPeriods').textContent = periods.filter(p => p.type === 'Break').length;
    }

    let hasUnsavedChanges = false;

    function markAsChanged() {
        hasUnsavedChanges = true;
    }

    function validatePeriods() {
        const errors = [];

        if (periods.length === 0) {
            errors.push("At least one period is required.");
            return errors;
        }

        const sortedPeriods = [...periods].sort((a, b) =>
            timeToMinutes(a.startTime) - timeToMinutes(b.startTime)
        );

        for (let i = 0; i < sortedPeriods.length; i++) {
            const period = sortedPeriods[i];
            const startMinutes = timeToMinutes(period.startTime);
            const endMinutes = timeToMinutes(period.endTime);

            if (startMinutes === -1 || endMinutes === -1) {
                errors.push(`Period ${period.periodNumber}: Start and end times are required.`);
                continue;
            }

            if (endMinutes <= startMinutes) {
                errors.push(`Period ${period.periodNumber}: End time must be after start time.`);
            }

            if (i < sortedPeriods.length - 1) {
                const nextPeriod = sortedPeriods[i + 1];
                const nextStartMinutes = timeToMinutes(nextPeriod.startTime);

                if (nextStartMinutes < endMinutes) {
                    errors.push(`Period ${period.periodNumber} overlaps with Period ${nextPeriod.periodNumber}.`);
                } else if (nextStartMinutes > endMinutes) {
                    errors.push(`Gap detected between Period ${period.periodNumber} and Period ${nextPeriod.periodNumber}.`);
                }
            }
        }

        if (errors.length > 0) {
            validationSummary.innerHTML = errors.map(error => `<p>• ${error}</p>`).join('');
            validationSummary.classList.remove('hidden');
        } else {
            validationSummary.classList.add('hidden');
        }

        return errors;
    }

    function updatePeriodsData() {
        periodsDataInput.value = JSON.stringify(periods);
    }

    // Form submit handler
    form.addEventListener('submit', (e) => {
        const errors = validatePeriods();
        if (errors.length > 0) {
            e.preventDefault();
            window.scrollTo(0, validationSummary.offsetTop - 20);
        } else {
            hasUnsavedChanges = false;
        }
    });

    // Initialize with empty period if needed
    if (periods.length === 0) {
        addPeriodBtn.click();
    }

    // Drag and drop functionality
    let draggedItem = null;

    periodsTableBody.addEventListener('dragstart', (e) => {
        draggedItem = e.target.closest('tr');
        e.target.style.opacity = '0.5';
    });

    periodsTableBody.addEventListener('dragend', (e) => {
        e.target.style.opacity = '1';
        draggedItem = null;
    });

    periodsTableBody.addEventListener('dragover', (e) => {
        e.preventDefault();
        const tr = e.target.closest('tr');
        if (tr && tr !== draggedItem) {
            const allTrs = [...periodsTableBody.querySelectorAll('tr')];
            const draggedIndex = allTrs.indexOf(draggedItem);
            const droppedIndex = allTrs.indexOf(tr);

            if (draggedIndex < droppedIndex) {
                tr.after(draggedItem);
            } else {
                tr.before(draggedItem);
            }

            const newPeriods = [...periods];
            const [movedPeriod] = newPeriods.splice(draggedIndex, 1);
            newPeriods.splice(droppedIndex, 0, movedPeriod);
            periods = newPeriods;

            periods.forEach((period, i) => {
                period.periodNumber = i + 1;
            });

            renderPeriodsTable();
            validatePeriods();
            updatePeriodsData();
            markAsChanged();
        }
    });

    // Warn about unsaved changes
    window.addEventListener('beforeunload', (e) => {
        if (hasUnsavedChanges) {
            e.preventDefault();
            e.returnValue = '';
        }
    });
});