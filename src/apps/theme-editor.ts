/**
 * Theme Editor Application
 * Allows users to create and customize themes
 */
import { GuiApplication } from '../core/gui-application';
import { Theme, DEFAULT_THEME } from '../core/theme';
import { ThemeManager } from '../core/ThemeManager';
import { OS } from '../core/os';
import { FileSystem } from '../core/filesystem';
import { UserSettings } from '../core/UserSettings';

export class ThemeEditorApp extends GuiApplication {
    protected initApplication(): void {
        // Create layout
        this.createLayout();

        // Initialize color pickers
        this.initializeColorPickers();

        // Set up event listeners
        this.setupEventListeners();

        // Initialize preview
        this.updatePreview();

        // Add CSS class for styling
        if (this.container)
            this.container.classList.add('theme-editor');
    }
    protected getApplicationName(): string {
        return "theme-editor";
    }
    private themeManager: ThemeManager;
    private currentTheme: Theme;
    private originalTheme: Theme;
    private previewContainer?: HTMLElement;
    private colorPickers: Map<keyof Theme, HTMLInputElement> = new Map();

    constructor(
        os: OS) {
        super(os);
        this.themeManager = ThemeManager.getInstance(os.getFileSystem(), os.getUserSettings());
        this.currentTheme = JSON.parse(JSON.stringify(this.themeManager.getCurrentTheme()));
        this.originalTheme = this.themeManager.getCurrentTheme();


    }



