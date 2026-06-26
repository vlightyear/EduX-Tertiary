document.addEventListener('DOMContentLoaded', function () {
    // Modal elements
    const createDepartmentModal = document.getElementById('createDepartmentModal');
    const updateDepartmentModal = document.getElementById('updateDepartmentModal');
    const deleteDepartmentModal = document.getElementById('deleteDepartmentModal');

    // Form elements
    const createDepartmentForm = document.getElementById('createDepartmentForm');
    const updateDepartmentForm = document.getElementById('updateDepartmentForm');
    const deleteDepartmentForm = document.getElementById('deleteDepartmentForm');

    // Button elements
    const confirmCreateBtn = document.getElementById('confirmCreate');
    const confirmUpdateBtn = document.getElementById('confirmUpdate');
    const confirmDeleteBtn = document.getElementById('confirmDelete');

    // Modal handling functions
    function showModal(modalElement) {
        modalElement.classList.remove('hidden');
        document.body.style.overflow = 'hidden';
    }

    function hideModal(modalElement) {
        modalElement.classList.add('hidden');
        document.body.style.overflow = 'auto';
    }

    // Close modal when clicking outside
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
    [createDepartmentModal, updateDepartmentModal, deleteDepartmentModal].forEach(setupModalCloseHandlers);

    // Show create modal
    window.showCreateDepartmentModal = function () {
        createDepartmentForm.reset();
        showModal(createDepartmentModal);
    };

    // Show update modal
    window.showUpdateDepartmentModal = async function (id) {
        try {
            const response = await fetch(`/Admin/GetDepartment/${id}`);
            if (!response.ok) {
                console.error('Error:', response.statusText);
                window.showAppToast('Error fetching department details');
                return;
            }
            const department = await response.json();

            // Populate the form
            document.getElementById('updateDepId').value = department.id;
            document.getElementById('updateName').value = department.name;
            document.getElementById('updateDescription').value = department.description;
            document.getElementById('updateSchoolId').value = department.schoolId;
            document.getElementById('updateHODId').value = department.hodId;

            showModal(updateDepartmentModal);
        } catch (error) {
            console.error('Error:', error);
            window.showAppToast('Error fetching department details');
        }
    };

    // Show delete modal
    window.showDeleteDepartmentModal = function (id, name) {
        document.getElementById('deleteDepId').value = id;
        document.getElementById('deleteDepName').textContent = name;
        showModal(deleteDepartmentModal);
    };

    // Handle form submissions
    confirmCreateBtn.addEventListener('click', function () {
        if (createDepartmentForm.checkValidity()) {
            createDepartmentForm.submit();
        } else {
            createDepartmentForm.reportValidity();
        }
    });

    confirmUpdateBtn.addEventListener('click', function () {
        if (updateDepartmentForm.checkValidity()) {
            updateDepartmentForm.submit();
        } else {
            updateDepartmentForm.reportValidity();
        }
    });

    confirmDeleteBtn.addEventListener('click', function () {
        deleteDepartmentForm.submit();
    });

    // Handle Escape key to close modals
    document.addEventListener('keydown', function (e) {
        if (e.key === 'Escape') {
            [createDepartmentModal, updateDepartmentModal, deleteDepartmentModal].forEach(modal => {
                if (!modal.classList.contains('hidden')) {
                    hideModal(modal);
                }
            });
        }
    });

    // Initialize DataTable
    const dataTable = new simpleDatatables.DataTable("#departmentsTable", {
        perPage: 10,
        searchable: true,
        sortable: true,
        labels: {
            placeholder: "Search...",
            perPage: "Show {select} entries",
            noRows: "No departments found",
            info: "Showing {start} to {end} of {rows} departments",
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

    // Initialize Distribution Chart
    const chartOptions = {
        series: [44, 55, 30], // Replace with actual data
        chart: {
            type: 'donut',
            height: '100%'
        },
        labels: ['Science', 'Arts', 'Engineering'], // Example labels
        colors: ['#4F46E5', '#10B981', '#F59E0B'],
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
                            offsetY: -4
                        },
                        value: {
                            show: true,
                            fontSize: '16px',
                            fontFamily: 'inherit',
                            formatter: function (val) {
                                return val + ' departments'
                            }
                        },
                        total: {
                            show: true,
                            label: 'Total Departments',
                            color: '#374151',
                            formatter: function (w) {
                                return w.globals.seriesTotals.reduce((a, b) => a + b, 0) + ' departments'
                            }
                        }
                    }
                }
            }
        },
        legend: {
            show: false
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

    const chart = new ApexCharts(document.querySelector("#department-distribution-chart"), chartOptions);
    chart.render();
});