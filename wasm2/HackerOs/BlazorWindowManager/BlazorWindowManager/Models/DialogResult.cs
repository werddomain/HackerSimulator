using System;

namespace BlazorWindowManager.Models
{
    /// <summary>
    /// Represents the result of a dialog operation
    /// </summary>
    /// <typeparam name="T">The type of the result data</typeparam>
    public class DialogResult<T>
    {
        /// <summary>
        /// Gets whether the dialog was completed successfully (not cancelled)
        /// </summary>
        public bool Success { get; init; }

        /// <summary>
        /// Gets the result data from the dialog
        /// </summary>
        public T? Data { get; init; }

        /// <summary>
        /// Gets the button or action that was used to close the dialog
        /// </summary>
        public string? CloseReason { get; init; }

        /// <summary>
        /// Gets any error message if the dialog failed
        /// </summary>
        public string? ErrorMessage { get; init; }

        /// <summary>
        /// Creates a successful dialog result
        /// </summary>
        /// <param name="data">The result data</param>
        /// <param name="closeReason">The reason the dialog was closed</param>
        /// <returns>A successful DialogResult</returns>
        public static DialogResult<T> Ok(T data, string closeReason = "OK")
        {
            return new DialogResult<T>
            {
                Success = true,
                Data = data,
                CloseReason = closeReason
            };
        }

        /// <summary>
        /// Creates a cancelled dialog result
        /// </summary>
        /// <param name="closeReason">The reason the dialog was cancelled</param>
        /// <returns>A cancelled DialogResult</returns>
        public static DialogResult<T> Cancel(string closeReason = "Cancel")
        {
            return new DialogResult<T>
            {
                Success = false,
                Data = default,
                CloseReason = closeReason
            };
        }

        /// <summary>
        /// Creates a failed dialog result
        /// </summary>
        /// <param name="errorMessage">The error message</param>
        /// <param name="closeReason">The reason the dialog was closed</param>
        /// <returns>A failed DialogResult</returns>
        public static DialogResult<T> Error(string errorMessage, string closeReason = "Error")
        {
            return new DialogResult<T>
            {
                Success = false,
                Data = default,
                CloseReason = closeReason,
                ErrorMessage = errorMessage
            };
        }
    }

    /// <summary>
    /// Non-generic version for dialogs that don't return data
    /// </summary>
    public class DialogResult : DialogResult<object>
    {
        /// <summary>
        /// Creates a successful dialog result
        /// </summary>
        /// <param name="closeReason">The reason the dialog was closed</param>
        /// <returns>A successful DialogResult</returns>
        public static DialogResult Ok(string closeReason = "OK")
        {
            return new DialogResult
            {
                Success = true,
                CloseReason = closeReason
            };
        }

        /// <summary>
        /// Creates a cancelled dialog result
        /// </summary>
        /// <param name="closeReason">The reason the dialog was cancelled</param>
        /// <returns>A cancelled DialogResult</returns>
        public new static DialogResult Cancel(string closeReason = "Cancel")
        {
            return new DialogResult
            {
                Success = false,
                CloseReason = closeReason
            };
        }

        /// <summary>
        /// Creates a failed dialog result
        /// </summary>
        /// <param name="errorMessage">The error message</param>
        /// <param name="closeReason">The reason the dialog was closed</param>
        /// <returns>A failed DialogResult</returns>
        public new static DialogResult Error(string errorMessage, string closeReason = "Error")
        {
            return new DialogResult
            {
                Success = false,
                CloseReason = closeReason,
                ErrorMessage = errorMessage
            };
        }
    }
}