    /**
     * Create the application layout
     */
    private createLayout(): void {
        const content = this.container;
if (!content) return;
        // Create main container
        const mainContainer = document.createElement('div');
        mainContainer.className = 'theme-editor-container';
        content.appendChild(mainContainer);

        // Create left panel (settings)
        const leftPanel = document.createElement('div');
        leftPanel.className = 'settings-panel';
        mainContainer.appendChild(leftPanel);

        // Create right panel (preview)
        const rightPanel = document.createElement('div');
        rightPanel.className = 'preview-panel';
        mainContainer.appendChild(rightPanel);

        // Create theme selection section
        const themeSelectionSection = document.createElement('div');
        themeSelectionSection.className = 'section theme-selection-section';
        leftPanel.appendChild(themeSelectionSection);

        const themeSelectionTitle = document.createElement('h2');
        themeSelectionTitle.textContent = 'Theme Selection';
        themeSelectionSection.appendChild(themeSelectionTitle);

        const themeSelectContainer = document.createElement('div');
        themeSelectContainer.className = 'theme-select-container';
        themeSelectionSection.appendChild(themeSelectContainer);

        const themeSelect = document.createElement('select');
        themeSelect.id = 'theme-select';
        themeSelectContainer.appendChild(themeSelect);

        // Populate theme select with available themes
        this.populateThemeSelect(themeSelect);

        // Create theme name and metadata section
        const metadataSection = document.createElement('div');
        metadataSection.className = 'section metadata-section';
        leftPanel.appendChild(metadataSection);

        const metadataTitle = document.createElement('h2');
        metadataTitle.textContent = 'Theme Information';
        metadataSection.appendChild(metadataTitle);

        const nameInput = document.createElement('input');
        nameInput.type = 'text';
        nameInput.id = 'theme-name';
        nameInput.placeholder = 'Theme Name';
        nameInput.value = this.currentTheme.name;
        metadataSection.appendChild(this.createFormGroup('Theme Name', nameInput));

        const descriptionInput = document.createElement('textarea');
        descriptionInput.id = 'theme-description';
        descriptionInput.placeholder = 'Theme Description';
        descriptionInput.value = this.currentTheme.description || '';
        metadataSection.appendChild(this.createFormGroup('Description', descriptionInput));

        const authorInput = document.createElement('input');
        authorInput.type = 'text';
        authorInput.id = 'theme-author';
        authorInput.placeholder = 'Author Name';
        authorInput.value = this.currentTheme.author || '';
        metadataSection.appendChild(this.createFormGroup('Author', authorInput));

        // Create color sections

        // Main colors section
        const mainColorsSection = document.createElement('div');
        mainColorsSection.className = 'section colors-section';
        leftPanel.appendChild(mainColorsSection);

        const mainColorsTitle = document.createElement('h2');
        mainColorsTitle.textContent = 'Main Colors';
        mainColorsSection.appendChild(mainColorsTitle);

        // Create color pickers for main colors
        mainColorsSection.appendChild(this.createColorPicker('accentColor', 'Accent Color'));
        mainColorsSection.appendChild(this.createColorPicker('accentColorLight', 'Accent Color (Light)'));
        mainColorsSection.appendChild(this.createColorPicker('accentColorDark', 'Accent Color (Dark)'));
        mainColorsSection.appendChild(this.createColorPicker('primaryColor', 'Primary Color'));
        mainColorsSection.appendChild(this.createColorPicker('secondaryColor', 'Secondary Color'));
        mainColorsSection.appendChild(this.createColorPicker('tertiaryColor', 'Tertiary Color'));

        // Text colors section
        const textColorsSection = document.createElement('div');
        textColorsSection.className = 'section colors-section';
        leftPanel.appendChild(textColorsSection);

        const textColorsTitle = document.createElement('h2');
        textColorsTitle.textContent = 'Text Colors';
        textColorsSection.appendChild(textColorsTitle);

        // Create color pickers for text colors
        textColorsSection.appendChild(this.createColorPicker('textColorPrimary', 'Primary Text'));
        textColorsSection.appendChild(this.createColorPicker('textColorSecondary', 'Secondary Text'));
        textColorsSection.appendChild(this.createColorPicker('textColorDisabled', 'Disabled Text'));

        // Status colors section
        const statusColorsSection = document.createElement('div');
        statusColorsSection.className = 'section colors-section';
        leftPanel.appendChild(statusColorsSection);

        const statusColorsTitle = document.createElement('h2');
        statusColorsTitle.textContent = 'Status Colors';
        statusColorsSection.appendChild(statusColorsTitle);

        // Create color pickers for status colors
        statusColorsSection.appendChild(this.createColorPicker('successColor', 'Success'));
        statusColorsSection.appendChild(this.createColorPicker('warningColor', 'Warning'));
        statusColorsSection.appendChild(this.createColorPicker('errorColor', 'Error'));
        statusColorsSection.appendChild(this.createColorPicker('infoColor', 'Info'));

        // Create preview section
        const previewTitle = document.createElement('h2');
        previewTitle.textContent = 'Preview';
        rightPanel.appendChild(previewTitle);

        this.previewContainer = document.createElement('div');
        this.previewContainer.className = 'preview-container';
        rightPanel.appendChild(this.previewContainer);

        // Create actions section
        const actionsSection = document.createElement('div');
        actionsSection.className = 'section actions-section';
        rightPanel.appendChild(actionsSection);

        const saveButton = document.createElement('button');
        saveButton.textContent = 'Save Theme';
        saveButton.id = 'save-theme';
        saveButton.className = 'primary-button';
        actionsSection.appendChild(saveButton);

        const applyButton = document.createElement('button');
        applyButton.textContent = 'Apply Theme';
        applyButton.id = 'apply-theme';
        actionsSection.appendChild(applyButton);

        const resetButton = document.createElement('button');
        resetButton.textContent = 'Reset Changes';
        resetButton.id = 'reset-theme';
        actionsSection.appendChild(resetButton);

        const exportButton = document.createElement('button');
        exportButton.textContent = 'Export Theme';
        exportButton.id = 'export-theme';
        actionsSection.appendChild(exportButton);

        const importButton = document.createElement('button');
        importButton.textContent = 'Import Theme';
        importButton.id = 'import-theme';
        actionsSection.appendChild(importButton);
    }

    /**
     * Create a form group with label and input
     * @param label The label text
     * @param input The input element
     */
    private createFormGroup(label: string, input: HTMLElement): HTMLElement {
        const formGroup = document.createElement('div');
        formGroup.className = 'form-group';

        const labelElement = document.createElement('label');
        labelElement.textContent = label;
        labelElement.htmlFor = input.id;

        formGroup.appendChild(labelElement);
        formGroup.appendChild(input);

        return formGroup;
    }

    /**
     * Create a color picker form group
     * @param property The theme property to bind to
     * @param label The label text
     */
    private createColorPicker(property: keyof Theme, label: string): HTMLElement {
        const formGroup = document.createElement('div');
        formGroup.className = 'form-group color-picker-group';

        const labelElement = document.createElement('label');
        labelElement.textContent = label;
        labelElement.htmlFor = `color-${property}`;

        const colorInput = document.createElement('input');
        colorInput.type = 'color';
        colorInput.id = `color-${property}`;
        colorInput.value = this.convertColorToHex(this.currentTheme[property] as string);

        // Store in map for easy access
        this.colorPickers.set(property, colorInput);

        formGroup.appendChild(labelElement);
        formGroup.appendChild(colorInput);

        return formGroup;
    }

