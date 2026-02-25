// Chart initialization - Only runs if the element exists
document.addEventListener('DOMContentLoaded', function () {
    const chartElement = document.querySelector("#fee-summary-chart");
    if (chartElement) {
        // Data from the Fee Configuration Summary Info
        const feeData = {
            "Total Fee Configurations": 10,
            "Active Configurations": 7,
            "Inactive Configurations": 3,
            "Pending Configurations": 2
        };

        const feeSeries = Object.values(feeData);
        const feeLabels = Object.keys(feeData);

        const feeChartOptions = {
            series: feeSeries,
            chart: {
                type: 'donut',
                height: 250
            },
            labels: feeLabels,
            colors: ['#172554', '#155e75', '#4b5563', '#94a3b8'],
            legend: {
                show: false
            },
            responsive: [{
                breakpoint: 400,
                options: {
                    chart: {
                        width: 220,
                    },
                }
            }],
        };

        var feeChart = new ApexCharts(chartElement, feeChartOptions);
        feeChart.render();
    }

    // Delete modal functionality - Only runs if the modal exists
    const deleteModal = document.getElementById('deleteFeeModal');
    if (deleteModal) {
        const confirmDeleteBtn = document.getElementById('confirmDelete');

        // Function to show modal with fee configuration details
        window.showDeleteFeeModal = function (id, academicYear, programme, amount) {
            document.getElementById('deleteFeeId').value = id;
            document.getElementById('deleteAcademicYear').textContent = academicYear;
            document.getElementById('deleteProgramme').textContent = programme;
            document.getElementById('deleteAmount').textContent = amount;

            const modal = new bootstrap.Modal(deleteModal);
            modal.show();
        };

        // Handle delete confirmation
        if (confirmDeleteBtn) {
            confirmDeleteBtn.addEventListener('click', function () {
                const form = document.getElementById('deleteFeeForm');
                if (form) form.submit();
            });
        }
    }

    // Form fields toggle functionality - Only runs if the checkbox exists
    const universalCheckbox = document.getElementById('appliesUniversally');
    if (universalCheckbox) {
        function toggleFields(isDisabled) {
            const fieldsToToggle = ['SchoolId', 'ProgrammeId', 'ModeOfStudyId', 'YearOfStudy', 'ProgramLevelId'];

            fieldsToToggle.forEach(fieldId => {
                const element = document.getElementById(fieldId);
                if (element) {
                    element.disabled = isDisabled;
                    if (isDisabled) {
                        element.value = '';
                    }
                }
            });
        }

        // Initialize fields based on initial checkbox state
        toggleFields(universalCheckbox.checked);

        // Add change event listener
        universalCheckbox.addEventListener('change', function () {
            toggleFields(this.checked);
        });
    }
});