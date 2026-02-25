document.addEventListener('DOMContentLoaded', function () {
    // Modal elements
    const createModeModal = document.getElementById('createModeModal');
    const updateModeModal = document.getElementById('updateModeModal');
    const deleteModeModal = document.getElementById('deleteModeModal');

    // Form elements
    const createModeForm = document.getElementById('createModeForm');
    const updateModeForm = document.getElementById('updateModeForm');
    const deleteModeForm = document.getElementById('deleteModeForm');

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
    [createModeModal, updateModeModal, deleteModeModal].forEach(setupModalCloseHandlers);

    // Show create modal
    window.showCreateModeModal = function () {
        createModeForm.reset();
        showModal(createModeModal);
    };

    // Show update modal
    window.showUpdateModeModal = function (id, name, code) {
        document.getElementById('updateModeId').value = id;
        document.getElementById('updateModeName').value = name;
        document.getElementById('updateCode').value = code;
        showModal(updateModeModal);
    };

    // Show delete modal
    window.showDeleteModeModal = function (id, name) {
        document.getElementById('deleteModeId').value = id;
        document.getElementById('deleteModeName').textContent = name;
        showModal(deleteModeModal);
    };

    // Handle form submissions
    confirmCreateBtn.addEventListener('click', function () {
        if (createModeForm.checkValidity()) {
            createModeForm.submit();
        } else {
            createModeForm.reportValidity();
        }
    });

    confirmUpdateBtn.addEventListener('click', function () {
        if (updateModeForm.checkValidity()) {
            updateModeForm.submit();
        } else {
            updateModeForm.reportValidity();
        }
    });

    confirmDeleteBtn.addEventListener('click', function () {
        deleteModeForm.submit();
    });

    // Handle Escape key to close modals
    document.addEventListener('keydown', function (e) {
        if (e.key === 'Escape') {
            [createModeModal, updateModeModal, deleteModeModal].forEach(modal => {
                if (!modal.classList.contains('hidden')) {
                    hideModal(modal);
                }
            });
        }
    });

    // Make the table responsive
    window.addEventListener('resize', () => {
        dataTable.columns().rebuild();
    });

    // Initialize Mode Statistics Chart
    const modeData = [
        { mode: "Full Time", students: 1500 },
        { mode: "Part Time", students: 750 },
        { mode: "Distance", students: 201 }
    ];

    const modeStatsChart = new ApexCharts(document.querySelector("#modeStatsChart"), {
        series: modeData.map(item => item.students),
        chart: {
            type: 'donut',
            height: '100%'
        },
        labels: modeData.map(item => item.mode),
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
                                return val + ' students'
                            }
                        },
                        total: {
                            show: true,
                            label: 'Total Students',
                            color: '#373d3f',
                            formatter: function (w) {
                                const total = w.globals.seriesTotals.reduce((a, b) => a + b, 0)
                                return total + ' students'
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
        responsive: [{
            breakpoint: 480,
            options: {
                chart: {
                    height: 200
                }
            }
        }]
    });

    modeStatsChart.render();

    // Initialize DataTable
    const dataTable = new simpleDatatables.DataTable("#modesTable", {
        perPage: 15,
        searchable: true,
        sortable: true,
        labels: {
            placeholder: "Search...",
            perPage: "Show {select} entries",
            noRows: "No modes found",
            info: "Showing {start} to {end} of {rows} modes",
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
});