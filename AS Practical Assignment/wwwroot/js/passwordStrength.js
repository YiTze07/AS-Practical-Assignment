// =====================================================
// Client-side Password Strength Checker & Match Checker
// =====================================================

document.addEventListener('DOMContentLoaded', function () {

    const passwordInput = document.getElementById('passwordInput');
    const confirmInput = document.getElementById('confirmPasswordInput');
    const strengthBar = document.getElementById('strengthBar');
    const strengthText = document.getElementById('strengthText');

    // Requirement elements
    const reqLength = document.getElementById('reqLength');
    const reqUpper = document.getElementById('reqUpper');
    const reqLower = document.getElementById('reqLower');
    const reqNumber = document.getElementById('reqNumber');
    const reqSpecial = document.getElementById('reqSpecial');

    const matchText = document.getElementById('matchText');

    // --- Password Strength Logic ---
    if (passwordInput) {
        passwordInput.addEventListener('input', function () {
            const password = this.value;

            // Check each requirement
            const hasLength = password.length >= 12;
            const hasUpper = /[A-Z]/.test(password);
            const hasLower = /[a-z]/.test(password);
            const hasNumber = /[0-9]/.test(password);
            const hasSpecial = /[^A-Za-z0-9]/.test(password);

            // Toggle classes
            toggleReq(reqLength, hasLength);
            toggleReq(reqUpper, hasUpper);
            toggleReq(reqLower, hasLower);
            toggleReq(reqNumber, hasNumber);
            toggleReq(reqSpecial, hasSpecial);

            // Calculate score
            let score = 0;
            if (hasLength) score++;
            if (hasUpper) score++;
            if (hasLower) score++;
            if (hasNumber) score++;
            if (hasSpecial) score++;

            // Update bar
            let width, color, label, labelClass;

            if (score <= 2) {
                width = 20;
                color = '#e53e3e';
                label = 'Weak';
                labelClass = 'strength-weak';
            } else if (score === 3) {
                width = 45;
                color = '#dd6b20';
                label = 'Fair';
                labelClass = 'strength-fair';
            } else if (score === 4) {
                width = 70;
                color = '#38a169';
                label = 'Good';
                labelClass = 'strength-good';
            } else {
                width = 100;
                color = '#2b6cb0';
                label = '✓ Strong';
                labelClass = 'strength-strong';
            }

            if (password.length === 0) {
                width = 0;
                label = '';
                labelClass = '';
                color = '#e2e8f0';
            }

            strengthBar.style.width = width + '%';
            strengthBar.style.backgroundColor = color;
            strengthText.textContent = label;
            strengthText.className = 'strength-text ' + labelClass;

            // Also check match if confirm exists
            checkMatch();
        });
    }

    // --- Password Match Logic ---
    if (confirmInput) {
        confirmInput.addEventListener('input', checkMatch);
    }

    function checkMatch() {
        if (!passwordInput || !confirmInput || !matchText) return;

        const pw = passwordInput.value;
        const cpw = confirmInput.value;

        if (cpw.length === 0) {
            matchText.textContent = '';
            matchText.className = 'match-text';
        } else if (pw === cpw) {
            matchText.textContent = '✓ Passwords match';
            matchText.className = 'match-text match-yes';
        } else {
            matchText.textContent = '✗ Passwords do not match';
            matchText.className = 'match-text match-no';
        }
    }

    // --- Helper ---
    function toggleReq(element, condition) {
        if (condition) {
            element.classList.add('met');
        } else {
            element.classList.remove('met');
        }
    }
});