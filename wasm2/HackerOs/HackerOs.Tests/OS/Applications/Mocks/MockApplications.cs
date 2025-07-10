using HackerOs.OS.Applications.Attributes;
using HackerOs.OS.Applications.Lifecycle;
using HackerOs.OS.Applications.UI;
using Microsoft.AspNetCore.Components;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HackerOs.Tests.OS.Applications.Mocks
{
    /// <summary>
    /// Mock applications for testing the application registry and launcher
    /// </summary>
    
    [App(
        Id = "mock.BasicApp",
        Name = "Basic Mock App",
        IconPath = "fa-solid:cube"
    )]
    public class MockBasicApplication : ApplicationBase
    {
        public bool StartCalled { get; private set; }
        public bool CloseCalled { get; private set; }
        public bool CloseRequestCalled { get; private set; }
        public bool AllowClose { get; set; } = true;
        
        public override async Task OnStartAsync(ApplicationLifecycleContext context)
        {
            await base.OnStartAsync(context);
            StartCalled = true;
        }
        
        public override async Task OnCloseAsync(ApplicationLifecycleContext context)
        {
            await base.OnCloseAsync(context);
            CloseCalled = true;
        }
        
        public override async Task<bool> OnCloseRequestAsync(ApplicationLifecycleContext context)
        {
            CloseRequestCalled = true;
            return AllowClose;
        }
        
        protected override Dictionary<string, object> GetStateForSerialization()
        {
            var state = new Dictionary<string, object>
            {
                { "testValue", "serialized-value" }
            };
            
            return state;
        }
    }
    
    [App(
        Id = "mock.WindowApp",
        Name = "Window Mock App",
        IconPath = "fa-solid:window-maximize"
    )]
    [AppDescription("A mock window application for testing")]
    public class MockWindowApplication : WindowApplicationBase
    {
        public bool ContentRequested { get; private set; }
        public Dictionary<string, object> LastState { get; private set; } = new Dictionary<string, object>();
        
        public string TestData { get; set; } = "Initial Value";
        
        protected override RenderFragment GetWindowContent()
        {
            ContentRequested = true;
            
            return builder =>
            {
                builder.OpenElement(0, "div");
                builder.AddContent(1, "Mock Window Content");
                builder.CloseElement();
            };
        }
        
        protected override Dictionary<string, object> GetStateForSerialization()
        {
            var state = new Dictionary<string, object>
            {
                { "testData", TestData }
            };
            
            return state;
        }
        
        protected override async Task ApplyStateFromSerializationAsync(Dictionary<string, System.Text.Json.JsonElement> state)
        {
            await base.ApplyStateFromSerializationAsync(state);
            
            if (state.TryGetValue("testData", out var testDataElement) && 
                testDataElement.ValueKind == System.Text.Json.JsonValueKind.String)
            {
                TestData = testDataElement.GetString() ?? "Default Value";
            }
            
            foreach (var item in state)
            {
                LastState[item.Key] = item.Value.ToString() ?? "";
            }
        }
    }
    
    [App(
        Id = "mock.CategoryApp1",
        Name = "Category App 1",
        IconPath = "fa-solid:folder",
        Categories = new[] { "Test", "Category1" }
    )]
    public class MockCategoryApplication1 : ApplicationBase
    {
    }
    
    [App(
        Id = "mock.CategoryApp2",
        Name = "Category App 2",
        IconPath = "fa-solid:folder-open",
        Categories = new[] { "Test", "Category2" }
    )]
    public class MockCategoryApplication2 : ApplicationBase
    {
    }
    
    // Non-application class with App attribute for testing filtering
    [App(
        Id = "mock.NonApp",
        Name = "Non Application",
        IconPath = "fa-solid:question"
    )]
    public class MockNonApplication
    {
        // This is not an application as it doesn't inherit from ApplicationBase
    }
}
