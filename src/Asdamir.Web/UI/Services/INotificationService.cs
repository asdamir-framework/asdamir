// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

namespace Asdamir.Web.UI.Services;

/// <summary>
/// UI-facing abstraction that app code calls to surface toasts, confirmation dialogs, a loading
/// overlay, and action notifications. Implementations raise the events below; a subscribed host
/// component (e.g. <c>AsdamirNotificationHost</c>) renders them. Messages are localization keys that
/// the implementation resolves against the DB-backed localizer, so callers pass keys, not literals.
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Raised when a plain toast is requested (the <c>Success</c>/<c>Info</c>/<c>Warning</c>/
    /// <c>Error</c> and localized <c>Show*</c> methods). The notification-bar component subscribes
    /// to render the toast.
    /// </summary>
    event EventHandler<NotificationRequest>? NotificationRequested;

    /// <summary>
    /// Raised when a confirmation dialog is requested. The host completes the request's task with the
    /// user's yes/no choice. When no host is subscribed, <c>ConfirmAsync</c>/<c>ConfirmDangerAsync</c>
    /// deny (return false) so a destructive action is never allowed to proceed un-confirmed.
    /// </summary>
    event EventHandler<ConfirmationRequest>? ConfirmationRequested;

    /// <summary>Raised to show or hide the global loading overlay (see <c>ShowLoading</c>/<see cref="HideLoading"/>).</summary>
    event EventHandler<LoadingRequest>? LoadingChanged;

    /// <summary>Raised when a persistent toast carrying clickable action buttons is requested (see <c>ShowWithActions</c>).</summary>
    event EventHandler<ActionNotificationRequest>? ActionNotificationRequested;

    // Simple Toast Notifications (backward compatible)

    /// <summary>Shows a success (green) toast from an already-resolved literal message. Kept for backward compatibility with callers that pass a literal rather than a localization key.</summary>
    /// <param name="message">The literal message text to display.</param>
    /// <param name="timeoutMs">Auto-dismiss delay in milliseconds; defaults to a short duration.</param>
    void Success(string message, int timeoutMs = 3500);

    /// <summary>Shows an informational (neutral) toast from an already-resolved literal message.</summary>
    /// <param name="message">The literal message text to display.</param>
    /// <param name="timeoutMs">Auto-dismiss delay in milliseconds; defaults to a short duration.</param>
    void Info(string message, int timeoutMs = 3500);

    /// <summary>Shows a warning (amber) toast from an already-resolved literal message.</summary>
    /// <param name="message">The literal message text to display.</param>
    /// <param name="timeoutMs">Auto-dismiss delay in milliseconds; defaults to a short duration.</param>
    void Warning(string message, int timeoutMs = 3500);

    /// <summary>Shows an error (red) toast from an already-resolved literal message, with a longer default timeout than the others so the user has time to read it.</summary>
    /// <param name="message">The literal message text to display.</param>
    /// <param name="timeoutMs">Auto-dismiss delay in milliseconds; defaults to a longer duration than the non-error toasts.</param>
    void Error(string message, int timeoutMs = 5000);

    // Localized Toast Notifications

    /// <summary>Resolves <paramref name="messageKey"/> against the DB-backed localizer for the current culture and shows the result as a success toast. Preferred over <c>Success</c> for user-facing strings.</summary>
    /// <param name="messageKey">The localization key to resolve and display.</param>
    /// <param name="args">Optional format arguments substituted into the resolved string.</param>
    void ShowSuccess(string messageKey, params object[] args);

    /// <summary>Resolves <paramref name="messageKey"/> against the DB-backed localizer and shows the result as an informational toast.</summary>
    /// <param name="messageKey">The localization key to resolve and display.</param>
    /// <param name="args">Optional format arguments substituted into the resolved string.</param>
    void ShowInfo(string messageKey, params object[] args);

    /// <summary>Resolves <paramref name="messageKey"/> against the DB-backed localizer and shows the result as a warning toast.</summary>
    /// <param name="messageKey">The localization key to resolve and display.</param>
    /// <param name="args">Optional format arguments substituted into the resolved string.</param>
    void ShowWarning(string messageKey, params object[] args);

    /// <summary>Resolves <paramref name="messageKey"/> against the DB-backed localizer and shows the result as an error toast (longer timeout).</summary>
    /// <param name="messageKey">The localization key to resolve and display.</param>
    /// <param name="args">Optional format arguments substituted into the resolved string.</param>
    void ShowError(string messageKey, params object[] args);

    // With custom options

    /// <summary>Shows a fully customized notification — the escape hatch for control the convenience methods don't expose (title, position, persistence, sound, icon, actions, correlation id).</summary>
    /// <param name="options">The complete notification configuration to display.</param>
    void Show(NotificationOptions options);

    // Confirmation Dialogs

    /// <summary>Shows a standard (non-destructive) confirmation dialog and asynchronously returns the user's choice. Returns false if no host is subscribed to answer.</summary>
    /// <param name="messageKey">Localization key for the dialog body.</param>
    /// <param name="titleKey">Optional localization key for the dialog title.</param>
    /// <param name="args">Optional format arguments for the message.</param>
    /// <returns>A task that resolves to true if the user confirmed, otherwise false.</returns>
    Task<bool> ConfirmAsync(string messageKey, string? titleKey = null, params object[] args);

    /// <summary>Shows a danger-styled confirmation dialog for destructive actions (delete, purge, etc.), emphasizing the risk, and asynchronously returns the user's choice. Returns false if no host is subscribed.</summary>
    /// <param name="messageKey">Localization key for the dialog body.</param>
    /// <param name="titleKey">Optional localization key for the dialog title.</param>
    /// <param name="args">Optional format arguments for the message.</param>
    /// <returns>A task that resolves to true if the user confirmed the destructive action, otherwise false.</returns>
    Task<bool> ConfirmDangerAsync(string messageKey, string? titleKey = null, params object[] args);

    // Progress/Loading

    /// <summary>Shows the global loading overlay with a localized message while a long-running operation runs. Pair every call with <see cref="HideLoading"/>.</summary>
    /// <param name="messageKey">Localization key for the overlay message.</param>
    /// <param name="args">Optional format arguments for the message.</param>
    void ShowLoading(string messageKey, params object[] args);

    /// <summary>Hides the loading overlay previously shown by <c>ShowLoading</c>.</summary>
    void HideLoading();

    // Custom Actions

    /// <summary>Shows a persistent notification that carries clickable action buttons (e.g. "Undo", "Retry"), letting the user act directly from the toast.</summary>
    /// <param name="messageKey">Localization key for the notification body.</param>
    /// <param name="actions">The action buttons to render on the notification.</param>
    /// <param name="args">Optional format arguments for the message.</param>
    void ShowWithActions(string messageKey, List<NotificationAction> actions, params object[] args);
}

