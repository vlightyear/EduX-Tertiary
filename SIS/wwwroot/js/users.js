document.addEventListener('DOMContentLoaded', function () {
    document.getElementById('createNationId')?.addEventListener('change', async function () {
        clearSelect('createProvinceId', 'Select Province');
        clearSelect('createDistrictId', 'Select District');

        if (this.value) {
            await loadSelect('/Admin/GetProvinces', { nationId: this.value }, 'createProvinceId', 'Select Province');
        }
    });

    document.getElementById('createProvinceId')?.addEventListener('change', async function () {
        clearSelect('createDistrictId', 'Select District');

        if (this.value) {
            await loadSelect('/Admin/GetDistricts', { provinceId: this.value }, 'createDistrictId', 'Select District');
        }
    });

    document.getElementById('updateNationId')?.addEventListener('change', async function () {
        clearSelect('updateProvinceId', 'Select Province');
        clearSelect('updateDistrictId', 'Select District');

        if (this.value) {
            await loadSelect('/Admin/GetProvinces', { nationId: this.value }, 'updateProvinceId', 'Select Province');
        }
    });

    document.getElementById('updateProvinceId')?.addEventListener('change', async function () {
        clearSelect('updateDistrictId', 'Select District');

        if (this.value) {
            await loadSelect('/Admin/GetDistricts', { provinceId: this.value }, 'updateDistrictId', 'Select District');
        }
    });

    // Modal elements
    const createUserModal = document.getElementById('createUserModal');
    const updateUserModal = document.getElementById('updateUserModal');
    const deleteUserModal = document.getElementById('deleteUserModal');

    // Form elements
    const createUserForm = document.getElementById('createUserForm');
    const updateUserForm = document.getElementById('updateUserForm');
    const deleteUserForm = document.getElementById('deleteUserForm');

    // Button elements
    const confirmCreateBtn = document.getElementById('confirmCreate');
    const confirmUpdateBtn = document.getElementById('confirmUpdate');
    const confirmDeleteBtn = document.getElementById('confirmDelete');
    const confirmDeleteCheckbox = document.getElementById('confirmDeleteCheckbox');

    // Multi-role elements
    const roleCheckboxes = document.getElementById('roleCheckboxes');
    const selectedRolesInput = document.getElementById('selectedRoles');
    const updateRoleCheckboxes = document.getElementById('updateRoleCheckboxes');
    const updateSelectedRolesInput = document.getElementById('updateSelectedRoles');

    const createScopeSection = document.getElementById('createUserScopeSection');
    const updateScopeSection = document.getElementById('updateUserScopeSection');

    function resolveScopeFromRoles(roles) {
        if (roles.includes('PS') || roles.includes('SA')) {
            return 'national';
        }

        if (roles.includes('PEO')) {
            return 'provincial';
        }

        if (roles.includes('DEBS')) {
            return 'district';
        }

        return roles.length > 0 ? 'school' : '';
    }

    function clearSelect(selectId, placeholder) {
        const el = document.getElementById(selectId);
        if (!el) return;

        el.innerHTML = `<option value="">${placeholder}</option>`;
        el.value = '';
    }

    async function loadSelect(url, params, selectId, placeholder, selectedValue = '') {
        const el = document.getElementById(selectId);
        if (!el) return;

        el.innerHTML = `<option value="">Loading...</option>`;

        const query = new URLSearchParams(params).toString();
        const response = await fetch(`${url}?${query}`);

        if (!response.ok) {
            el.innerHTML = `<option value="">${placeholder}</option>`;
            return;
        }

        const data = await response.json();

        el.innerHTML = `<option value="">${placeholder}</option>`;

        data.forEach(item => {
            const option = document.createElement('option');
            option.value = item.id;
            option.textContent = item.name;
            el.appendChild(option);
        });

        if (selectedValue !== null && selectedValue !== undefined && selectedValue !== '') {
            el.value = selectedValue;
        }
    }

    function setRequired(id, isRequired) {
        const el = document.getElementById(id);
        if (!el) return;

        if (isRequired) {
            el.setAttribute('required', 'required');
        } else {
            el.removeAttribute('required');
            el.value = '';
        }
    }

    function toggleGroup(id, show) {
        const el = document.getElementById(id);
        if (!el) return;

        if (show) {
            el.classList.remove('hidden');
        } else {
            el.classList.add('hidden');
        }
    }

    function applyScopeUi(prefix, roles) {
        const scope = resolveScopeFromRoles(roles);

        const section = prefix === 'create'
            ? createScopeSection
            : updateScopeSection;

        if (!section) return;

        section.classList.toggle('hidden', !scope);

        const showNation = scope === 'national' || scope === 'provincial' || scope === 'district';
        const showProvince = scope === 'provincial' || scope === 'district';
        const showDistrict = scope === 'district';
        const showSchool = scope === 'school';

        toggleGroup(`${prefix}NationGroup`, showNation);
        toggleGroup(`${prefix}ProvinceGroup`, showProvince);
        toggleGroup(`${prefix}DistrictGroup`, showDistrict);
        toggleGroup(`${prefix}SchoolGroup`, showSchool);

        setRequired(`${prefix}NationId`, showNation);
        setRequired(`${prefix}ProvinceId`, showProvince);
        setRequired(`${prefix}DistrictId`, showDistrict);
        setRequired(`${prefix}SchoolId`, showSchool);

        if (!showProvince) clearSelect(`${prefix}ProvinceId`, 'Select Province');
        if (!showDistrict) clearSelect(`${prefix}DistrictId`, 'Select District');

        if (!showSchool) {
            const school = document.getElementById(`${prefix}SchoolId`);
            if (school) school.value = '';
        }

        if (!showNation) {
            const nation = document.getElementById(`${prefix}NationId`);
            if (nation) nation.value = '';
        }
    }

    // Modal handling functions
    function showModal(modalElement) {
        modalElement.classList.remove('hidden');
        document.body.style.overflow = 'hidden';

        // Add entrance animation
        const modalContent = modalElement.querySelector('.relative');
        if (modalContent) {
            modalContent.classList.add('modal-enter');
            setTimeout(() => {
                modalContent.classList.remove('modal-enter');
                modalContent.classList.add('modal-enter-active');
            }, 10);
        }
    }

    function hideModal(modalElement) {
        const modalContent = modalElement.querySelector('.relative');
        if (modalContent) {
            modalContent.classList.add('modal-exit-active');
            setTimeout(() => {
                modalElement.classList.add('hidden');
                document.body.style.overflow = 'auto';
                modalContent.classList.remove('modal-enter-active', 'modal-exit-active');
            }, 200);
        } else {
            modalElement.classList.add('hidden');
            document.body.style.overflow = 'auto';
        }
    }

    // Initialize DataTable with enhanced configuration
    let dataTable;
    if (typeof simpleDatatables !== 'undefined') {
        dataTable = new simpleDatatables.DataTable("#usersTable", {
            perPage: 10,
            searchable: true,
            sortable: true,
            tableRender: (_data, table, type) => {
                if (type === "print") {
                    return table;
                }
                return table;
            },
            labels: {
                placeholder: "Search users...",
                perPage: "Show {select} entries",
                noRows: "No administrative users found",
                info: "Showing {start} to {end} of {rows} users",
            },
            classes: {
                wrapper: "datatable-wrapper",
                input: "datatable-input",
                selector: "datatable-selector",
                paginationButton: "datatable-pagination-button",
                paginationButtonActive: "datatable-pagination-active",
                paginationButtonDisabled: "datatable-pagination-disabled",
            }
        });

        // Make the table responsive
        window.addEventListener('resize', () => {
            if (dataTable && typeof dataTable.refresh === 'function') {
                dataTable.refresh();
            }
        });

        // Enhanced DataTable styling after initialization
        setTimeout(() => {
            const wrapper = document.querySelector('.datatable-wrapper');
            if (wrapper) {
                wrapper.style.borderRadius = '0.5rem';
                wrapper.style.overflow = 'hidden';
            }

            // Style search input
            const searchInput = document.querySelector('.datatable-input');
            if (searchInput) {
                searchInput.className = 'datatable-input px-3 py-2 text-sm border border-gray-300 rounded-lg w-full';
                searchInput.placeholder = 'Search users by name, email, or role...';

                searchInput.addEventListener('focus', function () {
                    this.style.borderColor = '#2563eb';
                    this.style.boxShadow = '0 0 0 2px rgba(37, 99, 235, 0.2)';
                });

                searchInput.addEventListener('blur', function () {
                    this.style.borderColor = '#d1d5db';
                    this.style.boxShadow = 'none';
                });
            }

            // Style selector dropdown
            const selector = document.querySelector('.datatable-selector');
            if (selector) {
                selector.className = 'datatable-selector px-3 py-2 text-sm border border-gray-300 rounded-lg';

                selector.addEventListener('focus', function () {
                    this.style.borderColor = '#2563eb';
                    this.style.boxShadow = '0 0 0 2px rgba(37, 99, 235, 0.2)';
                });

                selector.addEventListener('blur', function () {
                    this.style.borderColor = '#d1d5db';
                    this.style.boxShadow = 'none';
                });
            }

            // Style pagination buttons
            const paginationButtons = document.querySelectorAll('.datatable-pagination-button');
            paginationButtons.forEach(button => {
                button.className = 'datatable-pagination-button px-3 py-1 text-sm text-secondary-600 rounded-md transition-colors';

                if (!button.classList.contains('datatable-pagination-active') &&
                    !button.classList.contains('datatable-pagination-disabled')) {
                    button.addEventListener('mouseenter', function () {
                        this.style.backgroundColor = '#eff6ff';
                    });
                    button.addEventListener('mouseleave', function () {
                        this.style.backgroundColor = 'transparent';
                    });
                }
            });

            // Style active pagination button
            const activeButton = document.querySelector('.datatable-pagination-active');
            if (activeButton) {
                activeButton.style.backgroundColor = '#dbeafe';
                activeButton.style.color = '#1d4ed8';
            }

            // Style disabled pagination buttons
            const disabledButtons = document.querySelectorAll('.datatable-pagination-disabled');
            disabledButtons.forEach(button => {
                button.style.color = '#9ca3af';
                button.style.cursor = 'not-allowed';
            });
        }, 100);
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
    [createUserModal, updateUserModal, deleteUserModal].forEach(setupModalCloseHandlers);

    // Enable/disable delete button based on checkbox
    confirmDeleteCheckbox?.addEventListener('change', function () {
        confirmDeleteBtn.disabled = !this.checked;
        if (this.checked) {
            confirmDeleteBtn.classList.remove('opacity-50', 'cursor-not-allowed');
        } else {
            confirmDeleteBtn.classList.add('opacity-50', 'cursor-not-allowed');
        }
    });

    // Multi-role handling functions
    function createRoleCheckboxes(container, roles, selectedRoles = []) {
        if (!container) return;

        container.innerHTML = '';

        roles.forEach(role => {
            const isSelected = selectedRoles.includes(role.name);
            const checkboxId = `${container.id}_${role.name}`;

            const checkboxHTML = `
                <div class="flex items-center p-2 bg-gray-50 rounded-lg hover:bg-gray-100 transition-colors">
                    <input type="checkbox" 
                           id="${checkboxId}" 
                           value="${role.name}" 
                           class="role-checkbox w-4 h-4 text-primary-600 border-gray-300 rounded focus:ring-primary-500"
                           ${isSelected ? 'checked' : ''}>
                    <label for="${checkboxId}" 
                           class="ml-2 text-sm text-gray-700 font-medium cursor-pointer select-none">
                        ${role.name}
                    </label>
                </div>
            `;

            container.insertAdjacentHTML('beforeend', checkboxHTML);
        });

        // Add event listeners to checkboxes
        const checkboxes = container.querySelectorAll('.role-checkbox');
        checkboxes.forEach(checkbox => {
            checkbox.addEventListener('change', function () {
                updateSelectedRoles(container);
            });
        });

        // Initial update
        updateSelectedRoles(container);
    }

    function updateSelectedRoles(container) {
        const checkboxes = container.querySelectorAll('.role-checkbox:checked');
        const selectedRoles = Array.from(checkboxes).map(cb => cb.value);

        // Update the hidden input based on which container this is
        if (container.id === 'roleCheckboxes') {
            selectedRolesInput.value = selectedRoles.join(',');

            // Update form validation
            if (selectedRoles.length > 0) {
                selectedRolesInput.setCustomValidity('');
                container.closest('.group').classList.remove('form-error');
            } else {
                selectedRolesInput.setCustomValidity('Please select at least one role.');
                container.closest('.group').classList.add('form-error');
            }

            applyScopeUi('create', selectedRoles);
        } else if (container.id === 'updateRoleCheckboxes') {
            updateSelectedRolesInput.value = selectedRoles.join(',');

            // Update form validation
            if (selectedRoles.length > 0) {
                updateSelectedRolesInput.setCustomValidity('');
                container.closest('.group').classList.remove('form-error');
            } else {
                updateSelectedRolesInput.setCustomValidity('Please select at least one role.');
                container.closest('.group').classList.add('form-error');
            }

            applyScopeUi('update', selectedRoles);
        }
    }

    function getSelectedRoles(container) {
        const checkboxes = container.querySelectorAll('.role-checkbox:checked');
        return Array.from(checkboxes).map(cb => cb.value);
    }

    function setSelectedRoles(container, roles) {
        const checkboxes = container.querySelectorAll('.role-checkbox');
        checkboxes.forEach(checkbox => {
            checkbox.checked = roles.includes(checkbox.value);
        });
        updateSelectedRoles(container);
    }

    // Fetch roles for checkboxes (excluding Student and Candidate)
    async function fetchRoles() {
        try {
            const response = await fetch('/Admin/GetRoles');
            if (!response.ok) {
                throw new Error('Failed to fetch roles');
            }
            const roles = await response.json();
            return roles;
        } catch (error) {
            console.error('Error fetching roles:', error);
            showNotification('Failed to load user roles. Please refresh the page.', 'error');
            return [];
        }
    }

    // Show create modal
    window.showCreateUserModal = async function () {
        try {
            createUserForm.reset();
            applyScopeUi('create', []);
            clearSelect('createProvinceId', 'Select Province');
            clearSelect('createDistrictId', 'Select District');

            const roles = await fetchRoles();

            if (roles.length > 0) {
                createRoleCheckboxes(roleCheckboxes, roles);
            }

            showModal(createUserModal);

            // Focus on the first input
            setTimeout(() => {
                const firstInput = createUserForm.querySelector('input[type="text"]');
                if (firstInput) firstInput.focus();
            }, 300);
        } catch (error) {
            console.error('Error opening create modal:', error);
            showNotification('Failed to open create user form.', 'error');
        }
    };

    // Show update modal
    window.showUpdateUserModal = async function (id) {
        try {
            // Show loading state
            const loadingOverlay = showLoadingOverlay('Loading user details...');

            const [roles, userResponse] = await Promise.all([
                fetchRoles(),
                fetch(`/Admin/GetUser/${id}`)
            ]);

            if (!userResponse.ok) {
                throw new Error('Failed to fetch user details');
            }

            const user = await userResponse.json();

            // Populate form fields
            document.getElementById('updateUserId').value = user.id || '';
            document.getElementById('updateFullName').value = user.fullName || '';
            document.getElementById('updateEmail').value = user.email || '';
            document.getElementById('updatePhoneNumber').value = user.phoneNumber || '';

            // Create role checkboxes and set selected roles
            if (roles.length > 0) {
                const userRoles = user.roles || [];
                createRoleCheckboxes(updateRoleCheckboxes, roles, userRoles);
            }

            const userRoles = user.roles || [];
            applyScopeUi('update', userRoles);

            document.getElementById('updateNationId').value = user.nationId || '';
            document.getElementById('updateSchoolId').value = user.schoolId || '';

            clearSelect('updateProvinceId', 'Select Province');
            clearSelect('updateDistrictId', 'Select District');

            if (user.nationId) {
                await loadSelect(
                    '/Admin/GetProvinces',
                    { nationId: user.nationId },
                    'updateProvinceId',
                    'Select Province',
                    user.provinceId || ''
                );
            }

            if (user.provinceId) {
                await loadSelect(
                    '/Admin/GetDistricts',
                    { provinceId: user.provinceId },
                    'updateDistrictId',
                    'Select District',
                    user.districtId || ''
                );
            }

            // Set checkboxes
            document.getElementById('updateEmailConfirmed').checked = user.emailConfirmed || false;
            document.getElementById('updatePhoneConfirmed').checked = user.phoneNumberConfirmed || false;
            document.getElementById('updateTwoFactor').checked = user.twoFactorEnabled || false;
            document.getElementById('updateLockoutEnabled').checked = user.lockoutEnabled || false;

            // Set lockout end date if exists
            if (user.lockoutEnd) {
                const date = new Date(user.lockoutEnd);
                document.getElementById('updateLockoutEnd').value = date.toISOString().slice(0, 16);
            } else {
                document.getElementById('updateLockoutEnd').value = '';
            }

            hideLoadingOverlay(loadingOverlay);
            showModal(updateUserModal);

            // Focus on the first input
            setTimeout(() => {
                const firstInput = updateUserForm.querySelector('input[type="text"]');
                if (firstInput) firstInput.focus();
            }, 300);

        } catch (error) {
            console.error('Error fetching user details:', error);
            hideLoadingOverlay();
            showNotification('Failed to load user details. Please try again.', 'error');
        }
    };

    // Show delete modal
    window.showDeleteUserModal = async function (id, userName) {
        try {
            const response = await fetch(`/Admin/GetUser/${id}`);

            if (response.ok) {
                const user = await response.json();
                document.getElementById('deleteUserId').value = user.id;
                document.getElementById('deleteUserName').textContent = user.fullName || userName;
                document.getElementById('deleteUserEmail').textContent = user.email || '';

                // Handle multiple roles display
                const userRoles = user.roles || [];
                const roleText = userRoles.length > 0 ? userRoles.join(', ') : 'No Role';
                document.getElementById('deleteUserRole').textContent = roleText;
            } else {
                // Fallback to provided data
                document.getElementById('deleteUserId').value = id;
                document.getElementById('deleteUserName').textContent = userName;
                document.getElementById('deleteUserEmail').textContent = '';
                document.getElementById('deleteUserRole').textContent = '';
            }

            // Reset checkbox and button state
            confirmDeleteCheckbox.checked = false;
            confirmDeleteBtn.disabled = true;
            confirmDeleteBtn.classList.add('opacity-50', 'cursor-not-allowed');

            showModal(deleteUserModal);
        } catch (error) {
            console.error('Error preparing delete modal:', error);
            // Still show modal with basic info
            document.getElementById('deleteUserId').value = id;
            document.getElementById('deleteUserName').textContent = userName;
            showModal(deleteUserModal);
        }
    };

    // Enhanced form submission with loading states
    function addLoadingState(button, text = 'Processing...') {
        const originalContent = button.innerHTML;
        button.disabled = true;
        button.innerHTML = `<i class="material-icons animate-spin mr-2">refresh</i>${text}`;

        return function removeLoadingState() {
            button.disabled = false;
            button.innerHTML = originalContent;
        };
    }

    // Custom form validation for multi-role
    function validateCreateForm() {
        const selectedRoles = getSelectedRoles(roleCheckboxes);

        if (selectedRoles.length === 0) {
            showNotification('Please select at least one role.', 'error');
            roleCheckboxes.scrollIntoView({ behavior: 'smooth', block: 'center' });
            return false;
        }

        applyScopeUi('create', selectedRoles);

        return createUserForm.checkValidity();
    }

    function validateUpdateForm() {
        const selectedRoles = getSelectedRoles(updateRoleCheckboxes);

        if (selectedRoles.length === 0) {
            showNotification('Please select at least one role.', 'error');
            updateRoleCheckboxes.scrollIntoView({ behavior: 'smooth', block: 'center' });
            return false;
        }

        applyScopeUi('update', selectedRoles);

        return updateUserForm.checkValidity();
    }

    // Handle form submissions
    confirmCreateBtn.addEventListener('click', function () {
        if (validateCreateForm()) {
            const removeLoading = addLoadingState(this, 'Creating User...');

            // Submit after brief delay to show loading state
            setTimeout(() => {
                createUserForm.submit();
            }, 100);
        } else {
            createUserForm.reportValidity();

            // Highlight first invalid field
            const firstInvalid = createUserForm.querySelector(':invalid');
            if (firstInvalid) {
                firstInvalid.focus();
                firstInvalid.scrollIntoView({ behavior: 'smooth', block: 'center' });
            }
        }
    });

    confirmUpdateBtn.addEventListener('click', function () {
        if (validateUpdateForm()) {
            const removeLoading = addLoadingState(this, 'Updating User...');

            // Before submitting, ensure the hidden input has the selected roles
            const selectedRoles = getSelectedRoles(updateRoleCheckboxes);

            // Create hidden inputs for each role (ASP.NET Core model binding)
            const existingRoleInputs = updateUserForm.querySelectorAll('input[name="Roles"]');
            existingRoleInputs.forEach(input => input.remove());

            selectedRoles.forEach((role, index) => {
                const input = document.createElement('input');
                input.type = 'hidden';
                input.name = `Roles[${index}]`;
                input.value = role;
                updateUserForm.appendChild(input);
            });

            setTimeout(() => {
                updateUserForm.submit();
            }, 100);
        } else {
            updateUserForm.reportValidity();

            const firstInvalid = updateUserForm.querySelector(':invalid');
            if (firstInvalid) {
                firstInvalid.focus();
                firstInvalid.scrollIntoView({ behavior: 'smooth', block: 'center' });
            }
        }
    });

    confirmDeleteBtn.addEventListener('click', function () {
        if (confirmDeleteCheckbox.checked) {
            const removeLoading = addLoadingState(this, 'Deleting User...');

            setTimeout(() => {
                deleteUserForm.submit();
            }, 100);
        }
    });

    // Initialize User Statistics Chart
    function initializeUserDistributionChart() {
        const chartElement = document.querySelector("#userStatsChart");
        if (!chartElement) {
            console.warn('Chart element not found');
            return;
        }

        if (typeof ApexCharts === 'undefined') {
            console.warn('ApexCharts not loaded');
            chartElement.innerHTML = `
                <div class="flex items-center justify-center h-full text-gray-500">
                    <div class="text-center">
                        <i class="material-icons text-4xl mb-2 text-gray-300">pie_chart</i>
                        <p class="text-sm">Chart library not loaded</p>
                    </div>
                </div>
            `;
            return;
        }

        // Get data from the ViewBag elements with better error handling
        let adminUsers = 0;
        let studentUsers = 0;
        let candidateUsers = 0;

        // Try to find admin users count
        const adminElement = document.getElementById('adminUsers');
        if (adminElement && adminElement.textContent) {
            adminUsers = parseInt(adminElement.textContent.trim()) || 0;
        }

        // Try to find student users count
        const studentElement = document.getElementById('studentUsers');
        if (studentElement && studentElement.textContent) {
            studentUsers = parseInt(studentElement.textContent.trim()) || 0;
        }

        // Try to find candidate users count (might not exist)
        const candidateElement = document.getElementById('candidateUsers');
        if (candidateElement && candidateElement.textContent) {
            candidateUsers = parseInt(candidateElement.textContent.trim()) || 0;
        }

        // Fallback: try to get from any element with specific classes or patterns
        if (adminUsers === 0) {
            const blueTextElement = document.querySelector('.text-blue-700');
            if (blueTextElement && blueTextElement.textContent) {
                adminUsers = parseInt(blueTextElement.textContent.trim()) || 0;
            }
        }

        if (studentUsers === 0) {
            const greenTextElement = document.querySelector('.text-green-700');
            if (greenTextElement && greenTextElement.textContent) {
                studentUsers = parseInt(greenTextElement.textContent.trim()) || 0;
            }
        }

        const totalUsers = adminUsers + studentUsers + candidateUsers;

        if (totalUsers === 0) {
            chartElement.innerHTML = `
                <div class="flex items-center justify-center h-full text-gray-500">
                    <div class="text-center">
                        <i class="material-icons text-4xl mb-2 text-gray-300">pie_chart</i>
                        <p class="text-sm">No data available</p>
                    </div>
                </div>
            `;
            return;
        }

        // Filter out zero values and their corresponding labels
        const data = [];
        const labels = [];
        const colors = [];

        if (adminUsers > 0) {
            data.push(adminUsers);
            labels.push('Administrators');
            colors.push('#06b6d4');
        }

        if (studentUsers > 0) {
            data.push(studentUsers);
            labels.push('Students');
            colors.push('#10b981');
        }

        if (candidateUsers > 0) {
            data.push(candidateUsers);
            labels.push('Candidates');
            colors.push('#f59e0b');
        }

        const chartOptions = {
            series: data,
            chart: {
                type: 'donut',
                height: 200,
                fontFamily: 'inherit',
                animations: {
                    enabled: true,
                    easing: 'easeinout',
                    speed: 800,
                    animateGradually: {
                        enabled: true,
                        delay: 150
                    },
                    dynamicAnimation: {
                        enabled: true,
                        speed: 350
                    }
                }
            },
            labels: labels,
            colors: colors,
            plotOptions: {
                pie: {
                    donut: {
                        size: '55%',
                        labels: {
                            show: true,
                            name: {
                                show: true,
                                fontSize: '14px',
                                fontWeight: 500,
                                color: '#374151',
                                offsetY: -4
                            },
                            value: {
                                show: true,
                                fontSize: '20px',
                                fontWeight: 700,
                                color: '#111827',
                                offsetY: 8,
                                formatter: function (val) {
                                    return val;
                                }
                            },
                            total: {
                                show: true,
                                label: 'Total',
                                fontSize: '14px',
                                fontWeight: 500,
                                color: '#6b7280',
                                formatter: function (w) {
                                    return totalUsers;
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
                show: false
            },
            stroke: {
                width: 2,
                colors: ['#ffffff']
            },
            tooltip: {
                style: {
                    fontSize: '14px',
                    fontFamily: 'inherit'
                },
                y: {
                    formatter: function (val, opts) {
                        const label = opts.w.globals.labels[opts.seriesIndex];
                        const percentage = ((val / totalUsers) * 100).toFixed(1);
                        return `${val} ${label} (${percentage}%)`;
                    }
                }
            },
            responsive: [{
                breakpoint: 480,
                options: {
                    chart: {
                        height: 200
                    },
                    plotOptions: {
                        pie: {
                            donut: {
                                labels: {
                                    show: true,
                                    name: {
                                        show: false
                                    },
                                    value: {
                                        show: true,
                                        fontSize: '16px'
                                    },
                                    total: {
                                        show: true,
                                        fontSize: '12px'
                                    }
                                }
                            }
                        }
                    }
                }
            }]
        };

        try {
            const chart = new ApexCharts(chartElement, chartOptions);
            chart.render();
        } catch (error) {
            console.error('Error rendering chart:', error);
            chartElement.innerHTML = `
                <div class="flex items-center justify-center h-full text-gray-500">
                    <div class="text-center">
                        <i class="material-icons text-4xl mb-2 text-gray-300">error</i>
                        <p class="text-sm">Chart failed to load</p>
                    </div>
                </div>
            `;
        }
    }

    // Utility functions
    function showLoadingOverlay(message = 'Loading...') {
        const overlay = document.createElement('div');
        overlay.className = 'fixed inset-0 bg-black bg-opacity-50 z-50 flex items-center justify-center';
        overlay.innerHTML = `
            <div class="bg-white rounded-lg p-6 flex items-center space-x-3">
                <div class="loading-spinner"></div>
                <span class="text-gray-700">${message}</span>
            </div>
        `;
        document.body.appendChild(overlay);
        return overlay;
    }

    function hideLoadingOverlay(overlay) {
        if (overlay && overlay.parentNode) {
            overlay.parentNode.removeChild(overlay);
        }
    }

    function showNotification(message, type = 'info') {
        const notification = document.createElement('div');
        const colors = {
            success: 'bg-green-50 border-green-200 text-green-800',
            error: 'bg-red-50 border-red-200 text-red-800',
            warning: 'bg-yellow-50 border-yellow-200 text-yellow-800',
            info: 'bg-blue-50 border-blue-200 text-blue-800'
        };

        notification.className = `fixed top-4 right-4 z-50 p-4 border rounded-lg shadow-lg ${colors[type]} max-w-sm`;
        notification.innerHTML = `
            <div class="flex items-center">
                <i class="material-icons mr-2">${type === 'error' ? 'error' : type === 'success' ? 'check_circle' : 'info'}</i>
                <span>${message}</span>
                <button class="ml-auto text-current hover:opacity-70" onclick="this.parentElement.parentElement.remove()">
                    <i class="material-icons">close</i>
                </button>
            </div>
        `;

        document.body.appendChild(notification);

        // Auto remove after 5 seconds
        setTimeout(() => {
            if (notification.parentNode) {
                notification.parentNode.removeChild(notification);
            }
        }, 5000);
    }

    // Initialize chart
    initializeUserDistributionChart();

    // Handle Escape key to close modals
    document.addEventListener('keydown', function (e) {
        if (e.key === 'Escape') {
            [createUserModal, updateUserModal, deleteUserModal].forEach(modal => {
                if (modal && !modal.classList.contains('hidden')) {
                    hideModal(modal);
                }
            });
        }
    });

    // Form validation enhancements
    function setupFormValidation() {
        const forms = [createUserForm, updateUserForm];

        forms.forEach(form => {
            if (!form) return;

            const inputs = form.querySelectorAll('input[required], select[required]');
            inputs.forEach(input => {
                // Real-time validation
                input.addEventListener('blur', function () {
                    validateField(this);
                });

                input.addEventListener('input', function () {
                    // Clear error state when user starts typing
                    clearFieldError(this);
                });
            });
        });
    }

    function validateField(field) {
        const isValid = field.checkValidity();
        const fieldContainer = field.closest('.group') || field.parentElement;

        if (!isValid) {
            fieldContainer.classList.add('form-field', 'error');
            showFieldError(field, field.validationMessage);
        } else {
            fieldContainer.classList.remove('form-field', 'error');
            clearFieldError(field);
        }

        return isValid;
    }

    function showFieldError(field, message) {
        clearFieldError(field);

        const errorElement = document.createElement('div');
        errorElement.className = 'form-error mt-1';
        errorElement.textContent = message;
        errorElement.setAttribute('data-error-for', field.id || field.name);

        field.parentElement.appendChild(errorElement);
    }

    function clearFieldError(field) {
        const fieldContainer = field.closest('.group') || field.parentElement;
        const existingError = fieldContainer.querySelector('.form-error');
        if (existingError) {
            existingError.remove();
        }
        fieldContainer.classList.remove('form-field', 'error');
    }

    // Initialize form validation
    setupFormValidation();

    // Initialize tooltips if available
    if (typeof bootstrap !== 'undefined' && bootstrap.Tooltip) {
        const tooltipTriggerList = [].slice.call(document.querySelectorAll('[data-bs-toggle="tooltip"]'));
        tooltipTriggerList.map(function (tooltipTriggerEl) {
            return new bootstrap.Tooltip(tooltipTriggerEl);
        });
    }

    // Override form submission to handle multi-role data properly
    createUserForm.addEventListener('submit', function (e) {
        e.preventDefault();

        const selectedRoles = getSelectedRoles(roleCheckboxes);

        // Create form data
        const formData = new FormData(this);

        // Remove any existing Roles entries
        formData.delete('Roles');

        // Add selected roles
        selectedRoles.forEach((role, index) => {
            formData.append(`Roles[${index}]`, role);
        });

        // Submit via fetch
        fetch(this.action, {
            method: 'POST',
            body: formData
        })
            .then(response => {
                if (response.ok) {
                    // Check if response is JSON or redirect
                    const contentType = response.headers.get('content-type');
                    if (contentType && contentType.includes('application/json')) {
                        return response.json();
                    } else {
                        // It's a redirect, reload the page
                        window.location.reload();
                        return;
                    }
                }
                throw new Error('Network response was not ok');
            })
            .then(data => {
                if (data && !data.success) {
                    showNotification(data.message, 'error');
                } else if (data && data.success) {
                    showNotification('User created successfully!', 'success');
                    setTimeout(() => {
                        window.location.reload();
                    }, 1500);
                }
            })
            .catch(error => {
                console.error('Error:', error);
                showNotification('An error occurred while creating the user.', 'error');
            });
    });

    updateUserForm.addEventListener('submit', function (e) {
        e.preventDefault();

        const selectedRoles = getSelectedRoles(updateRoleCheckboxes);

        // Create form data
        const formData = new FormData(this);

        // Remove any existing Roles entries
        formData.delete('Roles');

        // Add selected roles
        selectedRoles.forEach((role, index) => {
            formData.append(`Roles[${index}]`, role);
        });

        // Submit via fetch
        fetch(this.action, {
            method: 'POST',
            body: formData
        })
            .then(response => {
                if (response.ok) {
                    // Check if response is JSON or redirect
                    const contentType = response.headers.get('content-type');
                    if (contentType && contentType.includes('application/json')) {
                        return response.json();
                    } else {
                        // It's a redirect, reload the page
                        window.location.reload();
                        return;
                    }
                }
                throw new Error('Network response was not ok');
            })
            .then(data => {
                if (data && !data.success) {
                    showNotification(data.message, 'error');
                } else if (data && data.success) {
                    showNotification('User updated successfully!', 'success');
                    setTimeout(() => {
                        window.location.reload();
                    }, 1500);
                }
            })
            .catch(error => {
                console.error('Error:', error);
                showNotification('An error occurred while updating the user.', 'error');
            });
    });

    console.log('User management interface with multi-role support initialized successfully');
});