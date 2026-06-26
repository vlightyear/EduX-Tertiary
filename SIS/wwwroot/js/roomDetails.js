document.addEventListener('DOMContentLoaded', function () {
    // Initialize variables and elements
    const updateRoomModal = document.getElementById('updateRoomModal');
    const deleteRoomModal = document.getElementById('deleteRoomModal');
    const updateResourcesContainer = document.getElementById('updateResourcesContainer');

    // Initialize chart if it exists
    if (document.getElementById('occupancyChart')) {
        initializeOccupancyChart();
    }

    // Function to initialize the occupancy chart
    function initializeOccupancyChart() {
        const occupiedBeds = parseInt(document.getElementById('occupiedBedsCount').textContent);
        const availableBeds = parseInt(document.getElementById('availableBedsCount').textContent);

        const options = {
            series: [occupiedBeds, availableBeds],
            chart: {
                type: 'donut',
                height: 240
            },
            labels: ['Occupied', 'Available'],
            colors: ['#f59e0b', '#10b981'],
            legend: {
                position: 'bottom',
                fontFamily: 'inherit',
                fontSize: '12px'
            },
            plotOptions: {
                pie: {
                    donut: {
                        size: '65%'
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
                        position: 'bottom'
                    }
                }
            }]
        };

        const chart = new ApexCharts(document.getElementById('occupancyChart'), options);
        chart.render();
    }

    // Resource management in update modal
    if (document.getElementById('updateAddResourceBtn')) {
        document.getElementById('updateAddResourceBtn').addEventListener('click', addUpdateResourceRow);
    }

    // Add new resource row function for update modal
    function addUpdateResourceRow(resourceTypeId = '', quantity = 1, resourceId = 0) {
        const resourceItem = document.createElement('div');
        resourceItem.className = 'resource-item grid grid-cols-2 gap-4';

        // Get resource type options from any existing select
        const firstSelect = document.querySelector('.updateResourceType');
        const options = firstSelect ? firstSelect.innerHTML : '<option value="">Select Resource Type</option>';

        resourceItem.innerHTML = `
            <input type="hidden" name="resourceIds" value="${resourceId}" />
            <div class="relative z-0 w-full group">
                <select name="resourceTypeIds" class="updateResourceType block py-2.5 px-0 w-full text-sm text-gray-900 bg-transparent border-0 border-b-2 border-gray-300 appearance-none focus:outline-none focus:ring-0 focus:border-cyan-600 peer">
                    ${options}
                </select>
                <label class="peer-focus:font-medium absolute text-sm text-gray-500 duration-300 transform -translate-y-6 scale-75 top-3 -z-10 origin-[0] peer-focus:start-0 peer-focus:text-cyan-600 peer-placeholder-shown:scale-100 peer-placeholder-shown:translate-y-0 peer-focus:scale-75 peer-focus:-translate-y-6">
                    Resource Type
                </label>
            </div>
            <div class="relative z-0 w-full group flex items-center">
                <input type="number" name="resourceQuantities" class="updateResourceQuantity block py-2.5 px-0 w-full text-sm text-gray-900 bg-transparent border-0 border-b-2 border-gray-300 appearance-none focus:outline-none focus:ring-0 focus:border-cyan-600 peer" min="1" value="${quantity}" />
                <label class="peer-focus:font-medium absolute text-sm text-gray-500 duration-300 transform -translate-y-6 scale-75 top-3 -z-10 origin-[0] peer-focus:start-0 peer-focus:text-cyan-600 peer-placeholder-shown:scale-100 peer-placeholder-shown:translate-y-0 peer-focus:scale-75 peer-focus:-translate-y-6">
                    Quantity
                </label>
                <button type="button" class="updateRemoveResourceBtn ml-2 p-1 text-red-500 hover:text-red-700 transition-colors">
                    <i class="material-icons text-sm">remove_circle</i>
                </button>
            </div>
        `;

        updateResourcesContainer.appendChild(resourceItem);

        // Set the selected resource type
        if (resourceTypeId) {
            const select = resourceItem.querySelector('.updateResourceType');
            select.value = resourceTypeId;
        }
    }

    // Handle remove resource buttons in update modal
    document.addEventListener('click', function (e) {
        if (e.target.classList.contains('updateRemoveResourceBtn') || e.target.parentElement.classList.contains('updateRemoveResourceBtn')) {
            const resourceItem = e.target.closest('.resource-item');
            if (updateResourcesContainer.children.length > 1) {
                resourceItem.remove();
            } else {
                // Clear values if it's the last row
                const typeSelect = resourceItem.querySelector('.updateResourceType');
                const quantityInput = resourceItem.querySelector('.updateResourceQuantity');
                const resourceIdInput = resourceItem.querySelector('input[name="resourceIds"]');
                typeSelect.value = '';
                quantityInput.value = '1';
                resourceIdInput.value = '0';
            }
        }
    });

    // Handle bed space status toggle
    document.querySelectorAll('.bed-status-toggle').forEach(toggle => {
        toggle.addEventListener('click', async function () {
            const bedId = this.getAttribute('data-bed-id');
            const currentStatus = this.getAttribute('data-current-status');
            // Show confirmation or directly perform action depending on requirements
            if (currentStatus === 'Occupied') {
                if (await window.showAppConfirm('This bed is currently occupied. Are you sure you want to change its status?', {
                    title: 'Change Bed Status',
                    icon: 'warning',
                    confirmButtonText: 'Change'
                })) {
                    updateBedStatus(bedId);
                }
            } else {
                updateBedStatus(bedId);
            }
        });
    });

    // Function to update bed status (you'll need to implement the endpoint)
    function updateBedStatus(bedId) {
        // Implementation depends on your backend
        console.log(`Update bed status for bed ID: ${bedId}`);
        // Example:
        // fetch(`/Admin/UpdateBedStatus/${bedId}`, { method: 'POST' })
        //  .then(response => response.json())
        //  .then(data => {
        //    if (data.success) {
        //      window.location.reload();
        //    } else {
        //      window.showAppToast('Failed to update bed status: ' + data.message);
        //    }
        //  });
    }

    // Resource status update functionality
    document.querySelectorAll('.resource-status-toggle').forEach(toggle => {
        toggle.addEventListener('click', function () {
            const resourceId = this.getAttribute('data-resource-id');
            const currentStatus = this.getAttribute('data-current-status');

            // Implement resource status updating functionality
            console.log(`Toggle resource status: ${resourceId} from ${currentStatus}`);
        });
    });

    // Update Room Modal functionality
    window.showUpdateRoomModal = async function (id) {
        try {
            // Show loading indicator if it exists
            const loadingIndicator = document.getElementById('loadingIndicator');
            if (loadingIndicator) {
                loadingIndicator.style.display = 'flex';
            }

            // Fetch room data
            const response = await fetch(`/Admin/GetRoom/${id}`);
            if (!response.ok) {
                throw new Error('Failed to fetch room data');
            }

            const room = await response.json();

            // Set form values
            document.getElementById('updateRoomId').value = room.id;
            document.getElementById('updateRoomNumber').value = room.roomNumber;
            document.getElementById('updateFloor').value = room.floor;
            document.getElementById('updateRoomType').value = room.roomType;
            document.getElementById('updateCapacity').value = room.capacity;
            document.getElementById('updateGender').value = room.gender;
            document.getElementById('updateStatus').value = room.status;
            document.getElementById('updateIsSpecialReservation').checked = room.isSpecialReservation;

            // Update bed space info
            document.getElementById('currentCapacity').textContent = room.capacity;
            document.getElementById('occupiedBeds').textContent = room.bedSpaces.filter(b => b.status === 'Occupied').length;

            // Clear and populate resources
            if (updateResourcesContainer) {
                updateResourcesContainer.innerHTML = '';

                if (room.resources && room.resources.length > 0) {
                    room.resources.forEach(resource => {
                        addUpdateResourceRow(resource.resourceTypeId, resource.quantity, resource.id);
                    });
                } else {
                    // Add an empty row if no resources
                    addUpdateResourceRow();
                }
            }

            // Show the modal
            updateRoomModal.classList.remove('hidden');
        } catch (error) {
            console.error('Error fetching room data:', error);
            window.showAppToast(`Error loading room data: ${error.message}. Please try again.`);
        } finally {
            // Hide loading indicator if it exists
            const loadingIndicator = document.getElementById('loadingIndicator');
            if (loadingIndicator) {
                loadingIndicator.style.display = 'none';
            }
        }
    };

    window.hideUpdateRoomModal = function () {
        updateRoomModal.classList.add('hidden');
    };

    // Delete Room Modal functionality
    window.showDeleteRoomModal = async function (id) {
        try {
            // Show loading indicator if it exists
            const loadingIndicator = document.getElementById('loadingIndicator');
            if (loadingIndicator) {
                loadingIndicator.style.display = 'flex';
            }

            // Fetch room data
            const response = await fetch(`/Admin/GetRoom/${id}`);
            if (!response.ok) {
                throw new Error('Failed to fetch room data');
            }

            const room = await response.json();
            const occupiedBedCount = room.bedSpaces.filter(b => b.status === 'Occupied').length;

            // Set form values and display info
            document.getElementById('deleteRoomId').value = room.id;
            document.getElementById('deleteRoomNumber').textContent = `Room ${room.roomNumber}`;
            document.getElementById('deleteRoomType').textContent = room.roomType;
            document.getElementById('deleteRoomCapacity').textContent = `Capacity: ${room.capacity}`;
            document.getElementById('deleteRoomBedCount').textContent = `${room.bedSpaces.length} Beds`;
            document.getElementById('deleteRoomOccupiedBeds').textContent = `${occupiedBedCount} Occupied`;

            // Show warning if there are occupied beds
            const hasOccupiedBedsWarning = document.getElementById('hasOccupiedBedsWarning');
            const confirmDeleteBtn = document.getElementById('confirmDeleteBtn');

            if (occupiedBedCount > 0) {
                hasOccupiedBedsWarning.classList.remove('hidden');
                confirmDeleteBtn.disabled = true;
                confirmDeleteBtn.classList.add('opacity-50', 'cursor-not-allowed');
            } else {
                hasOccupiedBedsWarning.classList.add('hidden');
                confirmDeleteBtn.disabled = false;
                confirmDeleteBtn.classList.remove('opacity-50', 'cursor-not-allowed');
            }

            // Show the modal
            deleteRoomModal.classList.remove('hidden');
        } catch (error) {
            console.error('Error fetching room data:', error);
            window.showAppToast(`Error loading room data: ${error.message}. Please try again.`);
        } finally {
            // Hide loading indicator if it exists
            const loadingIndicator = document.getElementById('loadingIndicator');
            if (loadingIndicator) {
                loadingIndicator.style.display = 'none';
            }
        }
    };

    window.hideDeleteRoomModal = function () {
        deleteRoomModal.classList.add('hidden');
    };

    // Handle Escape key to close modals
    document.addEventListener('keydown', function (e) {
        if (e.key === 'Escape') {
            hideUpdateRoomModal();
            hideDeleteRoomModal();
        }
    });
});
