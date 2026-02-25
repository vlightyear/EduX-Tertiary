// wwwroot/js/toast.js
class ToastManager {
    static init() {
        this.autoHideToasts();
        this.initializeCloseButtons();
    }

    static autoHideToasts() {
        const toasts = document.querySelectorAll('[id^="toast-"]');
        toasts.forEach(toast => {
            setTimeout(() => {
                this.hideToast(toast);
            }, 5000);
        });
    }

    static initializeCloseButtons() {
        document.querySelectorAll('.toast-close').forEach(button => {
            button.addEventListener('click', () => {
                const toast = button.closest('[id^="toast-"]');
                this.hideToast(toast);
            });
        });
    }

    static hideToast(toast) {
        toast.style.opacity = '0';
        setTimeout(() => {
            toast.remove();
        }, 300);
    }
}

// Initialize when DOM is loaded
document.addEventListener('DOMContentLoaded', () => {
    ToastManager.init();


});