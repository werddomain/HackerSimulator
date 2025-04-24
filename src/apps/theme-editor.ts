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
import { notification } from '../core/components/notification';
import { ThemeCssEditor } from './theme-editor-css';
import { getPropertyHelp } from './theme-editor-help';
import { ThemePreviewEnhancer } from './theme-preview-enhancer';

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
    private cssEditor: ThemeCssEditor;
    private previewEnhancer?: ThemePreviewEnhancer;

    constructor(
        os: OS) {
        super(os);
        this.themeManager = ThemeManager.getInstance(os.getFileSystem(), os.getUserSettings());
        this.currentTheme = JSON.parse(JSON.stringify(this.themeManager.getCurrentTheme()));
        this.originalTheme = this.themeManager.getCurrentTheme();
        this.cssEditor = new ThemeCssEditor(os);
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

        // Custom Fonts section
        const customFontsSection = document.createElement('div');
        customFontsSection.className = 'section fonts-section';
        leftPanel.appendChild(customFontsSection);

        const customFontsTitle = document.createElement('h2');
        customFontsTitle.textContent = 'Custom Fonts';
        customFontsSection.appendChild(customFontsTitle);

        // System font input
        const systemFontInput = document.createElement('input');
        systemFontInput.type = 'text';
        systemFontInput.id = 'system-font';
        systemFontInput.placeholder = 'Arial, Helvetica, sans-serif';
        systemFontInput.value = this.currentTheme.customFonts?.systemFont || '';
        customFontsSection.appendChild(this.createFormGroup('System Font', systemFontInput));

        // Monospace font input
        const monospaceFontInput = document.createElement('input');
        monospaceFontInput.type = 'text';
        monospaceFontInput.id = 'monospace-font';
        monospaceFontInput.placeholder = 'Monaco, Consolas, monospace';
        monospaceFontInput.value = this.currentTheme.customFonts?.monospaceFont || '';
        customFontsSection.appendChild(this.createFormGroup('Monospace Font', monospaceFontInput));

        // Header font input
        const headerFontInput = document.createElement('input');
        headerFontInput.type = 'text';
        headerFontInput.id = 'header-font';
        headerFontInput.placeholder = 'Impact, Arial Black, sans-serif';
        headerFontInput.value = this.currentTheme.customFonts?.headerFont || '';
        customFontsSection.appendChild(this.createFormGroup('Header Font', headerFontInput));

        // UI Element Sizes section
        const uiElementSizesSection = document.createElement('div');
        uiElementSizesSection.className = 'section sizes-section';
        leftPanel.appendChild(uiElementSizesSection);

        const uiElementSizesTitle = document.createElement('h2');
        uiElementSizesTitle.textContent = 'UI Element Sizes';
        uiElementSizesSection.appendChild(uiElementSizesTitle);

        // Border radius input
        const borderRadiusInput = document.createElement('input');
        borderRadiusInput.type = 'text';
        borderRadiusInput.id = 'border-radius';
        borderRadiusInput.placeholder = '4px';
        borderRadiusInput.value = this.currentTheme.uiElementSizes?.borderRadius || '';
        uiElementSizesSection.appendChild(this.createFormGroup('Border Radius', borderRadiusInput));

        // Button height input
        const buttonHeightInput = document.createElement('input');
        buttonHeightInput.type = 'text';
        buttonHeightInput.id = 'button-height';
        buttonHeightInput.placeholder = '32px';
        buttonHeightInput.value = this.currentTheme.uiElementSizes?.buttonHeight || '';
        uiElementSizesSection.appendChild(this.createFormGroup('Button Height', buttonHeightInput));

        // Input height input
        const inputHeightInput = document.createElement('input');
        inputHeightInput.type = 'text';
        inputHeightInput.id = 'input-height';
        inputHeightInput.placeholder = '28px';
        inputHeightInput.value = this.currentTheme.uiElementSizes?.inputHeight || '';
        uiElementSizesSection.appendChild(this.createFormGroup('Input Height', inputHeightInput));

        // Animation Speed section
        const animationSpeedSection = document.createElement('div');
        animationSpeedSection.className = 'section animation-section';
        leftPanel.appendChild(animationSpeedSection);

        const animationSpeedTitle = document.createElement('h2');
        animationSpeedTitle.textContent = 'Animation';
        animationSpeedSection.appendChild(animationSpeedTitle);

        // Animation speed input with slider
        const animationSpeedContainer = document.createElement('div');
        animationSpeedContainer.className = 'slider-container';
        
        const animationSpeedLabel = document.createElement('label');
        animationSpeedLabel.htmlFor = 'animation-speed';
        animationSpeedLabel.textContent = 'Animation Speed';
        
        const animationSpeedValue = document.createElement('span');
        animationSpeedValue.className = 'slider-value';
        animationSpeedValue.textContent = String(this.currentTheme.animationSpeed || 1);
        
        const animationSpeedInput = document.createElement('input');
        animationSpeedInput.type = 'range';
        animationSpeedInput.id = 'animation-speed';
        animationSpeedInput.min = '0.1';
        animationSpeedInput.max = '2';
        animationSpeedInput.step = '0.1';
        animationSpeedInput.value = String(this.currentTheme.animationSpeed || 1);
        
        animationSpeedInput.addEventListener('input', () => {
            animationSpeedValue.textContent = animationSpeedInput.value;
        });
        
        animationSpeedContainer.appendChild(animationSpeedLabel);
        animationSpeedContainer.appendChild(animationSpeedInput);
        animationSpeedContainer.appendChild(animationSpeedValue);
        
        animationSpeedSection.appendChild(animationSpeedContainer);

        // Title Bar Configuration section
        const titleBarSection = document.createElement('div');
        titleBarSection.className = 'section titlebar-section';
        leftPanel.appendChild(titleBarSection);

        const titleBarTitle = document.createElement('h2');
        titleBarTitle.textContent = 'Title Bar Configuration';
        titleBarSection.appendChild(titleBarTitle);

        // Initialize titleBar if it doesn't exist
        if (!this.currentTheme.titleBar) {
            this.currentTheme.titleBar = {
                buttonPlacement: 'right',
                useGradient: false,
                showIcon: true,
            };
        }

        // Button Placement
        const buttonPlacementGroup = document.createElement('div');
        buttonPlacementGroup.className = 'form-group';
        
        const buttonPlacementLabel = document.createElement('label');
        buttonPlacementLabel.textContent = 'Button Placement';
        buttonPlacementLabel.htmlFor = 'button-placement';
        
        const buttonPlacementSelect = document.createElement('select');
        buttonPlacementSelect.id = 'button-placement';
        
        const leftOption = document.createElement('option');
        leftOption.value = 'left';
        leftOption.textContent = 'Left (Windows style)';
        leftOption.selected = this.currentTheme.titleBar.buttonPlacement === 'left';
        
        const rightOption = document.createElement('option');
        rightOption.value = 'right';
        rightOption.textContent = 'Right (macOS style)';
        rightOption.selected = this.currentTheme.titleBar.buttonPlacement === 'right';
        
        buttonPlacementSelect.appendChild(leftOption);
        buttonPlacementSelect.appendChild(rightOption);
        
        buttonPlacementGroup.appendChild(buttonPlacementLabel);
        buttonPlacementGroup.appendChild(buttonPlacementSelect);
        titleBarSection.appendChild(buttonPlacementGroup);

        // Use Gradient checkbox
        const useGradientGroup = document.createElement('div');
        useGradientGroup.className = 'form-group checkbox-group';
        
        const useGradientInput = document.createElement('input');
        useGradientInput.type = 'checkbox';
        useGradientInput.id = 'use-gradient';
        useGradientInput.checked = !!this.currentTheme.titleBar.useGradient;
        
        const useGradientLabel = document.createElement('label');
        useGradientLabel.textContent = 'Use Gradient (Windows 98 style)';
        useGradientLabel.htmlFor = 'use-gradient';
        
        useGradientGroup.appendChild(useGradientInput);
        useGradientGroup.appendChild(useGradientLabel);
        titleBarSection.appendChild(useGradientGroup);

        // Show Icon checkbox
        const showIconGroup = document.createElement('div');
        showIconGroup.className = 'form-group checkbox-group';
        
        const showIconInput = document.createElement('input');
        showIconInput.type = 'checkbox';
        showIconInput.id = 'show-icon';
        showIconInput.checked = !!this.currentTheme.titleBar.showIcon;
        
        const showIconLabel = document.createElement('label');
        showIconLabel.textContent = 'Show Window Icon';
        showIconLabel.htmlFor = 'show-icon';
        
        showIconGroup.appendChild(showIconInput);
        showIconGroup.appendChild(showIconLabel);
        titleBarSection.appendChild(showIconGroup);

        // Button Style
        const buttonStyleGroup = document.createElement('div');
        buttonStyleGroup.className = 'form-group';
        
        const buttonStyleLabel = document.createElement('label');
        buttonStyleLabel.textContent = 'Button Style';
        buttonStyleLabel.htmlFor = 'button-style';
        
        const buttonStyleSelect = document.createElement('select');
        buttonStyleSelect.id = 'button-style';
        
        const squareOption = document.createElement('option');
        squareOption.value = 'square';
        squareOption.textContent = 'Square';
        squareOption.selected = this.currentTheme.titleBar.buttonStyle === 'square';
        
        const circleOption = document.createElement('option');
        circleOption.value = 'circle';
        circleOption.textContent = 'Circle (macOS style)';
        circleOption.selected = this.currentTheme.titleBar.buttonStyle === 'circle';
        
        const pillOption = document.createElement('option');
        pillOption.value = 'pill';
        pillOption.textContent = 'Pill';
        pillOption.selected = this.currentTheme.titleBar.buttonStyle === 'pill';
        
        buttonStyleSelect.appendChild(squareOption);
        buttonStyleSelect.appendChild(circleOption);
        buttonStyleSelect.appendChild(pillOption);
        
        buttonStyleGroup.appendChild(buttonStyleLabel);
        buttonStyleGroup.appendChild(buttonStyleSelect);
        titleBarSection.appendChild(buttonStyleGroup);

        // Text Alignment
        const textAlignmentGroup = document.createElement('div');
        textAlignmentGroup.className = 'form-group';
        
        const textAlignmentLabel = document.createElement('label');
        textAlignmentLabel.textContent = 'Text Alignment';
        textAlignmentLabel.htmlFor = 'text-alignment';
        
        const textAlignmentSelect = document.createElement('select');
        textAlignmentSelect.id = 'text-alignment';
        
        const leftAlignOption = document.createElement('option');
        leftAlignOption.value = 'left';
        leftAlignOption.textContent = 'Left';
        leftAlignOption.selected = this.currentTheme.titleBar.textAlignment === 'left';
        
        const centerAlignOption = document.createElement('option');
        centerAlignOption.value = 'center';
        centerAlignOption.textContent = 'Center';
        centerAlignOption.selected = this.currentTheme.titleBar.textAlignment === 'center';
        
        const rightAlignOption = document.createElement('option');
        rightAlignOption.value = 'right';
        rightAlignOption.textContent = 'Right';
        rightAlignOption.selected = this.currentTheme.titleBar.textAlignment === 'right';
        
        textAlignmentSelect.appendChild(leftAlignOption);
        textAlignmentSelect.appendChild(centerAlignOption);
        textAlignmentSelect.appendChild(rightAlignOption);
        
        textAlignmentGroup.appendChild(textAlignmentLabel);
        textAlignmentGroup.appendChild(textAlignmentSelect);
        titleBarSection.appendChild(textAlignmentGroup);

        // Title Bar Height
        const titleBarHeightGroup = document.createElement('div');
        titleBarHeightGroup.className = 'form-group';
        
        const titleBarHeightLabel = document.createElement('label');
        titleBarHeightLabel.textContent = 'Title Bar Height';
        titleBarHeightLabel.htmlFor = 'titlebar-height';
        
        const titleBarHeightInput = document.createElement('input');
        titleBarHeightInput.type = 'text';
        titleBarHeightInput.id = 'titlebar-height';
        titleBarHeightInput.placeholder = '28px';
        titleBarHeightInput.value = this.currentTheme.titleBar.height || '';
        
        titleBarHeightGroup.appendChild(titleBarHeightLabel);
        titleBarHeightGroup.appendChild(titleBarHeightInput);
        titleBarSection.appendChild(titleBarHeightGroup);

        // Custom CSS Button
        const titleBarCssGroup = document.createElement('div');
        titleBarCssGroup.className = 'form-group';
        
        const titleBarCssButton = document.createElement('button');
        titleBarCssButton.id = 'edit-titlebar-css';
        titleBarCssButton.textContent = 'Edit Custom CSS';
        titleBarCssButton.className = 'secondary-button';
        
        titleBarCssGroup.appendChild(titleBarCssButton);
        titleBarSection.appendChild(titleBarCssGroup);

        // Taskbar Configuration section
        const taskbarSection = document.createElement('div');
        taskbarSection.className = 'section taskbar-section';
        leftPanel.appendChild(taskbarSection);

        const taskbarTitle = document.createElement('h2');
        taskbarTitle.textContent = 'Taskbar Configuration';
        taskbarSection.appendChild(taskbarTitle);

        // Initialize taskbar if it doesn't exist
        if (!this.currentTheme.taskbar) {
            this.currentTheme.taskbar = {
                position: 'bottom'
            };
        }

        // Taskbar Position
        const taskbarPositionGroup = document.createElement('div');
        taskbarPositionGroup.className = 'form-group';
        
        const taskbarPositionLabel = document.createElement('label');
        taskbarPositionLabel.textContent = 'Position';
        taskbarPositionLabel.htmlFor = 'taskbar-position';
        
        const taskbarPositionSelect = document.createElement('select');
        taskbarPositionSelect.id = 'taskbar-position';
        
        const positionOptions = [
            { value: 'top', text: 'Top' },
            { value: 'bottom', text: 'Bottom' },
            { value: 'left', text: 'Left' },
            { value: 'right', text: 'Right' }
        ];
        
        positionOptions.forEach(option => {
            const optionEl = document.createElement('option');
            optionEl.value = option.value;
            optionEl.textContent = option.text;
            optionEl.selected = this.currentTheme.taskbar?.position === option.value;
            taskbarPositionSelect.appendChild(optionEl);
        });
        
        taskbarPositionGroup.appendChild(taskbarPositionLabel);
        taskbarPositionGroup.appendChild(taskbarPositionSelect);
        taskbarSection.appendChild(taskbarPositionGroup);

        // Taskbar Size
        const taskbarSizeGroup = document.createElement('div');
        taskbarSizeGroup.className = 'form-group';
        
        const taskbarSizeLabel = document.createElement('label');
        taskbarSizeLabel.textContent = 'Size';
        taskbarSizeLabel.htmlFor = 'taskbar-size';
        
        const taskbarSizeInput = document.createElement('input');
        taskbarSizeInput.type = 'text';
        taskbarSizeInput.id = 'taskbar-size';
        taskbarSizeInput.placeholder = '40px';
        taskbarSizeInput.value = this.currentTheme.taskbar?.size || '';
        
        taskbarSizeGroup.appendChild(taskbarSizeLabel);
        taskbarSizeGroup.appendChild(taskbarSizeInput);
        taskbarSection.appendChild(taskbarSizeGroup);

        // Taskbar Transparency
        const taskbarTransparencyGroup = document.createElement('div');
        taskbarTransparencyGroup.className = 'form-group';
        
        const taskbarTransparencyLabel = document.createElement('label');
        taskbarTransparencyLabel.textContent = 'Transparency';
        taskbarTransparencyLabel.htmlFor = 'taskbar-transparency';
        
        const taskbarTransparencyContainer = document.createElement('div');
        taskbarTransparencyContainer.className = 'slider-container';
        
        const taskbarTransparencyValue = document.createElement('span');
        taskbarTransparencyValue.className = 'slider-value';
        taskbarTransparencyValue.textContent = String(this.currentTheme.taskbar?.transparency || 0);
        
        const taskbarTransparencyInput = document.createElement('input');
        taskbarTransparencyInput.type = 'range';
        taskbarTransparencyInput.id = 'taskbar-transparency';
        taskbarTransparencyInput.min = '0';
        taskbarTransparencyInput.max = '1';
        taskbarTransparencyInput.step = '0.1';
        taskbarTransparencyInput.value = String(this.currentTheme.taskbar?.transparency || 0);
        
        taskbarTransparencyInput.addEventListener('input', () => {
            taskbarTransparencyValue.textContent = taskbarTransparencyInput.value;
        });
        
        taskbarTransparencyContainer.appendChild(taskbarTransparencyInput);
        taskbarTransparencyContainer.appendChild(taskbarTransparencyValue);
        
        taskbarTransparencyGroup.appendChild(taskbarTransparencyLabel);
        taskbarTransparencyGroup.appendChild(taskbarTransparencyContainer);
        taskbarSection.appendChild(taskbarTransparencyGroup);

        // Taskbar Blur
        const taskbarBlurGroup = document.createElement('div');
        taskbarBlurGroup.className = 'form-group checkbox-group';
        
        const taskbarBlurInput = document.createElement('input');
        taskbarBlurInput.type = 'checkbox';
        taskbarBlurInput.id = 'taskbar-blur';
        taskbarBlurInput.checked = !!this.currentTheme.taskbar?.blur;
        
        const taskbarBlurLabel = document.createElement('label');
        taskbarBlurLabel.textContent = 'Apply Blur Effect';
        taskbarBlurLabel.htmlFor = 'taskbar-blur';
        
        taskbarBlurGroup.appendChild(taskbarBlurInput);
        taskbarBlurGroup.appendChild(taskbarBlurLabel);
        taskbarSection.appendChild(taskbarBlurGroup);

        // Taskbar Item Spacing
        const taskbarItemSpacingGroup = document.createElement('div');
        taskbarItemSpacingGroup.className = 'form-group';
        
        const taskbarItemSpacingLabel = document.createElement('label');
        taskbarItemSpacingLabel.textContent = 'Item Spacing';
        taskbarItemSpacingLabel.htmlFor = 'taskbar-item-spacing';
        
        const taskbarItemSpacingInput = document.createElement('input');
        taskbarItemSpacingInput.type = 'text';
        taskbarItemSpacingInput.id = 'taskbar-item-spacing';
        taskbarItemSpacingInput.placeholder = '4px';
        taskbarItemSpacingInput.value = this.currentTheme.taskbar?.itemSpacing || '';
        
        taskbarItemSpacingGroup.appendChild(taskbarItemSpacingLabel);
        taskbarItemSpacingGroup.appendChild(taskbarItemSpacingInput);
        taskbarSection.appendChild(taskbarItemSpacingGroup);

        // Custom CSS Button for Taskbar
        const taskbarCssGroup = document.createElement('div');
        taskbarCssGroup.className = 'form-group';
        
        const taskbarCssButton = document.createElement('button');
        taskbarCssButton.id = 'edit-taskbar-css';
        taskbarCssButton.textContent = 'Edit Custom CSS';
        taskbarCssButton.className = 'secondary-button';
        
        taskbarCssGroup.appendChild(taskbarCssButton);
        taskbarSection.appendChild(taskbarCssGroup);

        // Start Menu Configuration section
        const startMenuSection = document.createElement('div');
        startMenuSection.className = 'section startmenu-section';
        leftPanel.appendChild(startMenuSection);

        const startMenuTitle = document.createElement('h2');
        startMenuTitle.textContent = 'Start Menu Configuration';
        startMenuSection.appendChild(startMenuTitle);

        // Initialize startMenu if it doesn't exist
        if (!this.currentTheme.startMenu) {
            this.currentTheme.startMenu = {};
        }

        // Custom CSS Button for Start Menu
        const startMenuCssGroup = document.createElement('div');
        startMenuCssGroup.className = 'form-group';
        
        const startMenuCssButton = document.createElement('button');
        startMenuCssButton.id = 'edit-startmenu-css';
        startMenuCssButton.textContent = 'Edit Custom CSS';
        startMenuCssButton.className = 'secondary-button';
        
        startMenuCssGroup.appendChild(startMenuCssButton);
        startMenuSection.appendChild(startMenuCssGroup);

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
    }    /**
     * Set up event listeners
     */
    private setupEventListeners(): void {
        // Theme selection
        const themeSelect = document.getElementById('theme-select') as HTMLSelectElement;
        if (!themeSelect) {
            console.warn('Theme select element not found in the DOM');
            return;
        }

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

        // Custom Fonts event listeners
        const systemFontInput = document.getElementById('system-font') as HTMLInputElement;
        systemFontInput.addEventListener('input', () => {
            if (!this.currentTheme.customFonts) {
                this.currentTheme.customFonts = {};
            }
            this.currentTheme.customFonts.systemFont = systemFontInput.value;
        });

        const monospaceFontInput = document.getElementById('monospace-font') as HTMLInputElement;
        monospaceFontInput.addEventListener('input', () => {
            if (!this.currentTheme.customFonts) {
                this.currentTheme.customFonts = {};
            }
            this.currentTheme.customFonts.monospaceFont = monospaceFontInput.value;
        });

        const headerFontInput = document.getElementById('header-font') as HTMLInputElement;
        headerFontInput.addEventListener('input', () => {
            if (!this.currentTheme.customFonts) {
                this.currentTheme.customFonts = {};
            }
            this.currentTheme.customFonts.headerFont = headerFontInput.value;
        });

        // UI Element Sizes event listeners
        const borderRadiusInput = document.getElementById('border-radius') as HTMLInputElement;
        borderRadiusInput.addEventListener('input', () => {
            if (!this.currentTheme.uiElementSizes) {
                this.currentTheme.uiElementSizes = {};
            }
            this.currentTheme.uiElementSizes.borderRadius = borderRadiusInput.value;
        });

        const buttonHeightInput = document.getElementById('button-height') as HTMLInputElement;
        buttonHeightInput.addEventListener('input', () => {
            if (!this.currentTheme.uiElementSizes) {
                this.currentTheme.uiElementSizes = {};
            }
            this.currentTheme.uiElementSizes.buttonHeight = buttonHeightInput.value;
        });

        const inputHeightInput = document.getElementById('input-height') as HTMLInputElement;
        inputHeightInput.addEventListener('input', () => {
            if (!this.currentTheme.uiElementSizes) {
                this.currentTheme.uiElementSizes = {};
            }
            this.currentTheme.uiElementSizes.inputHeight = inputHeightInput.value;
        });

        // Animation Speed event listener
        const animationSpeedInput = document.getElementById('animation-speed') as HTMLInputElement;
        animationSpeedInput.addEventListener('input', () => {
            this.currentTheme.animationSpeed = parseFloat(animationSpeedInput.value);
        });

        // Title Bar configuration event listeners
        if (!this.currentTheme.titleBar) {
            this.currentTheme.titleBar = {
                buttonPlacement: 'right',
                useGradient: false,
                showIcon: true
            };
        }

        const buttonPlacementSelect = document.getElementById('button-placement') as HTMLSelectElement;
        buttonPlacementSelect.addEventListener('change', () => {
            if (!this.currentTheme.titleBar) this.currentTheme.titleBar = { buttonPlacement: 'right', useGradient: false, showIcon: true };
            this.currentTheme.titleBar.buttonPlacement = buttonPlacementSelect.value as 'left' | 'right';
        });

        const useGradientInput = document.getElementById('use-gradient') as HTMLInputElement;
        useGradientInput.addEventListener('change', () => {
            if (!this.currentTheme.titleBar) this.currentTheme.titleBar = { buttonPlacement: 'right', useGradient: false, showIcon: true };
            this.currentTheme.titleBar.useGradient = useGradientInput.checked;
        });

        const showIconInput = document.getElementById('show-icon') as HTMLInputElement;
        showIconInput.addEventListener('change', () => {
            if (!this.currentTheme.titleBar) this.currentTheme.titleBar = { buttonPlacement: 'right', useGradient: false, showIcon: true };
            this.currentTheme.titleBar.showIcon = showIconInput.checked;
        });

        const buttonStyleSelect = document.getElementById('button-style') as HTMLSelectElement;
        buttonStyleSelect.addEventListener('change', () => {
            if (!this.currentTheme.titleBar) this.currentTheme.titleBar = { buttonPlacement: 'right', useGradient: false, showIcon: true };
            this.currentTheme.titleBar.buttonStyle = buttonStyleSelect.value as 'square' | 'circle' | 'pill';
        });

        const textAlignmentSelect = document.getElementById('text-alignment') as HTMLSelectElement;
        textAlignmentSelect.addEventListener('change', () => {
            if (!this.currentTheme.titleBar) this.currentTheme.titleBar = { buttonPlacement: 'right', useGradient: false, showIcon: true };
            this.currentTheme.titleBar.textAlignment = textAlignmentSelect.value as 'left' | 'center' | 'right';
        });

        const titleBarHeightInput = document.getElementById('titlebar-height') as HTMLInputElement;
        titleBarHeightInput.addEventListener('input', () => {
            if (!this.currentTheme.titleBar) this.currentTheme.titleBar = { buttonPlacement: 'right', useGradient: false, showIcon: true };
            this.currentTheme.titleBar.height = titleBarHeightInput.value;
        });

        // Taskbar configuration event listeners
        if (!this.currentTheme.taskbar) {
            this.currentTheme.taskbar = {
                position: 'bottom'
            };
        }

        const taskbarPositionSelect = document.getElementById('taskbar-position') as HTMLSelectElement;
        taskbarPositionSelect.addEventListener('change', () => {
            if (!this.currentTheme.taskbar) this.currentTheme.taskbar = { position: 'bottom' };
            this.currentTheme.taskbar.position = taskbarPositionSelect.value as 'top' | 'bottom' | 'left' | 'right';
        });

        const taskbarSizeInput = document.getElementById('taskbar-size') as HTMLInputElement;
        taskbarSizeInput.addEventListener('input', () => {
            if (!this.currentTheme.taskbar) this.currentTheme.taskbar = { position: 'bottom' };
            this.currentTheme.taskbar.size = taskbarSizeInput.value;
        });

        const taskbarTransparencyInput = document.getElementById('taskbar-transparency') as HTMLInputElement;
        taskbarTransparencyInput.addEventListener('input', () => {
            if (!this.currentTheme.taskbar) this.currentTheme.taskbar = { position: 'bottom' };
            this.currentTheme.taskbar.transparency = parseFloat(taskbarTransparencyInput.value);
        });

        const taskbarBlurInput = document.getElementById('taskbar-blur') as HTMLInputElement;
        taskbarBlurInput.addEventListener('change', () => {
            if (!this.currentTheme.taskbar) this.currentTheme.taskbar = { position: 'bottom' };
            this.currentTheme.taskbar.blur = taskbarBlurInput.checked;
        });

        const taskbarItemSpacingInput = document.getElementById('taskbar-item-spacing') as HTMLInputElement;
        taskbarItemSpacingInput.addEventListener('input', () => {
            if (!this.currentTheme.taskbar) this.currentTheme.taskbar = { position: 'bottom' };
            this.currentTheme.taskbar.itemSpacing = taskbarItemSpacingInput.value;
        });

        // CSS editor button event listeners
        const titleBarCssButton = document.getElementById('edit-titlebar-css') as HTMLButtonElement;
        titleBarCssButton.addEventListener('click', () => {
            this.openCssEditor('titleBar');
        });

        const taskbarCssButton = document.getElementById('edit-taskbar-css') as HTMLButtonElement;
        taskbarCssButton.addEventListener('click', () => {
            this.openCssEditor('taskbar');
        });

        const startMenuCssButton = document.getElementById('edit-startmenu-css') as HTMLButtonElement;
        startMenuCssButton.addEventListener('click', () => {
            this.openCssEditor('startMenu');
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
    }    /**
     * Update preview with current theme
     */
    private updatePreview(): void {
        // Apply current theme to preview container
        const previewTheme = this.currentTheme;
        if (!this.previewContainer) return;
        
        // Create custom style blocks for theme properties
        let customStyles = '';
        
        // Add custom fonts styling if defined
        if (previewTheme.customFonts) {
            if (previewTheme.customFonts.systemFont) {
                customStyles += `.preview-system-font { font-family: ${previewTheme.customFonts.systemFont}; }\n`;
            }
            if (previewTheme.customFonts.monospaceFont) {
                customStyles += `.preview-monospace-font { font-family: ${previewTheme.customFonts.monospaceFont}; }\n`;
            }
            if (previewTheme.customFonts.headerFont) {
                customStyles += `.preview-header-font { font-family: ${previewTheme.customFonts.headerFont}; }\n`;
            }
        }
        
        // Add UI element sizes styling if defined
        if (previewTheme.uiElementSizes) {
            if (previewTheme.uiElementSizes.borderRadius) {
                customStyles += `.preview-border-radius { border-radius: ${previewTheme.uiElementSizes.borderRadius}; }\n`;
            }
            if (previewTheme.uiElementSizes.buttonHeight) {
                customStyles += `.preview-button-height { height: ${previewTheme.uiElementSizes.buttonHeight}; }\n`;
            }
            if (previewTheme.uiElementSizes.inputHeight) {
                customStyles += `.preview-input-height { height: ${previewTheme.uiElementSizes.inputHeight}; }\n`;
            }
        }
        
        // Add title bar custom styles if defined
        if (previewTheme.titleBar?.customCss) {
            customStyles += `/* Title Bar Custom CSS */\n${previewTheme.titleBar.customCss}\n`;
        }
        
        // Add taskbar custom styles if defined
        if (previewTheme.taskbar?.customCss) {
            customStyles += `/* Taskbar Custom CSS */\n${previewTheme.taskbar.customCss}\n`;
        }
        
        // Add start menu custom styles if defined
        if (previewTheme.startMenu?.customCss) {
            customStyles += `/* Start Menu Custom CSS */\n${previewTheme.startMenu.customCss}\n`;
        }
        
        // Create animation speed styles
        const animSpeed = previewTheme.animationSpeed || 1;
        customStyles += `.preview-animated { transition: all ${animSpeed}s ease; }\n`;
        
        // Create the style element
        const styleElement = `<style>${customStyles}</style>`;
        
        // Create the tabbed preview interface
        const previewHTML = `
            ${styleElement}
            <div class="preview-tabs">
                <div class="preview-tab active" data-tab="window">Window</div>
                <div class="preview-tab" data-tab="taskbar">Taskbar</div>
                <div class="preview-tab" data-tab="fonts">Fonts & Sizes</div>
                <div class="preview-tab" data-tab="animation">Animation</div>
            </div>
            
            <div class="preview-content">
                <!-- Window Preview -->
                <div class="preview-pane active" data-tab="window">
                    <div class="preview-window" style="background-color: ${previewTheme.primaryColor}; border: 1px solid ${previewTheme.windowBorderColor};">
                        <div class="preview-window-header" style="background-color: ${previewTheme.windowHeaderColor}; color: ${previewTheme.textColorPrimary};">
                            ${previewTheme.titleBar?.showIcon ? '<span class="window-icon">🔹</span>' : ''}
                            <span class="window-title">Window Title</span>
                            <div class="window-controls" style="display: flex; ${previewTheme.titleBar?.buttonPlacement === 'left' ? 'order: -1;' : ''}">
                                <button class="window-button">−</button>
                                <button class="window-button">□</button>
                                <button class="window-button">×</button>
                            </div>
                        </div>
                        <div class="preview-window-content" style="color: ${previewTheme.textColorPrimary}; padding: 10px;">
                            <h3 style="color: ${previewTheme.textColorPrimary};">Text Colors</h3>
                            <p style="color: ${previewTheme.textColorPrimary};">Primary Text</p>
                            <p style="color: ${previewTheme.textColorSecondary};">Secondary Text</p>
                            <p style="color: ${previewTheme.textColorDisabled};">Disabled Text</p>
                            
                            <h3 style="color: ${previewTheme.textColorPrimary};">Status Colors</h3>
                            <div style="color: ${previewTheme.successColor};">Success Status</div>
                            <div style="color: ${previewTheme.warningColor};">Warning Status</div>
                            <div style="color: ${previewTheme.errorColor};">Error Status</div>
                            <div style="color: ${previewTheme.infoColor};">Info Status</div>
                            
                            <h3 style="color: ${previewTheme.textColorPrimary};">UI Elements</h3>
                            <div style="display: flex; gap: 10px; margin-top: 10px;">
                                <button class="preview-border-radius preview-button-height" 
                                    style="background: linear-gradient(to bottom, ${previewTheme.accentColor}, ${previewTheme.accentColorDark}); color: white; border: none; padding: 0 15px;">
                                    Primary Button
                                </button>
                                <input type="text" class="preview-border-radius preview-input-height"
                                    placeholder="Input field" 
                                    style="background-color: ${previewTheme.tertiaryColor}; color: ${previewTheme.textColorPrimary}; border: 1px solid rgba(255,255,255,0.1); padding: 0 10px;" />
                            </div>
                        </div>
                    </div>
                </div>
                
                <!-- Taskbar Preview -->
                <div class="preview-pane" data-tab="taskbar">
                    <div class="preview-taskbar" style="background: linear-gradient(to bottom, ${previewTheme.taskbarBgStart}, ${previewTheme.taskbarBgEnd}); border-top: 1px solid ${previewTheme.taskbarBorderTopColor}; padding: 5px; display: flex; gap: 5px;">
                        <button class="preview-start-button" style="background: linear-gradient(to bottom, ${previewTheme.startButtonBgStart}, ${previewTheme.startButtonBgEnd}); color: white; border: none; padding: 5px 10px;">
                            Start
                        </button>
                        <div style="background-color: ${previewTheme.taskbarItemBg}; padding: 5px 10px;">
                            App 1
                        </div>
                        <div style="background-color: ${previewTheme.taskbarItemActiveBg}; border-bottom: ${previewTheme.taskbarItemActiveBorder}; padding: 5px 10px;">
                            App 2
                        </div>
                    </div>
                    <div class="taskbar-info" style="margin-top: 20px; background-color: ${previewTheme.primaryColor}; padding: 10px; color: ${previewTheme.textColorPrimary};">
                        <h3>Taskbar Configuration</h3>
                        <p>Position: ${previewTheme.taskbar?.position || 'bottom'}</p>
                        <p>Size: ${previewTheme.taskbar?.size || 'default'}</p>
                        <p>Transparency: ${previewTheme.taskbar?.transparency || '0'}</p>
                        <p>Blur Effect: ${previewTheme.taskbar?.blur ? 'Enabled' : 'Disabled'}</p>
                    </div>
                </div>
                
                <!-- Fonts & Sizes Preview -->
                <div class="preview-pane" data-tab="fonts">
                    <div style="background-color: ${previewTheme.primaryColor}; padding: 15px; color: ${previewTheme.textColorPrimary};">
                        <h3>Custom Fonts</h3>
                        <p class="preview-system-font">System Font: ${previewTheme.customFonts?.systemFont || 'Default'}</p>
                        <p class="preview-monospace-font">Monospace Font: ${previewTheme.customFonts?.monospaceFont || 'Default'}</p>
                        <p class="preview-header-font">Header Font: ${previewTheme.customFonts?.headerFont || 'Default'}</p>
                        
                        <h3>UI Element Sizes</h3>
                        <p>Border Radius: ${previewTheme.uiElementSizes?.borderRadius || 'Default'}</p>
                        <p>Button Height: ${previewTheme.uiElementSizes?.buttonHeight || 'Default'}</p>
                        <p>Input Height: ${previewTheme.uiElementSizes?.inputHeight || 'Default'}</p>
                        
                        <div style="display: flex; gap: 10px; margin-top: 10px;">
                            <button class="preview-border-radius preview-button-height" 
                                style="background: linear-gradient(to bottom, ${previewTheme.accentColor}, ${previewTheme.accentColorDark}); color: white; border: none; padding: 0 15px;">
                                Button with Custom Height
                            </button>
                            <input type="text" class="preview-border-radius preview-input-height"
                                placeholder="Input with Custom Height" 
                                style="background-color: ${previewTheme.tertiaryColor}; color: ${previewTheme.textColorPrimary}; border: 1px solid rgba(255,255,255,0.1); padding: 0 10px;" />
                        </div>
                    </div>
                </div>
                
                <!-- Animation Preview -->
                <div class="preview-pane" data-tab="animation">
                    <div style="background-color: ${previewTheme.primaryColor}; padding: 15px; color: ${previewTheme.textColorPrimary};">
                        <h3>Animation Speed: ${previewTheme.animationSpeed || '1'}</h3>
                        <p>Hover over elements below to see animation:</p>
                        
                        <div style="display: flex; gap: 20px; margin-top: 20px;">
                            <button class="preview-animated" 
                                style="background-color: ${previewTheme.accentColor}; color: white; border: none; padding: 10px 20px; border-radius: 4px;"
                                onmouseover="this.style.transform='scale(1.1)'" 
                                onmouseout="this.style.transform='scale(1)'">
                                Hover Me
                            </button>
                            
                            <div class="preview-animated" 
                                style="width: 50px; height: 50px; background-color: ${previewTheme.accentColor}; border-radius: 4px;"
                                onmouseover="this.style.transform='rotate(45deg)'" 
                                onmouseout="this.style.transform='rotate(0deg)'">
                            </div>
                        </div>
                    </div>
                </div>
            </div>
        `;
        
        // Update the container
        this.previewContainer.innerHTML = previewHTML;
        
        // Add tab switching functionality
        const tabs = this.previewContainer.querySelectorAll('.preview-tab');
        tabs.forEach(tab => {
            tab.addEventListener('click', () => {
                // Update active tab
                tabs.forEach(t => t.classList.remove('active'));
                tab.classList.add('active');
                
                // Update active pane
                const tabId = tab.getAttribute('data-tab');
                const panes = this.previewContainer?.querySelectorAll('.preview-pane');
                panes?.forEach(pane => {
                    pane.classList.remove('active');
                    if (pane.getAttribute('data-tab') === tabId) {
                        pane.classList.add('active');
                    }
                });
            });
        });
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
    }    /**
     * Show a notification
     * @param message The message to show
     * @param type The notification type
     */
    private showNotification(message: string, type: 'success' | 'error' = 'success'): void {
        // Use the reusable notification component
        if (type === 'success') {
            notification.success(message);
        } else {
            notification.error(message);
        }
    }    /**
     * Open CSS Editor for customCss properties
     * This launches the code-editor app to edit customCss
     * @param property The property containing customCss ('titleBar', 'taskbar', or 'startMenu')
     */
    private async openCssEditor(property: 'titleBar' | 'taskbar' | 'startMenu'): Promise<void> {
        // Get the current CSS content or create empty content
        let cssContent = '';
        if (this.currentTheme[property] && this.currentTheme[property]?.customCss) {
            cssContent = this.currentTheme[property]?.customCss || '';
        }

        // Create CSS file for editing
        const filePath = await this.cssEditor.createCssFile(property, cssContent);
        if (!filePath) {
            this.showNotification('Failed to create CSS file', 'error');
            return;
        }

        // Launch editor and handle callback
        await this.cssEditor.launchEditor(filePath, (updatedCss) => {
            if (updatedCss !== null) {
                // Update the theme object
                if (!this.currentTheme[property]) {
                    // Initialize the object if it doesn't exist
                    if (property === 'titleBar') {
                        this.currentTheme.titleBar = {
                            buttonPlacement: 'right',
                            useGradient: false,
                            showIcon: true,
                            customCss: updatedCss
                        };
                    } else if (property === 'taskbar') {
                        this.currentTheme.taskbar = {
                            position: 'bottom',
                            customCss: updatedCss
                        };
                    } else if (property === 'startMenu') {
                        this.currentTheme.startMenu = {
                            customCss: updatedCss
                        };
                    }
                } else {
                    // Just update the customCss property
                    this.currentTheme[property]!.customCss = updatedCss;
                }
                
                // Update preview
                this.updatePreview();
                
                // Show success notification
                this.showNotification('CSS updated successfully');
            }
        });
    }
}