/// <summary>
/// Full configuration for a notification passed to <see cref="INotificationService.Show"/>. Bundles
/// every knob the convenience methods leave at defaults — severity, title, timing, persistence,
/// dismissibility, actions, position, sound, icon, and a correlation id for log tie-back.
/// </summary>
public class NotificationOptions
{
    /// <summary>Severity that drives the toast's color and icon; defaults to <see cref="NotificationSeverity.Info"/>.</summary>
    public NotificationSeverity Severity { get; set; } = NotificationSeverity.Info;

    /// <summary>Localization key resolved to produce the notification body text.</summary>
    public string MessageKey { get; set; } = string.Empty;

    /// <summary>Format arguments substituted into the resolved message.</summary>
    public object[] MessageArgs { get; set; } = Array.Empty<object>();

    /// <summary>Optional localization key for a bold title shown above the message.</summary>
    public string? TitleKey { get; set; }

    /// <summary>Auto-dismiss delay in milliseconds; ignored when <see cref="IsPersistent"/> is true.</summary>
    public int TimeoutMs { get; set; } = 3500;

    /// <summary>When true, the toast stays until dismissed and does not auto-close on the timeout.</summary>
    public bool IsPersistent { get; set; }

    /// <summary>When true, the user may manually dismiss the toast (shows a close affordance).</summary>
    public bool AllowDismiss { get; set; } = true;

    /// <summary>Optional action buttons to render on the notification.</summary>
    public List<NotificationAction>? Actions { get; set; }

    /// <summary>On-screen anchor where the toast appears; defaults to the top-right corner.</summary>
    public NotificationPosition Position { get; set; } = NotificationPosition.TopRight;

    /// <summary>When true, plays a notification sound as the toast appears.</summary>
    public bool EnableSound { get; set; } = true;

    /// <summary>Optional icon override; when null an icon is derived from <see cref="Severity"/>.</summary>
    public string? Icon { get; set; }

