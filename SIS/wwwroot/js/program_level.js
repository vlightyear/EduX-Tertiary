document.addEventListener('DOMContentLoaded', function () {
    // Modal elements
    const createLevelModal = document.getElementById('createLevelModal');
    const updateLevelModal = document.getElementById('updateLevelModal');
    const deleteLevelModal = document.getElementById('deleteLevelModal');

    // Form elements
    const createLevelForm = document.getElementById('createLevelForm');
    const updateLevelForm = document.getElementById('updateLevelForm');
    const deleteLevelForm = document.getElementById('deleteLevelForm');

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
    [createLevelModal, updateLevelModal, deleteLevelModal].forEach(setupModalCloseHandlers);

    // Show create modal
    window.showCreateLevelModal = function () {
        createLevelForm.reset();
        showModal(createLevelModal);
    };

    // Show update modal
    window.showUpdateLevelModal = function (id, name, description, rank, isActive) {
        document.getElementById('updateLevelId').value = id;
        document.getElementById('updateLevelName').value = name;
        document.getElementById('updateDescription').value = description;
        document.getElementById('updateRank').value = rank;
        document.getElementById('updateIsActive').checked = isActive === 'true';
        showModal(updateLevelModal);
    };

    // Show delete modal
    window.showDeleteLevelModal = function (id, name, rank) {
        document.getElementById('deleteLevelId').value = id;
        document.getElementById('deleteLevelName').textContent = name;
        document.getElementById('deleteLevelRank').textContent = rank;
        showModal(deleteLevelModal);
    };

    // Handle form submissions
    confirmCreateBtn.addEventListener('click', function () {
        if (createLevelForm.checkValidity()) {
            createLevelForm.submit();
        } else {
            createLevelForm.reportValidity();
        }
    });

    confirmUpdateBtn.addEventListener('click', function () {
        if (updateLevelForm.checkValidity()) {
            updateLevelForm.submit();
        } else {
            updateLevelForm.reportValidity();
        }
    });

    confirmDeleteBtn.addEventListener('click', function () {
        deleteLevelForm.submit();
    });

    // Handle Escape key to close modals
    document.addEventListener('keydown', function (e) {
        if (e.key === 'Escape') {
            [createLevelModal, updateLevelModal, deleteLevelModal].forEach(modal => {
                if (!modal.classList.contains('hidden')) {
                    hideModal(modal);
                }
            });
        }
    });

    // Initialize DataTable
    const dataTable = new simpleDatatables.DataTable("#programLevelTable", {
        perPage: 10,
        searchable: true,
        sortable: true,
        labels: {
            placeholder: "Search...",
            perPage: "Show {select} entries",
            noRows: "No program levels found",
            info: "Showing {start} to {end} of {rows} levels",
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

    // Level Distribution Chart
    const levelData = {
        "Total Program Levels": document.querySelectorAll('#programLevelTable tbody tr').length,
        "Active Levels": document.querySelectorAll('span.bg-green-100').length,
        "Inactive Levels": document.querySelectorAll('span.bg-red-100').length
    };

    const chartOptions = {
        series: Object.values(levelData),
        chart: {
            type: 'donut',
            height: '90%'
        },
        labels: Object.keys(levelData),
        colors: ['#4F46E5', '#10B981', '#EF4444'],
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
                                return val + ' levels'
                            }
                        },
                        total: {
                            show: true,
                            label: 'Total Levels',
                            color: '#374151',
                            formatter: function (w) {
                                return w.globals.seriesTotals.reduce((a, b) => a + b, 0) + ' levels'
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

    const chart = new ApexCharts(document.querySelector("#level-distribution-chart"), chartOptions);
    chart.render();
});