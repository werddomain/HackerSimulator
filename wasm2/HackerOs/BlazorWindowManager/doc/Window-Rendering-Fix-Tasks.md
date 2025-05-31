# Window Rendering Fix - Task List

## Issue Analysis
Windows are being created and tracked by WindowManagerService but not being visually rendered. The problem is:

1. **WindowArchitectureTest has its own isolated DesktopArea** - Windows created here only show in that specific desktop area
2. **No global DesktopArea exists** - Windows created via WindowManager.CreateWindow() need a global rendering surface
3. **Home page lacks DesktopArea** - The main page where users should see windows doesn't have a desktop area

## Fix Tasks

### Task 1: Update Home Page with Global DesktopArea
- [ ] Add DesktopArea component to Home.razor  
- [ ] Configure it as the main window rendering surface
- [ ] Style it appropriately for desktop experience

### Task 2: Fix WindowArchitectureTest Page
- [ ] Remove isolated DesktopArea from WindowArchitectureTest
- [ ] Update instructions to guide users to Home page to see windows
- [ ] Ensure windows created here appear in global desktop area

### Task 3: Verify WindowRenderComponent Integration
- [ ] Ensure WindowRenderComponent is properly receiving window events
- [ ] Test that windows render correctly when created
- [ ] Verify window lifecycle (create, move, close) works end-to-end

### Task 4: Update Layout/Navigation
- [ ] Ensure DesktopArea spans full viewport appropriately
- [ ] Test window rendering across page navigation
- [ ] Verify windows persist when navigating between pages

### Task 5: Test and Validate
- [ ] Create test windows and verify they appear visually
- [ ] Test window interactions (drag, resize, close)
- [ ] Verify multiple windows can be created and managed
- [ ] Test theme integration with rendered windows

## Expected Outcome
After these fixes:
- Windows created via WindowManager.CreateWindow() will be visually rendered
- Users can see and interact with windows on the Home page
- WindowArchitectureTest page will correctly create windows that appear in the global desktop
- Full window lifecycle management will work correctly
