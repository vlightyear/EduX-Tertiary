document.addEventListener('DOMContentLoaded', function () {
    // Modal elements
    const createModal = document.getElementById('createAcademicYearModal');
    const updateModal = document.getElementById('updateAcademicYearModal');
    const deleteModal = document.getElementById('deleteAcademicYearModal');

    // Form elements
    const createForm = document.getElementById('createAcademicYearForm');
    const updateForm = document.getElementById('updateAcademicYearForm');
    const deleteForm = document.getElementById('deleteAcademicYearForm');

    // Table elements
    const academicYearTable = document.getElementById('academicYearTable');
    const tableBody = academicYearTable.querySelector('tbody');
    const yearSearch = document.getElementById('yearSearch');
    const statusFilter = document.getElementById('statusFilter');
    const typeFilter = document.getElementById('typeFilter');

    // Pagination elements
    const paginationButtons = document.getElementById('paginationButtons');
    const prevPageBtn = document.getElementById('prevPage');
    const nextPageBtn = document.getElementById('nextPage');
    const startRowElem = document.getElementById('startRow');
    const endRowElem = document.getElementById('endRow');
    const totalRowsElem = document.getElementById('totalRows');

    // Academic type radio buttons and semester sections
    const annualTypeRadio = document.getElementById('annualType');
    const semesterTypeRadio = document.getElementById('semesterType');
    const semesterDatesSection = document.getElementById('semesterDatesSection');

    const updateAnnualTypeRadio = document.getElementById('updateAnnualType');
    const updateSemesterTypeRadio = document.getElementById('updateSemesterType');
    const updateSemesterDatesSection = document.getElementById('updateSemesterDatesSection');

    // Table variables
    let currentPage = 1;
    const rowsPerPage = 10;
    let originalRows = [];
    let filteredRows = [];
    let sortColumn = 'index';
    let sortDirection = 'asc';

    // Toast notification function
    function showNotification(message, type = 'success') {
        const toastClasses = {
            success: 'bg-green-500',
            error: 'bg-red-500'
        };

        const toast = document.createElement('div');
        toast.className = `fixed top-4 right-4 p-4 rounded-lg text-white ${toastClasses[type]} shadow-lg z-50 transition-opacity duration-300`;
        toast.textContent = message;
        document.body.appendChild(toast);

        setTimeout(() => {
            toast.style.opacity = '0';
            setTimeout(() => toast.remove(), 300);
        }, 3000);
    }

    // Modal handling
    const modalHandler = {
        show(modal) {
            modal.classList.remove('hidden');
            document.body.style.overflow = 'hidden';
        },
        hide(modal) {
            modal.classList.add('hidden');
            document.body.style.overflow = 'auto';
        }
    };

    // Setup close handlers for all modals
    [createModal, updateModal, deleteModal].forEach(modal => {
        if (!modal) return;

        modal.addEventListener('click', (e) => {
            if (e.target === modal) modalHandler.hide(modal);
        });

        modal.querySelectorAll('[data-bs-dismiss="modal"]').forEach(button => {
            button.addEventListener('click', () => modalHandler.hide(modal));
        });
    });

    // Academic type change handlers
    function setupAcademicTypeHandlers() {
        // Create form handlers
        if (annualTypeRadio && semesterTypeRadio && semesterDatesSection) {
            annualTypeRadio.addEventListener('change', function () {
                if (this.checked) {
                    semesterDatesSection.classList.add('hidden');
                    clearSemesterFields(createForm);
                }
            });

            semesterTypeRadio.addEventListener('change', function () {
                if (this.checked) {
                    semesterDatesSection.classList.remove('hidden');
                    setSemesterFieldsRequired(createForm, true);
                }
            });
        }

        // Update form handlers
        if (updateAnnualTypeRadio && updateSemesterTypeRadio && updateSemesterDatesSection) {
            updateAnnualTypeRadio.addEventListener('change', function () {
                if (this.checked) {
                    updateSemesterDatesSection.classList.add('hidden');
                    clearSemesterFields(updateForm);
                }
            });

            updateSemesterTypeRadio.addEventListener('change', function () {
                if (this.checked) {
                    updateSemesterDatesSection.classList.remove('hidden');
                    setSemesterFieldsRequired(updateForm, true);
                }
            });
        }
    }

    function clearSemesterFields(form) {
        const semesterFields = [
            'Semester1StartDate', 'Semester1EndDate',
            'Semester2StartDate', 'Semester2EndDate'
        ];

        semesterFields.forEach(fieldName => {
            const field = form.querySelector(`input[name="${fieldName}"]`);
            if (field) {
                field.value = '';
                field.required = false;
            }
        });
    }

    function setSemesterFieldsRequired(form, required) {
        const semesterFields = [
            'Semester1StartDate', 'Semester1EndDate',
            'Semester2StartDate', 'Semester2EndDate'
        ];

        semesterFields.forEach(fieldName => {
            const field = form.querySelector(`input[name="${fieldName}"]`);
            if (field) {
                field.required = required;
            }
        });
    }

    // Form validation
    function validateForm(form) {
        const startDate = new Date(form.querySelector('input[name="StartDate"]').value);
        const endDate = new Date(form.querySelector('input[name="EndDate"]').value);

        if (startDate >= endDate) {
            showNotification('Academic Year Start Date must be earlier than End Date.', 'error');
            return false;
        }

        // Check if semester type is selected
        const academicTypeRadio = form.querySelector('input[name="AcademicType"]:checked');
        const isSemesterType = academicTypeRadio && academicTypeRadio.value === '1';

        if (isSemesterType) {
            // Validate semester dates
            const sem1Start = form.querySelector('input[name="Semester1StartDate"]').value;
            const sem1End = form.querySelector('input[name="Semester1EndDate"]').value;
            const sem2Start = form.querySelector('input[name="Semester2StartDate"]').value;
            const sem2End = form.querySelector('input[name="Semester2EndDate"]').value;

            if (!sem1Start || !sem1End || !sem2Start || !sem2End) {
                showNotification('All semester dates are required for semester-based academic years.', 'error');
                return false;
            }

            const semester1StartDate = new Date(sem1Start);
            const semester1EndDate = new Date(sem1End);
            const semester2StartDate = new Date(sem2Start);
            const semester2EndDate = new Date(sem2End);

            if (semester1StartDate >= semester1EndDate) {
                showNotification('Semester 1 Start Date must be earlier than Semester 1 End Date.', 'error');
                return false;
            }

            if (semester2StartDate >= semester2EndDate) {
                showNotification('Semester 2 Start Date must be earlier than Semester 2 End Date.', 'error');
                return false;
            }

            if (semester1EndDate >= semester2StartDate) {
                showNotification('Semester 1 must end before Semester 2 starts.', 'error');
                return false;
            }

            if (semester1StartDate < startDate || semester2EndDate > endDate) {
                showNotification('Semester dates must be within the academic year period.', 'error');
                return false;
            }
        }

        // Validate registration dates if both are provided
        const regStartInput = form.querySelector('input[name="RegistrationStartDate"]');
        const regEndInput = form.querySelector('input[name="RegistrationEndDate"]');

        if (regStartInput.value && regEndInput.value) {
            const regStartDate = new Date(regStartInput.value);
            const regEndDate = new Date(regEndInput.value);

            if (regStartDate >= regEndDate) {
                showNotification('Registration Start Date must be earlier than Registration End Date.', 'error');
                return false;
            }
        }

        // Validate final exam dates if both are provided
        const examStartInput = form.querySelector('input[name="FinalExamStartDate"]');
        const examEndInput = form.querySelector('input[name="FinalExamEndDate"]');

        if (examStartInput.value && examEndInput.value) {
            const examStartDate = new Date(examStartInput.value);
            const examEndDate = new Date(examEndInput.value);

            if (examStartDate >= examEndDate) {
                showNotification('Final Exam Start Date must be earlier than Final Exam End Date.', 'error');
                return false;
            }
        }

        // Validate grade submission dates if both are provided
        const gradeStartInput = form.querySelector('input[name="GradeSubmissionStartDate"]');
        const gradeEndInput = form.querySelector('input[name="GradeSubmissionEndDate"]');

        if (gradeStartInput.value && gradeEndInput.value) {
            const gradeStartDate = new Date(gradeStartInput.value);
            const gradeEndDate = new Date(gradeEndInput.value);

            if (gradeStartDate >= gradeEndDate) {
                showNotification('Grade Submission Start Date must be earlier than Grade Submission End Date.', 'error');
                return false;
            }
        }

        return form.checkValidity();
    }

    // AJAX form submission handler
    async function submitForm(form) {
        try {
            const formData = new FormData(form);

            // Ensure IsActive is properly handled
            const isActiveCheckbox = form.querySelector('input[name="IsActive"]');
            if (isActiveCheckbox) {
                formData.set('IsActive', isActiveCheckbox.checked);
            }

            const response = await fetch(form.action, {
                method: 'POST',
                body: formData,
                headers: {
                    'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]').value
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

    // Create Academic Year
    window.showCreateAcademicYearModal = function () {
        createForm.reset();

        // Reset to Annual type by default
        if (annualTypeRadio) annualTypeRadio.checked = true;
        if (semesterTypeRadio) semesterTypeRadio.checked = false;
        if (semesterDatesSection) semesterDatesSection.classList.add('hidden');

        clearSemesterFields(createForm);
        modalHandler.show(createModal);
    };

    createForm.addEventListener('submit', async function (e) {
        e.preventDefault();
        if (validateForm(this)) {
            await submitForm(this);
            modalHandler.hide(createModal);
        }
    });

    // Update Academic Year
    window.showUpdateAcademicYearModal = function (academicYear) {
        console.log('Received academic year data:', academicYear);

        // Get all form elements
        const updateYearId = document.getElementById('updateYearId');
        const updateYearValue = document.getElementById('updateYearValue');
        const updateStartDate = document.getElementById('updateStartDate');
        const updateEndDate = document.getElementById('updateEndDate');
        const updateMinRegistrationPayment = document.getElementById('updateMinRegistrationPayment');
        const updateMinExamPayment = document.getElementById('updateMinExamPayment');
        const updateIsActive = document.getElementById('updateIsActive');

        // Semester date fields
        const updateSemester1StartDate = document.getElementById('updateSemester1StartDate');
        const updateSemester1EndDate = document.getElementById('updateSemester1EndDate');
        const updateSemester2StartDate = document.getElementById('updateSemester2StartDate');
        const updateSemester2EndDate = document.getElementById('updateSemester2EndDate');

        // Optional period date fields
        const updateRegistrationStartDate = document.getElementById('updateRegistrationStartDate');
        const updateRegistrationEndDate = document.getElementById('updateRegistrationEndDate');
        const updateFinalExamStartDate = document.getElementById('updateFinalExamStartDate');
        const updateFinalExamEndDate = document.getElementById('updateFinalExamEndDate');
        const updateGradeSubmissionStartDate = document.getElementById('updateGradeSubmissionStartDate');
        const updateGradeSubmissionEndDate = document.getElementById('updateGradeSubmissionEndDate');

        if (!updateYearId || !updateYearValue || !updateStartDate || !updateEndDate) {
            console.error('Could not find all required form fields');
            showNotification('Error: Could not populate the form. Please check the console for details.', 'error');
            return;
        }

        // Populate basic fields
        updateYearId.value = academicYear.yearId;
        updateYearValue.value = academicYear.yearValue;
        updateStartDate.value = academicYear.startDate;
        updateEndDate.value = academicYear.endDate;
        updateMinRegistrationPayment.value = academicYear.minRegistrationPaymentPercentage;
        updateMinExamPayment.value = academicYear.minExamPaymentPercentage;

        // Set academic type and show/hide semester section
        const academicType = parseInt(academicYear.academicType);
        if (academicType === 1) { // Semester
            if (updateSemesterTypeRadio) updateSemesterTypeRadio.checked = true;
            if (updateAnnualTypeRadio) updateAnnualTypeRadio.checked = false;
            if (updateSemesterDatesSection) updateSemesterDatesSection.classList.remove('hidden');
            setSemesterFieldsRequired(updateForm, true);
        } else { // Annual
            if (updateAnnualTypeRadio) updateAnnualTypeRadio.checked = true;
            if (updateSemesterTypeRadio) updateSemesterTypeRadio.checked = false;
            if (updateSemesterDatesSection) updateSemesterDatesSection.classList.add('hidden');
            setSemesterFieldsRequired(updateForm, false);
        }

        // Populate semester dates if available
        if (updateSemester1StartDate && academicYear.semester1StartDate) {
            updateSemester1StartDate.value = academicYear.semester1StartDate;
        }
        if (updateSemester1EndDate && academicYear.semester1EndDate) {
            updateSemester1EndDate.value = academicYear.semester1EndDate;
        }
        if (updateSemester2StartDate && academicYear.semester2StartDate) {
            updateSemester2StartDate.value = academicYear.semester2StartDate;
        }
        if (updateSemester2EndDate && academicYear.semester2EndDate) {
            updateSemester2EndDate.value = academicYear.semester2EndDate;
        }

        // Populate optional period dates
        if (updateRegistrationStartDate && academicYear.registrationStartDate) {
            updateRegistrationStartDate.value = academicYear.registrationStartDate;
        }
        if (updateRegistrationEndDate && academicYear.registrationEndDate) {
            updateRegistrationEndDate.value = academicYear.registrationEndDate;
        }
        if (updateFinalExamStartDate && academicYear.finalExamStartDate) {
            updateFinalExamStartDate.value = academicYear.finalExamStartDate;
        }
        if (updateFinalExamEndDate && academicYear.finalExamEndDate) {
            updateFinalExamEndDate.value = academicYear.finalExamEndDate;
        }
        if (updateGradeSubmissionStartDate && academicYear.gradeSubmissionStartDate) {
            updateGradeSubmissionStartDate.value = academicYear.gradeSubmissionStartDate;
        }
        if (updateGradeSubmissionEndDate && academicYear.gradeSubmissionEndDate) {
            updateGradeSubmissionEndDate.value = academicYear.gradeSubmissionEndDate;
        }

        // Populate next academic year
        const updateNextAcademicYearId = document.getElementById('updateNextAcademicYearId');
        if (updateNextAcademicYearId && academicYear.nextAcademicYearId) {
            updateNextAcademicYearId.value = academicYear.nextAcademicYearId;
        } else if (updateNextAcademicYearId) {
            updateNextAcademicYearId.value = '';
        }

        // Handle IsActive checkbox
        if (updateIsActive) {
            updateIsActive.checked = academicYear.isActive === true || academicYear.isActive === 'true';
        }

        modalHandler.show(updateModal);
    };

    updateForm.addEventListener('submit', async function (e) {
        e.preventDefault();
        if (validateForm(this)) {
            await submitForm(this);
            modalHandler.hide(updateModal);
        }
    });

    // Delete Academic Year
    window.showDeleteAcademicYearModal = function (id, yearValue, academicType) {
        document.getElementById('deleteYearId').value = id;
        document.getElementById('deleteYearValue').textContent = yearValue;
        document.getElementById('deleteAcademicType').textContent = academicType;
        modalHandler.show(deleteModal);
    };

    deleteForm.addEventListener('submit', async function (e) {
        e.preventDefault();
        await submitForm(this);
        modalHandler.hide(deleteModal);
    });

    // Handle Escape key
    document.addEventListener('keydown', function (e) {
        if (e.key === 'Escape') {
            [createModal, updateModal, deleteModal].forEach(modal => {
                if (modal && !modal.classList.contains('hidden')) {
                    modalHandler.hide(modal);
                }
            });
        }
    });

    // Table functionality
    if (tableBody) {
        originalRows = Array.from(tableBody.querySelectorAll('tr'));
        filteredRows = [...originalRows];

        // Set up sorting
        const tableHeaders = academicYearTable.querySelectorAll('th[data-sort]');
        tableHeaders.forEach(header => {
            header.addEventListener('click', () => {
                const column = header.getAttribute('data-sort');

                if (sortColumn === column) {
                    sortDirection = sortDirection === 'asc' ? 'desc' : 'asc';
                } else {
                    sortColumn = column;
                    sortDirection = 'asc';
                }

                sortTable(column, sortDirection);
                renderTable();
            });
        });

        // Set up search and filter
        yearSearch.addEventListener('input', filterTable);
        statusFilter.addEventListener('change', filterTable);
        typeFilter.addEventListener('change', filterTable);

        setupPagination();
        renderTable();
    }

    // Table sorting function
    function sortTable(column, direction) {
        filteredRows.sort((a, b) => {
            let aValue, bValue;

            switch (column) {
                case 'index':
                    aValue = parseInt(a.cells[0].textContent);
                    bValue = parseInt(b.cells[0].textContent);
                    break;
                case 'year':
                    aValue = a.cells[1].textContent;
                    bValue = b.cells[1].textContent;
                    break;
                case 'type':
                    aValue = a.cells[2].textContent;
                    bValue = b.cells[2].textContent;
                    break;
                case 'status':
                    aValue = a.cells[3].textContent.includes('Active') ? 1 : 0;
                    bValue = b.cells[3].textContent.includes('Active') ? 1 : 0;
                    break;
                case 'nextYear':
                    aValue = a.cells[5].textContent.trim(); // Adjust index if needed
                    bValue = b.cells[5].textContent.trim();
                    // Handle "Not set" values
                    if (aValue === 'Not set') aValue = 'ZZZZ';
                    if (bValue === 'Not set') bValue = 'ZZZZ';
                    break;
                default:
                    aValue = a.cells[0].textContent;
                    bValue = b.cells[0].textContent;
            }

            if (direction === 'asc') {
                return aValue > bValue ? 1 : -1;
            } else {
                return aValue < bValue ? 1 : -1;
            }
        });
    }

    // Table filter function
    function filterTable() {
        const searchTerm = yearSearch.value.toLowerCase();
        const statusFilterValue = statusFilter.value;
        const typeFilterValue = typeFilter.value;

        filteredRows = originalRows.filter(row => {
            const yearValue = row.cells[1].textContent.toLowerCase();
            const typeValue = row.cells[2].textContent;
            const isActive = row.cells[3].textContent.includes('Active');

            // Apply search filter
            const matchesSearch = searchTerm === '' || yearValue.includes(searchTerm);

            // Apply status filter
            const matchesStatus = statusFilterValue === 'all' ||
                (statusFilterValue === 'active' && isActive) ||
                (statusFilterValue === 'inactive' && !isActive);

            // Apply type filter
            const matchesType = typeFilterValue === 'all' ||
                typeValue.includes(typeFilterValue);

            return matchesSearch && matchesStatus && matchesType;
        });

        currentPage = 1;
        setupPagination();
        renderTable();
    }

    // Set up pagination
    function setupPagination() {
        const totalPages = Math.ceil(filteredRows.length / rowsPerPage);

        totalRowsElem.textContent = filteredRows.length;

        paginationButtons.innerHTML = '';

        for (let i = 1; i <= totalPages; i++) {
            const button = document.createElement('button');
            button.textContent = i;
            button.className = `px-3 py-1 text-sm ${i === currentPage ? 'bg-primary-100 text-primary-600' : 'text-secondary-600 hover:bg-gray-100'} rounded-md`;
            button.addEventListener('click', () => {
                currentPage = i;
                renderTable();
            });
            paginationButtons.appendChild(button);
        }

        prevPageBtn.disabled = currentPage === 1;
        nextPageBtn.disabled = currentPage === totalPages || totalPages === 0;

        prevPageBtn.onclick = () => {
            if (currentPage > 1) {
                currentPage--;
                renderTable();
            }
        };

        nextPageBtn.onclick = () => {
            if (currentPage < totalPages) {
                currentPage++;
                renderTable();
            }
        };
    }

    // Render the table with current filters and pagination
    function renderTable() {
        const startIndex = (currentPage - 1) * rowsPerPage;
        const endIndex = Math.min(startIndex + rowsPerPage, filteredRows.length);

        startRowElem.textContent = filteredRows.length > 0 ? startIndex + 1 : 0;
        endRowElem.textContent = endIndex;

        tableBody.innerHTML = '';

        if (filteredRows.length === 0) {
            const emptyRow = document.createElement('tr');
            emptyRow.innerHTML = `<td colspan="6" class="px-6 py-4 text-center text-gray-500">No academic years found matching your criteria.</td>`;
            tableBody.appendChild(emptyRow);
            return;
        }

        const visibleRows = filteredRows.slice(startIndex, endIndex);
        visibleRows.forEach(row => {
            tableBody.appendChild(row.cloneNode(true));
        });

        const pageButtons = paginationButtons.querySelectorAll('button');
        pageButtons.forEach((button, index) => {
            const pageNum = index + 1;
            button.className = `px-3 py-1 text-sm ${pageNum === currentPage ? 'bg-primary-100 text-primary-600' : 'text-secondary-600 hover:bg-gray-100'} rounded-md`;
        });

        const totalPages = Math.ceil(filteredRows.length / rowsPerPage);
        prevPageBtn.disabled = currentPage === 1;
        nextPageBtn.disabled = currentPage === totalPages;
    }

    // Initialize academic type handlers
    setupAcademicTypeHandlers();

    // Filter next academic year dropdown based on current year's end date
    function setupNextYearFiltering() {
        const startDateInput = document.getElementById('startDate');
        const endDateInput = document.getElementById('endDate');
        const nextYearSelect = document.getElementById('nextAcademicYearId');

        const updateStartDateInput = document.getElementById('updateStartDate');
        const updateEndDateInput = document.getElementById('updateEndDate');
        const updateNextYearSelect = document.getElementById('updateNextAcademicYearId');

        function filterNextYearOptions(endDate, selectElement, currentYearId = null) {
            if (!endDate || !selectElement) return;

            const options = selectElement.querySelectorAll('option');
            options.forEach(option => {
                if (option.value === '') {
                    option.disabled = false;
                    return;
                }

                const optionStartDate = option.getAttribute('data-start-date');
                const optionYearId = option.value;

                // Disable if it's the same year (for update modal)
                if (currentYearId && optionYearId === currentYearId) {
                    option.disabled = true;
                    option.text = option.getAttribute('data-year-value') + ' (Cannot link to itself)';
                    return;
                }

                // Disable if start date is before current year's end date
                if (optionStartDate && optionStartDate < endDate) {
                    option.disabled = true;
                    option.text = option.getAttribute('data-year-value') + ' (Starts too early)';
                } else {
                    option.disabled = false;
                    option.text = option.getAttribute('data-year-value');
                }
            });
        }

        // Create modal filtering
        if (endDateInput && nextYearSelect) {
            endDateInput.addEventListener('change', function () {
                filterNextYearOptions(this.value, nextYearSelect);
            });
        }

        // Update modal filtering
        if (updateEndDateInput && updateNextYearSelect) {
            updateEndDateInput.addEventListener('change', function () {
                const currentYearId = document.getElementById('updateYearId')?.value;
                filterNextYearOptions(this.value, updateNextYearSelect, currentYearId);
            });

            // Also filter when modal opens
            const observer = new MutationObserver(function (mutations) {
                mutations.forEach(function (mutation) {
                    if (mutation.target.id === 'updateAcademicYearModal' &&
                        !mutation.target.classList.contains('hidden')) {
                        const endDate = updateEndDateInput.value;
                        const currentYearId = document.getElementById('updateYearId')?.value;
                        filterNextYearOptions(endDate, updateNextYearSelect, currentYearId);
                    }
                });
            });

            const updateModal = document.getElementById('updateAcademicYearModal');
            if (updateModal) {
                observer.observe(updateModal, { attributes: true, attributeFilter: ['class'] });
            }
        }
    }

    // Initialize next year filtering
    setupNextYearFiltering();

    console.log('ACADEMIC YEAR Loaded');
});