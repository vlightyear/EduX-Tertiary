document.addEventListener('DOMContentLoaded', function () {
    const form = document.getElementById('workingDayForm');
    const workingDaysTableBody = document.getElementById('workingDaysTableBody');
    const workingDaysDataInput = document.getElementById('workingDaysData');
    const validationSummary = document.getElementById('validationSummary');

    let workingDays = initialWorkingDaysData;
    let hasUnsavedChanges = false;

    // Initialize table
    renderWorkingDaysTable();
    updateSummary();
    updateWorkingDaysData();

    // Render working days table
    function renderWorkingDaysTable() {
        workingDaysTableBody.innerHTML = workingDays.map((day, index) => `
            <tr class="border-b ${day.isWorkingDay ? 'bg-green-50' : ''}" data-index="${index}">
                <td class="px-4 py-3 font-medium">${day.day}</td>
                <td class="px-4 py-3">
                    <label class="relative inline-flex items-center cursor-pointer">
                        <input type="checkbox" 
                               class="sr-only peer" 
                               ${day.isWorkingDay ? 'checked' : ''}
                               data-field="isWorkingDay">
                        <div class="w-11 h-6 bg-gray-200 peer-focus:outline-none peer-focus:ring-4 
                                    peer-focus:ring-blue-300 rounded-full peer 
                                    peer-checked:after:translate-x-full rtl:peer-checked:after:-translate-x-full 
                                    peer-checked:after:border-white after:content-[''] after:absolute 
                                    after:top-[2px] after:start-[2px] after:bg-white after:border-gray-300 
                                    after:border after:rounded-full after:h-5 after:w-5 after:transition-all 
                                    peer-checked:bg-primary-600"></div>
                    </label>
                </td>
                <td class="px-4 py-3">
                    <select class="w-full px-3 py-2 border border-gray-300 rounded-lg 
                                 focus:ring-blue-500 focus:border-blue-500 ${!day.isWorkingDay ? 'bg-gray-100' : ''}"
                            data-field="timeSlotConfigId"
                            ${!day.isWorkingDay ? 'disabled' : ''}>
                        <option value="">Select Time Slot Configuration</option>
                        ${timeSlotConfigs.map(config => `
                            <option value="${config.value}" ${day.timeSlotConfigId == config.value ? 'selected' : ''}>
                                ${config.text}
                            </option>
                        `).join('')}
                    </select>
                </td>
            </tr>
        `).join('');
    }

    // Event delegation for working days table interactions
    workingDaysTableBody.addEventListener('change', (e) => {
        const field = e.target.dataset.field;
        if (!field) return;

        const row = e.target.closest('tr');
        if (!row) return;

        const index = parseInt(row.dataset.index);
        if (isNaN(index)) return;

        switch (field) {
            case 'isWorkingDay':
                workingDays[index].isWorkingDay = e.target.checked;
                if (!e.target.checked) {
                    workingDays[index].timeSlotConfigId = null;
                }
                renderWorkingDaysTable();
                break;
            case 'timeSlotConfigId':
                workingDays[index].timeSlotConfigId = e.target.value;
                break;
        }

        validateWorkingDays();
        updateSummary();
        updateWorkingDaysData();
        markAsChanged();
    });

    function validateWorkingDays() {
        const errors = [];

        const workingDaysCount = workingDays.filter(d => d.isWorkingDay).length;
        if (workingDaysCount === 0) {
            errors.push("At least one working day must be configured.");
        }

        workingDays.forEach(day => {
            if (day.isWorkingDay && !day.timeSlotConfigId) {
                errors.push(`${day.day} is marked as a working day but has no time slot configuration assigned.`);
            }
        });

        if (errors.length > 0) {
            validationSummary.innerHTML = errors.map(error => `<p>• ${error}</p>`).join('');
            validationSummary.classList.remove('hidden');
        } else {
            validationSummary.classList.add('hidden');
        }

        return errors;
    }

    function updateSummary() {
        const workingDaysCount = workingDays.filter(d => d.isWorkingDay).length;
        document.getElementById('totalWorkingDays').textContent = workingDaysCount;
        document.getElementById('totalNonWorkingDays').textContent = workingDays.length - workingDaysCount;
    }

    function updateWorkingDaysData() {
        workingDaysDataInput.value = JSON.stringify(workingDays);
    }

    function markAsChanged() {
        hasUnsavedChanges = true;
    }

    // Form submit handler
    form.addEventListener('submit', (e) => {
        const errors = validateWorkingDays();
        if (errors.length > 0) {
            e.preventDefault();
            window.scrollTo(0, validationSummary.offsetTop - 20);
        } else {
            hasUnsavedChanges = false;
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
console.log('.........>');