document.addEventListener('DOMContentLoaded', function () {
    // Initialize variables and elements
    const searchInput = document.getElementById('searchInput');
    const filterUsage = document.getElementById('filterUsage');
    const resourceTypesTable = document.getElementById('resourceTypesTable');
    const resourceTypesTableBody = document.getElementById('resourceTypesTableBody');
    const emptyStateMessage = document.getElementById('emptyStateMessage');
    const totalResourceTypes = document.getElementById('totalResourceTypes');
    const totalResources = document.getElementById('totalResources');
    const activeResourceTypes = document.getElementById('activeResourceTypes');

    // Modal elements
    const createResourceTypeModal = document.getElementById('createResourceTypeModal');
    const updateResourceTypeModal = document.getElementById('updateResourceTypeModal');
    const deleteResourceTypeModal = document.getElementById('deleteResourceTypeModal');

    // Form elements
    const createResourceTypeForm = document.getElementById('createResourceTypeForm');
    const updateResourceTypeForm = document.getElementById('updateResourceTypeForm');
    const deleteResourceTypeForm = document.getElementById('deleteResourceTypeForm');

    // Resource type filtering
    searchInput.addEventListener('input', filterTable);
    filterUsage.addEventListener('change', filterTable);

    // Setup sorting buttons
    document.querySelectorAll('.sort-btn').forEach(button => {
        button.addEventListener('click', function () {
            const sortKey = this.getAttribute('data-sort');
            sortTable(sortKey);
        });
    });

    // Initialize chart if we have data
    if (resourceTypesTableBody.children.length > 0) {
        initializeResourceUsageChart();
    }

    // Table filtering function
    function filterTable() {
        const searchTerm = searchInput.value.toLowerCase();
        const filterValue = filterUsage.value;
        let visibleRows = 0;
        let visibleResources = 0;
        let visibleActive = 0;

        // Get all rows
        const rows = resourceTypesTableBody.querySelectorAll('tr');

        rows.forEach(row => {
            const name = row.children[1].textContent.toLowerCase();
            const description = row.children[2].textContent.toLowerCase();
            const usageCount = parseInt(row.children[3].textContent.trim());
            const usageStatus = row.getAttribute('data-usage');

            // Combine search term and filter conditions
            let showRow = (name.includes(searchTerm) || description.includes(searchTerm));

            if (filterValue === 'used' && usageStatus !== 'used') {
                showRow = false;
            } else if (filterValue === 'unused' && usageStatus !== 'unused') {
                showRow = false;
            }

            // Show/hide the row
            row.style.display = showRow ? '' : 'none';
            if (showRow) {
                visibleRows++;
                visibleResources += usageCount;
                if (usageCount > 0) {
                    visibleActive++;
                }
            }
        });

        // Update stats for filtered view
        totalResourceTypes.textContent = visibleRows;
        // Don't update totalResources as it's from the server-side

        // Show empty state if no visible rows
        emptyStateMessage.style.display = visibleRows === 0 ? 'flex' : 'none';
        resourceTypesTable.style.display = visibleRows === 0 ? 'none' : '';
    }

    // Table sorting function
    function sortTable(key) {
        const rows = Array.from(resourceTypesTableBody.querySelectorAll('tr'));

        // Define sort functions for different columns
        const sortFunctions = {
            'index': (a, b) => {
                return parseInt(a.children[0].textContent) - parseInt(b.children[0].textContent);
            },
            'name': (a, b) => {
                return a.children[1].textContent.localeCompare(b.children[1].textContent);
            },
            'usage': (a, b) => {
                return parseInt(a.children[3].textContent) - parseInt(b.children[3].textContent);
            }
        };

        // Sort the rows
        rows.sort(sortFunctions[key]);

        // Check if we need to reverse the order (if already sorted)
        const button = document.querySelector(`.sort-btn[data-sort="${key}"]`);
        if (button.classList.contains('sorted-asc')) {
            rows.reverse();
            button.classList.remove('sorted-asc');
            button.classList.add('sorted-desc');
        } else {
            // Clear all other sort classes
            document.querySelectorAll('.sort-btn').forEach(btn => {
                btn.classList.remove('sorted-asc');
                btn.classList.remove('sorted-desc');
            });
            button.classList.add('sorted-asc');
        }

        // Reorder the table
        resourceTypesTableBody.innerHTML = '';
        rows.forEach(row => resourceTypesTableBody.appendChild(row));
    }

    // Initialize ApexCharts donut chart with resource type data
    function initializeResourceUsageChart() {
        const chartContainer = document.getElementById('resourceUsageChart');

        // Check if we have actual data
        const rows = resourceTypesTableBody.querySelectorAll('tr');
        if (rows.length === 0) {
            chartContainer.innerHTML = `
                <div class="flex flex-col items-center justify-center h-full text-slate-500">
                    <p class="text-sm">Chart data will appear here once resource types are added.</p>
                </div>
            `;
            return;
        }

        // Count used and unused resource types
        let usedResourceTypeCount = 0;
        let unusedResourceTypeCount = 0;

        rows.forEach(row => {
            const usageStatus = row.getAttribute('data-usage');
            if (usageStatus === 'used') {
                usedResourceTypeCount++;
            } else {
                unusedResourceTypeCount++;
            }
        });

        const options = {
            series: [usedResourceTypeCount, unusedResourceTypeCount],
            chart: {
                type: 'donut',
                height: '90%'
            },
            labels: ['In Use', 'Not Used'],
            colors: ['#10B981', '#9CA3AF'],
            plotOptions: {
                pie: {
                    donut: {
                        size: '65%',
                        labels: {
                            show: true,
                            name: {
                                show: true
                            },
                            value: {
                                show: true,
                                formatter: function (val) {
                                    return val;
                                }
                            },
                            total: {
                                show: true,
                                label: 'Total',
                                formatter: function (w) {
                                    return w.globals.seriesTotals.reduce((a, b) => a + b, 0);
                                }
                            }
                        }
                    }
                }
            },
            dataLabels: {
                enabled: false
            },
            responsive: [{
                breakpoint: 480,
                options: {
                    chart: {
                        height: 200
                    },
                    legend: {
                        show: false
                    }
                }
            }],
            legend: {
                show: false
            }
        };

        // Create the chart
        const chart = new ApexCharts(chartContainer, options);
        chart.render();
    }

    // Modal functions
    window.showCreateResourceTypeModal = function () {
        createResourceTypeForm.reset();
        createResourceTypeModal.classList.remove('hidden');
    };

    window.hideCreateResourceTypeModal = function () {
        createResourceTypeModal.classList.add('hidden');
    };

    window.showUpdateResourceTypeModal = async function (id) {
        try {
            // Show loading state
            document.getElementById('loadingIndicator').style.display = 'flex';

            const response = await fetch(`/Admin/GetResourceType/${id}`);
            if (!response.ok) {
                throw new Error('Failed to fetch resource type data');
            }

            const resourceType = await response.json();

            // Populate form fields
            document.getElementById('updateResourceTypeId').value = resourceType.id;
            document.getElementById('updateName').value = resourceType.name;
            document.getElementById('updateDescription').value = resourceType.description || '';

            // Show modal
            updateResourceTypeModal.classList.remove('hidden');
        } catch (error) {
            console.error('Error fetching resource type details:', error);
            window.showAppToast('Error loading resource type details. Please try again.');
        } finally {
            document.getElementById('loadingIndicator').style.display = 'none';
        }
    };

    window.hideUpdateResourceTypeModal = function () {
        updateResourceTypeModal.classList.add('hidden');
    };

    window.showDeleteResourceTypeModal = function (id, name, usageCount) {
        document.getElementById('deleteResourceTypeId').value = id;
        document.getElementById('deleteResourceTypeName').textContent = name;
        document.getElementById('deleteResourceTypeUsageCount').textContent = `Used in ${usageCount} rooms`;

        // Show warning if resource type is in use
        const inUseWarning = document.getElementById('inUseWarning');
        const confirmDeleteBtn = document.getElementById('confirmDeleteBtn');

        if (parseInt(usageCount) > 0) {
            inUseWarning.classList.remove('hidden');
            confirmDeleteBtn.disabled = true;
            confirmDeleteBtn.classList.add('opacity-50', 'cursor-not-allowed');
        } else {
            inUseWarning.classList.add('hidden');
            confirmDeleteBtn.disabled = false;
            confirmDeleteBtn.classList.remove('opacity-50', 'cursor-not-allowed');
        }

        deleteResourceTypeModal.classList.remove('hidden');
    };

    window.hideDeleteResourceTypeModal = function () {
        deleteResourceTypeModal.classList.add('hidden');
    };

    // Function to create common resource types
    window.createCommonResourceType = function (name, description) {
        // Check if this resource type already exists
        const rows = resourceTypesTableBody.querySelectorAll('tr');
        for (let i = 0; i < rows.length; i++) {
            const existingName = rows[i].children[1].textContent.trim();
            if (existingName.toLowerCase() === name.toLowerCase()) {
                window.showAppToast(`Resource type "${name}" already exists.`);
                return;
            }
        }

        // Fill the form
        document.getElementById('Name').value = name;
        document.getElementById('Description').value = description;

        // Show the modal
        showCreateResourceTypeModal();
    };

    // Handle Escape key to close modals
    document.addEventListener('keydown', function (e) {
        if (e.key === 'Escape') {
            hideCreateResourceTypeModal();
            hideUpdateResourceTypeModal();
            hideDeleteResourceTypeModal();
        }
    });

    console.log('ResourceTypes initialized');
});