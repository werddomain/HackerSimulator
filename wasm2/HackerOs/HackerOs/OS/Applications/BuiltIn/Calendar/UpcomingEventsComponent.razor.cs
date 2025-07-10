using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Components;

namespace HackerOs.OS.Applications.BuiltIn.Calendar;

/// <summary>
/// Component for displaying upcoming events in the sidebar
/// </summary>
public partial class UpcomingEventsComponent : ComponentBase
{
    /// <summary>
    /// Gets or sets the list of events to display
    /// </summary>
    [Parameter] public List<EventOccurrence> Events { get; set; } = new();
    
    /// <summary>
    /// Event callback for when an event is selected
    /// </summary>
    [Parameter] public EventCallback<EventOccurrence> OnEventSelected { get; set; }
}