    /**
     * Convert color string to hex format
     * Handles rgba() format and converts to hex
     * @param color The color to convert
     */
    private convertColorToHex(color: string): string {
        // Check if color is already hex
        if (color.startsWith('#')) {
            return color;
        }

        // Check if color is rgba
        if (color.startsWith('rgba')) {
            // Create a temporary element to parse the color
            const tempElement = document.createElement('div');
            tempElement.style.color = color;
            document.body.appendChild(tempElement);

            // Get computed color
            const computedColor = getComputedStyle(tempElement).color;
            document.body.removeChild(tempElement);

            // Parse rgb values
            const rgbMatch = computedColor.match(/^rgb\((\d+),\s*(\d+),\s*(\d+)\)$/);
            if (rgbMatch) {
                const r = parseInt(rgbMatch[1], 10);
                const g = parseInt(rgbMatch[2], 10);
                const b = parseInt(rgbMatch[3], 10);
                return `#${r.toString(16).padStart(2, '0')}${g.toString(16).padStart(2, '0')}${b.toString(16).padStart(2, '0')}`;
            }
        }

        // Fallback to black if conversion fails
        return '#000000';
    }

    /**
     * Populate theme select with available themes
     * @param select The select element to populate
     */
    private populateThemeSelect(select: HTMLSelectElement): void {
        // Clear existing options
        select.innerHTML = '';

        // Get all themes
        const allThemes = this.themeManager.getAllThemes();

        // Add themes to select
        allThemes.forEach((theme, id) => {
            const option = document.createElement('option');
            option.value = id;
            option.textContent = theme.name;
            option.selected = id === this.currentTheme.id;
            select.appendChild(option);
        });

        // Add option to create new theme
        const newOption = document.createElement('option');
        newOption.value = 'new';
        newOption.textContent = '+ Create New Theme';
        select.appendChild(newOption);
    }

    /**
     * Initialize color pickers with current theme values
     */
    private initializeColorPickers(): void {
        this.colorPickers.forEach((picker, property) => {
            // Set initial value
            picker.value = this.convertColorToHex(this.currentTheme[property] as string);

            // Add change listener
            picker.addEventListener('input', () => {
                this.currentTheme[property] = <never>picker.value;
                this.updatePreview();
            });
        });
    }

    /**
     * Set up event listeners
     */
    private setupEventListeners(): void {
        // Theme selection
        const themeSelect = document.getElementById('theme-select') as HTMLSelectElement;
        themeSelect.addEventListener('change', () => {
            const selectedThemeId = themeSelect.value;

            if (selectedThemeId === 'new') {
                // Create new theme based on current
                this.createNewTheme();
            } else {
                // Load selected theme
                this.loadTheme(selectedThemeId);
            }
        });

        // Theme name input
        const nameInput = document.getElementById('theme-name') as HTMLInputElement;
        nameInput.addEventListener('input', () => {
            this.currentTheme.name = nameInput.value;
        });

        // Theme description input
        const descriptionInput = document.getElementById('theme-description') as HTMLTextAreaElement;
        descriptionInput.addEventListener('input', () => {
            this.currentTheme.description = descriptionInput.value;
        });

        // Theme author input
        const authorInput = document.getElementById('theme-author') as HTMLInputElement;
        authorInput.addEventListener('input', () => {
            this.currentTheme.author = authorInput.value;
        });

        // Save button
        const saveButton = document.getElementById('save-theme') as HTMLButtonElement;
        saveButton.addEventListener('click', async () => {
            await this.saveTheme();
        });

        // Apply button
        const applyButton = document.getElementById('apply-theme') as HTMLButtonElement;
        applyButton.addEventListener('click', async () => {
            await this.applyTheme();
        });

        // Reset button
        const resetButton = document.getElementById('reset-theme') as HTMLButtonElement;
        resetButton.addEventListener('click', () => {
            this.resetTheme();
        });

        // Export button
        const exportButton = document.getElementById('export-theme') as HTMLButtonElement;
        exportButton.addEventListener('click', () => {
            this.exportTheme();
        });

        // Import button
        const importButton = document.getElementById('import-theme') as HTMLButtonElement;
        importButton.addEventListener('click', () => {
            this.importTheme();
        });
    }

