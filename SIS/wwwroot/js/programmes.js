document.addEventListener('DOMContentLoaded', function () {
    // Modal elements
    const deleteProgrammeModal = document.getElementById('deleteProgrammeModal');

    // Form elements
    const deleteProgrammeForm = document.getElementById('deleteProgrammeForm');

    // Button elements
    const confirmDeleteBtn = document.getElementById('confirmDelete');
    const confirmDeleteCheckbox = document.getElementById('confirmDeleteCheckbox');

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

    // Setup close handlers for modal
    setupModalCloseHandlers(deleteProgrammeModal);

    // Show delete modal - make it global
    window.showDeleteProgrammeModal = function (id, name) {
        document.getElementById('deleteProgrammeId').value = id;
        document.getElementById('deleteProgrammeName').textContent = name;

        // Reset checkbox and button state
        confirmDeleteCheckbox.checked = false;
        confirmDeleteBtn.disabled = true;

        showModal(deleteProgrammeModal);
    };

    // Handle checkbox change to enable/disable delete button
    if (confirmDeleteCheckbox) {
        confirmDeleteCheckbox.addEventListener('change', function () {
            confirmDeleteBtn.disabled = !this.checked;
        });
    }

    // Handle form submission
    confirmDeleteBtn.addEventListener('click', function () {
        if (confirmDeleteCheckbox.checked) {
            deleteProgrammeForm.submit();
        }
    });

    // Handle Escape key to close modal
    document.addEventListener('keydown', function (e) {
        if (e.key === 'Escape' && !deleteProgrammeModal.classList.contains('hidden')) {
            hideModal(deleteProgrammeModal);
        }
    });

    // Initialize DataTable
    const dataTable = new simpleDatatables.DataTable("#programmesTable", {
        perPage: 10,
        searchable: true,
        sortable: true,
        labels: {
            placeholder: "Search programmes...",
            perPage: "Show {select} entries",
            noRows: "No programmes found",
            info: "Showing {start} to {end} of {rows} programmes",
        },
        classes: {
            wrapper: "datatable-wrapper",
            input: "datatable-input px-3 py-2 text-sm border border-gray-300 rounded-lg",
            selector: "datatable-selector px-3 py-2 text-sm border border-gray-300 rounded-lg",
            paginationButton: "px-3 py-1 text-sm text-gray-600 rounded-md transition-colors",
            paginationButtonActive: "bg-blue-50 text-blue-600 border border-blue-200",
            paginationButtonDisabled: "text-gray-400 cursor-not-allowed",
        }
    });

    // Initialize Distribution Chart with better colors
    const chartData = window.chartData || [];

    // Prepare data for the chart
    const series = chartData.map(item => item.count);
    const labels = chartData.map(item => item.level);

    // If no data, show default message
    if (series.length === 0) {
        series.push(1);
        labels.push('No Data');
    }

    // System-friendly color palette
    const systemColors = [
        '#4F46E5', // Primary - Indigo
        '#10B981', // Secondary - Emerald
        '#F59E0B', // Amber
        '#EF4444', // Red
        '#8B5CF6', // Purple
        '#06B6D4', // Cyan
        '#F97316', // Orange
        '#84CC16', // Lime
        '#EC4899', // Pink
        '#6B7280'  // Gray
    ];

    const chartOptions = {
        series: series,
        chart: {
            type: 'donut',
            height: '100%',
            fontFamily: 'inherit',
        },
        labels: labels,
        colors: systemColors,
        plotOptions: {
            pie: {
                donut: {
                    size: '75%',
                    labels: {
                        show: true,
                        name: {
                            show: true,
                            fontSize: '14px',
                            fontFamily: 'inherit',
                            fontWeight: 600,
                            color: '#374151',
                            offsetY: -4,
                            formatter: function (val) {
                                return val
                            }
                        },
                        value: {
                            show: true,
                            fontSize: '16px',
                            fontFamily: 'inherit',
                            fontWeight: 700,
                            color: '#111827',
                            offsetY: 4,
                            formatter: function (val) {
                                return val + ' programmes'
                            }
                        },
                        total: {
                            show: true,
                            showAlways: true,
                            label: 'Total',
                            fontSize: '14px',
                            fontWeight: 600,
                            color: '#6B7280',
                            formatter: function (w) {
                                const total = w.globals.seriesTotals.reduce((a, b) => a + b, 0);
                                return total + ' programmes'
                            }
                        }
                    }
                }
            }
        },
        dataLabels: {
            enabled: false
        },
        legend: {
            show: true,
            position: 'bottom',
            horizontalAlign: 'center',
            fontSize: '12px',
            fontFamily: 'inherit',
            fontWeight: 500,
            labels: {
                colors: '#374151',
                useSeriesColors: false
            },
            markers: {
                width: 8,
                height: 8,
                radius: 2,
            },
            itemMargin: {
                horizontal: 8,
                vertical: 4
            }
        },
        stroke: {
            show: true,
            width: 2,
            colors: ['#ffffff']
        },
        responsive: [{
            breakpoint: 480,
            options: {
                chart: {
                    height: 200
                },
                legend: {
                    fontSize: '10px'
                }
            }
        }],
        tooltip: {
            enabled: true,
            theme: 'light',
            style: {
                fontSize: '12px',
                fontFamily: 'inherit'
            },
            y: {
                formatter: function (val) {
                    return val + ' programmes'
                }
            }
        }
    };

    const chart = new ApexCharts(document.querySelector("#programme-distribution-chart"), chartOptions);
    chart.render();

    // Make the table responsive
    window.addEventListener('resize', () => {
        if (dataTable.initialized) {
            dataTable.refresh();
        }
    });
});