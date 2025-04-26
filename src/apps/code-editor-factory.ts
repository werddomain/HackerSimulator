// Code Editor Factory
// Provides platform-specific code editor implementations

import { OS } from '../core/os';
import { CodeEditorApp } from './code-editor';
import { MobileCodeEditorApp } from './mobile-code-editor';
import { PlatformType, platformDetector } from '../core/platform-detector';
import { GuiApplication } from '../core/gui-application';

/**
 * Code Editor Factory
 * Creates the appropriate code editor implementation based on platform
 */
export class CodeEditorFactory {
  private static instance: CodeEditorFactory;
  
  /**
   * Get singleton instance
   */
  public static getInstance(): CodeEditorFactory {
    if (!CodeEditorFactory.instance) {
      CodeEditorFactory.instance = new CodeEditorFactory();
    }
    return CodeEditorFactory.instance;
  }
  
  /**
   * Create a code editor appropriate for the current platform
   * @param os Operating system instance
   * @returns Code editor application instance
   */
  public createCodeEditor(os: OS): GuiApplication {
    // Check platform type
    const platformType = platformDetector.getPlatformType();
    
    if (platformType === PlatformType.MOBILE) {
      // Create mobile code editor for mobile platforms
      return new MobileCodeEditorApp(os);
    } else {
      // Create standard code editor for desktop platforms
      return new CodeEditorApp(os);
    }
  }
}
