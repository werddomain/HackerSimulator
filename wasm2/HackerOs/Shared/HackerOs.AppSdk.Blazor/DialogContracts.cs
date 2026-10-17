using HackerOs.App.Abstractions;

namespace HackerOs.AppSdk.Blazor;

/// <summary>
/// Specifies the combination of buttons to display in a message box dialog.
/// </summary>
public enum MessageBoxType
{
    /// <summary>Displays an OK button.</summary>
    Ok,

    /// <summary>Displays OK and Cancel buttons.</summary>
    OkCancel,

    /// <summary>Displays Yes and No buttons.</summary>
    YesNo,

    /// <summary>Displays OK, Cancel, and Retry buttons.</summary>
    OkCancelRetry,

    /// <summary>Displays Yes, No, and Cancel buttons.</summary>
    YesNoCancel,

    /// <summary>Displays Retry and Cancel buttons.</summary>
    RetryCancel
}

/// <summary>
/// Alias wrapper for <see cref="MessageBoxType"/> to support alternate casing.
/// </summary>
public readonly struct MessageboxType : IEquatable<MessageboxType>, IEquatable<MessageBoxType>
{
    /// <summary>Gets the underlying enum value.</summary>
    public MessageBoxType Value { get; }

    /// <summary>Creates a wrapper instance over a <see cref="MessageBoxType"/> value.</summary>
    public MessageboxType(MessageBoxType value) => Value = value;

    public static MessageboxType Ok => new(MessageBoxType.Ok);
    public static MessageboxType OkCancel => new(MessageBoxType.OkCancel);
    public static MessageboxType YesNo => new(MessageBoxType.YesNo);
    public static MessageboxType OkCancelRetry => new(MessageBoxType.OkCancelRetry);
    public static MessageboxType YesNoCancel => new(MessageBoxType.YesNoCancel);
    public static MessageboxType RetryCancel => new(MessageBoxType.RetryCancel);

    public static implicit operator MessageboxType(MessageBoxType v) => new(v);
    public static implicit operator MessageBoxType(MessageboxType v) => v.Value;

    public static bool operator ==(MessageBoxType left, MessageboxType right) => left == right.Value;
    public static bool operator !=(MessageBoxType left, MessageboxType right) => left != right.Value;
    public static bool operator ==(MessageboxType left, MessageBoxType right) => left.Value == right;
    public static bool operator !=(MessageboxType left, MessageBoxType right) => left.Value != right;
    public static bool operator ==(MessageboxType left, MessageboxType right) => left.Value == right.Value;
    public static bool operator !=(MessageboxType left, MessageboxType right) => left.Value != right.Value;

    public bool Equals(MessageboxType other) => Value == other.Value;
    public bool Equals(MessageBoxType other) => Value == other;
    public override bool Equals(object? obj) => obj is MessageboxType m && Equals(m) || obj is MessageBoxType b && Equals(b);
    public override int GetHashCode() => Value.GetHashCode();
}

/// <summary>
/// Specifies which button was clicked in a message box dialog.
/// </summary>
public enum MessageBoxResult
{
    /// <summary>The OK button was clicked.</summary>
    Ok,

    /// <summary>The Cancel button was clicked or the dialog was closed.</summary>
    Cancel,

    /// <summary>The Yes button was clicked.</summary>
    Yes,

    /// <summary>The No button was clicked.</summary>
    No,

    /// <summary>The Retry button was clicked.</summary>
    Retry
}

/// <summary>
/// Alias wrapper for <see cref="MessageBoxResult"/> to support alternate casing.
/// </summary>
public readonly struct MessageboxResult : IEquatable<MessageboxResult>, IEquatable<MessageBoxResult>
{
    /// <summary>Gets the underlying enum value.</summary>
    public MessageBoxResult Value { get; }

    /// <summary>Creates a wrapper instance over a <see cref="MessageBoxResult"/> value.</summary>
    public MessageboxResult(MessageBoxResult value) => Value = value;

    public static MessageboxResult Ok => new(MessageBoxResult.Ok);
    public static MessageboxResult Cancel => new(MessageBoxResult.Cancel);
    public static MessageboxResult Yes => new(MessageBoxResult.Yes);
    public static MessageboxResult No => new(MessageBoxResult.No);
    public static MessageboxResult Retry => new(MessageBoxResult.Retry);

    public static implicit operator MessageboxResult(MessageBoxResult v) => new(v);
    public static implicit operator MessageBoxResult(MessageboxResult v) => v.Value;

    public static bool operator ==(MessageBoxResult left, MessageboxResult right) => left == right.Value;
    public static bool operator !=(MessageBoxResult left, MessageboxResult right) => left != right.Value;
    public static bool operator ==(MessageboxResult left, MessageBoxResult right) => left.Value == right;
    public static bool operator !=(MessageboxResult left, MessageBoxResult right) => left.Value != right;
    public static bool operator ==(MessageboxResult left, MessageboxResult right) => left.Value == right.Value;
    public static bool operator !=(MessageboxResult left, MessageboxResult right) => left.Value != right.Value;

    public bool Equals(MessageboxResult other) => Value == other.Value;
    public bool Equals(MessageBoxResult other) => Value == other;
    public override bool Equals(object? obj) => obj is MessageboxResult m && Equals(m) || obj is MessageBoxResult b && Equals(b);
    public override int GetHashCode() => Value.GetHashCode();
}

/// <summary>
/// Configures a message box dialog.
/// </summary>
public sealed record MessageBoxDialogRequest
{
    /// <summary>Gets the dialog title.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Gets the dialog content message.</summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>Gets the message box button configuration.</summary>
    public MessageBoxType DialogType { get; init; } = MessageBoxType.Ok;
}

