document.addEventListener('DOMContentLoaded', function () {
    // Room search and filtering
    const roomSearchInput = document.getElementById('roomSearchInput');
    const roomTypeFilter = document.getElementById('roomTypeFilter');
    const roomStatusFilter = document.getElementById('roomStatusFilter');
    const roomsGridContainer = document.getElementById('roomsGridContainer');
    const emptyRoomsMessage = document.getElementById('emptyRoomsMessage');

    // Initialize room type chart if we have data
    if (document.getElementById('roomTypeChart')) {
        initializeRoomTypeChart();
    }

    // Room filtering
    if (roomSearchInput && roomTypeFilter && roomStatusFilter) {
        roomSearchInput.addEventListener('input', filterRooms);
        roomTypeFilter.addEventListener('change', filterRooms);
        roomStatusFilter.addEventListener('change', filterRooms);
    }

    // Room filtering function
    function filterRooms() {
        const searchTerm = roomSearchInput.value.toLowerCase();
        const typeFilter = roomTypeFilter.value.toLowerCase();
        const statusFilter = roomStatusFilter.value;

        const roomCards = roomsGridContainer.querySelectorAll('.room-card');
        let visibleRooms = 0;

        roomCards.forEach(card => {
            const roomNumber = card.getAttribute('data-room-number').toLowerCase();
            const roomType = card.getAttribute('data-room-type').toLowerCase();
            const roomStatus = card.getAttribute('data-room-status');

            // Check if room matches all filters
            const matchesSearch = roomNumber.includes(searchTerm);
            const matchesType = typeFilter === 'all' || roomType === typeFilter;
            const matchesStatus = statusFilter === 'all' || roomStatus === statusFilter;

            // Show/hide the room card
            if (matchesSearch && matchesType && matchesStatus) {
                card.style.display = '';
                visibleRooms++;
            } else {
                card.style.display = 'none';
            }
        });

        // Show empty message if no rooms match filters
        if (visibleRooms === 0 && roomCards.length > 0) {
            emptyRoomsMessage.style.display = 'flex';
        } else {
            emptyRoomsMessage.style.display = 'none';
        }
    }

    // Room type chart initialization
    function initializeRoomTypeChart() {
        const chartContainer = document.getElementById('roomTypeChart');

        // Get the room type distribution data from the server
        const roomTypeDistributionElement = document.getElementById('roomTypeDistribution');
        if (!roomTypeDistributionElement || !roomTypeDistributionElement.textContent) {
            chartContainer.innerHTML = '<div class="flex items-center justify-center h-full text-slate-500"><p>No room data available</p></div>';
            return;
        }

        // Parse the distribution data (this assumes your server is passing the data in a specific format)
        // You might need to adjust this depending on how your data is structured
        const roomTypeDistribution = JSON.parse(roomTypeDistributionElement.textContent);

        if (!roomTypeDistribution || roomTypeDistribution.length === 0) {
            chartContainer.innerHTML = '<div class="flex items-center justify-center h-full text-slate-500"><p>No room type data available</p></div>';
            return;
        }

        // Format data for ApexCharts
        const labels = roomTypeDistribution.map(item => item.type);
        const data = roomTypeDistribution.map(item => item.count);

        // Define colors for different room types
        const colors = {
            'Single': '#38bdf8', // cyan-400
            'Double': '#a78bfa', // violet-400
            'Triple': '#fb923c', // orange-400
            'Suite': '#34d399'   // emerald-400
        };

        // Use default colors for any types not defined above
        const defaultColors = ['#38bdf8', '#a78bfa', '#fb923c', '#34d399', '#f87171'];
        const colorArray = labels.map((label, index) => colors[label] || defaultColors[index % defaultColors.length]);

        const options = {
            series: data,
            chart: {
                type: 'donut',
                height: '100%'
            },
            labels: labels,
            colors: colorArray,
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

        const chart = new ApexCharts(chartContainer, options);
        chart.render();
    }

    // Create Room Modal
    // ----------------------------

    // Resource management in create modal
    const addResourceBtn = document.getElementById('addResourceBtn');
    const resourcesContainer = document.getElementById('resourcesContainer');

    if (addResourceBtn) {
        addResourceBtn.addEventListener('click', addResourceRow);
    }

    // Handle remove resource buttons
    document.addEventListener('click', function (e) {
        if (e.target.classList.contains('removeResourceBtn') || e.target.parentElement.classList.contains('removeResourceBtn')) {
            const resourceItem = e.target.closest('.resource-item');
            if (resourcesContainer.children.length > 1) {
                resourceItem.remove();
            } else {
                // Clear values if it's the last row
                const typeSelect = resourceItem.querySelector('.resourceType');
                const quantityInput = resourceItem.querySelector('.resourceQuantity');
                typeSelect.value = '';
                quantityInput.value = '1';
            }
        }
    });

    // Add new resource row function
    function addResourceRow() {
        const resourceItem = document.createElement('div');
        resourceItem.className = 'resource-item grid grid-cols-2 gap-4';

        // Get resource type options from the first row
        const firstSelect = document.querySelector('.resourceType');
        const options = firstSelect ? firstSelect.innerHTML : '<option value="">Select Resource Type</option>';

        resourceItem.innerHTML = `
            <div class="relative z-0 w-full group">
                <select name="resourceTypeIds" class="resourceType block py-2.5 px-0 w-full text-sm text-gray-900 bg-transparent border-0 border-b-2 border-gray-300 appearance-none focus:outline-none focus:ring-0 focus:border-cyan-600 peer">
                    ${options}
                </select>
                <label class="peer-focus:font-medium absolute text-sm text-gray-500 duration-300 transform -translate-y-6 scale-75 top-3 -z-10 origin-[0] peer-focus:start-0 peer-focus:text-cyan-600 peer-placeholder-shown:scale-100 peer-placeholder-shown:translate-y-0 peer-focus:scale-75 peer-focus:-translate-y-6">
                    Resource Type
                </label>
            </div>
            <div class="relative z-0 w-full group flex items-center">
                <input type="number" name="resourceQuantities" class="resourceQuantity block py-2.5 px-0 w-full text-sm text-gray-900 bg-transparent border-0 border-b-2 border-gray-300 appearance-none focus:outline-none focus:ring-0 focus:border-cyan-600 peer" min="1" value="1" />
                <label class="peer-focus:font-medium absolute text-sm text-gray-500 duration-300 transform -translate-y-6 scale-75 top-3 -z-10 origin-[0] peer-focus:start-0 peer-focus:text-cyan-600 peer-placeholder-shown:scale-100 peer-placeholder-shown:translate-y-0 peer-focus:scale-75 peer-focus:-translate-y-6">
                    Quantity
                </label>
                <button type="button" class="removeResourceBtn ml-2 p-1 text-red-500 hover:text-red-700 transition-colors">
                    <i class="material-icons text-sm">remove_circle</i>
                </button>
            </div>
        `;

        resourcesContainer.appendChild(resourceItem);
    }

    // Room template application
    window.applyTemplate = function (templateType) {
        // Set the room type field
        const roomTypeSelect = document.getElementById('RoomType');
        if (roomTypeSelect) {
            switch (templateType) {
                case 'single':
                    roomTypeSelect.value = 'Single';
                    document.getElementById('Capacity').value = '1';
                    break;
                case 'double':
                    roomTypeSelect.value = 'Double';
                    document.getElementById('Capacity').value = '2';
                    break;
                case 'suite':
                    roomTypeSelect.value = 'Suite';
                    document.getElementById('Capacity').value = '4';
                    break;
            }
        }

        // Clear current resources
        while (resourcesContainer.children.length > 1) {
            resourcesContainer.removeChild(resourcesContainer.lastChild);
        }

        // Reset the first row
        const firstRow = resourcesContainer.children[0];
        if (firstRow) {
            firstRow.querySelector('.resourceType').value = '';
            firstRow.querySelector('.resourceQuantity').value = '1';
        }

        // Add template resources
        const resourceTypes = document.querySelectorAll('.resourceType option');
        const resourceTypeMap = {};

        // Build a map of resource names to their IDs
        resourceTypes.forEach(option => {
            if (option.textContent && option.value) {
                resourceTypeMap[option.textContent.toLowerCase()] = option.value;
            }
        });

        // Define templates
        const templates = {
            'single': [
                { name: 'bed', quantity: 1 },
                { name: 'chair', quantity: 1 },
                { name: 'table', quantity: 1 },
                { name: 'lamp', quantity: 1 }
            ],
            'double': [
                { name: 'bed', quantity: 2 },
                { name: 'chair', quantity: 2 },
                { name: 'table', quantity: 2 },
                { name: 'lamp', quantity: 2 }
            ],
            'suite': [
                { name: 'bed', quantity: 4 },
                { name: 'chair', quantity: 4 },
                { name: 'table', quantity: 2 },
                { name: 'lamp', quantity: 4 },
                { name: 'fan', quantity: 1 }
            ]
        };

        // Apply the template
        const selectedTemplate = templates[templateType] || [];

        selectedTemplate.forEach((item, index) => {
            if (index === 0 && firstRow) {
                // Update the first row
                const select = firstRow.querySelector('.resourceType');
                const quantity = firstRow.querySelector('.resourceQuantity');

                // Find the ID that matches the resource name
                for (const optionValue in resourceTypeMap) {
                    if (optionValue.includes(item.name.toLowerCase())) {
                        select.value = resourceTypeMap[optionValue];
                        break;
                    }
                }

                quantity.value = item.quantity;
            } else {
                // Add new rows for additional resources
                addResourceRow();
                const newRow = resourcesContainer.lastChild;
                const select = newRow.querySelector('.resourceType');
                const quantity = newRow.querySelector('.resourceQuantity');

                // Find the ID that matches the resource name
                for (const optionValue in resourceTypeMap) {
                    if (optionValue.includes(item.name.toLowerCase())) {
                        select.value = resourceTypeMap[optionValue];
                        break;
                    }
                }

                quantity.value = item.quantity;
            }
        });
    };

    // Show/hide modal functions
    window.showCreateRoomModal = function () {
        document.getElementById('createRoomModal').classList.remove('hidden');
        document.getElementById('createRoomForm').reset();

        // Clear additional resource rows
        const resourceItems = document.querySelectorAll('.resource-item');
        for (let i = 1; i < resourceItems.length; i++) {
            resourceItems[i].remove();
        }

        // Reset the first resource row
        const firstRow = document.querySelector('.resource-item');
        if (firstRow) {
            firstRow.querySelector('.resourceType').value = '';
            firstRow.querySelector('.resourceQuantity').value = '1';
        }
    };

    window.hideCreateRoomModal = function () {
        document.getElementById('createRoomModal').classList.add('hidden');
    };

    // Update Room Modal
    // ----------------------------

    // Resource management in update modal
    const updateAddResourceBtn = document.getElementById('updateAddResourceBtn');
    const updateResourcesContainer = document.getElementById('updateResourcesContainer');

    if (updateAddResourceBtn) {
        updateAddResourceBtn.addEventListener('click', addUpdateResourceRow);
    }

    // Add new resource row function for update modal
    function addUpdateResourceRow(resourceTypeId = '', quantity = 1, resourceId = 0) {
        const resourceItem = document.createElement('div');
        resourceItem.className = 'resource-item grid grid-cols-2 gap-4';

        // Get resource type options from the create modal
        const firstSelect = document.querySelector('.resourceType');
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

    window.showUpdateRoomModal = async function (id) {
        try {
            // Show loading indicator if it exists
            const loadingIndicator = document.getElementById('loadingIndicator');
            if (loadingIndicator) {
                loadingIndicator.style.display = 'flex';
            }

            // Rest of your function...
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
            const updateResourcesContainer = document.getElementById('updateResourcesContainer');
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
            document.getElementById('updateRoomModal').classList.remove('hidden');
        } catch (error) {
            console.error('Error fetching room data:', error);
            alert(`Error loading room data: ${error.message}. Please try again.`);
        } finally {
            // Hide loading indicator if it exists
            const loadingIndicator = document.getElementById('loadingIndicator');
            if (loadingIndicator) {
                loadingIndicator.style.display = 'none';
            }
        }
    };

    window.hideUpdateRoomModal = function () {
        document.getElementById('updateRoomModal').classList.add('hidden');
    };

    // Delete Room Modal
    // ----------------------------

    window.showDeleteRoomModal = async function (id) {
        try {
            // Show loading indicator
            document.getElementById('loadingIndicator').style.display = 'flex';

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
            document.getElementById('deleteRoomModal').classList.remove('hidden');
        } catch (error) {
            console.error('Error fetching room data:', error);
            alert('Error loading room data. Please try again.');
        } finally {
            document.getElementById('loadingIndicator').style.display = 'none';
        }
    };

    window.hideDeleteRoomModal = function () {
        document.getElementById('deleteRoomModal').classList.add('hidden');
    };

    // Handle Escape key to close modals
    document.addEventListener('keydown', function (e) {
        if (e.key === 'Escape') {
            hideCreateRoomModal();
            hideUpdateRoomModal();
            hideDeleteRoomModal();
        }
    });
    console.log('SNG');
});