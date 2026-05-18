document.addEventListener('DOMContentLoaded', function () {
    // Initialize DataTable
    const dataTable = new simpleDatatables.DataTable("#schoolsTable", {
        perPage: 10,
        searchable: true,
        sortable: true,
        tableRender: (_data, table, type) => {
            if (type === "print") {
                return table
            }
            const tHead = table.childNodes[0]
            const filterHeaders = {
                nodeName: "TR",
                attributes: {
                    class: "search-filtering-row"
                },
                //childNodes: tHead.childNodes[0].childNodes.map(
                //    (_th, index) => {
                //        // Don't add search input for the Actions column
                //        if (index === 3) {
                //            return { nodeName: "TH" }
                //        }
                //        return {
                //            nodeName: "TH",
                //            childNodes: [
                //                {
                //                    nodeName: "INPUT",
                //                    attributes: {
                //                        class: "datatable-input px-3 py-2 text-sm border border-gray-300 rounded-lg w-full",
                //                        type: "search",
                //                        placeholder: "Search...",
                //                        "data-columns": "[" + index + "]"
                //                    }
                //                }
                //            ]
                //        }
                //    }
                //)
            }
            tHead.childNodes.push(filterHeaders)
            return table
        },
        labels: {
            placeholder: "Search...",
            perPage: "Show {select} entries",
            noRows: "No schools found",
            info: "Showing {start} to {end} of {rows} schools",
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

    // Make the table responsive
    window.addEventListener('resize', () => {
        //dataTable.columns().rebuild();
    });

    // Modal elements
    const createSchoolModal = document.getElementById('createSchoolModal');
    const updateSchoolModal = document.getElementById('updateSchoolModal');
    const deleteSchoolModal = document.getElementById('deleteSchoolModal');

    // Form elements
    const createSchoolForm = document.getElementById('createSchoolForm');
    const updateSchoolForm = document.getElementById('updateSchoolForm');
    const deleteSchoolForm = document.getElementById('deleteSchoolForm');

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
    [createSchoolModal, updateSchoolModal, deleteSchoolModal].forEach(setupModalCloseHandlers);

    // Function to toggle between the modes based on the selected radio button
    function toggleMode() {
        const selectedMode = document.querySelector('input[name="mode"]:checked').value;
        if (selectedMode === 'single') {
            document.getElementById('singleSchool').classList.remove('hidden');
            document.getElementById('bulkSchool').classList.add('hidden');
        } else if (selectedMode === 'bulk') {
            document.getElementById('singleSchool').classList.add('hidden');
            document.getElementById('bulkSchool').classList.remove('hidden');
        }
    }

    // Add event listeners to handle mode changes (single / bulk)
    document.querySelectorAll('input[name="mode"]').forEach(radio => {
        radio.addEventListener('change', toggleMode);
    });

    // Show create modal
    window.showCreateSchoolModal = function () {
        createSchoolForm.reset();
        showModal(createSchoolModal);
        toggleMode(); // Initially set the mode
    };

    // Show update modal
    window.showUpdateSchoolModal = async function (id) {

        try {

            const response = await fetch(`/Admin/GetSchool/${id}`);

            if (!response.ok) {
                throw new Error('Failed to fetch school');
            }

            const data = await response.json();

            // =========================================
            // BASIC FIELDS
            // =========================================

            document.getElementById('updateSchoolId').value = data.id;
            document.getElementById('updateSchoolName').value = data.name;
            document.getElementById('updateDescription').value = data.description ?? '';

            // =========================================
            // USER SELECTS
            // =========================================

            $('#updateDeanId').val(data.deanId);
            $('#updateAssistantDeanId').val(data.assistantDeanId);
            $('#updateAssistantRegistrarId').val(data.assistantRegistrarId);

            // =========================================
            // LOCATION HIERARCHY
            // =========================================

            // Nation
            $('#updateNationId').val(data.nationId);

            // =========================================
            // LOAD PROVINCES
            // =========================================

            await $.get('/Admin/GetProvinces',
                { nationId: data.nationId },
                function (provinces) {

                    $('#updateProvinceId').empty();
                    $('#updateProvinceId')
                        .append('<option value="">Select Province</option>');

                    provinces.forEach(p => {

                        $('#updateProvinceId').append(
                            `<option value="${p.id}">${p.name}</option>`
                        );
                    });

                    $('#updateProvinceId').val(data.provinceId);
                });

            // =========================================
            // LOAD DISTRICTS
            // =========================================

            await $.get('/Admin/GetDistricts',
                { provinceId: data.provinceId },
                function (districts) {

                    $('#updateDistrictId').empty();

                    $('#updateDistrictId')
                        .append('<option value="">Select District</option>');

                    districts.forEach(d => {

                        $('#updateDistrictId').append(
                            `<option value="${d.id}">${d.name}</option>`
                        );
                    });

                    $('#updateDistrictId').val(data.districtId);
                });

            // =========================================
            // LOAD CONSTITUENCIES
            // =========================================

            if (data.districtId) {

                await $.get('/Admin/GetConstituencies',
                    { districtId: data.districtId },
                    function (constituencies) {

                        $('#updateConstituencyId').empty();

                        $('#updateConstituencyId')
                            .append('<option value="">Select Constituency</option>');

                        constituencies.forEach(c => {

                            $('#updateConstituencyId').append(
                                `<option value="${c.id}">${c.name}</option>`
                            );
                        });

                        $('#updateConstituencyId').val(data.constituencyId);
                    });
            }

            // =========================================
            // LOAD WARDS
            // =========================================

            if (data.constituencyId) {

                await $.get('/Admin/GetWards',
                    { constituencyId: data.constituencyId },
                    function (wards) {

                        $('#updateWardId').empty();

                        $('#updateWardId')
                            .append('<option value="">Select Ward</option>');

                        wards.forEach(w => {

                            $('#updateWardId').append(
                                `<option value="${w.id}">${w.name}</option>`
                            );
                        });

                        $('#updateWardId').val(data.wardId);
                    });
            }

            // =========================================
            // SHOW MODAL
            // =========================================

            showModal(updateSchoolModal);

        }
        catch (error) {

            console.error('Error loading school:', error);

            alert('Failed to load school data.');
        }
    };

    // Show delete modal
    window.showDeleteSchoolModal = function (id, name) {
        document.getElementById('deleteSchoolId').value = id;
        document.getElementById('deleteSchoolName').textContent = name;
        showModal(deleteSchoolModal);
    };

    // Handle form submissions
    confirmCreateBtn.addEventListener('click', function () {
        if (createSchoolForm.checkValidity()) {
            createSchoolForm.submit();
        } else {
            createSchoolForm.reportValidity();
        }
    });

    confirmUpdateBtn.addEventListener('click', function () {
        if (updateSchoolForm.checkValidity()) {
            updateSchoolForm.submit();
        } else {
            updateSchoolForm.reportValidity();
        }
    });

    confirmDeleteBtn.addEventListener('click', function () {
        deleteSchoolForm.submit();
    });

    // Handle Escape key to close modals
    document.addEventListener('keydown', function (e) {
        if (e.key === 'Escape') {
            [createSchoolModal, updateSchoolModal, deleteSchoolModal].forEach(modal => {
                if (!modal.classList.contains('hidden')) {
                    hideModal(modal);
                }
            });
        }
    });

    // File upload UI enhancement
    const fileInput = document.getElementById('file');
    if (fileInput) {
        const fileLabel = fileInput.nextElementSibling;
        fileInput.addEventListener('change', function (e) {
            if (e.target.files.length > 0) {
                fileLabel.textContent = e.target.files[0].name;
            } else {
                fileLabel.textContent = 'Upload a file';
            }
        });
    }
});

document.addEventListener('DOMContentLoaded', function () {

    const api = {
        provinces: '/Admin/GetProvinces',
        districts: '/Admin/GetDistricts',
        constituencies: '/Admin/GetConstituencies',
        wards: '/Admin/GetWards'
    };

    function loadDropdown(url, param, value, target, placeholder) {
        $(target).empty();

        if (!value) return;

        $.get(url, { [param]: value }, function (data) {
            $(target).append(`<option value="">${placeholder}</option>`);
            data.forEach(x => {
                $(target).append(`<option value="${x.id}">${x.name}</option>`);
            });
        });
    }

    // CREATE FLOW
    $('#nationId').change(function () {
        loadDropdown(api.provinces, 'nationId', this.value, '#provinceId', 'Select Province');
        $('#districtId, #constituencyId, #wardId').empty();
    });

    $('#provinceId').change(function () {
        loadDropdown(api.districts, 'provinceId', this.value, '#districtId', 'Select District');
        $('#constituencyId, #wardId').empty();
    });

    $('#districtId').change(function () {
        loadDropdown(api.constituencies, 'districtId', this.value, '#constituencyId', 'Select Constituency');
        $('#wardId').empty();
    });

    $('#constituencyId').change(function () {
        loadDropdown(api.wards, 'constituencyId', this.value, '#wardId', 'Select Ward');
    });

    // UPDATE FLOW (separate IDs)

    $('#updateNationId').change(function () {
        loadDropdown(api.provinces, 'nationId', this.value, '#updateProvinceId', 'Select Province');
        $('#updateDistrictId, #updateConstituencyId, #updateWardId').empty();
    });

    $('#updateProvinceId').change(function () {
        loadDropdown(api.districts, 'provinceId', this.value, '#updateDistrictId', 'Select District');
        $('#updateConstituencyId, #updateWardId').empty();
    });

    $('#updateDistrictId').change(function () {
        loadDropdown(api.constituencies, 'districtId', this.value, '#updateConstituencyId', 'Select Constituency');
        $('#updateWardId').empty();
    });

    $('#updateConstituencyId').change(function () {
        loadDropdown(api.wards, 'constituencyId', this.value, '#updateWardId', 'Select Ward');
    });

});