    /**
     * Update preview with current theme
     */
    private updatePreview(): void {
        // Apply current theme to preview container
        const previewTheme = this.currentTheme;
if (!this.previewContainer) return;
        // Create preview elements
        this.previewContainer.innerHTML = `
      <div class="preview-window" style="background-color: ${previewTheme.primaryColor}; border: 1px solid ${previewTheme.windowBorderColor};">
        <div class="preview-window-header" style="background-color: ${previewTheme.windowHeaderColor}; color: ${previewTheme.textColorPrimary};">
          Window Title
        </div>
        <div class="preview-window-content" style="color: ${previewTheme.textColorPrimary};">
          <div class="preview-section">
            <h3 style="color: ${previewTheme.textColorPrimary};">Text Colors</h3>
            <p style="color: ${previewTheme.textColorPrimary};">Primary Text</p>
            <p style="color: ${previewTheme.textColorSecondary};">Secondary Text</p>
            <p style="color: ${previewTheme.textColorDisabled};">Disabled Text</p>
          </div>
          
          <div class="preview-section">
            <h3 style="color: ${previewTheme.textColorPrimary};">UI Elements</h3>
            <button class="preview-button" style="background: linear-gradient(to bottom, ${previewTheme.accentColor}, ${previewTheme.accentColorDark}); color: white;">
              Primary Button
            </button>
            <button class="preview-button secondary">
              Secondary Button
            </button>
            <div class="preview-input-group">
              <input type="text" placeholder="Input field" style="background-color: ${previewTheme.tertiaryColor}; color: ${previewTheme.textColorPrimary}; border: 1px solid rgba(255,255,255,0.1);" />
            </div>
          </div>
          
          <div class="preview-section">
            <h3 style="color: ${previewTheme.textColorPrimary};">Status Colors</h3>
            <div class="preview-status" style="color: ${previewTheme.successColor};">Success Status</div>
            <div class="preview-status" style="color: ${previewTheme.warningColor};">Warning Status</div>
            <div class="preview-status" style="color: ${previewTheme.errorColor};">Error Status</div>
            <div class="preview-status" style="color: ${previewTheme.infoColor};">Info Status</div>
          </div>
        </div>
      </div>
      
      <div class="preview-taskbar" style="background: linear-gradient(to bottom, ${previewTheme.taskbarBgStart}, ${previewTheme.taskbarBgEnd}); border-top: 1px solid ${previewTheme.taskbarBorderTopColor};">
        <button class="preview-start-button" style="background: linear-gradient(to bottom, ${previewTheme.startButtonBgStart}, ${previewTheme.startButtonBgEnd}); color: white;">
          Start
        </button>
        <div class="preview-taskbar-item" style="background-color: ${previewTheme.taskbarItemBg};">
          App 1
        </div>
        <div class="preview-taskbar-item active" style="background-color: ${previewTheme.taskbarItemActiveBg}; border-bottom: ${previewTheme.taskbarItemActiveBorder};">
          App 2
        </div>
      </div>
    `;
    }

    /**
     * Create a new theme based on the current theme
     */
    private createNewTheme(): void {
        // Create a copy of the current theme
        const newTheme: Theme = JSON.parse(JSON.stringify(this.currentTheme));

        // Set new ID and name
        newTheme.id = `custom-theme-${Date.now()}`;
        newTheme.name = `Custom Theme ${new Date().toLocaleDateString()}`;

        // Update current theme
        this.currentTheme = newTheme;

        // Update UI
        (document.getElementById('theme-name') as HTMLInputElement).value = newTheme.name;

        // Update preview
        this.updatePreview();
    }

    /**
     * Load a theme by ID
     * @param themeId The ID of the theme to load
     */
    private async loadTheme(themeId: string): Promise<void> {
        // Get all themes
        const allThemes = this.themeManager.getAllThemes();

        // Find theme by ID
        const theme = allThemes.get(themeId);

        if (theme) {
            // Update current theme
            this.currentTheme = JSON.parse(JSON.stringify(theme));

            // Update UI
            (document.getElementById('theme-name') as HTMLInputElement).value = theme.name;
            (document.getElementById('theme-description') as HTMLTextAreaElement).value = theme.description || '';
            (document.getElementById('theme-author') as HTMLInputElement).value = theme.author || '';

            // Update color pickers
            this.initializeColorPickers();

            // Update preview
            this.updatePreview();
        }
    }

    /**
     * Save the current theme
     */
    private async saveTheme(): Promise<void> {
        try {
            // Save theme
            await this.themeManager.saveTheme(this.currentTheme);

            // Update original theme
            this.originalTheme = JSON.parse(JSON.stringify(this.currentTheme));

            // Update theme select
            this.populateThemeSelect(document.getElementById('theme-select') as HTMLSelectElement);

            // Show success message
            this.showNotification('Theme saved successfully!');
        } catch (error) {
            console.error('Error saving theme:', error);
            this.showNotification('Error saving theme', 'error');
        }
    }

