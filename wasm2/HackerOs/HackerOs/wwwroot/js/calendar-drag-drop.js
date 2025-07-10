/**
 * Calendar Drag and Drop functionality
 */
window.calendarDragDrop = {
    dotNetHelper: null,
    dragData: {},
    
    /**
     * Initialize the drag and drop functionality
     * @param {any} dotNetHelper - The .NET helper object
     */
    initialize: function(dotNetHelper) {
        this.dotNetHelper = dotNetHelper;
        console.log('Calendar drag and drop initialized');
    },
    
    /**
     * Make an element draggable
     * @param {string} elementId - The ID of the element
     * @param {string} eventId - The ID of the event
     * @param {string} viewType - The type of view (Month, Week, Day)
     */
    makeElementDraggable: function(elementId, eventId, viewType) {
        const element = document.getElementById(elementId);
        if (!element) return;
        
        element.setAttribute('draggable', 'true');
        element.classList.add('calendar-draggable');
        
        element.addEventListener('dragstart', (e) => this.handleDragStart(e, eventId, viewType));
        element.addEventListener('dragend', this.handleDragEnd);
    },
    
    /**
     * Make an element resizable
     * @param {string} elementId - The ID of the element
     * @param {string} eventId - The ID of the event
     * @param {string} viewType - The type of view (Week, Day)
     */
    makeElementResizable: function(elementId, eventId, viewType) {
        const element = document.getElementById(elementId);
        if (!element) return;
        
        // Add resize handles
        const resizeHandle = document.createElement('div');
        resizeHandle.className = 'event-resize-handle';
        element.appendChild(resizeHandle);
        
        // Store original dimensions
        let originalHeight = 0;
        let originalY = 0;
        let originalTop = 0;
        
        // Add mouse events for resizing
        resizeHandle.addEventListener('mousedown', (e) => {
            e.stopPropagation();
            e.preventDefault();
            
            originalHeight = element.offsetHeight;
            originalY = e.clientY;
            originalTop = element.offsetTop;
            
            document.addEventListener('mousemove', handleMouseMove);
            document.addEventListener('mouseup', handleMouseUp);
            
            element.classList.add('resizing');
        });
        
        const handleMouseMove = (e) => {
            const deltaY = e.clientY - originalY;
            const newHeight = Math.max(originalHeight + deltaY, 25); // Minimum height
            
            element.style.height = newHeight + 'px';
            
            // Calculate and display duration tooltip
            const minutesPerPixel = viewType === 'Week' || viewType === 'Day' ? 1 : 15;
            const durationMinutes = Math.round(newHeight * minutesPerPixel);
            const hours = Math.floor(durationMinutes / 60);
            const minutes = durationMinutes % 60;
            
            this.showResizeFeedback(element, `${hours}h ${minutes}m`);
        };
        
        const handleMouseUp = () => {
            document.removeEventListener('mousemove', handleMouseMove);
            document.removeEventListener('mouseup', handleMouseUp);
            
            element.classList.remove('resizing');
            this.hideResizeFeedback();
            
            // Calculate new duration in minutes
            const minutesPerPixel = viewType === 'Week' || viewType === 'Day' ? 1 : 15;
            const durationMinutes = Math.round(element.offsetHeight * minutesPerPixel);
            
            // Notify .NET
            this.dotNetHelper.invokeMethodAsync('OnEventResized', eventId, durationMinutes);
        };
    },
    
    /**
     * Initialize drop zones for a view
     * @param {string} viewType - The type of view (Month, Week, Day)
     */
    initializeDropZones: function(viewType) {
        let selector = '';
        
        if (viewType === 'Month') {
            selector = '.day-cell';
        } else if (viewType === 'Week') {
            selector = '.day-column .hour-cell';
        } else if (viewType === 'Day') {
            selector = '.day-column .hour-cell';
        }
        
        const dropZones = document.querySelectorAll(selector);
        
        dropZones.forEach(zone => {
            zone.addEventListener('dragover', this.handleDragOver);
            zone.addEventListener('dragenter', this.handleDragEnter);
            zone.addEventListener('dragleave', this.handleDragLeave);
            zone.addEventListener('drop', (e) => this.handleDrop(e, viewType));
        });
    },
    
    /**
     * Handle drag start event
     * @param {DragEvent} e - The drag event
     * @param {string} eventId - The ID of the event
     * @param {string} viewType - The type of view
     */
    handleDragStart: function(e, eventId, viewType) {
        // Set drag image
        const dragImage = e.target.cloneNode(true);
        dragImage.style.width = e.target.offsetWidth + 'px';
        dragImage.style.opacity = '0.7';
        document.body.appendChild(dragImage);
        e.dataTransfer.setDragImage(dragImage, 10, 10);
        
        // Clean up the clone after drag starts
        setTimeout(() => {
            document.body.removeChild(dragImage);
        }, 0);
        
        // Store the event data
        this.dragData = {
            eventId: eventId,
            viewType: viewType,
            sourceElement: e.target
        };
        
        e.dataTransfer.setData('text/plain', eventId);
        e.dataTransfer.effectAllowed = 'move';
        
        // Add dragging class to body
        document.body.classList.add('calendar-dragging');
    },
    
    /**
     * Handle drag end event
     * @param {DragEvent} e - The drag event
     */
    handleDragEnd: function(e) {
        document.body.classList.remove('calendar-dragging');
    },
    
    /**
     * Handle drag over event
     * @param {DragEvent} e - The drag event
     */
    handleDragOver: function(e) {
        e.preventDefault();
        e.dataTransfer.dropEffect = 'move';
    },
    
    /**
     * Handle drag enter event
     * @param {DragEvent} e - The drag event
     */
    handleDragEnter: function(e) {
        e.preventDefault();
        e.currentTarget.classList.add('drag-over');
    },
    
    /**
     * Handle drag leave event
     * @param {DragEvent} e - The drag event
     */
    handleDragLeave: function(e) {
        e.currentTarget.classList.remove('drag-over');
    },
    
    /**
     * Handle drop event
     * @param {DragEvent} e - The drag event
     * @param {string} viewType - The type of view
     */
    handleDrop: function(e, viewType) {
        e.preventDefault();
        e.currentTarget.classList.remove('drag-over');
        
        const eventId = e.dataTransfer.getData('text/plain');
        if (!eventId) return;
        
        // Calculate new date and time based on where it was dropped
        let newDate = null;
        let newTimeMinutes = 0;
        
        if (viewType === 'Month') {
            // For month view, we just need the date from the day cell
            newDate = this.getDateFromMonthCell(e.currentTarget);
        } else if (viewType === 'Week') {
            // For week view, we need both date and time
            const cellInfo = this.getDateTimeFromWeekCell(e.currentTarget);
            newDate = cellInfo.date;
            newTimeMinutes = cellInfo.timeMinutes;
        } else if (viewType === 'Day') {
            // For day view, we need the time from the hour cell
            newDate = this.getSelectedDateFromDayView();
            newTimeMinutes = this.getTimeFromDayCell(e.currentTarget);
        }
        
        if (newDate) {
            // Notify .NET about the drop
            this.dotNetHelper.invokeMethodAsync('OnEventDragged', eventId, newDate.toISOString(), newTimeMinutes);
        }
    },
    
    /**
     * Get date from a month view cell
     * @param {HTMLElement} cell - The day cell
     * @returns {Date} The date
     */
    getDateFromMonthCell: function(cell) {
        // This is a simplified implementation; in a real app, you'd need to 
        // determine the date from the cell's position or data attributes
        const dateAttr = cell.getAttribute('data-date');
        if (dateAttr) {
            return new Date(dateAttr);
        }
        
        // Fallback: try to get the date from the cell content
        const dayNumber = cell.querySelector('.day-number')?.textContent;
        if (dayNumber) {
            // Create a date based on the current month and the day number
            const today = new Date();
            return new Date(today.getFullYear(), today.getMonth(), parseInt(dayNumber));
        }
        
        return null;
    },
    
    /**
     * Get date and time from a week view cell
     * @param {HTMLElement} cell - The hour cell
     * @returns {Object} Object with date and timeMinutes
     */
    getDateTimeFromWeekCell: function(cell) {
        // This is a simplified implementation; in a real app, you'd need to 
        // determine the date and time from the cell's position or data attributes
        const dateAttr = cell.getAttribute('data-date');
        const timeAttr = cell.getAttribute('data-time');
        
        let date = null;
        let timeMinutes = 0;
        
        if (dateAttr) {
            date = new Date(dateAttr);
        }
        
        if (timeAttr) {
            // Time attribute should be in "HH:MM" format
            const [hours, minutes] = timeAttr.split(':').map(Number);
            timeMinutes = hours * 60 + minutes;
        } else {
            // Fallback: calculate time from cell position
            const cellIndex = Array.from(cell.parentNode.children).indexOf(cell);
            timeMinutes = cellIndex * 60; // Assuming 1-hour cells
        }
        
        return { date, timeMinutes };
    },
    
    /**
     * Get the selected date from day view
     * @returns {Date} The selected date
     */
    getSelectedDateFromDayView: function() {
        // This is a simplified implementation; in a real app, you'd need to 
        // get the selected date from the day view component
        const dateHeader = document.querySelector('.day-details-header .date-display');
        if (dateHeader) {
            // Parse the date from the header text (e.g., "July 5, 2025")
            return new Date(dateHeader.textContent);
        }
        
        return new Date(); // Fallback to today
    },
    
    /**
     * Get time from a day view cell
     * @param {HTMLElement} cell - The hour cell
     * @returns {number} Time in minutes from midnight
     */
    getTimeFromDayCell: function(cell) {
        const timeAttr = cell.getAttribute('data-time');
        
        if (timeAttr) {
            // Time attribute should be in "HH:MM" format
            const [hours, minutes] = timeAttr.split(':').map(Number);
            return hours * 60 + minutes;
        } else {
            // Fallback: calculate time from cell position
            const cellIndex = Array.from(cell.parentNode.children).indexOf(cell);
            return cellIndex * 60; // Assuming 1-hour cells
        }
    },
    
    /**
     * Show resize feedback
     * @param {HTMLElement} element - The element being resized
     * @param {string} durationText - The duration text to show
     */
    showResizeFeedback: function(element, durationText) {
        let feedback = document.getElementById('resize-feedback');
        
        if (!feedback) {
            feedback = document.createElement('div');
            feedback.id = 'resize-feedback';
            feedback.className = 'resize-feedback';
            document.body.appendChild(feedback);
        }
        
        feedback.textContent = durationText;
        feedback.style.display = 'block';
        
        // Position the feedback next to the cursor
        document.addEventListener('mousemove', this.positionResizeFeedback);
    },
    
    /**
     * Position the resize feedback tooltip
     * @param {MouseEvent} e - The mouse event
     */
    positionResizeFeedback: function(e) {
        const feedback = document.getElementById('resize-feedback');
        if (feedback) {
            feedback.style.left = (e.clientX + 15) + 'px';
            feedback.style.top = (e.clientY + 15) + 'px';
        }
    },
    
    /**
     * Hide resize feedback
     */
    hideResizeFeedback: function() {
        const feedback = document.getElementById('resize-feedback');
        if (feedback) {
            feedback.style.display = 'none';
            document.removeEventListener('mousemove', this.positionResizeFeedback);
        }
    }
};
