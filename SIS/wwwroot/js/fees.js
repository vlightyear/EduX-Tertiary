document.addEventListener('DOMContentLoaded', function () {
    // Initialize elements and check if they exist
    const elements = {
        modals: {
            createFeeModal: document.getElementById('createFeeModal'),
            updateFeeModal: document.getElementById('updateFeeModal'),
            deleteFeeModal: document.getElementById('deleteFeeModal')
        },
        forms: {
            createFeeForm: document.getElementById('createFeeForm'),
            updateFeeForm: document.getElementById('updateFeeForm'),
            deleteFeeForm: document.getElementById('deleteFeeForm')
        },
        buttons: {
            createFee: document.getElementById('createFeeButton'),    // Updated ID
            confirmUpdate: document.getElementById('confirmUpdate'),
            confirmDelete: document.getElementById('confirmDelete')
        }
    };

    // Verify critical elements exist
    const missingElements = [];
    Object.entries(elements).forEach(([category, categoryElements]) => {
        Object.entries(categoryElements).forEach(([name, element]) => {
            if (!element) {
                missingElements.push(name);
            }
        });
    });

    if (missingElements.length > 0) {
        console.error('Missing elements:', missingElements);
        return; // Exit if critical elements are missing
    }

    // Modal handling functions
    function showModal(modalElement) {
        if (!modalElement) return;
        modalElement.classList.remove('hidden');
        document.body.style.overflow = 'hidden';
    }

    function hideModal(modalElement) {
        if (!modalElement) return;
        modalElement.classList.add('hidden');
        document.body.style.overflow = 'auto';
    }

    // Close modal when clicking outside
    function setupModalCloseHandlers(modalElement) {
        if (!modalElement) return;

        modalElement.addEventListener('click', (e) => {
            if (e.target === modalElement) {
                hideModal(modalElement);
            }
        });

        // Close button handlers
        const closeButtons = modalElement.querySelectorAll('[data-bs-dismiss="modal"]');
        closeButtons.forEach(button => {
            button.addEventListener('click', () => hideModal(modalElement));
        });
    }

    // Setup close handlers for all modals
    Object.values(elements.modals).forEach(setupModalCloseHandlers);

    // Show create modal
    window.showCreateFeeModal = function () {
        if (elements.forms.createFeeForm) {
            elements.forms.createFeeForm.reset();
        }
        showModal(elements.modals.createFeeModal);
    };

    // Show update modal with data
    // Show update modal with data
    window.showUpdateFeeModal = function (id, feeTypeId, academicYearId, schoolId, programmeId,
        modeOfStudyId, yearOfStudy, programLevelId, amount, appliesUniversally, appliesOnlyToAccommodated) {
        // Helper function to safely set form values
        function setFormValue(id, value) {
            const element = document.getElementById(id);
            if (element) {
                if (element.type === 'checkbox') {
                    // For checkboxes, check if the value represents true
                    element.checked = value === 'True' || value === 'true' || value === true;
                } else {
                    element.value = value;
                }
            }
        }

        const fieldsToUpdate = {
            'updateFeeId': id,
            'updateFeeTypeId': feeTypeId,
            'updateAcademicYearId': academicYearId,
            'updateSchoolId': schoolId,
            'updateProgrammeId': programmeId,
            'updateModeOfStudyId': modeOfStudyId,
            'updateYearOfStudy': yearOfStudy,
            'updateProgramLevelId': programLevelId,
            'updateAmount': amount,
            'updateAppliesUniversally': appliesUniversally,
            'updateAppliesOnlyToAccommodated': appliesOnlyToAccommodated
        };

        Object.entries(fieldsToUpdate).forEach(([id, value]) => {
            setFormValue(id, value);
        });

        // Also trigger change events to update form state
        const universalCheckbox = document.getElementById('updateAppliesUniversally');
        const accommodatedCheckbox = document.getElementById('updateAppliesOnlyToAccommodated');

        if (universalCheckbox) {
            universalCheckbox.dispatchEvent(new Event('change'));
        }

        if (accommodatedCheckbox) {
            accommodatedCheckbox.dispatchEvent(new Event('change'));
        }

        showModal(elements.modals.updateFeeModal);
    };

    // Show delete modal with confirmation details
    window.showDeleteFeeModal = function (id, academicYear, programme, amount) {
        function setElementContent(id, content) {
            const element = document.getElementById(id);
            if (element) {
                if (element.type === 'hidden') {
                    element.value = content;
                } else {
                    element.textContent = content;
                }
            }
        }

        setElementContent('deleteFeeId', id);
        setElementContent('deleteAcademicYear', academicYear);
        setElementContent('deleteProgramme', programme);
        setElementContent('deleteAmount', amount);

        showModal(elements.modals.deleteFeeModal);
    };

    // Handle form submissions with validation
    // Handle form submissions with validation
    function setupFormSubmission(button, form) {
        if (!button || !form) return;

        button.addEventListener('click', function () {
            if (form.checkValidity()) {
                // Find the checkboxes
                const universalCheckbox = form.querySelector('#appliesUniversally') ||
                    form.querySelector('#updateAppliesUniversally');
                const accommodatedCheckbox = form.querySelector('#appliesOnlyToAccommodated') ||
                    form.querySelector('#updateAppliesOnlyToAccommodated');

                // Remove any existing hidden fields for these properties
                form.querySelectorAll('input[name="AppliesUniversally"], input[name="AppliesOnlyToAccommodated"]').forEach(el => {
                    el.remove();
                });

                // Add new hidden fields with current values
                const universalField = document.createElement('input');
                universalField.type = 'hidden';
                universalField.name = 'AppliesUniversally';
                universalField.value = universalCheckbox && universalCheckbox.checked ? 'true' : 'false';
                form.appendChild(universalField);

                const accommodatedField = document.createElement('input');
                accommodatedField.type = 'hidden';
                accommodatedField.name = 'AppliesOnlyToAccommodated';
                accommodatedField.value = accommodatedCheckbox && accommodatedCheckbox.checked ? 'true' : 'false';
                form.appendChild(accommodatedField);

                form.submit();
            } else {
                form.reportValidity();
            }
        });
    }

    setupFormSubmission(elements.buttons.createFee, elements.forms.createFeeForm);
    setupFormSubmission(elements.buttons.confirmUpdate, elements.forms.updateFeeForm);

    if (elements.buttons.confirmDelete && elements.forms.deleteFeeForm) {
        elements.buttons.confirmDelete.addEventListener('click', function () {
            elements.forms.deleteFeeForm.submit();
        });
    }

    // Handle Escape key to close modals
    document.addEventListener('keydown', function (e) {
        if (e.key === 'Escape') {
            Object.values(elements.modals).forEach(modal => {
                if (modal && !modal.classList.contains('hidden')) {
                    hideModal(modal);
                }
            });
        }
    });

    // Initialize DataTable if the table exists
    const feeConfigTable = document.getElementById('feeConfigTable');
    if (feeConfigTable) {
        const dataTable = new simpleDatatables.DataTable(feeConfigTable, {
            perPage: 10,
            searchable: true,
            sortable: true,
            labels: {
                placeholder: "Search fees...",
                perPage: "Show {select} entries",
                noRows: "No fee configurations found",
                info: "Showing {start} to {end} of {rows} configurations",
            },
            classes: {
                wrapper: "datatable-wrapper",
                input: "datatable-input px-3 py-2 text-sm border border-gray-300 rounded-lg",
                selector: "datatable-selector px-3 py-2 text-sm border border-gray-300 rounded-lg",
                paginationButton: "px-3 py-1 text-sm text-secondary-600 hover:bg-gray-100 rounded-md",
                paginationButtonActive: "bg-blue-50 text-blue-600 hover:bg-blue-100",
                paginationButtonDisabled: "text-gray-400 hover:bg-transparent cursor-not-allowed",
            }
        });

        // Make the table responsive
        window.addEventListener('resize', () => {
            dataTable.columns().rebuild();
        });
    }

    // Initialize Fee Distribution Chart if the element exists
    const chartElement = document.querySelector("#fee-summary-chart");
    if (chartElement) {
        const chartData = [
            { type: "Tuition Fees", count: 35 },
            { type: "Registration", count: 20 },
            { type: "Library Fees", count: 15 },
            { type: "Activity Fees", count: 10 },
            { type: "Other Fees", count: 20 }
        ];

        const feeSummaryChart = new ApexCharts(chartElement, {
            series: chartData.map(item => item.count),
            chart: {
                type: 'donut',
                height: '85%'
            },
            labels: chartData.map(item => item.type),
            colors: ['#4F46E5', '#10B981', '#F59E0B', '#EF4444', '#8B5CF6'],
            plotOptions: {
                pie: {
                    donut: {
                        size: '70%',
                        labels: {
                            show: false,
                            name: {
                                show: true,
                                fontSize: '14px',
                                fontFamily: 'inherit',
                                offsetY: -4
                            },
                            value: {
                                show: true,
                                fontSize: '16px',
                                fontFamily: 'inherit',
                                formatter: function (val) {
                                    return val + ' fees'
                                }
                            },
                            total: {
                                show: true,
                                label: 'Total Fees',
                                color: '#373d3f',
                                formatter: function (w) {
                                    return w.globals.seriesTotals.reduce((a, b) => a + b, 0) + ' total'
                                }
                            },
                            legend: {
                                show: false,
                            }
                        }
                    }
                }
            },
            legend: {
                show: false,
            },
            responsive: [{
                breakpoint: 480,
                options: {
                    chart: {
                        height: 200
                    },
                    legend: {
                        position: 'bottom'
                    }
                }
            }]
        });

        feeSummaryChart.render();
    }

    function setupAccommodationRequiredToggle(checkboxId) {
        const checkbox = document.getElementById(checkboxId);
        if (!checkbox) return;

        // Function to toggle required attribute
        function toggleRequiredFields() {
            const form = checkbox.closest('form');
            if (!form) return;

            // Fields that should not be required when accommodation is checked
            const fieldsToToggle = [
                'SchoolId', 'ProgrammeId', 'ModeOfStudyId',
                'YearOfStudy', 'ProgramLevelId'
            ].map(field => form.querySelector(`[name="${field}"]`));
            console.log('Here');

            fieldsToToggle.forEach(field => {
                if (field) {
                    if (checkbox.checked) {
                        // If checked, remove required attribute
                        field.removeAttribute('required');
                    } else {
                        // If not checked and universal is not checked, add required attribute
                        const universalCheckbox = form.querySelector('[name="AppliesUniversally"]');
                        if (!(universalCheckbox && universalCheckbox.checked)) {
                            field.setAttribute('required', '');
                        }
                    }
                }
            });
        }

        // Call the function whenever checkbox state changes
        checkbox.addEventListener('change', toggleRequiredFields);

        // Also call it on initial load to set the correct state
        toggleRequiredFields();
    }


    // Handle universal checkbox logic
    function setupUniversalCheckbox(checkboxId) {
        const checkbox = document.getElementById(checkboxId);
        if (!checkbox) return;

        checkbox.addEventListener('change', function () {
            const form = this.closest('form');
            if (!form) return;

            const fieldsToToggle = [
                'SchoolId', 'ProgrammeId', 'ModeOfStudyId',
                'YearOfStudy', 'ProgramLevelId'
            ].map(field => form.querySelector(`[name="${field}"]`));

            fieldsToToggle.forEach(field => {
                if (field) {
                    field.disabled = this.checked;
                    if (this.checked) {
                        field.value = '';
                    }
                }
            });

            // If universal is checked, uncheck "applies only to accommodated"
            const accommodatedCheckbox = form.querySelector('[name="AppliesOnlyToAccommodated"]');
            if (accommodatedCheckbox && this.checked) {
                accommodatedCheckbox.checked = false;
            }
        });
    }

    // Handle accommodation checkbox logic
    function setupAccommodationCheckbox(checkboxId) {
        const checkbox = document.getElementById(checkboxId);
        if (!checkbox) return;

        checkbox.addEventListener('change', function () {
            const form = this.closest('form');
            if (!form) return;

            // If accommodation is checked, uncheck "applies universally"
            const universalCheckbox = form.querySelector('[name="AppliesUniversally"]');
            if (universalCheckbox && this.checked) {
                universalCheckbox.checked = false;

                // Re-enable fields that universal checkbox would have disabled
                const fieldsToEnable = [
                    'SchoolId', 'ProgrammeId', 'ModeOfStudyId',
                    'YearOfStudy', 'ProgramLevelId'
                ].map(field => form.querySelector(`[name="${field}"]`));

                fieldsToEnable.forEach(field => {
                    if (field) {
                        field.disabled = false;
                    }
                });
            }
        });
    }

    function setupUniversalCheckbox(checkboxId) {
        const checkbox = document.getElementById(checkboxId);
        if (!checkbox) return;

        function toggleUniversalFields() {
            const form = checkbox.closest('form');
            if (!form) return;

            const fieldsToToggle = [
                'SchoolId', 'ProgrammeId', 'ModeOfStudyId',
                'YearOfStudy', 'ProgramLevelId'
            ].map(field => form.querySelector(`[name="${field}"]`));

            fieldsToToggle.forEach(field => {
                if (field) {
                    field.disabled = checkbox.checked;

                    // If universal is checked, also remove required attribute
                    if (checkbox.checked) {
                        field.removeAttribute('required');
                        field.value = '';
                    } else {
                        // Only add required back if accommodation is not checked
                        const accommodatedCheckbox = form.querySelector('[name="AppliesOnlyToAccommodated"]');
                        if (!(accommodatedCheckbox && accommodatedCheckbox.checked)) {
                            field.setAttribute('required', '');
                        }
                    }
                }
            });

            // If universal is checked, uncheck "applies only to accommodated"
            const accommodatedCheckbox = form.querySelector('[name="AppliesOnlyToAccommodated"]');
            if (accommodatedCheckbox && checkbox.checked) {
                accommodatedCheckbox.checked = false;
                // Trigger change event to update any dependent logic
                accommodatedCheckbox.dispatchEvent(new Event('change'));
            }
        }

        checkbox.addEventListener('change', toggleUniversalFields);

        // Initial setup
        toggleUniversalFields();
    }

    //  setupUniversalCheckbox calls 
    ['appliesUniversally', 'updateAppliesUniversally'].forEach(setupUniversalCheckbox);

    ['appliesOnlyToAccommodated', 'updateAppliesOnlyToAccommodated'].forEach(setupAccommodationRequiredToggle)

});