    /// <summary>Optional correlation id echoed into logs so an on-screen notice can be traced to its server-side event.</summary>
    public string? CorrelationId { get; set; } // For logging
}

/// <summary>
/// A single clickable button rendered on an action notification, pairing a localized label with a
/// callback and its visual style.
/// </summary>
public class NotificationAction
{
    /// <summary>Localization key resolved to the button's caption.</summary>
    public string LabelKey { get; set; } = string.Empty;

    /// <summary>Callback invoked when the button is clicked.</summary>
    public Action? OnClick { get; set; }

    /// <summary>When true, the notification closes after the button is clicked.</summary>
    public bool CloseOnClick { get; set; } = true;

    /// <summary>Visual emphasis for the button; defaults to <see cref="NotificationActionStyle.Default"/>.</summary>
    public NotificationActionStyle Style { get; set; } = NotificationActionStyle.Default;
}

/// <summary>Severity level of a notification, controlling its color, icon, and default timing.</summary>
public enum NotificationSeverity
{
    /// <summary>Neutral informational message.</summary>
    Info,

    /// <summary>Positive outcome — an operation completed successfully.</summary>
    Success,

    /// <summary>Cautionary message that needs attention but is not an error.</summary>
    Warning,

    /// <summary>An operation failed.</summary>
    Error,

    /// <summary>A severe, high-urgency failure warranting stronger emphasis than <see cref="Error"/>.</summary>
    Critical
}

/// <summary>On-screen corner or edge where a toast is anchored.</summary>
public enum NotificationPosition
{
    /// <summary>Top-left corner.</summary>
    TopLeft,

    /// <summary>Top edge, horizontally centered.</summary>
    TopCenter,

    /// <summary>Top-right corner (the default).</summary>
    TopRight,

    /// <summary>Bottom-left corner.</summary>
    BottomLeft,

    /// <summary>Bottom edge, horizontally centered.</summary>
    BottomCenter,

    /// <summary>Bottom-right corner.</summary>
    BottomRight
}

/// <summary>Visual style of a notification action button.</summary>
public enum NotificationActionStyle
{
    /// <summary>Standard, low-emphasis button.</summary>
    Default,

    /// <summary>Accent/primary button for the recommended action.</summary>
    Primary,

    /// <summary>Danger-styled button for a destructive action.</summary>
    Danger
}

/// <summary>
/// Immutable payload carried by <see cref="INotificationService.NotificationRequested"/> — the
/// already-resolved toast the host component renders.
/// </summary>
/// <param name="Severity">Severity driving the toast's color and icon.</param>
/// <param name="Message">The resolved, display-ready message text.</param>
/// <param name="TimeoutMs">Auto-dismiss delay in milliseconds.</param>
/// <param name="Icon">Optional icon override; when null the icon is derived from the severity.</param>
/// <param name="CorrelationId">Optional correlation id for tracing the notice back to its logged event.</param>
public sealed record NotificationRequest(
    NotificationSeverity Severity,
    string Message,
    int TimeoutMs,
    string? Icon = null,
    string? CorrelationId = null);

/// <summary>
/// A confirmation prompt awaiting a yes/no answer. The host completes <see cref="Completion"/> with the
/// user's choice; <see cref="INotificationService.ConfirmAsync"/> awaits that task.
/// </summary>
/// <param name="Message">The resolved dialog body text.</param>
/// <param name="Title">Optional resolved dialog title.</param>
/// <param name="IsDanger">True when the dialog is danger-styled for a destructive action.</param>
/// <param name="Completion">The task source the host completes with the user's yes/no answer.</param>
public sealed record ConfirmationRequest(
    string Message,
    string? Title,
    bool IsDanger,
    TaskCompletionSource<bool> Completion);

/// <summary>Show/hide the loading overlay (with an optional message when showing).</summary>
/// <param name="IsVisible">True to show the overlay, false to hide it.</param>
/// <param name="Message">Optional resolved message shown while the overlay is visible.</param>
public sealed record LoadingRequest(bool IsVisible, string? Message);

/// <summary>A persistent notification carrying clickable action buttons.</summary>
/// <param name="Severity">Severity driving the notification's color and icon.</param>
/// <param name="Message">The resolved notification body text.</param>
/// <param name="Actions">The action buttons rendered on the notification.</param>
public sealed record ActionNotificationRequest(
    NotificationSeverity Severity,
    string Message,
    IReadOnlyList<NotificationAction> Actions);
