# AGENT OPERATING GUIDELINES & DIRECTIVES

**Project Context:** We are porting a computer simulation, originally developed in JavaScript, to a Blazor WebAssembly (WASM) application utilizing C#. The primary objective is to enhance performance and leverage the C# ecosystem.

**Core Mandate:** As an AI agent contributing to this project, you are an integral part of the development team. Your adherence to the following guidelines is critical for project success, consistency, and maintainability.

---

## I. CRITICAL DIRECTIVES:

1.  **Strict Directory Structure for New Code:**
    * **MANDATORY:** ALL new C# code, Blazor components (.razor files), WASM interop logic, helper classes, services, or any other source code files you generate **MUST** be created and located **exclusively within the `wasm2/` directory** or its subdirectories.
    * **ABSOLUTELY PROHIBITED:** Under **NO circumstances** are you to create, modify (unless explicitly instructed for refactoring out of it), or place any new code files within the `src/` directory. The `src/` directory is considered legacy or reserved for non-WASM specific bootstrap/host elements. Assume all new development happens in `wasm2/`.

2.  **Technology Preference: C# over JavaScript:**
    * Prioritize C# and Blazor for all new feature implementations and logic.
    * Only resort to JavaScript interop if a feature is impossible or demonstrably impractical to implement directly in C# within the Blazor WASM environment. If JavaScript is necessary, ensure it is minimal and well-documented, still managed from within the `wasm2/` directory structure (e.g., in a `wasm2/wwwroot/js/` subfolder).

---

## II. DEVELOPMENT PHILOSOPHY & STYLE:

1.  **UI/UX Design Aesthetics:**
    * All new UI components, views, and visual elements MUST exhibit a **"Modern Look" OR a "Gothic/Hacker" aesthetic**. Strive for visually distinct, thematic, and polished designs.
    * Avoid generic, unstyled, or purely functional appearances. Consider elements like color schemes (e.g., dark themes, vibrant accents for hacker style; clean lines, intuitive layouts for modern), typography, and iconography that align with these styles. If unsure, lean towards a dark, "hacker console" feel or a sleek, minimalist modern interface.
    * **Tools**: Please use MudBlazor when creating complex component. Like menu, data grid tab pages, etc ...

2.  **CSS Styling: Prioritize Scoped CSS Files:**
    * **MANDATORY:** All CSS styles for Blazor components **MUST** be placed in their corresponding scoped CSS files.
    * For a component file named `MyComponent.razor`, its styles **MUST** be located in a file named `MyComponent.razor.css` in the same directory.
    * **PROHIBITED:** Do **NOT** use inline styles (e.g., `style="color: red;"`) directly in `.razor` markup or HTML elements.
    * **PROHIBITED:** Do **NOT** embed `<style>` blocks directly within `.razor` files if the styling can be achieved using a scoped `.razor.css` file. Global styles should be handled in a dedicated global CSS file (e.g., `wasm2/wwwroot/css/app.css`), not within components.
    * This approach ensures component encapsulation and maintainability.

3.  **Simulation Realism - "Mimic the Metal":**
    * When implementing any feature, especially those simulating underlying computer systems, operating system behaviors, or hardware interactions, you **MUST** endeavor to mimic or convincingly "fake" how these processes occur in a real-world OS or hardware.
    * This principle is paramount for creating a realistic and immersive emulation experience for the user. Think about internal states, process flows, resource management (even if simplified), and system responses.

---

## III. WORKFLOW & COLLABORATION PROTOCOLS:

1.  **Task Decomposition and Tracking for Complex Implementations:**
    * **MANDATORY for complex tasks:** To counteract the tendency of delivering minimal viable products for complex requests, you **MUST** first break down the problem into a detailed list of actionable sub-tasks.
    * Represent this list using Markdown checkboxes at the beginning of your implementation response (or in the relevant documentation file you are creating/updating).
        * Example:
            ```
            Okay, I will implement the new virtual file system. Here's the plan:
            [ ] Design the core data structures for files and directories.
            [ ] Implement C# classes for `VirtualFile` and `VirtualDirectory`.
            [ ] Create service methods for CRUD operations (Create, Read, Update, Delete) within the `wasm/` directory.
            [ ] Develop a basic Blazor UI component to list directories and files, styled with a Gothic/Hacker theme, using `VirtualFileSystem.razor.css`.
            [ ] Add methods to simulate file access permissions.
            ```
    * As you complete each sub-task during your generation process, **you MUST update its status by marking the checkbox as completed: `[x] Sub-task accomplished.`** This provides clear progress tracking for complex requests.

2.  **Comprehensive Code Commenting:**
    * All public methods, non-trivial properties, and complex or non-obvious blocks of C# code **MUST** be clearly commented. Explain the *why* and *how* of the code.
    * Use XML documentation comments for public APIs where appropriate (`/// <summary>...</summary>`).

3.  **Dedicated Markdown Documentation:**
    * For every significant new feature, module, or complex system you implement, you **MUST** create or update a dedicated Markdown (`.md`) file.
    * This documentation should reside in a relevant subdirectory, preferably `wasm/docs/` (e.g., `wasm/docs/virtual-file-system.md`).
    * **Content of Documentation:**
        * **Purpose:** What the feature does.
        * **Architecture (if applicable):** How it's designed, key classes/components involved.
        * **Usage/API:** How to use it (e.g., public methods, Blazor component parameters).
        * **Key Decisions:** Any important design choices made.
        * **Task List (copy or link to the completed task list you created).**
    * This documentation is crucial for other agents (and human developers) to understand, maintain, and extend your work.

---

**Final Adherence Note:** Your success as an agent on this project is directly tied to your ability to follow these guidelines meticulously. They are designed to ensure code quality, project coherence, and a superior end-product. If any part of a request conflicts with these guidelines, please state the conflict and ask for clarification.