    /**
     * Apply the current theme
     */
    private async applyTheme(): Promise<void> {
        try {
            // Apply theme
            await this.themeManager.applyTheme(this.currentTheme);

            // Update original theme
            this.originalTheme = JSON.parse(JSON.stringify(this.currentTheme));

            // Show success message
            this.showNotification('Theme applied successfully!');
        } catch (error) {
            console.error('Error applying theme:', error);
            this.showNotification('Error applying theme', 'error');
        }
    }

    /**
     * Reset theme to original
     */
    private resetTheme(): void {
        // Reset to original theme
        this.currentTheme = JSON.parse(JSON.stringify(this.originalTheme));

        // Update UI
        (document.getElementById('theme-name') as HTMLInputElement).value = this.currentTheme.name;
        (document.getElementById('theme-description') as HTMLTextAreaElement).value = this.currentTheme.description || '';
        (document.getElementById('theme-author') as HTMLInputElement).value = this.currentTheme.author || '';

        // Update color pickers
        this.initializeColorPickers();

        // Update preview
        this.updatePreview();

        // Show message
        this.showNotification('Theme reset to original');
    }

    /**
     * Export the current theme as JSON
     */
    private exportTheme(): void {
        try {
            // Get theme as JSON
            const themeJson = JSON.stringify(this.currentTheme, null, 2);

            // Create a blob with the theme JSON
            const blob = new Blob([themeJson], { type: 'application/json' });

            // Create a download link
            const url = URL.createObjectURL(blob);
            const a = document.createElement('a');
            a.href = url;
            a.download = `${this.currentTheme.name.replace(/\s+/g, '-').toLowerCase()}.json`;

            // Trigger download
            document.body.appendChild(a);
            a.click();

            // Cleanup
            document.body.removeChild(a);
            URL.revokeObjectURL(url);

            // Show success message
            this.showNotification('Theme exported successfully!');
        } catch (error) {
            console.error('Error exporting theme:', error);
            this.showNotification('Error exporting theme', 'error');
        }
    }

    /**
     * Import a theme from JSON
     */
    private importTheme(): void {
        // Create file input
        const fileInput = document.createElement('input');
        fileInput.type = 'file';
        fileInput.accept = '.json';

        // Add change listener
        fileInput.addEventListener('change', async (event) => {
            const files = (event.target as HTMLInputElement).files;

            if (files && files.length > 0) {
                const file = files[0];

                try {
                    // Read file
                    const themeJson = await this.readFileAsText(file);

                    // Import theme
                    const theme = await this.themeManager.importTheme(themeJson);

                    // Update current theme
                    this.currentTheme = JSON.parse(JSON.stringify(theme));

                    // Update original theme
                    this.originalTheme = JSON.parse(JSON.stringify(theme));

                    // Update UI
                    (document.getElementById('theme-name') as HTMLInputElement).value = theme.name;
                    (document.getElementById('theme-description') as HTMLTextAreaElement).value = theme.description || '';
                    (document.getElementById('theme-author') as HTMLInputElement).value = theme.author || '';

                    // Update color pickers
                    this.initializeColorPickers();

                    // Update preview
                    this.updatePreview();

                    // Update theme select
                    this.populateThemeSelect(document.getElementById('theme-select') as HTMLSelectElement);

                    // Show success message
                    this.showNotification('Theme imported successfully!');
                } catch (error) {
                    console.error('Error importing theme:', error);
                    this.showNotification('Error importing theme', 'error');
                }
            }
        });

        // Trigger file input
        fileInput.click();
    }

    /**
     * Read a file as text
     * @param file The file to read
     */
    private readFileAsText(file: File): Promise<string> {
        return new Promise((resolve, reject) => {
            const reader = new FileReader();

            reader.onload = (event) => {
                if (event.target) {
                    resolve(event.target.result as string);
                } else {
                    reject(new Error('Error reading file'));
                }
            };

            reader.onerror = () => {
                reject(new Error('Error reading file'));
            };

            reader.readAsText(file);
        });
    }

    /**
     * Show a notification
     * @param message The message to show
     * @param type The notification type
     */
    private showNotification(message: string, type: 'success' | 'error' = 'success'): void {
        // Create notification element
        const notification = document.createElement('div');
        notification.className = `notification ${type}`;
        notification.textContent = message;

        // Add to UI
        const content = this.ContainerElement;
        if (!content) return;
        content.appendChild(notification);

        // Remove after delay
        setTimeout(() => {
            content.removeChild(notification);
        }, 3000);
    }
}
