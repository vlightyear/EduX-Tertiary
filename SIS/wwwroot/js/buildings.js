document.addEventListener('DOMContentLoaded', function ()
{
    // Modal elements
    const createBuildingModal = document.getElementById('createBuildingModal');
    const updateBuildingModal = document.getElementById('updateBuildingModal');
    const deleteBuildingModal = document.getElementById('deleteBuildingModal');

    // Form elements
    const createBuildingForm = document.getElementById('createBuildingForm');
    const updateBuildingForm = document.getElementById('updateBuildingForm');
    const deleteBuildingForm = document.getElementById('deleteBuildingForm');

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
    [createBuildingModal, updateBuildingModal, deleteBuildingModal].forEach(setupModalCloseHandlers);

    // Show create modal
    window.showCreateBuildingModal = function () {
        createBuildingForm.reset();
        showModal(createBuildingModal);
    };

    // Show update modal
    window.showUpdateBuildingModal = function (id, name, description, schoolId) {
        document.getElementById('updateBuildingId').value = id;
        document.getElementById('updateBuildingName').value = name;
        document.getElementById('updateDescription').value = description;
        document.getElementById('updateSchoolId').value = schoolId;
        showModal(updateBuildingModal);
    };

    // Show delete modal
    window.showDeleteBuildingModal = function (id, name) {
        document.getElementById('deleteBuildingId').value = id;
        document.getElementById('deleteBuildingName').textContent = name;
        showModal(deleteBuildingModal);
    };

    // Handle form submissions
    confirmCreateBtn.addEventListener('click', function () {
        if (createBuildingForm.checkValidity()) {
            createBuildingForm.submit();
        } else {
            createBuildingForm.reportValidity();
        }
    });

    confirmUpdateBtn.addEventListener('click', function () {
        if (updateBuildingForm.checkValidity()) {
            updateBuildingForm.submit();
        } else {
            updateBuildingForm.reportValidity();
        }
    });

    confirmDeleteBtn.addEventListener('click', function () {
        deleteBuildingForm.submit();
    });

    // Handle Escape key to close modals
    document.addEventListener('keydown', function (e) {
        if (e.key === 'Escape') {
            [createBuildingModal, updateBuildingModal, deleteBuildingModal].forEach(modal => {
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



    // Initialize Building Statistics Chart
    const buildingData = [
        { school: "Main Campus", buildings: 5 },
        { school: "East Wing", buildings: 3 },
        { school: "West Campus", buildings: 4 },
        { school: "South Block", buildings: 2 },
        { school: "North Extension", buildings: 3 }
    ];

    const buildingStatsChart = new ApexCharts(document.querySelector("#buildingStatsChart"), {
        series: buildingData.map(item => item.buildings),
        chart: {
            type: 'donut',
            height: '100%'
        },
        labels: buildingData.map(item => item.school),
        colors: ['#4F46E5', '#10B981', '#F59E0B', '#EF4444', '#8B5CF6'],
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
                                return val + ' buildings'
                            }
                        },
                        total: {
                            show: true,
                            label: 'Total Buildings',
                            color: '#373d3f',
                            formatter: function (w) {
                                const total = w.globals.seriesTotals.reduce((a, b) => a + b, 0)
                                return total + ' buildings'
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

    buildingStatsChart.render();

    // Initialize DataTable
    console.log('Test');
    const dataTable = new simpleDatatables.DataTable("#buildingTable", {
        perPage: 15,
        searchable: true,
        sortable: true,
        labels: {
            placeholder: "Search...",
            perPage: "Show {select} entries",
            noRows: "No buildings found",
            info: "Showing {start} to {end} of {rows} buildings",
        },
        classes: {
            wrapper: "datatable-wrapper",
            input: "datatable-input px-3 py-2 text-sm border border-gray-300 rounded-lg",
            selector: "datatable-selector px-3 py-2 text-sm border border-gray-300 rounded-lg",
            paginationButton: "px-3 py-1 text-sm text-gray-600 hover:bg-gray-100 rounded-md",
            paginationButtonActive: "bg-blue-50 text-blue-600 hover:bg-blue-100",
            paginationButtonDisabled: "text-gray-400 hover:bg-transparent cursor-not-allowed",
        }
    });

    console.log('Test');
});