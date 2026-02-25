document.addEventListener('DOMContentLoaded', function () {
    // Modal elements
    const createProgressionRuleModal = document.getElementById('createProgressionRuleModal');
    const updateProgressionRuleModal = document.getElementById('updateProgressionRuleModal');
    const deleteProgressionRuleModal = document.getElementById('deleteProgressionRuleModal');

    // Form elements
    const createProgressionRuleForm = document.getElementById('createProgressionRuleForm');
    const updateProgressionRuleForm = document.getElementById('updateProgressionRuleForm');
    const deleteProgressionRuleForm = document.getElementById('deleteProgressionRuleForm');

    // Button elements
    const confirmCreateBtn = document.getElementById('confirmCreate');
    const confirmUpdateBtn = document.getElementById('confirmUpdate');
    const confirmDeleteBtn = document.getElementById('confirmDelete');

    // Debug: Check which elements exist
    console.log('Elements found:', {
        createModal: !!createProgressionRuleModal,
        updateModal: !!updateProgressionRuleModal,
        deleteModal: !!deleteProgressionRuleModal,
        createForm: !!createProgressionRuleForm,
        updateForm: !!updateProgressionRuleForm,
        deleteForm: !!deleteProgressionRuleForm,
        createBtn: !!confirmCreateBtn,
        updateBtn: !!confirmUpdateBtn,
        deleteBtn: !!confirmDeleteBtn
    });

    // Get checkbox element safely
    let confirmDeleteCheckbox = null;

    // Modal handling functions
    function showModal(modalElement) {
        modalElement.classList.remove('hidden');
        document.body.style.overflow = 'hidden';
    }

    function hideModal(modalElement) {
        modalElement.classList.add('hidden');
        document.body.style.overflow = 'auto';
    }

    // Close modal when clicking outside or on close button
    function setupModalCloseHandlers(modalElement) {
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
    [createProgressionRuleModal, updateProgressionRuleModal, deleteProgressionRuleModal].forEach(setupModalCloseHandlers);

    // Load schools data for dropdowns
    async function loadSchools() {
        try {
            const response = await fetch('/Admin/GetSchools');
            const schools = await response.json();

            // Populate create modal school dropdown
            const createSchoolSelect = document.getElementById('schoolId');
            createSchoolSelect.innerHTML = '<option value="">Select School (Leave empty for global rule)</option>';
            schools.forEach(school => {
                createSchoolSelect.innerHTML += `<option value="${school.id}">${school.name}</option>`;
            });

            // Populate update modal school dropdown
            const updateSchoolSelect = document.getElementById('updateSchoolId');
            updateSchoolSelect.innerHTML = '<option value="">Select School (Leave empty for global rule)</option>';
            schools.forEach(school => {
                updateSchoolSelect.innerHTML += `<option value="${school.id}">${school.name}</option>`;
            });
        } catch (error) {
            console.error('Error loading schools:', error);
        }
    }

    // Show/hide modal functions
    window.showCreateProgressionRuleModal = function () {
        createProgressionRuleForm.reset();
        loadSchools(); // Load schools when opening create modal
        showModal(createProgressionRuleModal);
    };

    window.hideCreateProgressionRuleModal = function () {
        hideModal(createProgressionRuleModal);
    };

    window.showUpdateProgressionRuleModal = async function (id) {
        try {
            await loadSchools(); // Load schools first

            const response = await fetch(`/Admin/GetProgressionRule/${id}`);
            const rule = await response.json();

            document.getElementById('updateRuleId').value = rule.id;
            document.getElementById('updateRuleName').value = rule.name;
            document.getElementById('updateMaximumFailedCourses').value = rule.maximumFailedCourses;
            document.getElementById('updateDescription').value = rule.description || '';
            document.getElementById('updateAction').value = rule.action;
            document.getElementById('updateIsActive').checked = rule.isActive;

            // Set school selection
            const updateSchoolSelect = document.getElementById('updateSchoolId');
            if (rule.schoolId) {
                updateSchoolSelect.value = rule.schoolId;
            } else {
                updateSchoolSelect.value = '';
            }

            showModal(updateProgressionRuleModal);
        } catch (error) {
            console.error('Error fetching progression rule:', error);
            alert('Error loading rule data. Please try again.');
        }
    };

    window.hideUpdateProgressionRuleModal = function () {
        hideModal(updateProgressionRuleModal);
    };

    window.showDeleteRuleModal = async function (id, name) {
        try {
            // Fetch rule details for display
            const response = await fetch(`/Admin/GetProgressionRule/${id}`);
            const rule = await response.json();

            document.getElementById('deleteRuleId').value = id;
            document.getElementById('deleteRuleName').textContent = name;
            document.getElementById('deleteRuleSchool').textContent = rule.schoolName || 'Global Rule';
            document.getElementById('deleteRuleAction').textContent = rule.action;

            // Get checkbox reference after modal content is ready
            confirmDeleteCheckbox = document.getElementById('confirmDeleteCheckbox');

            // Reset checkbox and disable delete button
            if (confirmDeleteCheckbox) {
                confirmDeleteCheckbox.checked = false;
            }
            if (confirmDeleteBtn) {
                confirmDeleteBtn.disabled = true;
            }

            showModal(deleteProgressionRuleModal);
        } catch (error) {
            console.error('Error fetching rule details:', error);
            // Fallback to basic display
            document.getElementById('deleteRuleId').value = id;
            document.getElementById('deleteRuleName').textContent = name;
            document.getElementById('deleteRuleSchool').textContent = 'Unknown';
            document.getElementById('deleteRuleAction').textContent = 'Unknown';

            // Get checkbox reference after modal content is ready
            confirmDeleteCheckbox = document.getElementById('confirmDeleteCheckbox');

            if (confirmDeleteCheckbox) {
                confirmDeleteCheckbox.checked = false;
            }
            if (confirmDeleteBtn) {
                confirmDeleteBtn.disabled = true;
            }

            showModal(deleteProgressionRuleModal);
        }
    };

    window.hideDeleteProgressionRuleModal = function () {
        hideModal(deleteProgressionRuleModal);
    };

    // Handle form submissions
    if (confirmCreateBtn && createProgressionRuleForm) {
        confirmCreateBtn.addEventListener('click', function () {
            if (createProgressionRuleForm.checkValidity()) {
                createProgressionRuleForm.submit();
            } else {
                createProgressionRuleForm.reportValidity();
            }
        });
    }

    if (confirmUpdateBtn && updateProgressionRuleForm) {
        confirmUpdateBtn.addEventListener('click', function () {
            if (updateProgressionRuleForm.checkValidity()) {
                updateProgressionRuleForm.submit();
            } else {
                updateProgressionRuleForm.reportValidity();
            }
        });
    }

    if (confirmDeleteBtn && deleteProgressionRuleForm) {
        confirmDeleteBtn.addEventListener('click', function () {
            const checkbox = document.getElementById('confirmDeleteCheckbox');
            if (checkbox && checkbox.checked) {
                deleteProgressionRuleForm.submit();
            }
        });
    }

    // Handle delete confirmation checkbox - use event delegation
    document.addEventListener('change', function (e) {
        if (e.target && e.target.id === 'confirmDeleteCheckbox') {
            const deleteBtn = document.getElementById('confirmDelete');
            if (deleteBtn) {
                deleteBtn.disabled = !e.target.checked;
            }
        }
    });

    // Handle Escape key to close modals
    document.addEventListener('keydown', function (e) {
        if (e.key === 'Escape') {
            [createProgressionRuleModal, updateProgressionRuleModal, deleteProgressionRuleModal].forEach(modal => {
                if (!modal.classList.contains('hidden')) {
                    hideModal(modal);
                }
            });
        }
    });

    // Initialize DataTable
    if (typeof simpleDatatables !== 'undefined' && document.getElementById("progressionRuleTable")) {
        const dataTable = new simpleDatatables.DataTable("#progressionRuleTable", {
            perPage: 15,
            searchable: true,
            sortable: true,
            labels: {
                placeholder: "Search progression rules...",
                perPage: "Show {select} entries",
                noRows: "No progression rules found",
                info: "Showing {start} to {end} of {rows} rules",
            },
            classes: {
                wrapper: "datatable-wrapper",
                input: "datatable-input px-3 py-2 text-sm border border-gray-300 rounded-lg",
                selector: "datatable-selector px-3 py-2 text-sm border border-gray-300 rounded-lg",
                paginationButton: "px-3 py-1 text-sm text-secondary-600 hover:bg-gray-100 rounded-md",
                paginationButtonActive: "bg-primary-50 text-primary-600 hover:bg-primary-100",
                paginationButtonDisabled: "text-gray-400 hover:bg-transparent cursor-not-allowed",
            }
        });
    }

    // Initialize Distribution Chart
    /*if (typeof ApexCharts !== 'undefined') {
        const activeRules = document.querySelectorAll('span.bg-secondary-100').length;
        const inactiveRules = document.querySelectorAll('span.bg-red-100').length;
        const totalRules = activeRules + inactiveRules;

        if (totalRules > 0) {
            const ruleData = {
                series: [activeRules, inactiveRules],
                chart: {
                    type: 'donut',
                    height: '100%',
                    toolbar: {
                        show: false
                    }
                },
                labels: ['Active Rules', 'Inactive Rules'],
                colors: ['#10B981', '#EF4444'], // Green and Red from Tailwind
                plotOptions: {
                    pie: {
                        donut: {
                            size: '70%',
                            labels: {
                                show: true,
                                name: {
                                    show: true,
                                    fontSize: '14px',
                                    fontFamily: 'inherit',
                                    offsetY: -4,
                                    color: '#374151'
                                },
                                value: {
                                    show: true,
                                    fontSize: '16px',
                                    fontFamily: 'inherit',
                                    fontWeight: 600,
                                    color: '#1F2937',
                                    formatter: function (val) {
                                        return val + ' rules'
                                    }
                                },
                                total: {
                                    show: true,
                                    label: 'Total Rules',
                                    fontSize: '14px',
                                    fontWeight: 600,
                                    color: '#374151',
                                    formatter: function (w) {
                                        const total = w.globals.seriesTotals.reduce((a, b) => a + b, 0)
                                        return total + ' rules'
                                    }
                                }
                            }
                        }
                    }
                },
                legend: {
                    show: false
                },
                dataLabels: {
                    enabled: false
                },
                tooltip: {
                    y: {
                        formatter: function (val) {
                            return val + " rules"
                        }
                    }
                },
                responsive: [{
                    breakpoint: 480,
                    options: {
                        chart: {
                            height: 200
                        }
                    }
                }]
            };

            const chart = new ApexCharts(document.querySelector("#rule-distribution-chart"), ruleData);
            chart.render();
        } else {
            // Show no data message
            document.querySelector("#rule-distribution-chart").innerHTML = `
                <div class="flex flex-col items-center justify-center h-full text-gray-500">
                    <i class="material-icons text-4xl mb-2">pie_chart</i>
                    <p class="text-sm">No data to display</p>
                </div>
            `;
        }
    }*/

    // Load schools on page load
    loadSchools();
});