/// <summary>
/// Contains the result of a message box dialog.
/// </summary>
/// <param name="Result">The button clicked by the user.</param>
public sealed record MessageBoxDialogResult(MessageBoxResult Result);

/// <summary>
/// Configures a text input dialog.
/// </summary>
public sealed record TextInputDialogRequest
{
    /// <summary>Gets the dialog title.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Gets the dialog prompt content.</summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>Gets the initial text value.</summary>
    public string? DefaultValue { get; init; }

    /// <summary>Gets the input placeholder text.</summary>
    public string? Placeholder { get; init; }
}

/// <summary>
/// Describes whether the user submitted text or cancelled a text input dialog.
/// </summary>
public enum TextInputStatus
{
    /// <summary>The user submitted the text input.</summary>
    Submitted,

    /// <summary>The user cancelled the text input dialog.</summary>
    Cancelled
}

/// <summary>
/// Contains the result of a text input dialog.
/// </summary>
/// <param name="Status">Dialog outcome.</param>
/// <param name="Value">The submitted string, or null if cancelled.</param>
public sealed record TextInputDialogResult(
    TextInputStatus Status,
    string? Value);

/// <summary>
/// Configures a credential-verification prompt (`ADR 0042`) — used to re-authenticate an enabled
/// Administrator account, either to approve a denied permission for the requesting app instance, or
/// (from the terminal's <c>sudo</c> built-in) to authorize elevating one launched command to full
/// root. Never the requesting app's own credential: the login field always starts blank so any
/// enabled Administrator can approve, mirroring an over-the-shoulder UAC-style prompt.
/// </summary>
public sealed record CredentialPromptDialogRequest
{
    /// <summary>Gets the dialog title.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Gets the human-readable description of what is being authorized.</summary>
    public string Description { get; init; } = string.Empty;
}

/// <summary>Describes whether the user submitted credentials or cancelled a credential prompt.</summary>
public enum CredentialPromptStatus
{
    /// <summary>The user submitted a login name and password.</summary>
    Submitted,

    /// <summary>The user cancelled the credential prompt.</summary>
    Cancelled
}

/// <summary>
/// Contains the raw, unverified credentials submitted to a credential prompt. The caller is
/// responsible for verifying them (e.g. via <c>IAdminElevationVerifier</c>) — the dialog layer never
/// performs authentication itself.
/// </summary>
/// <param name="Status">Dialog outcome.</param>
/// <param name="LoginName">The submitted login name, or null if cancelled.</param>
/// <param name="Password">The submitted password, or null if cancelled.</param>
public sealed record CredentialPromptDialogResult(
    CredentialPromptStatus Status,
    string? LoginName,
    string? Password);

/// <summary>One app offered by the app chooser (ADR 0096, section 5).</summary>
/// <param name="AppId">App ID returned when the user picks it.</param>
/// <param name="Name">Display name.</param>
/// <param name="Description">One-line description, when the app has one.</param>
public sealed record AppChooserOption(string AppId, string Name, string? Description = null);

/// <summary>
/// Configures the app chooser, the "Open with" dialog of the OS (ADR 0096, section 5): which app
/// handles a file, a URI or an app-defined intent when several can and none is the default.
/// </summary>
public sealed record AppChooserDialogRequest
{
    /// <summary>Gets the dialog title; "Open with" when empty.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Gets the sentence above the list, e.g. what is being opened.</summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>Gets the apps to choose from, in display order.</summary>
    public IReadOnlyList<AppChooserOption> Candidates { get; init; } = [];

    /// <summary>Gets whether the dialog offers "Always use this app".</summary>
    public bool OfferAlwaysUse { get; init; } = true;
}

/// <summary>Describes whether the user picked an app or cancelled the chooser.</summary>
public enum AppChooserStatus
{
    /// <summary>The user picked an app.</summary>
    Selected,

    /// <summary>The user cancelled the chooser.</summary>
    Cancelled
}

/// <summary>Contains the result of the app chooser.</summary>
/// <param name="Status">Dialog outcome.</param>
/// <param name="AppId">The picked app, or null if cancelled.</param>
/// <param name="AlwaysUse">Whether the user ticked "Always use this app".</param>
public sealed record AppChooserDialogResult(AppChooserStatus Status, string? AppId, bool AlwaysUse);

/// <summary>
/// Displays modal system dialogs (including file dialogs, message boxes, and text inputs) on behalf of a window app.
/// </summary>
public interface IDialogService : IFileDialogService
{
    /// <summary>Displays a message box dialog scoped to the requesting app instance.</summary>
    ValueTask<MessageBoxDialogResult> MessageBoxAsync(
        IAppExecutionContext context,
        MessageBoxDialogRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Displays a text input dialog scoped to the requesting app instance.</summary>
    ValueTask<TextInputDialogResult> TextInputAsync(
        IAppExecutionContext context,
        TextInputDialogRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Displays a credential prompt scoped to the requesting app instance (`ADR 0042`).</summary>
    ValueTask<CredentialPromptDialogResult> CredentialPromptAsync(
        IAppExecutionContext context,
        CredentialPromptDialogRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Displays the app chooser scoped to the requesting app instance (ADR 0096, section 5). The
    /// default answers "cancelled", for hosts written before the chooser existed.
    /// </summary>
    ValueTask<AppChooserDialogResult> ChooseAppAsync(
        IAppExecutionContext context,
        AppChooserDialogRequest request,
        CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(new AppChooserDialogResult(AppChooserStatus.Cancelled, null, false));
}
