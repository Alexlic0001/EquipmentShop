// Функция для добавления товара в корзину через AJAX
async function addToCart(productId, quantity = 1) {
    try {
        const formData = new FormData();
        formData.append('productId', productId);
        formData.append('quantity', quantity);
        formData.append('__RequestVerificationToken',
            document.querySelector('input[name="__RequestVerificationToken"]').value);

        const response = await fetch('/Cart/AddToCart', {
            method: 'POST',
            body: formData
        });

        if (response.ok) {
            // Показать уведомление
            showNotification('Товар добавлен в корзину!', 'success');
            // Обновить мини-корзину
            updateMiniCart();
            return true;
        } else {
            const error = await response.text();
            showNotification('Ошибка: ' + error, 'error');
            return false;
        }
    } catch (error) {
        console.error('Ошибка добавления в корзину:', error);
        showNotification('Ошибка соединения', 'error');
        return false;
    }
}

// Обновление мини-корзины
async function updateMiniCart() {
    try {
        const response = await fetch('/Cart/MiniCart');
        const html = await response.text();

        // Обновляем контейнер мини-корзины
        const miniCartContainer = document.querySelector('.mini-cart-container');
        if (miniCartContainer) {
            miniCartContainer.innerHTML = html;
        }

        // Обновляем счетчик в хедере
        const cartResponse = await fetch('/Cart/GetCartSummary');
        const cartData = await cartResponse.json();

        const cartCount = document.getElementById('cartItemCount');
        const cartTotal = document.getElementById('cartTotalPrice');

        if (cartCount) {
            cartCount.textContent = cartData.itemCount;
            cartCount.style.display = cartData.itemCount > 0 ? 'inline' : 'none';
        }

        if (cartTotal) {
            cartTotal.textContent = cartData.total.toFixed(2) + ' BYN';
        }
    } catch (error) {
        console.error('Ошибка обновления корзины:', error);
    }
}

// Показать уведомление
function showNotification(message, type = 'info') {
    // Создаем элемент уведомления
    const notification = document.createElement('div');
    notification.className = `alert alert-${type} notification`;
    notification.style.cssText = `
        position: fixed;
        top: 20px;
        right: 20px;
        z-index: 9999;
        min-width: 300px;
        animation: slideIn 0.3s ease;
    `;

    notification.innerHTML = `
        <div class="d-flex align-items-center">
            <i class="fas fa-${type === 'success' ? 'check-circle' : 'exclamation-circle'} me-2"></i>
            <span>${message}</span>
            <button class="btn-close ms-auto" onclick="this.parentElement.parentElement.remove()"></button>
        </div>
    `;

    document.body.appendChild(notification);

    // Автоматическое удаление через 3 секунды
    setTimeout(() => {
        if (notification.parentNode) {
            notification.remove();
        }
    }, 3000);
}

// Инициализация при загрузке страницы
document.addEventListener('DOMContentLoaded', function () {
    // Добавляем стили для анимаций
    const style = document.createElement('style');
    style.textContent = `
        @keyframes slideIn {
            from { transform: translateX(100%); opacity: 0; }
            to { transform: translateX(0); opacity: 1; }
        }
    `;
    document.head.appendChild(style);

    // Инициализируем корзину
    updateMiniCart();

    // Обработчики для кнопок добавления в корзину
    document.querySelectorAll('.add-to-cart-btn').forEach(button => {
        button.addEventListener('click', function (e) {
            const productId = this.dataset.productId;
            const quantity = document.getElementById('quantityInput')?.value || 1;

            addToCart(productId, quantity);

            // Анимация кнопки
            this.classList.add('added-to-cart');
            setTimeout(() => {
                this.classList.remove('added-to-cart');
            }, 500);
        });
    });
});

// Экспортируем функции для использования в других скриптах
window.Cart = {
    addToCart,
    updateMiniCart,
    showNotification
};