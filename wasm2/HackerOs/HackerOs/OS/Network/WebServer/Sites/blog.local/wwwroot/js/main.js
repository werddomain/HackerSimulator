/**
 * Main JavaScript for HackerOS Blog
 */

document.addEventListener('DOMContentLoaded', function() {
    // Mobile navigation toggle
    const navToggle = document.querySelector('.nav-toggle');
    if (navToggle) {
        navToggle.addEventListener('click', function() {
            const nav = document.querySelector('nav ul');
            nav.classList.toggle('show');
        });
    }

    // Comment form validation
    const commentForm = document.querySelector('.comment-form form');
    if (commentForm) {
        commentForm.addEventListener('submit', function(e) {
            const nameInput = document.getElementById('name');
            const emailInput = document.getElementById('email');
            const contentInput = document.getElementById('content');
            
            let isValid = true;
            
            // Simple validation
            if (!nameInput.value.trim()) {
                showError(nameInput, 'Name is required');
                isValid = false;
            }
            
            if (!emailInput.value.trim()) {
                showError(emailInput, 'Email is required');
                isValid = false;
            } else if (!isValidEmail(emailInput.value)) {
                showError(emailInput, 'Please enter a valid email address');
                isValid = false;
            }
            
            if (!contentInput.value.trim()) {
                showError(contentInput, 'Comment is required');
                isValid = false;
            }
            
            if (!isValid) {
                e.preventDefault();
            }
        });
    }

    // Subscription form validation
    const subscribeForm = document.querySelector('.subscribe-form form');
    if (subscribeForm) {
        subscribeForm.addEventListener('submit', function(e) {
            const nameInput = document.getElementById('name');
            const emailInput = document.getElementById('email');
            
            let isValid = true;
            
            // Simple validation
            if (!nameInput.value.trim()) {
                showError(nameInput, 'Name is required');
                isValid = false;
            }
            
            if (!emailInput.value.trim()) {
                showError(emailInput, 'Email is required');
                isValid = false;
            } else if (!isValidEmail(emailInput.value)) {
                showError(emailInput, 'Please enter a valid email address');
                isValid = false;
            }
            
            if (!isValid) {
                e.preventDefault();
            }
        });
    }

    // Post creation form validation
    const postForm = document.querySelector('.post-form');
    if (postForm) {
        postForm.addEventListener('submit', function(e) {
            const titleInput = document.getElementById('title');
            const authorInput = document.getElementById('author');
            const contentInput = document.getElementById('content');
            
            let isValid = true;
            
            // Simple validation
            if (!titleInput.value.trim()) {
                showError(titleInput, 'Title is required');
                isValid = false;
            }
            
            if (!authorInput.value.trim()) {
                showError(authorInput, 'Author is required');
                isValid = false;
            }
            
            if (!contentInput.value.trim()) {
                showError(contentInput, 'Content is required');
                isValid = false;
            }
            
            if (!isValid) {
                e.preventDefault();
            }
        });
    }

    // Helper functions
    function showError(input, message) {
        const formGroup = input.parentElement;
        let errorElement = formGroup.querySelector('.field-error');
        
        if (!errorElement) {
            errorElement = document.createElement('div');
            errorElement.className = 'field-error';
            formGroup.appendChild(errorElement);
        }
        
        errorElement.textContent = message;
        input.classList.add('error-input');
    }

    function isValidEmail(email) {
        const re = /^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$/;
        return re.test(String(email).toLowerCase());
    }

    // Add smooth scrolling for anchor links
    document.querySelectorAll('a[href^="#"]').forEach(anchor => {
        anchor.addEventListener('click', function(e) {
            e.preventDefault();
            
            const targetId = this.getAttribute('href');
            if (targetId === '#') return;
            
            const targetElement = document.querySelector(targetId);
            if (targetElement) {
                targetElement.scrollIntoView({
                    behavior: 'smooth'
                });
            }
        });
    });

    // Add responsive navigation for mobile
    const header = document.querySelector('header');
    if (header && !header.querySelector('.nav-toggle')) {
        const navToggle = document.createElement('button');
        navToggle.className = 'nav-toggle';
        navToggle.innerHTML = '<span></span><span></span><span></span>';
        header.querySelector('.container').insertBefore(navToggle, header.querySelector('nav'));
        
        // Add CSS for the toggle button
        const style = document.createElement('style');
        style.textContent = `
            @media (max-width: 768px) {
                nav ul {
                    display: none;
                }
                
                nav ul.show {
                    display: flex;
                    flex-direction: column;
                    width: 100%;
                }
                
                .nav-toggle {
                    display: block;
                    background: none;
                    border: none;
                    cursor: pointer;
                    padding: 10px;
                }
                
                .nav-toggle span {
                    display: block;
                    width: 25px;
                    height: 3px;
                    background-color: #fff;
                    margin: 5px 0;
                    transition: all 0.3s ease;
                }
            }
            
            @media (min-width: 769px) {
                .nav-toggle {
                    display: none;
                }
            }
        `;
        document.head.appendChild(style);
    }

    // Implement share functionality
    const shareButtons = document.querySelectorAll('.share-button');
    if (shareButtons.length > 0) {
        shareButtons.forEach(button => {
            button.addEventListener('click', function(e) {
                e.preventDefault();
                
                const pageTitle = document.title;
                const pageUrl = window.location.href;
                
                if (this.classList.contains('twitter')) {
                    window.open(`https://twitter.com/intent/tweet?text=${encodeURIComponent(pageTitle)}&url=${encodeURIComponent(pageUrl)}`, '_blank');
                } else if (this.classList.contains('facebook')) {
                    window.open(`https://www.facebook.com/sharer/sharer.php?u=${encodeURIComponent(pageUrl)}`, '_blank');
                } else if (this.classList.contains('linkedin')) {
                    window.open(`https://www.linkedin.com/shareArticle?mini=true&url=${encodeURIComponent(pageUrl)}&title=${encodeURIComponent(pageTitle)}`, '_blank');
                }
            });
        });
    }
});
