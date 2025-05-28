using BlazorWindowManager.Components;
using BlazorWindowManager.Models;
using Microsoft.AspNetCore.Components;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BlazorWindowManager.Services
{
    /// <summary>
    /// Service for creating and managing dialogs
    /// </summary>
    public class DialogService
    {
        private readonly WindowManagerService _windowManager;
        private readonly Dictionary<Guid, object> _activeDialogs = new();

        public DialogService(WindowManagerService windowManager)
        {
            _windowManager = windowManager;
        }

        /// <summary>
        /// Creates a new dialog instance of the specified type
        /// </summary>
        /// <typeparam name="TDialog">The dialog component type</typeparam>
        /// <typeparam name="TResult">The result type for the dialog</typeparam>
        /// <param name="parameters">Parameters to pass to the dialog</param>
        /// <param name="owner">The owner window (null for application-modal)</param>
        /// <returns>The created dialog instance</returns>
        public TDialog Create<TDialog, TResult>(
            Dictionary<string, object>? parameters = null,
            WindowBase? owner = null)
            where TDialog : DialogBase<TResult>, new()
        {
            var dialog = new TDialog();
            
            // Set basic dialog properties
            dialog.IsModal = true;
            dialog.OwnerWindow = owner;
            
            // Apply parameters if provided
            if (parameters != null)
            {
                foreach (var kvp in parameters)
                {
                    var property = typeof(TDialog).GetProperty(kvp.Key);
                    if (property != null && property.CanWrite)
                    {
                        property.SetValue(dialog, kvp.Value);
                    }
                }
            }

            // Track the dialog
            _activeDialogs[dialog.Id] = dialog;

            // Handle dialog cleanup when closed
            dialog.OnDialogClosed = EventCallback.Factory.Create<DialogResult<TResult>>(this, 
                (result) => OnDialogClosed(dialog.Id));

            return dialog;
        }

        /// <summary>
        /// Shows a simple message box dialog
        /// </summary>
        /// <param name="message">The message to display</param>
        /// <param name="title">The dialog title</param>
        /// <param name="buttons">The buttons to show</param>
        /// <param name="icon">The icon to display</param>
        /// <param name="owner">The owner window</param>
        /// <returns>The message box result</returns>
        public async Task<MessageBoxResult> ShowMessageBoxAsync(
            string message,
            string title = "Message",
            MessageBoxButtons buttons = MessageBoxButtons.OK,
            MessageBoxIcon icon = MessageBoxIcon.Information,
            WindowBase? owner = null)
        {
            var parameters = new Dictionary<string, object>
            {
                [nameof(MessageBoxDialog.Message)] = message,
                [nameof(MessageBoxDialog.Title)] = title,
                [nameof(MessageBoxDialog.Buttons)] = buttons,
                [nameof(MessageBoxDialog.Icon)] = icon
            };

            var dialog = Create<MessageBoxDialog, MessageBoxResult>(parameters, owner);
            return (await dialog.ShowDialogAsync()).Data ?? new(MessageBoxButton.Cancel, false);
        }

        /// <summary>
        /// Shows a prompt dialog for text input
        /// </summary>
        /// <param name="message">The prompt message</param>
        /// <param name="title">The dialog title</param>
        /// <param name="defaultValue">The default input value</param>
        /// <param name="owner">The owner window</param>
        /// <returns>The prompt result</returns>
        public async Task<DialogResult<string>> ShowPromptAsync(
            string message,
            string title = "Input",
            string defaultValue = "",
            WindowBase? owner = null)
        {
            var parameters = new Dictionary<string, object>
            {
                [nameof(PromptDialog.Message)] = message,
                [nameof(PromptDialog.Title)] = title,
                [nameof(PromptDialog.DefaultValue)] = defaultValue
            };

            var dialog = Create<PromptDialog, string>(parameters, owner);
            return await dialog.ShowDialogAsync();
        }

        /// <summary>
        /// Shows a confirmation dialog with Yes/No buttons
        /// </summary>
        /// <param name="message">The confirmation message</param>
        /// <param name="title">The dialog title</param>
        /// <param name="owner">The owner window</param>
        /// <returns>True if Yes was clicked, false if No or cancelled</returns>
        public async Task<bool> ShowConfirmationAsync(
            string message,
            string title = "Confirm",
            WindowBase? owner = null)
        {
            var result = await ShowMessageBoxAsync(message, title, MessageBoxButtons.YesNo, MessageBoxIcon.Question, owner);
            return result.Button == MessageBoxButton.Yes;
        }

        /// <summary>
        /// Gets all currently active dialogs
        /// </summary>
        /// <returns>Collection of active dialogs</returns>
        public IReadOnlyCollection<object> GetActiveDialogs()
        {
            return _activeDialogs.Values;
        }

        /// <summary>
        /// Closes all active dialogs
        /// </summary>
        /// <param name="force">Whether to force close dialogs</param>
        public async Task CloseAllDialogsAsync(bool force = false)
        {
            var dialogs = new List<object>(_activeDialogs.Values);
            
            foreach (var dialog in dialogs)
            {
                if (dialog is WindowBase dialogBase)
                {
                    if (force)
                    {
                        await dialogBase.Close(force: true);
                    }
                    else if (dialog is IDialogBase iDialog)
                    {
                        await iDialog.CancelDialogAsync("CloseAll");
                    }
                }
            }
        }

        private Task OnDialogClosed(Guid dialogId)
        {
            _activeDialogs.Remove(dialogId);
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Message box button options
    /// </summary>
    public enum MessageBoxButtons
    {
        OK,
        OKCancel,
        YesNo,
        YesNoCancel
    }

    /// <summary>
    /// Message box icon options
    /// </summary>
    public enum MessageBoxIcon
    {
        None,
        Information,
        Warning,
        Error,
        Question
    }

    /// <summary>
    /// Message box button that was clicked
    /// </summary>
    public enum MessageBoxButton
    {
        OK,
        Cancel,
        Yes,
        No
    }

    /// <summary>
    /// Result from a message box dialog
    /// </summary>
    public class MessageBoxResult : DialogResult<MessageBoxButton>
    {
        /// <summary>
        /// The button that was clicked
        /// </summary>
        public MessageBoxButton Button => Data;

        public MessageBoxResult(MessageBoxButton button, bool success = true)
        {
            Data = button;
            Success = success;
            CloseReason = button.ToString();
        }

        public static implicit operator MessageBoxResult(MessageBoxButton button)
        {
            return new MessageBoxResult(button);
        }

        public static implicit operator bool(MessageBoxResult result)
        {
            return result.Success && (result.Button == MessageBoxButton.OK || result.Button == MessageBoxButton.Yes);
        }
    }
}
