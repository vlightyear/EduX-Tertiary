document.addEventListener('DOMContentLoaded', () => {
    console.log("jQuery version:", jQuery.fn.jquery);
    console.log("Select2 loaded:", typeof jQuery.fn.select2 === 'function');
    // Utility Functions
    function showNotification(type, title, message, duration = 5000) {
        const alertComponent = document.createElement('div');
        alertComponent.className = `notification-container fixed top-4 right-4 z-50 w-full max-w-md transform transition-all duration-300 ease-in-out translate-x-0`;

        const bgColors = {
            success: 'bg-green-50 border-green-200',
            error: 'bg-red-50 border-red-200',
            warning: 'bg-yellow-50 border-yellow-200'
        };

        const textColors = {
            success: 'text-green-800',
            error: 'text-red-800',
            warning: 'text-yellow-800'
        };

        const icons = {
            success: `<svg class="h-6 w-6 text-green-600" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z"/>
                    </svg>`,
            error: `<svg class="h-6 w-6 text-red-600" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 8v4m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z"/>
                    </svg>`,
            warning: `<svg class="h-6 w-6 text-yellow-600" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z"/>
                    </svg>`
        };

        alertComponent.innerHTML = `
            <div class="rounded-lg border p-4 shadow-lg ${bgColors[type]} ${textColors[type]} animate-in slide-in-from-right-5">
                <div class="flex items-start space-x-4">
                    <div class="flex-shrink-0">
                        ${icons[type]}
                    </div>
                    <div class="flex-1 pt-0.5">
                        <h3 class="text-sm font-medium">${title}</h3>
                        <p class="mt-1 text-sm opacity-90">${message}</p>
                    </div>
                    <button class="flex-shrink-0 ml-4 text-sm opacity-50 hover:opacity-100 focus:outline-none focus:ring-2 focus:ring-offset-2 rounded-full">
                        <svg class="w-4 h-4" fill="currentColor" viewBox="0 0 20 20">
                            <path d="M4.293 4.293a1 1 0 011.414 0L10 8.586l4.293-4.293a1 1 0 111.414 1.414L11.414 10l4.293 4.293a1 1 0 01-1.414 1.414L10 11.414l-4.293 4.293a1 1 0 01-1.414-1.414L8.586 10 4.293 5.707a1 1 0 010-1.414z" clip-rule="evenodd" fill-rule="evenodd"></path>
                        </svg>
                    </button>
                </div>
            </div>
        `;

        document.body.appendChild(alertComponent);

        // Add click event to close button
        const closeButton = alertComponent.querySelector('button');
        closeButton.addEventListener('click', () => {
            alertComponent.classList.add('translate-x-full', 'opacity-0');
            setTimeout(() => alertComponent.remove(), 300);
        });

        // Auto remove after duration
        setTimeout(() => {
            alertComponent.classList.add('translate-x-full', 'opacity-0');
            setTimeout(() => alertComponent.remove(), 300);
        }, duration);
    }

    // Show success modal
    function showSuccessModal(referenceNumber, isFreeApplication = false) {
        // Create modal backdrop
        const modalBackdrop = document.createElement('div');
        modalBackdrop.className = 'fixed inset-0 bg-black bg-opacity-50 z-50 flex items-center justify-center';

        // Create modal content
        modalBackdrop.innerHTML = `
            <div class="bg-white rounded-lg max-w-md mx-auto p-6 shadow-xl transform transition-all animate-in zoom-in-50">
                <div class="text-center">
                    <div class="mx-auto flex items-center justify-center h-16 w-16 rounded-full bg-green-100 mb-4">
                        <svg class="h-10 w-10 text-green-600" fill="none" stroke="currentColor" viewBox="0 0 24 24" xmlns="http://www.w3.org/2000/svg">
                            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 13l4 4L19 7"></path>
                        </svg>
                    </div>
                    <h3 class="text-xl font-semibold text-gray-900 mb-2">Application Completed!</h3>
                    <p class="text-secondary-600 mb-6">Your application has been received. Your reference number is:</p>
                    <div class="bg-gray-100 p-3 rounded-lg mb-6">
                        <p class="font-mono text-lg font-semibold text-gray-900">${referenceNumber}</p>
                    </div>
                    <div class="flex flex-col space-y-3">
                        ${isFreeApplication ?
                        `<a href="/StudentApplication/Index" class="inline-flex justify-center py-3 px-4 border border-transparent shadow-sm text-sm font-medium rounded-md text-white bg-blue-600 hover:bg-blue-700 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-blue-500">
                                Go to Dashboard
                            </a>` :
                        `<a href="/Payments/PaymentSelection?referenceNumber=${referenceNumber}" class="inline-flex justify-center py-3 px-4 border border-transparent shadow-sm text-sm font-medium rounded-md text-white bg-green-600 hover:bg-green-700 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-green-500">
                                Proceed to Payment
                            </a>
                            <a href="/StudentApplication/Index" class="inline-flex justify-center py-3 px-4 border border-gray-300 shadow-sm text-sm font-medium rounded-md text-gray-700 bg-white hover:bg-gray-50 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-cyan-500">
                                Back to Dashboard
                            </a>`
                    }
                    </div>
                </div>
            </div>
        `;

        // Add modal to body
        document.body.appendChild(modalBackdrop);

        // Prevent page scrolling while modal is open
        document.body.style.overflow = 'hidden';

        // Close modal when clicking outside
        modalBackdrop.addEventListener('click', (e) => {
            if (e.target === modalBackdrop) {
                closeModal();
            }
        });

        // Close modal function
        function closeModal() {
            modalBackdrop.classList.add('opacity-0');
            setTimeout(() => {
                document.body.removeChild(modalBackdrop);
                document.body.style.overflow = 'auto';
            }, 300);
        }
    }


    // Form initialization
    const form = document.getElementById('application-form');
    if (!form) {
        console.error("Form not found. Ensure the form has the correct ID.");
        return;
    }

    // Step Navigation
    let currentStep = 1;
    const totalSteps = 5;
    const stepPanes = document.querySelectorAll('.step-pane');
    const stepItems = document.querySelectorAll('.step-item');
    const nextButton = document.querySelector('.next-step');
    const prevButton = document.querySelector('.prev-step');
    const submitButton = document.querySelector('.submit-form');

    function updateStepVisibility() {
        stepPanes.forEach((pane, index) => {
            pane.classList.toggle('active', index + 1 === currentStep);
            pane.classList.toggle('hidden', index + 1 !== currentStep);
        });

        stepItems.forEach((item, index) => {
            const stepNumber = index + 1;
            const span = item.querySelector('span');

            if (stepNumber < currentStep) {
                // Completed step
                item.classList.add('text-cyan-700');
                span.classList.add('bg-cyan-600', 'text-white');
                span.classList.remove('border-gray-300', 'text-gray-500', 'border-cyan-600', 'text-cyan-600');
            } else if (stepNumber === currentStep) {
                // Current step
                item.classList.add('text-cyan-700');
                span.classList.add('border-cyan-600', 'text-cyan-600');
                span.classList.remove('border-gray-300', 'text-gray-500', 'bg-cyan-600', 'text-white');
            } else {
                // Future step
                item.classList.remove('text-cyan-700');
                span.classList.remove('bg-cyan-600', 'text-white', 'border-cyan-600', 'text-cyan-600');
                span.classList.add('border-gray-300', 'text-gray-500');
            }
        });

        // Update button visibility
        prevButton.classList.toggle('hidden', currentStep === 1);
        nextButton.classList.toggle('hidden', currentStep === totalSteps);
        submitButton.classList.toggle('hidden', currentStep !== totalSteps);

        // Scroll to top on step change
        window.scrollTo({ top: 0, behavior: 'smooth' });
    }

    // Form validation functions
    function validateStep(step) {
        let isValid = true;
        const currentPane = document.querySelector(`.step-pane[data-step="${step}"]`);

        if (!currentPane) return false;

        // Clear previous validation states
        currentPane.querySelectorAll('.border-red-500').forEach(el => {
            el.classList.remove('border-red-500', 'focus:ring-red-500');
            el.classList.add('border-slate-300', 'focus:ring-cyan-500');
        });

        // Show validation summary if exists
        const validationSummary = document.querySelector('[asp-validation-summary]');
        if (validationSummary) {
            validationSummary.style.display = 'none';
        }

        // Validate required fields
        currentPane.querySelectorAll('input[required], select[required], textarea[required]').forEach(element => {
            if (!element.value || element.value.trim() === '' ||
                (element.tagName === 'SELECT' && element.value === '') ||
                (element.type === 'file' && (!element.files || element.files.length === 0))) {

                const name = element.getAttribute('data-name') || element.getAttribute('placeholder') || 'This field';
                showNotification('error', 'Required Field', `${name} is required.`);
                element.classList.remove('border-slate-300', 'focus:ring-cyan-500');
                element.classList.add('border-red-500', 'focus:ring-red-500');
                isValid = false;
            }
        });

        // Step-specific validation
        switch (step) {
            case 1:
                // Personal Details validation
                const phoneInput = currentPane.querySelector('#PhoneNumber');
                if (phoneInput && phoneInput.value && !isValidPhone(phoneInput.value)) {
                    showNotification('error', 'Invalid Input', 'Please enter a valid phone number.');
                    phoneInput.classList.remove('border-slate-300', 'focus:ring-cyan-500');
                    phoneInput.classList.add('border-red-500', 'focus:ring-red-500');
                    isValid = false;
                }
                break;

         
            case 4:
                // Education History validation - UPDATED FOR FOREIGN STUDENTS
                const isForeigner = document.getElementById('Foreigner')?.value;

                if (isForeigner !== 'true') { // Changed from 'Yes' to 'true'
                    // Only validate subjects for non-foreign students
                    const subjectsTable = document.getElementById('subjectsTable');
                    if (subjectsTable) {
                        const subjectRows = subjectsTable.querySelectorAll('tbody tr');
                        if (subjectRows.length < 5) {
                            showNotification('error', 'Incomplete Subjects', 'You must enter at least 5 subjects.');
                            document.getElementById('subject-grade-error').textContent = 'You must enter at least 5 subjects.';
                            isValid = false;
                        } else {
                            document.getElementById('subject-grade-error').textContent = '';
                        }
                    }
                } else {
                    // For foreign students, clear any subject validation errors
                    const subjectError = document.getElementById('subject-grade-error');
                    if (subjectError) {
                        subjectError.textContent = '';
                    }

                    // Show informational message that subjects are not required
                    console.log('Foreign student: Skipping subject validation');
                }
                break;

            
            case 5:
                // Documents validation - UPDATED FOR FOREIGN STUDENTS
                const fileInputs = currentPane.querySelectorAll('input[type="file"][required]');
                fileInputs.forEach(input => {
                    if (!input.files || input.files.length === 0) {
                        const name = input.getAttribute('data-name') || 'File';
                        showNotification('error', 'Missing Document', `${name} is required.`);
                        input.parentElement.classList.remove('border-slate-300');
                        input.parentElement.classList.add('border-red-500');
                        isValid = false;
                    }
                });

                // ✅ REMOVED: No longer check if submit button is disabled
                // The photo validation will handle warnings, but won't block submission

                // Additional validation for foreign students - ensure transcript is uploaded
                const foreignerStatus = document.getElementById('Foreigner')?.value;
                if (foreignerStatus === 'true') {
                    const transcriptInput = document.getElementById('ResultsAttachment');
                    if (!transcriptInput || !transcriptInput.files || transcriptInput.files.length === 0) {
                        showNotification('error', 'Missing Transcript', 'Foreign students must upload their academic transcript.');
                        if (transcriptInput) {
                            transcriptInput.parentElement.classList.remove('border-slate-300');
                            transcriptInput.parentElement.classList.add('border-red-500');
                        }
                        isValid = false;
                    }
                }
                break;
        }

        return isValid;
    }

   

    // Navigation handlers
    function goToNextStep() {
        if (currentStep < totalSteps && validateStep(currentStep)) {
            currentStep++;
            updateStepVisibility();


            // Verify foreign student status when entering step 3 (Program Choice)
            if (currentStep === 4) {
                verifyForeignStudentStatus();
            }
        }
    }

    function goToPreviousStep() {
        if (currentStep > 1) {
            currentStep--;
            updateStepVisibility();
        }
    }

    // Event Listeners for Navigation
    nextButton.addEventListener('click', goToNextStep);
    prevButton.addEventListener('click', goToPreviousStep);

    // Step indicators click handling
    stepItems.forEach((item, index) => {
        item.addEventListener('click', () => {
            const targetStep = index + 1;

            // Allow going to a step if it's before current step or next step (if validation passes)
            if (targetStep < currentStep || (targetStep === currentStep + 1 && validateStep(currentStep))) {
                currentStep = targetStep;
                updateStepVisibility();
            }
            // Or if it's the current step (do nothing)
            else if (targetStep === currentStep) {
                return;
            }
            // Otherwise, show a notification
            else {
                showNotification('warning', 'Step Navigation', 'Please complete the current step before skipping ahead.');
            }
        });
    });

    // Form submission handling 
    form.addEventListener('submit', async (e) => {
        e.preventDefault();

        if (!validateStep(currentStep)) {
            return;
        }

        // ✅ NEW: Confirm submission if photo has issues
        const passportPhotoValidation = document.getElementById('passport-photo-validation');
        if (passportPhotoValidation && passportPhotoValidation.textContent.includes('Photo Quality Issues')) {
            if (!confirm('Your passport photo has quality issues. Your application may be delayed for review. Do you want to proceed anyway?')) {
                return;
            }
        }

        // Create FormData and manually add subject data
        const formData = new FormData(form);


        const phoneInput = document.getElementById('PhoneNumber');
        const nextOfKinPhoneInput = document.getElementById('NextOfKinPhone');

        if (phoneInput && phoneInput.getAttribute('data-full-number')) {
            formData.set('Applicant.Phone', phoneInput.getAttribute('data-full-number'));
            console.log('Submitting primary phone:', phoneInput.getAttribute('data-full-number'));
        }

        if (nextOfKinPhoneInput && nextOfKinPhoneInput.getAttribute('data-full-number')) {
            formData.set('Applicant.NextOfKinPhone', nextOfKinPhoneInput.getAttribute('data-full-number'));
            console.log('Submitting next of kin phone:', nextOfKinPhoneInput.getAttribute('data-full-number'));
        }

        // Debug logging for subjects
        const subjectRows = document.querySelectorAll('.subject-grade-pair');
        console.log("Number of subject rows:", subjectRows.length);

        // Clear any existing subject data
        formData.delete('SelectedSubjects');

        // Add each subject manually to ensure proper indexing
        subjectRows.forEach((row, index) => {
            const subjectSelect = row.querySelector('.subject-select');
            const gradeSelect = row.querySelector('.grade-select');

            if (subjectSelect && gradeSelect && subjectSelect.value && gradeSelect.value) {
                formData.append(`SelectedSubjects[${index}].SubjectId`, subjectSelect.value);
                formData.append(`SelectedSubjects[${index}].GradeId`, gradeSelect.value);
                console.log(`Added subject ${index}: ${subjectSelect.value} - ${gradeSelect.value}`);
            }
        });

        // Disable submit button and show loading state
        submitButton.disabled = true;
        const originalText = submitButton.innerHTML;
        submitButton.innerHTML = `
        <svg class="animate-spin -ml-1 mr-3 h-5 w-5 text-white inline" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24">
            <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle>
            <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path>
        </svg>
        Submitting...
        `;

        try {
            const response = await fetch(form.action, {
                method: 'POST',
                body: formData,
                headers: {
                    'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]')?.value || ''
                }
            });

            const data = await response.json();

            if (data.success) {
                // Check if it's a free application
                if (data.isFreeApplication) {
                    // For free applications, redirect directly to success page with flag
                    window.location.href = `/StudentApplication/ApplicationSuccess?referenceNumber=${encodeURIComponent(data.referenceNumber)}&isFreeApplication=true`;
                } else {
                    // For paid applications, show the payment modal (existing behavior)
                    showSuccessModal(data.referenceNumber);
                }
            } else {
                showNotification('error', 'Error', data.message || 'Failed to submit application. Please try again.');
            }
        } catch (error) {
            console.error('Submission error:', error);
            showNotification('error', 'Error', 'An error occurred while submitting the application. Please try again.');
        } finally {
            submitButton.disabled = false;
            submitButton.innerHTML = originalText;
        }
    });

    // Helper function for phone validation - will be updated by intl-tel-input
    function isValidPhone(phone) {
        // This will be overridden by the intl-tel-input initialization
        // Fallback pattern for basic validation
        const phonePattern = /^\+?[\d\s-]{7,}$/;
        return phonePattern.test(phone);
    }

    // Initialize form state
    updateStepVisibility();

    // Subject Table Management
    const subjectContainer = document.getElementById('subject-grades-container');
    const addSubjectButton = document.getElementById('add-subject-grade');

    if (addSubjectButton && subjectContainer) {
        // Get the template structure from the first row
        const firstRow = document.querySelector('.subject-grade-pair');
        if (!firstRow) return;

        const templateHtml = firstRow.outerHTML;

        addSubjectButton.addEventListener('click', () => {
            const currentRowCount = document.querySelectorAll('.subject-grade-pair').length;

            // Update the subject count display
            const subjectCount = document.getElementById('subject-count');
            if (subjectCount) {
                subjectCount.textContent = (currentRowCount + 1).toString();
            }

            // Create new row using the template
            const newRow = document.createElement('tr');
            newRow.className = 'subject-grade-pair border-b border-slate-100 hover:bg-slate-50';
            newRow.innerHTML = templateHtml
                .replace(/SelectedSubjects\[\d+\]/g, `SelectedSubjects[${currentRowCount}]`);

            // Clear any selected values in the new row
            newRow.querySelectorAll('select').forEach(select => {
                select.value = '';
            });

            // Add the new row to the table
            document.querySelector('#subjects').appendChild(newRow);
        });

        // Update subject indexes when rows are added/removed
        function updateSubjectIndexes() {
            const rows = document.querySelectorAll('.subject-grade-pair');
            rows.forEach((row, index) => {
                row.querySelectorAll('select').forEach(select => {
                    select.name = select.name.replace(/\[\d+\]/, `[${index}]`);
                });
            });

            // Update the subject count display
            const subjectCount = document.getElementById('subject-count');
            if (subjectCount) {
                subjectCount.textContent = rows.length.toString();
            }
        }

        // Delegate event listener for remove buttons
        document.querySelector('#subjectsTable tbody').addEventListener('click', (e) => {
            if (e.target.classList.contains('remove-subject-grade') || e.target.closest('.remove-subject-grade')) {
                const row = e.target.closest('tr');
                const totalRows = document.querySelectorAll('.subject-grade-pair').length;

                if (totalRows > 1) {
                    row.remove();
                    updateSubjectIndexes();
                } else {
                    // If it's the last row, just clear the values
                    row.querySelectorAll('select').forEach(select => select.value = '');
                }
            }
        });

        // Prevent duplicate subject selection
        subjectContainer.addEventListener('change', (e) => {
            if (e.target.classList.contains('subject-select')) {
                const selectedValue = e.target.value;
                if (!selectedValue) return;

                const allSubjectSelects = Array.from(document.querySelectorAll('.subject-select'));
                const duplicateCount = allSubjectSelects.filter(select =>
                    select !== e.target && select.value === selectedValue
                ).length;

                if (duplicateCount > 0) {
                    showNotification('error', 'Duplicate Subject', 'Please select different subjects for each row.');
                    e.target.value = '';
                }
            }
        });
    }

    // File Upload Handling
    function handleFileUpload(inputId, previewId) {
        const input = document.getElementById(inputId);
        const preview = document.getElementById(previewId);

        if (!input || !preview) return;

        input.addEventListener('change', () => {
            const file = input.files[0];
            if (!file) {
                preview.classList.add('hidden');
                return;
            }

            if (file.type !== 'application/pdf') {
                showNotification('error', 'Invalid File', 'Please upload a PDF file.');
                input.value = '';
                preview.classList.add('hidden');
                return;
            }

            if (file.size > 5 * 1024 * 1024) {  // 5MB limit
                showNotification('error', 'File Too Large', 'Please upload a file smaller than 5MB.');
                input.value = '';
                preview.classList.add('hidden');
                return;
            }

            preview.classList.remove('hidden');
            preview.querySelector('.file-name').textContent = file.name;
        });

        // Handle clear button
        preview.querySelector('.clear-file')?.addEventListener('click', () => {
            input.value = '';
            preview.classList.add('hidden');
        });
    }

    // Initialize file upload handlers
    handleFileUpload('NrcOrPassportCopy', 'nrc-preview');
    handleFileUpload('ResultsAttachment', 'results-preview');
    handleFileUpload('StudyPermit', 'study-permit-preview');
    //handleFileUpload('PassportPhoto', 'passport-photo-preview');

    // Dependent Dropdowns
    const schoolSelect = document.getElementById('schools');
    const programmeLevelSelect = document.getElementById('ProgrammeLevel');
    const programmeSelect = document.getElementById('programmes');

    async function updateProgrammes() {
        if (!schoolSelect?.value || !programmeLevelSelect?.value) return;

        programmeSelect.disabled = true;
        try {
            const response = await fetch(`/StudentApplication/GetProgrammes?schoolId=${schoolSelect.value}&programmeLevelId=${programmeLevelSelect.value}`);
            const programmes = await response.json();

            programmeSelect.innerHTML = '<option value="">Select a Programme</option>';
            programmes.forEach(prog => {
                const option = document.createElement('option');
                option.value = prog.id;
                option.textContent = prog.name;
                programmeSelect.appendChild(option);
            });

            if (programmes.length === 0) {
                showNotification('warning', 'No Programmes', 'No programmes available for the selected criteria.');
            }

            programmeSelect.disabled = false;
        } catch (error) {
            console.error('Error fetching programmes:', error);
            showNotification('error', 'Error', 'Failed to load programmes. Please try again.');
            programmeSelect.disabled = false;
        }
    }

    schoolSelect?.addEventListener('change', updateProgrammes);
    programmeLevelSelect?.addEventListener('change', updateProgrammes);

    // Location Cascading Dropdowns with Manual Entry Fallback
    const countrySelect = document.getElementById('Country');
    let stateSelect = document.getElementById('State');
    let citySelect = document.getElementById('City');

    async function updateStates() {
        if (!countrySelect?.value) return;

        const currentStateElement = document.getElementById('State');
        currentStateElement.disabled = true;

        const currentCityElement = document.getElementById('City');
        currentCityElement.disabled = true;

        // Show loading state
        if (currentStateElement.tagName === 'SELECT') {
            currentStateElement.innerHTML = '<option value="">Loading provinces...</option>';
        }

        try {
            const response = await fetch(`/StudentApplication/GetStatesByCountry?country=${countrySelect.value}`);
            const states = await response.json();

            if (currentStateElement.tagName === 'SELECT') {
                currentStateElement.innerHTML = '<option value="">Select Province</option>';

                // Check if we got valid data
                if (Array.isArray(states) && states.length > 0) {
                    states.forEach(state => {
                        const option = document.createElement('option');
                        option.value = state;
                        option.textContent = state;
                        currentStateElement.appendChild(option);
                    });

                    // Reset city to dropdown when we have valid state data
                    const currentCityElement = document.getElementById('City');
                    if (currentCityElement && currentCityElement.tagName === 'INPUT') {
                        // Force city field reset through rebuild
                        const cityContainer = currentCityElement.closest('div').parentElement;
                        const cityName = currentCityElement.name;
                        const cityClassName = currentCityElement.className;

                        // Remove manual entry indicators
                        cityContainer.querySelectorAll('.manual-entry-indicator').forEach(el => el.remove());

                        // Clear and rebuild city container
                        cityContainer.innerHTML = `
                        <div class="relative">
                            <select id="City" 
                                    name="${cityName}"
                                    class="${cityClassName}"
                                    data-name="City"
                                    disabled
                                    required>
                                <option value="" disabled selected>Select City</option>
                            </select>
                            <div class="absolute inset-y-0 left-0 flex items-center pl-3 pointer-events-none text-slate-400">
                                <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5" viewBox="0 0 20 20" fill="currentColor">
                                    <path d="M10.707 2.293a1 1 0 00-1.414 0l-7 7a1 1 0 001.414 1.414L4 10.414V17a1 1 0 001 1h2a1 1 0 001-1v-2a1 1 0 011-1h2a1 1 0 011 1v2a1 1 0 001 1h2a1 1 0 001-1v-6.586l.293.293a1 1 0 001.414-1.414l-7-7z" />
                                </svg>
                            </div>
                        </div>
                        <span class="text-red-500 text-sm mt-1 block"></span>
                    `;
                    }
                } else {
                    // No states found - enable manual entry
                    enableManualStateEntry();
                    return;
                }
            }
        } catch (error) {
            console.error('Error fetching states:', error);
            enableManualStateEntry();
            return;
        } finally {
            const stateElement = document.getElementById('State');
            if (stateElement) {
                stateElement.disabled = false;
            }

            // Reset city dropdown
            const cityElement = document.getElementById('City');
            if (cityElement && cityElement.tagName === 'SELECT') {
                cityElement.disabled = true;
                cityElement.innerHTML = '<option value="">Select City</option>';
            }
        }
    }

    function enableManualStateEntry() {
        const currentStateElement = document.getElementById('State');
        const stateContainer = currentStateElement.parentElement;

        // Check if state is already in manual mode (input instead of select)
        if (currentStateElement.tagName === 'INPUT') {
            // Already in manual mode, don't do anything
            return;
        }

        // Remove any existing manual entry indicators
        const existingIndicator = stateContainer.querySelector('.manual-entry-indicator');
        if (existingIndicator) {
            existingIndicator.remove();
        }

        // Remove the original icon div (it's a sibling to the select)
        const originalIcon = stateContainer.querySelector('.absolute.inset-y-0.left-0');
        if (originalIcon) {
            originalIcon.remove();
        }

        // Create wrapper div to maintain icon positioning
        const inputWrapper = document.createElement('div');
        inputWrapper.className = 'relative';

        // Create new input element
        const stateInput = document.createElement('input');
        stateInput.type = 'text';
        stateInput.id = 'State';
        stateInput.name = currentStateElement.name;
        stateInput.className = currentStateElement.className;
        stateInput.placeholder = 'Enter Province/State manually';
        stateInput.required = currentStateElement.required;
        stateInput.setAttribute('data-name', 'Province');

        // Create the icon (same as in your HTML)
        const iconDiv = document.createElement('div');
        iconDiv.className = 'absolute inset-y-0 left-0 flex items-center pl-3 pointer-events-none text-slate-400';
        iconDiv.innerHTML = `
            <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5" viewBox="0 0 20 20" fill="currentColor">
                <path d="M10.707 2.293a1 1 0 00-1.414 0l-7 7a1 1 0 001.414 1.414L4 10.414V17a1 1 0 001 1h2a1 1 0 001-1v-2a1 1 0 011-1h2a1 1 0 011 1v2a1 1 0 001 1h2a1 1 0 001-1v-6.586l.293.293a1 1 0 001.414-1.414l-7-7z" />
            </svg>
        `;

        // Assemble the wrapper
        inputWrapper.appendChild(stateInput);
        inputWrapper.appendChild(iconDiv);

        // Replace select with input wrapper
        stateContainer.replaceChild(inputWrapper, currentStateElement);

        // Add manual entry indicator
        const manualIndicator = document.createElement('small');
        manualIndicator.className = 'text-amber-600 text-xs mt-1 block manual-entry-indicator';
        manualIndicator.textContent = 'Manual entry enabled - provinces not available for this country';
        stateContainer.appendChild(manualIndicator);

        // Also enable manual city entry
        enableManualCityEntry();

        showNotification('warning', 'Manual Entry', 'Provinces not available for this country. Please enter manually.');
    }

    async function updateCities() {
        const currentStateElement = document.getElementById('State');
        const currentCityElement = document.getElementById('City');

        if (!countrySelect?.value || !currentStateElement?.value) return;

        // If state is now an input (manual entry), enable manual city entry
        if (currentStateElement.tagName === 'INPUT') {
            enableManualCityEntry();
            return;
        }

        currentCityElement.disabled = true;

        if (currentCityElement.tagName === 'SELECT') {
            currentCityElement.innerHTML = '<option value="">Loading cities...</option>';
        }

        try {
            const response = await fetch(`/StudentApplication/GetCitiesByState?country=${countrySelect.value}&state=${currentStateElement.value}`);
            const cities = await response.json();

            if (currentCityElement.tagName === 'SELECT') {
                currentCityElement.innerHTML = '<option value="">Select City</option>';

                if (Array.isArray(cities) && cities.length > 0) {
                    cities.forEach(city => {
                        const option = document.createElement('option');
                        option.value = city;
                        option.textContent = city;
                        currentCityElement.appendChild(option);
                    });
                } else {
                    enableManualCityEntry();
                }
            }
        } catch (error) {
            console.error('Error fetching cities:', error);
            enableManualCityEntry();
        } finally {
            const cityElement = document.getElementById('City');
            if (cityElement) {
                cityElement.disabled = false;
            }
        }
    }

    function enableManualCityEntry() {
        const currentCityElement = document.getElementById('City');
        const cityContainer = currentCityElement.parentElement;

        // Check if city is already in manual mode (input instead of select)
        if (currentCityElement.tagName === 'INPUT') {
            // Already in manual mode, don't do anything
            return;
        }

        // Remove any existing manual entry indicators
        const existingIndicator = cityContainer.querySelector('.manual-entry-indicator');
        if (existingIndicator) {
            existingIndicator.remove();
        }

        // Remove the original icon div (it's a sibling to the select)
        const originalIcon = cityContainer.querySelector('.absolute.inset-y-0.left-0');
        if (originalIcon) {
            originalIcon.remove();
        }

        // Create wrapper div to maintain icon positioning
        const inputWrapper = document.createElement('div');
        inputWrapper.className = 'relative';

        // Create new input element
        const cityInput = document.createElement('input');
        cityInput.type = 'text';
        cityInput.id = 'City';
        cityInput.name = currentCityElement.name;
        cityInput.className = currentCityElement.className;
        cityInput.placeholder = 'Enter City manually';
        cityInput.required = currentCityElement.required;
        cityInput.setAttribute('data-name', 'City');

        // Create the icon (same as in your HTML)
        const iconDiv = document.createElement('div');
        iconDiv.className = 'absolute inset-y-0 left-0 flex items-center pl-3 pointer-events-none text-slate-400';
        iconDiv.innerHTML = `
            <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5" viewBox="0 0 20 20" fill="currentColor">
                <path d="M10.707 2.293a1 1 0 00-1.414 0l-7 7a1 1 0 001.414 1.414L4 10.414V17a1 1 0 001 1h2a1 1 0 001-1v-2a1 1 0 011-1h2a1 1 0 011 1v2a1 1 0 001 1h2a1 1 0 001-1v-6.586l.293.293a1 1 0 001.414-1.414l-7-7z" />
            </svg>
        `;

        // Assemble the wrapper
        inputWrapper.appendChild(cityInput);
        inputWrapper.appendChild(iconDiv);

        // Replace select with input wrapper
        cityContainer.replaceChild(inputWrapper, currentCityElement);

        // Add manual entry indicator
        const manualIndicator = document.createElement('small');
        manualIndicator.className = 'text-amber-600 text-xs mt-1 block manual-entry-indicator';
        manualIndicator.textContent = 'Manual entry enabled - cities not available';
        cityContainer.appendChild(manualIndicator);
    }


    // Reset function for when country changes
    function resetLocationInputs() {
        // Reset State field
        const currentState = document.getElementById('State');
        if (currentState && currentState.tagName === 'INPUT') {
            const stateContainer = currentState.closest('div').parentElement; // Get the parent container

            // Remove manual entry indicators
            stateContainer.querySelectorAll('.manual-entry-indicator').forEach(el => el.remove());

            // Clear the container and rebuild
            stateContainer.innerHTML = `
                <div class="relative">
                    <select id="State" 
                            name="${currentState.name}"
                            class="${currentState.className}"
                            data-name="Province"
                            required>
                        <option value="" disabled selected>Select Province</option>
                    </select>
                    <div class="absolute inset-y-0 left-0 flex items-center pl-3 pointer-events-none text-slate-400">
                        <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5" viewBox="0 0 20 20" fill="currentColor">
                            <path d="M10.707 2.293a1 1 0 00-1.414 0l-7 7a1 1 0 001.414 1.414L4 10.414V17a1 1 0 001 1h2a1 1 0 001-1v-2a1 1 0 011-1h2a1 1 0 011 1v2a1 1 0 001 1h2a1 1 0 001-1v-6.586l.293.293a1 1 0 001.414-1.414l-7-7z" />
                        </svg>
                    </div>
                </div>
                <span class="text-red-500 text-sm mt-1 block"></span>
            `;
        }

        // Reset City field
        const currentCity = document.getElementById('City');
        if (currentCity && currentCity.tagName === 'INPUT') {
            const cityContainer = currentCity.closest('div').parentElement; // Get the parent container

            // Remove manual entry indicators
            cityContainer.querySelectorAll('.manual-entry-indicator').forEach(el => el.remove());

            // Clear the container and rebuild
            cityContainer.innerHTML = `
                <div class="relative">
                    <select id="City" 
                            name="${currentCity.name}"
                            class="${currentCity.className}"
                            data-name="City"
                            required>
                        <option value="" disabled selected>Select City</option>
                    </select>
                    <div class="absolute inset-y-0 left-0 flex items-center pl-3 pointer-events-none text-slate-400">
                        <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5" viewBox="0 0 20 20" fill="currentColor">
                            <path d="M10.707 2.293a1 1 0 00-1.414 0l-7 7a1 1 0 001.414 1.414L4 10.414V17a1 1 0 001 1h2a1 1 0 001-1v-2a1 1 0 011-1h2a1 1 0 011 1v2a1 1 0 001 1h2a1 1 0 001-1v-6.586l.293.293a1 1 0 001.414-1.414l-7-7z" />
                        </svg>
                    </div>
                </div>
                <span class="text-red-500 text-sm mt-1 block"></span>
            `;
        }
    }

    // Event listeners with delegation for dynamic elements
    countrySelect?.addEventListener('change', () => {
        resetLocationInputs();
        updateStates();
    });

    // Use event delegation for state changes since element might be replaced
    document.addEventListener('change', (e) => {
        if (e.target.id === 'State') {
            updateCities();
        }
    });

    // Initialize date input
    const dateInput = document.getElementById('DateOfBirth');
    if (dateInput) {
        const today = new Date().toISOString().split('T')[0];
        dateInput.max = today;
        if (dateInput.value === '0001-01-01' || !dateInput.value) {
            dateInput.value = '';
        }
    }

    // Conditional field visibility
    function toggleStudyPermit() {
        const isForeigner = document.getElementById('Foreigner')?.value;
        const studyPermitField = document.getElementById('StudyPermitField');

        if (studyPermitField) {
            studyPermitField.classList.toggle('hidden', isForeigner !== 'Yes');
            const studyPermitInput = studyPermitField.querySelector('input');
            if (studyPermitInput) {
                studyPermitInput.required = isForeigner === 'Yes';
            }
        }
    }

    toggleSubjectsSection(); // Initialize subjects section state

    function toggleSubjectsSection() {
        const isForeigner = document.getElementById('Foreigner')?.value;
        const subjectsSection = document.getElementById('secondarySchoolResults');
        const subjectRows = document.querySelectorAll('.subject-grade-pair');

        if (subjectsSection) {
            if (isForeigner === 'true') {
                // Hide the subjects section for foreign students
                subjectsSection.style.display = 'none';

                // Remove required attributes from all subject and grade selects
                subjectRows.forEach(row => {
                    const subjectSelect = row.querySelector('.subject-select');
                    const gradeSelect = row.querySelector('.grade-select');

                    if (subjectSelect) {
                        subjectSelect.removeAttribute('required');
                        subjectSelect.value = ''; // Clear any selected values
                    }
                    if (gradeSelect) {
                        gradeSelect.removeAttribute('required');
                        gradeSelect.value = ''; // Clear any selected values
                    }
                });

                // Show a helpful message to foreign students
                showForeignStudentMessage();

            } else if (isForeigner === 'false') {
                // Show the subjects section for domestic students
                subjectsSection.style.display = 'block';

                // Restore required attributes for subject and grade selects
                subjectRows.forEach(row => {
                    const subjectSelect = row.querySelector('.subject-select');
                    const gradeSelect = row.querySelector('.grade-select');

                    if (subjectSelect) {
                        subjectSelect.setAttribute('required', 'required');
                    }
                    if (gradeSelect) {
                        gradeSelect.setAttribute('required', 'required');
                    }
                });

                // Hide foreign student message if it exists
                hideForeignStudentMessage();
            }
        }
    }

    function showForeignStudentMessage() {
        // Check if message already exists
        if (document.getElementById('foreign-student-message')) {
            return;
        }

        const subjectsSection = document.getElementById('secondarySchoolResults');
        if (subjectsSection) {
            const messageDiv = document.createElement('div');
            messageDiv.id = 'foreign-student-message';
            messageDiv.className = 'mt-8 bg-blue-50 border-l-4 border-blue-500 p-4 rounded-lg';
            messageDiv.innerHTML = `
            <div class="flex">
                <div class="flex-shrink-0">
                    <svg class="h-5 w-5 text-blue-600" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 20" fill="currentColor">
                        <path fill-rule="evenodd" d="M18 10a8 8 0 11-16 0 8 8 0 0116 0zm-7-4a1 1 0 11-2 0 1 1 0 012 0zM9 9a1 1 0 000 2v3a1 1 0 001 1h1a1 1 0 100-2v-3a1 1 0 00-1-1H9z" clip-rule="evenodd" />
                    </svg>
                </div>
                <div class="ml-3">
                    <h3 class="text-sm font-medium text-blue-800">Foreign Student Information</h3>
                    <div class="mt-2 text-sm text-blue-700">
                        <p>As a foreign student, you don't need to enter individual subjects and grades. Please ensure you upload your complete academic transcript in the Documents section. Our admissions team will review your qualifications directly from your transcript.</p>
                    </div>
                </div>
            </div>
        `;

            // Insert the message before the subjects section
            subjectsSection.parentNode.insertBefore(messageDiv, subjectsSection);
        }
    }

    function hideForeignStudentMessage() {
        const messageDiv = document.getElementById('foreign-student-message');
        if (messageDiv) {
            messageDiv.remove();
        }
    }

    document.getElementById('Foreigner')?.addEventListener('change', function () {
        toggleStudyPermit();
        toggleSubjectsSection();
    });


    // Toggle tertiary education details based on former school level
    function toggleTertiaryEducation() {
        const formerSchoolLevel = document.getElementById('FormerSchoolLevel')?.value;
        const tertiarySchoolResults = document.getElementById('tertiarySchoolResults');

        if (tertiarySchoolResults) {
            const showTertiary = ['Phd', 'Master\'s', 'Bachelor\'s', 'Diploma'].includes(formerSchoolLevel);
            tertiarySchoolResults.classList.toggle('hidden', !showTertiary);

            // Toggle required attribute on tertiary fields
            tertiarySchoolResults.querySelectorAll('input').forEach(input => {
                input.required = showTertiary;
            });
        }
    }

    document.getElementById('FormerSchoolLevel')?.addEventListener('change', toggleTertiaryEducation);
    toggleTertiaryEducation(); // Initialize state




    // Initialize international telephone input for the primary phone
    const phoneInput = document.getElementById('PhoneNumber');
    if (phoneInput) {
        const iti = window.intlTelInput(phoneInput, {
            utilsScript: "https://cdnjs.cloudflare.com/ajax/libs/intl-tel-input/17.0.13/js/utils.js",
            initialCountry: "zm", // Set Zambia as default country
            separateDialCode: true,
            autoPlaceholder: "polite",
            preferredCountries: ["zm", "za", "gb", "us"],
            formatOnDisplay: false // Disable auto-formatting to control input better
        });

        // Function to enforce numeric only (no fixed length limit)
        function enforcePhoneFormat(input, itiInstance) {
            let value = input.value.replace(/\D/g, ''); // Remove all non-digits

            // Limit to 15 digits (international standard max length)
            if (value.length > 15) {
                value = value.substring(0, 15);
            }

            // Update the input value
            input.value = value;

            return value;
        }

        // Function to update the full phone number for form submission
        function updateFullPhoneNumber(input, itiInstance) {
            const nationalNumber = input.value.replace(/\D/g, ''); // Get clean digits
            if (nationalNumber && nationalNumber.length > 0) {
                const countryCode = itiInstance.getSelectedCountryData().dialCode;
                const fullNumber = `+${countryCode}${nationalNumber}`;

                // Store the full number in a hidden input or data attribute
                input.setAttribute('data-full-number', fullNumber);
                console.log('Full phone number:', fullNumber);

                return fullNumber;
            }
            return null;
        }

        // Add input event listener to enforce format and validate
        phoneInput.addEventListener('input', function () {
            const cleanValue = enforcePhoneFormat(this, iti);

            // Validate using intl-tel-input's validation
            if (iti.isValidNumber()) {
                this.classList.remove('border-red-500');
                this.classList.add('border-slate-300');
                updateFullPhoneNumber(this, iti);
            } else if (cleanValue.length > 0) {
                this.classList.remove('border-slate-300');
                this.classList.add('border-red-500');
            }
        });

        // Add keypress event to prevent non-numeric input
        phoneInput.addEventListener('keypress', function (e) {
            // Allow backspace, delete, tab, escape, enter
            if ([8, 9, 27, 13, 46].indexOf(e.keyCode) !== -1 ||
                // Allow Ctrl+A, Ctrl+C, Ctrl+V, Ctrl+X
                (e.keyCode === 65 && e.ctrlKey === true) ||
                (e.keyCode === 67 && e.ctrlKey === true) ||
                (e.keyCode === 86 && e.ctrlKey === true) ||
                (e.keyCode === 88 && e.ctrlKey === true)) {
                return;
            }
            // Ensure that it is a number and stop the keypress
            if ((e.shiftKey || (e.keyCode < 48 || e.keyCode > 57)) && (e.keyCode < 96 || e.keyCode > 105)) {
                e.preventDefault();
            }
            // Also prevent if already at max length (15 digits)
            if (this.value.replace(/\D/g, '').length >= 15) {
                e.preventDefault();
            }
        });

        // Add paste event to handle pasted content
        phoneInput.addEventListener('paste', function (e) {
            e.preventDefault();
            const paste = (e.clipboardData || window.clipboardData).getData('text');
            const numbersOnly = paste.replace(/\D/g, '');
            const limited = numbersOnly.substring(0, 15);
            this.value = limited;

            // Trigger input event to validate
            this.dispatchEvent(new Event('input'));
        });

        // Add blur listener to finalize phone number
        phoneInput.addEventListener('blur', function () {
            const fullNumber = updateFullPhoneNumber(this, iti);
            if (fullNumber) {
                console.log('Primary phone blur - Full number:', fullNumber);
                // Update the actual form value that will be submitted
                this.value = this.getAttribute('data-full-number') || this.value;
            }
        });

        // Update validation function to use intl-tel-input's validation
        window.isValidPhone = function (phone) {
            // Use the intl-tel-input instance to validate the number
            return iti.isValidNumber();
        };
    }

    // Initialize international telephone input for next of kin phone
    const nextOfKinPhoneInput = document.getElementById('NextOfKinPhone');
    if (nextOfKinPhoneInput) {
        const nextOfKinIti = window.intlTelInput(nextOfKinPhoneInput, {
            utilsScript: "https://cdnjs.cloudflare.com/ajax/libs/intl-tel-input/17.0.13/js/utils.js",
            initialCountry: "zm", // Set Zambia as default country
            separateDialCode: true,
            autoPlaceholder: "polite",
            preferredCountries: ["zm", "za", "gb", "us"],
            formatOnDisplay: false // Disable auto-formatting to control input better
        });

        // Function to enforce numeric only (same as above)
        function enforceNextOfKinPhoneFormat(input, itiInstance) {
            let value = input.value.replace(/\D/g, ''); // Remove all non-digits

            // Limit to 15 digits (international standard max length)
            if (value.length > 15) {
                value = value.substring(0, 15);
            }

            // Update the input value
            input.value = value;

            return value;
        }

        // Function to update the full phone number for next of kin
        function updateNextOfKinFullPhoneNumber(input, itiInstance) {
            const nationalNumber = input.value.replace(/\D/g, ''); // Get clean digits
            if (nationalNumber && nationalNumber.length > 0) {
                const countryCode = itiInstance.getSelectedCountryData().dialCode;
                const fullNumber = `+${countryCode}${nationalNumber}`;

                // Store the full number in a hidden input or data attribute
                input.setAttribute('data-full-number', fullNumber);
                console.log('Next of Kin full phone number:', fullNumber);

                return fullNumber;
            }
            return null;
        }

        // Add input event listener to enforce format and validate
        nextOfKinPhoneInput.addEventListener('input', function () {
            const cleanValue = enforceNextOfKinPhoneFormat(this, nextOfKinIti);

            // Validate using intl-tel-input's validation
            if (nextOfKinIti.isValidNumber()) {
                this.classList.remove('border-red-500');
                this.classList.add('border-slate-300');
                updateNextOfKinFullPhoneNumber(this, nextOfKinIti);
            } else if (cleanValue.length > 0) {
                this.classList.remove('border-slate-300');
                this.classList.add('border-red-500');
            }
        });

        // Add keypress event to prevent non-numeric input
        nextOfKinPhoneInput.addEventListener('keypress', function (e) {
            // Allow backspace, delete, tab, escape, enter
            if ([8, 9, 27, 13, 46].indexOf(e.keyCode) !== -1 ||
                // Allow Ctrl+A, Ctrl+C, Ctrl+V, Ctrl+X
                (e.keyCode === 65 && e.ctrlKey === true) ||
                (e.keyCode === 67 && e.ctrlKey === true) ||
                (e.keyCode === 86 && e.ctrlKey === true) ||
                (e.keyCode === 88 && e.ctrlKey === true)) {
                return;
            }
            // Ensure that it is a number and stop the keypress
            if ((e.shiftKey || (e.keyCode < 48 || e.keyCode > 57)) && (e.keyCode < 96 || e.keyCode > 105)) {
                e.preventDefault();
            }
            // Also prevent if already at max length (15 digits)
            if (this.value.replace(/\D/g, '').length >= 15) {
                e.preventDefault();
            }
        });

        // Add paste event to handle pasted content
        nextOfKinPhoneInput.addEventListener('paste', function (e) {
            e.preventDefault();
            const paste = (e.clipboardData || window.clipboardData).getData('text');
            const numbersOnly = paste.replace(/\D/g, '');
            const limited = numbersOnly.substring(0, 15);
            this.value = limited;

            // Trigger input event to validate
            this.dispatchEvent(new Event('input'));
        });

        // Add blur listener to finalize phone number
        nextOfKinPhoneInput.addEventListener('blur', function () {
            const fullNumber = updateNextOfKinFullPhoneNumber(this, nextOfKinIti);
            if (fullNumber) {
                console.log('Next of Kin phone blur - Full number:', fullNumber);
                // Update the actual form value that will be submitted
                this.value = this.getAttribute('data-full-number') || this.value;
            }
        });
    }



    // Initialize nationality select with search functionality
    // Initialize nationality select with Choices.js (no jQuery required)
    const nationalitySelect = document.getElementById('Nationality');
    if (nationalitySelect) {
        // List of nationalities with flag codes
        const nationalities = [
            { code: 'af', name: 'Afghan' }, { code: 'al', name: 'Albanian' }, { code: 'dz', name: 'Algerian' },
            { code: 'us', name: 'American' }, { code: 'ad', name: 'Andorran' }, { code: 'ao', name: 'Angolan' },
            { code: 'ag', name: 'Antiguan' }, { code: 'ar', name: 'Argentine' }, { code: 'am', name: 'Armenian' },
            { code: 'au', name: 'Australian' }, { code: 'at', name: 'Austrian' }, { code: 'az', name: 'Azerbaijani' },
            { code: 'bs', name: 'Bahamian' }, { code: 'bh', name: 'Bahraini' }, { code: 'bd', name: 'Bangladeshi' },
            { code: 'bb', name: 'Barbadian' }, { code: 'by', name: 'Belarusian' }, { code: 'be', name: 'Belgian' },
            { code: 'bz', name: 'Belizean' }, { code: 'bj', name: 'Beninese' }, { code: 'bt', name: 'Bhutanese' },
            { code: 'bo', name: 'Bolivian' }, { code: 'ba', name: 'Bosnian' }, { code: 'bw', name: 'Batswana' },
            { code: 'br', name: 'Brazilian' }, { code: 'gb', name: 'British' }, { code: 'bn', name: 'Bruneian' },
            { code: 'bg', name: 'Bulgarian' }, { code: 'bf', name: 'Burkinabe' }, { code: 'bi', name: 'Burundian' },
            { code: 'kh', name: 'Cambodian' }, { code: 'cm', name: 'Cameroonian' }, { code: 'ca', name: 'Canadian' },
            { code: 'cv', name: 'Cape Verdean' }, { code: 'cf', name: 'Central African' }, { code: 'td', name: 'Chadian' },
            { code: 'cl', name: 'Chilean' }, { code: 'cn', name: 'Chinese' }, { code: 'co', name: 'Colombian' },
            { code: 'km', name: 'Comorian' }, { code: 'cd', name: 'Congolese (DRC)' }, { code: 'cg', name: 'Congolese (Republic)' },
            { code: 'cr', name: 'Costa Rican' }, { code: 'hr', name: 'Croatian' }, { code: 'cu', name: 'Cuban' },
            { code: 'cy', name: 'Cypriot' }, { code: 'cz', name: 'Czech' }, { code: 'dk', name: 'Danish' },
            { code: 'dj', name: 'Djiboutian' }, { code: 'dm', name: 'Dominican' }, { code: 'do', name: 'Dominican (Republic)' },
            { code: 'tl', name: 'East Timorese' }, { code: 'ec', name: 'Ecuadorian' }, { code: 'eg', name: 'Egyptian' },
            { code: 'sv', name: 'Salvadoran' }, { code: 'gq', name: 'Equatorial Guinean' }, { code: 'er', name: 'Eritrean' },
            { code: 'ee', name: 'Estonian' }, { code: 'et', name: 'Ethiopian' }, { code: 'fj', name: 'Fijian' },
            { code: 'fi', name: 'Finnish' }, { code: 'fr', name: 'French' }, { code: 'ga', name: 'Gabonese' },
            { code: 'gm', name: 'Gambian' }, { code: 'ge', name: 'Georgian' }, { code: 'de', name: 'German' },
            { code: 'gh', name: 'Ghanaian' }, { code: 'gr', name: 'Greek' }, { code: 'gd', name: 'Grenadian' },
            { code: 'gt', name: 'Guatemalan' }, { code: 'gn', name: 'Guinean' }, { code: 'gw', name: 'Guinea-Bissauan' },
            { code: 'gy', name: 'Guyanese' }, { code: 'ht', name: 'Haitian' }, { code: 'hn', name: 'Honduran' },
            { code: 'hu', name: 'Hungarian' }, { code: 'is', name: 'Icelandic' }, { code: 'in', name: 'Indian' },
            { code: 'id', name: 'Indonesian' }, { code: 'ir', name: 'Iranian' }, { code: 'iq', name: 'Iraqi' },
            { code: 'ie', name: 'Irish' }, { code: 'il', name: 'Israeli' }, { code: 'it', name: 'Italian' },
            { code: 'ci', name: 'Ivorian' }, { code: 'jm', name: 'Jamaican' }, { code: 'jp', name: 'Japanese' },
            { code: 'jo', name: 'Jordanian' }, { code: 'kz', name: 'Kazakhstani' }, { code: 'ke', name: 'Kenyan' },
            { code: 'ki', name: 'I-Kiribati' }, { code: 'kw', name: 'Kuwaiti' }, { code: 'kg', name: 'Kyrgyzstani' },
            { code: 'la', name: 'Laotian' }, { code: 'lv', name: 'Latvian' }, { code: 'lb', name: 'Lebanese' },
            { code: 'ls', name: 'Basotho' }, { code: 'lr', name: 'Liberian' }, { code: 'ly', name: 'Libyan' },
            { code: 'li', name: 'Liechtensteiner' }, { code: 'lt', name: 'Lithuanian' }, { code: 'lu', name: 'Luxembourger' },
            { code: 'mk', name: 'Macedonian' }, { code: 'mg', name: 'Malagasy' }, { code: 'mw', name: 'Malawian' },
            { code: 'my', name: 'Malaysian' }, { code: 'mv', name: 'Maldivian' }, { code: 'ml', name: 'Malian' },
            { code: 'mt', name: 'Maltese' }, { code: 'mh', name: 'Marshallese' }, { code: 'mr', name: 'Mauritanian' },
            { code: 'mu', name: 'Mauritian' }, { code: 'mx', name: 'Mexican' }, { code: 'fm', name: 'Micronesian' },
            { code: 'md', name: 'Moldovan' }, { code: 'mc', name: 'Monacan' }, { code: 'mn', name: 'Mongolian' },
            { code: 'me', name: 'Montenegrin' }, { code: 'ma', name: 'Moroccan' }, { code: 'mz', name: 'Mozambican' },
            { code: 'mm', name: 'Myanmar' }, { code: 'na', name: 'Namibian' }, { code: 'nr', name: 'Nauruan' },
            { code: 'np', name: 'Nepalese' }, { code: 'nl', name: 'Dutch' }, { code: 'nz', name: 'New Zealand' },
            { code: 'ni', name: 'Nicaraguan' }, { code: 'ne', name: 'Nigerien' }, { code: 'ng', name: 'Nigerian' },
            { code: 'no', name: 'Norwegian' }, { code: 'om', name: 'Omani' }, { code: 'pk', name: 'Pakistani' },
            { code: 'pw', name: 'Palauan' }, { code: 'pa', name: 'Panamanian' }, { code: 'pg', name: 'Papua New Guinean' },
            { code: 'py', name: 'Paraguayan' }, { code: 'pe', name: 'Peruvian' }, { code: 'ph', name: 'Filipino' },
            { code: 'pl', name: 'Polish' }, { code: 'pt', name: 'Portuguese' }, { code: 'qa', name: 'Qatari' },
            { code: 'ro', name: 'Romanian' }, { code: 'ru', name: 'Russian' }, { code: 'rw', name: 'Rwandan' },
            { code: 'kn', name: 'Kittian' }, { code: 'lc', name: 'Saint Lucian' }, { code: 'vc', name: 'Vincentian' },
            { code: 'ws', name: 'Samoan' }, { code: 'sm', name: 'Sammarinese' }, { code: 'st', name: 'Sao Tomean' },
            { code: 'sa', name: 'Saudi Arabian' }, { code: 'sn', name: 'Senegalese' }, { code: 'rs', name: 'Serbian' },
            { code: 'sc', name: 'Seychellois' }, { code: 'sl', name: 'Sierra Leonean' }, { code: 'sg', name: 'Singaporean' },
            { code: 'sk', name: 'Slovak' }, { code: 'si', name: 'Slovenian' }, { code: 'sb', name: 'Solomon Islander' },
            { code: 'so', name: 'Somali' }, { code: 'za', name: 'South African' }, { code: 'kr', name: 'South Korean' },
            { code: 'ss', name: 'South Sudanese' }, { code: 'es', name: 'Spanish' }, { code: 'lk', name: 'Sri Lankan' },
            { code: 'sd', name: 'Sudanese' }, { code: 'sr', name: 'Surinamese' }, { code: 'sz', name: 'Swazi' },
            { code: 'se', name: 'Swedish' }, { code: 'ch', name: 'Swiss' }, { code: 'sy', name: 'Syrian' },
            { code: 'tw', name: 'Taiwanese' }, { code: 'tj', name: 'Tajikistani' }, { code: 'tz', name: 'Tanzanian' },
            { code: 'th', name: 'Thai' }, { code: 'tg', name: 'Togolese' }, { code: 'to', name: 'Tongan' },
            { code: 'tt', name: 'Trinidadian' }, { code: 'tn', name: 'Tunisian' }, { code: 'tr', name: 'Turkish' },
            { code: 'tm', name: 'Turkmen' }, { code: 'tv', name: 'Tuvaluan' }, { code: 'ug', name: 'Ugandan' },
            { code: 'ua', name: 'Ukrainian' }, { code: 'ae', name: 'Emirati' }, { code: 'gb', name: 'British' },
            { code: 'us', name: 'American' }, { code: 'uy', name: 'Uruguayan' }, { code: 'uz', name: 'Uzbekistani' },
            { code: 'vu', name: 'Ni-Vanuatu' }, { code: 'va', name: 'Vatican' }, { code: 've', name: 'Venezuelan' },
            { code: 'vn', name: 'Vietnamese' }, { code: 'ye', name: 'Yemeni' }, { code: 'zm', name: 'Zambian' },
            { code: 'zw', name: 'Zimbabwean' }
        ];

        // Populate the select with nationalities (make sure default option is preserved)
        const defaultOption = nationalitySelect.querySelector('option[value=""]');
        nationalitySelect.innerHTML = '';
        if (defaultOption) {
            nationalitySelect.appendChild(defaultOption);
        }

        // Add all nationalities
        nationalities.forEach(nation => {
            const option = document.createElement('option');
            option.value = nation.name;
            option.textContent = nation.name;
            nationalitySelect.appendChild(option);
        });

        // Add a simple flag indicator using datalist for searching
        nationalitySelect.addEventListener('change', function () {
            const selectedOption = this.options[this.selectedIndex];
            const nation = nationalities.find(n => n.name === selectedOption.value);
            if (nation && nation.code) {
                // You could add an icon or flag here if needed
                console.log("Selected nationality:", nation.name, "Code:", nation.code);
            }
        });
    }

    // Handle "Other" religion option
    const religionSelect = document.getElementById('Religion');
    const otherReligionContainer = document.getElementById('otherReligionContainer');
    const otherReligionInput = document.getElementById('otherReligion');

    if (religionSelect && otherReligionContainer && otherReligionInput) {
        religionSelect.addEventListener('change', function () {
            if (this.value === 'Other') {
                otherReligionContainer.classList.remove('hidden');
                otherReligionInput.required = true;

                // Set a data attribute to track that we're using the custom input
                religionSelect.setAttribute('data-using-other', 'true');
            } else {
                otherReligionContainer.classList.add('hidden');
                otherReligionInput.required = false;
                religionSelect.removeAttribute('data-using-other');
            }
        });

        // Update form submission to include the "Other" religion value if needed
        form.addEventListener('submit', function (e) {
            if (religionSelect.value === 'Other' && otherReligionInput.value.trim()) {
                // Instead of modifying the select value directly, we'll submit the custom value
                const hiddenInput = document.createElement('input');
                hiddenInput.type = 'hidden';
                hiddenInput.name = 'Applicant.Religion';
                hiddenInput.value = otherReligionInput.value.trim();
                form.appendChild(hiddenInput);
            }
        });

        // Check if "Other" is already selected (in case of form reload/validation error)
        if (religionSelect.value === 'Other') {
            otherReligionContainer.classList.remove('hidden');
            otherReligionInput.required = true;
        }
    }



    // Handle "Other" relationship option
    const relationshipSelect = document.getElementById('NextOfKinRelation');
    const otherRelationContainer = document.getElementById('otherRelationContainer');
    const otherRelationInput = document.getElementById('otherRelation');

    if (relationshipSelect && otherRelationContainer && otherRelationInput) {
        relationshipSelect.addEventListener('change', function () {
            if (this.value === 'Other') {
                otherRelationContainer.classList.remove('hidden');
                otherRelationInput.required = true;
            } else {
                otherRelationContainer.classList.add('hidden');
                otherRelationInput.required = false;
            }
        });

        // Update form submission to include the "Other" relationship value if needed
        form.addEventListener('submit', function (e) {
            if (relationshipSelect.value === 'Other' && otherRelationInput.value.trim()) {
                // Create a hidden input for the custom value
                const hiddenInput = document.createElement('input');
                hiddenInput.type = 'hidden';
                hiddenInput.name = 'Applicant.NextOfKinRelation';
                hiddenInput.value = otherRelationInput.value.trim();
                form.appendChild(hiddenInput);
            }
        });

        // Check if "Other" is already selected (in case of form reload/validation error)
        if (relationshipSelect.value === 'Other') {
            otherRelationContainer.classList.remove('hidden');
            otherRelationInput.required = true;
        }
    }



    // Populate year dropdowns and handle period combination
    function populateYearDropdowns() {
        const currentYear = new Date().getFullYear();
        const startYear = currentYear - 50; // 50 years back
        const endYear = currentYear + 2; // 2 years ahead

        const startYearSelect = document.getElementById('SecondarySchoolStartYear');
        const endYearSelect = document.getElementById('SecondarySchoolEndYear');
        const hiddenPeriodInput = document.getElementById('SecondarySchoolPeriodHidden');

        if (startYearSelect && endYearSelect) {
            // Populate start year dropdown
            for (let year = endYear; year >= startYear; year--) {
                const option = document.createElement('option');
                option.value = year;
                option.textContent = year;
                startYearSelect.appendChild(option);
            }

            // Populate end year dropdown
            for (let year = endYear; year >= startYear; year--) {
                const option = document.createElement('option');
                option.value = year;
                option.textContent = year;
                endYearSelect.appendChild(option);
            }

            // Combine values when either dropdown changes
            function updatePeriod() {
                const startYear = startYearSelect.value;
                const endYear = endYearSelect.value;

                if (startYear && endYear) {
                    if (parseInt(startYear) > parseInt(endYear)) {
                        showNotification('error', 'Invalid Period', 'Start year cannot be later than end year.');
                        return;
                    }
                    hiddenPeriodInput.value = `${startYear} - ${endYear}`;
                } else {
                    hiddenPeriodInput.value = '';
                }
            }

            startYearSelect.addEventListener('change', updatePeriod);
            endYearSelect.addEventListener('change', updatePeriod);
        }
    }

    function populateYearDropdownsPrimary() {
        // Define the year range here as well
        const currentYear = new Date().getFullYear();
        const startYear = currentYear - 50; // 50 years back
        const endYear = currentYear + 2; // 2 years ahead

        const primaryStartYearSelect = document.getElementById('PrimarySchoolStartYear');
        const primaryEndYearSelect = document.getElementById('PrimarySchoolEndYear');
        const primaryHiddenPeriodInput = document.getElementById('PrimarySchoolPeriodHidden');

        if (primaryStartYearSelect && primaryEndYearSelect) {
            // Populate primary school year dropdowns
            for (let year = endYear; year >= startYear; year--) {
                const startOption = document.createElement('option');
                startOption.value = year;
                startOption.textContent = year;
                primaryStartYearSelect.appendChild(startOption);

                const endOption = document.createElement('option');
                endOption.value = year;
                endOption.textContent = year;
                primaryEndYearSelect.appendChild(endOption);
            }

            // Combine primary school period values
            function updatePrimaryPeriod() {
                const startYear = primaryStartYearSelect.value;
                const endYear = primaryEndYearSelect.value;

                if (startYear && endYear) {
                    if (parseInt(startYear) > parseInt(endYear)) {
                        showNotification('error', 'Invalid Period', 'Primary school start year cannot be later than end year.');
                        return;
                    }
                    primaryHiddenPeriodInput.value = `${startYear} - ${endYear}`;
                } else if (startYear || endYear) {
                    // If only one year is selected, still save it
                    primaryHiddenPeriodInput.value = startYear || endYear;
                } else {
                    primaryHiddenPeriodInput.value = '';
                }
            }

            primaryStartYearSelect.addEventListener('change', updatePrimaryPeriod);
            primaryEndYearSelect.addEventListener('change', updatePrimaryPeriod);
        }
    }

    // Call the functions to populate dropdowns
    populateYearDropdowns();
    populateYearDropdownsPrimary();


    function verifyForeignStudentStatus() {
        const isForeigner = document.getElementById('Foreigner')?.value;
        console.log(isForeigner);
        if (isForeigner === 'true' || isForeigner === 'false') {
            toggleSubjectsSection();
            toggleStudyPermit();
            //updateStepGuidanceForForeignStudents();
        }
    }

    // Passport Photo Upload Handling with Validation
    // Passport Photo Upload Handling with Validation
    function handlePassportPhotoUpload() {
        const input = document.getElementById('PassportPhoto');
        const preview = document.getElementById('passport-photo-preview');
        const validationDiv = document.getElementById('passport-photo-validation');
        const submitButton = document.querySelector('.submit-form');

        if (!input || !preview) return;

        // Track photo validation state
        let photoValidationPassed = false;
        let hasPhotoWarnings = false;

        function updateSubmitButtonState() {
            if (submitButton) {
                if (hasPhotoWarnings && !photoValidationPassed) {
                    // Has issues but allow submission with warning styling
                    submitButton.disabled = false;
                    submitButton.classList.remove('bg-cyan-600', 'hover:bg-cyan-700');
                    submitButton.classList.add('bg-amber-600', 'hover:bg-amber-700');
                    submitButton.innerHTML = `
                    <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5 inline mr-2" viewBox="0 0 20 20" fill="currentColor">
                        <path fill-rule="evenodd" d="M8.257 3.099c.765-1.36 2.722-1.36 3.486 0l5.58 9.92c.75 1.334-.213 2.98-1.742 2.98H4.42c-1.53 0-2.493-1.646-1.743-2.98l5.58-9.92zM11 13a1 1 0 11-2 0 1 1 0 012 0zm-1-8a1 1 0 00-1 1v3a1 1 0 002 0V6a1 1 0 00-1-1z" clip-rule="evenodd" />
                    </svg>
                    Submit Anyway
                `;
                } else if (hasPhotoWarnings && photoValidationPassed) {
                    // Has warnings but quality is acceptable
                    submitButton.disabled = false;
                    submitButton.classList.remove('bg-cyan-600', 'hover:bg-cyan-700', 'bg-red-600', 'hover:bg-red-700');
                    submitButton.classList.add('bg-amber-600', 'hover:bg-amber-700');
                    submitButton.innerHTML = `
                    <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5 inline mr-2" viewBox="0 0 20 20" fill="currentColor">
                        <path fill-rule="evenodd" d="M16.707 5.293a1 1 0 010 1.414l-8 8a1 1 0 01-1.414 0l-4-4a1 1 0 011.414-1.414L8 12.586l7.293-7.293a1 1 0 011.414 0z" clip-rule="evenodd" />
                    </svg>
                    Submit Application
                `;
                } else {
                    // Photo is perfect
                    submitButton.disabled = false;
                    submitButton.classList.remove('bg-amber-600', 'hover:bg-amber-700', 'bg-red-600', 'hover:bg-red-700');
                    submitButton.classList.add('bg-cyan-600', 'hover:bg-cyan-700');
                    submitButton.innerHTML = `
                    <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5 inline mr-2" viewBox="0 0 20 20" fill="currentColor">
                        <path fill-rule="evenodd" d="M16.707 5.293a1 1 0 010 1.414l-8 8a1 1 0 01-1.414 0l-4-4a1 1 0 011.414-1.414L8 12.586l7.293-7.293a1 1 0 011.414 0z" clip-rule="evenodd" />
                    </svg>
                    Submit Application
                `;
                }
            }
        }

        input.addEventListener('change', async () => {
            const file = input.files[0];
            if (!file) {
                preview.classList.add('hidden');
                photoValidationPassed = false;
                hasPhotoWarnings = false;
                validationDiv.innerHTML = '';
                updateSubmitButtonState();
                return;
            }

            // Basic client-side validation
            if (!file.type.match(/^image\/(jpeg|jpg|png)$/)) {
                showNotification('error', 'Invalid File', 'Please upload a JPG or PNG image.');
                input.value = '';
                preview.classList.add('hidden');
                photoValidationPassed = false;
                hasPhotoWarnings = true;
                updateSubmitButtonState();
                return;
            }

            if (file.size > 2 * 1024 * 1024) {  // 2MB limit
                showNotification('error', 'File Too Large', 'Please upload a photo smaller than 2MB.');
                input.value = '';
                preview.classList.add('hidden');
                photoValidationPassed = false;
                hasPhotoWarnings = true;
                updateSubmitButtonState();
                return;
            }

            // Show preview
            preview.classList.remove('hidden');
            preview.querySelector('.file-name').textContent = file.name;

            // Show loading state
            validationDiv.innerHTML = `
            <div class="flex items-center text-blue-600">
                <svg class="animate-spin -ml-1 mr-3 h-4 w-4 text-blue-600" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24">
                    <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle>
                    <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path>
                </svg>
                <span class="text-sm">Validating photo...</span>
            </div>
        `;

            // Temporarily disable submit button while validating
            if (submitButton) {
                submitButton.disabled = true;
            }

            // Server-side validation
            try {
                const formData = new FormData();
                formData.append('passportPhoto', file);

                const response = await fetch('/StudentApplication/ValidatePassportPhoto', {
                    method: 'POST',
                    body: formData,
                    headers: {
                        'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]')?.value || ''
                    }
                });

                const result = await response.json();

                // Clear loading state
                validationDiv.innerHTML = '';

                if (result.success) {
                    if (result.isValid) {
                        // ✅ Perfect photo - no warnings
                        photoValidationPassed = true;
                        hasPhotoWarnings = false;
                        validationDiv.innerHTML = `
                        <div class="border-l-4 border-green-500 bg-green-50 p-3 rounded">
                            <div class="text-green-600 text-sm flex items-center">
                                <svg class="h-4 w-4 mr-2" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 13l4 4L19 7"></path>
                                </svg>
                                ${result.message}
                            </div>
                        </div>
                    `;
                    } else if (result.warnings && result.warnings.length > 0 && (!result.errors || result.errors.length === 0)) {
                        // ⚠️ Has warnings only - allow submission with recommendations
                        photoValidationPassed = true;
                        hasPhotoWarnings = true;

                        const warningsHtml = result.warnings.map(warning =>
                            `<div class="text-amber-700 text-xs mt-1">• ${warning}</div>`
                        ).join('');

                        validationDiv.innerHTML = `
                        <div class="border-l-4 border-amber-500 bg-amber-50 p-3 rounded">
                            <div class="text-amber-800 text-sm font-medium flex items-center mb-2">
                                <svg class="h-4 w-4 mr-2" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z"></path>
                                </svg>
                                Photo uploaded with recommendations
                            </div>
                            ${warningsHtml}
                            <div class="text-amber-700 text-xs mt-2 font-medium">
                                You can proceed with submission, but for best results, consider uploading a better quality photo.
                            </div>
                        </div>
                    `;
                    } else {
                        // ❌ Has errors - allow submission but show strong warning
                        photoValidationPassed = false;
                        hasPhotoWarnings = true;

                        let issuesHtml = '';
                        if (result.errors && result.errors.length > 0) {
                            issuesHtml += result.errors.map(error =>
                                `<div class="text-red-700 text-xs mt-1">• ${error}</div>`
                            ).join('');
                        }
                        if (result.warnings && result.warnings.length > 0) {
                            issuesHtml += result.warnings.map(warning =>
                                `<div class="text-red-700 text-xs mt-1">• ${warning}</div>`
                            ).join('');
                        }

                        validationDiv.innerHTML = `
                        <div class="border-l-4 border-red-500 bg-red-50 p-3 rounded">
                            <div class="text-red-800 text-sm font-medium flex items-center mb-2">
                                <svg class="h-4 w-4 mr-2" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12"></path>
                                </svg>
                                Photo Quality Issues Detected
                            </div>
                            ${issuesHtml}
                            <div class="text-red-700 text-xs mt-2 font-medium">
                                ⚠️ Your application may be delayed if photo quality is poor. Please upload a better photo if possible.
                            </div>
                        </div>
                    `;

                        // Enable submit button after 2 seconds
                        setTimeout(() => {
                            updateSubmitButtonState();
                        }, 2000);
                    }
                }

                // Update submit button state (except when delayed for errors)
                if (!(!result.isValid && result.errors && result.errors.length > 0)) {
                    updateSubmitButtonState();
                }

            } catch (error) {
                console.error('Photo validation error:', error);
                photoValidationPassed = false;
                hasPhotoWarnings = false;
                validationDiv.innerHTML = `
                <div class="border-l-4 border-blue-500 bg-blue-50 p-3 rounded">
                    <div class="text-blue-800 text-sm flex items-center">
                        <svg class="h-4 w-4 mr-2" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13 16h-1v-4h-1m1-4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z"></path>
                        </svg>
                        Photo validation temporarily unavailable. You may proceed with submission.
                    </div>
                </div>
            `;
                updateSubmitButtonState();
            }
        });

        // Handle clear button
        preview.querySelector('.clear-file')?.addEventListener('click', () => {
            input.value = '';
            preview.classList.add('hidden');
            validationDiv.innerHTML = '';
            photoValidationPassed = false;
            hasPhotoWarnings = true; // Require photo upload
            updateSubmitButtonState();
        });

        // Initialize submit button state (disabled until valid photo uploaded)
        photoValidationPassed = false;
        hasPhotoWarnings = true; // Start with disabled state requiring photo
        updateSubmitButtonState();
    }

    // Initialize passport photo upload
    handlePassportPhotoUpload();



    console.log('Application form initialized successfully');
});